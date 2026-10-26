using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;

namespace StockTracker.Domain.Counts;

/// <summary>What the snapshot expects at one location when a count starts.</summary>
public sealed record ExpectedStock(Guid LocationId, Guid ProductId, Guid? ItemId, decimal Quantity);

public enum CountScanOutcome
{
    /// <summary>An expected line was counted (or a quantity added to it).</summary>
    Counted,

    /// <summary>A new unexpected line was opened, or a quantity added to one.</summary>
    Unexpected,

    /// <summary>The item was already counted at this location; nothing changed.</summary>
    AlreadyCounted,
}

public sealed record CountScanResult(CountScanOutcome Outcome, CountLine Line, CountScan? Scan);

/// <summary>
/// A stock count of a location (optionally with its sub-locations).
/// Counting → Reviewing (locked) → Approved; Counting or Reviewing → Cancelled.
/// Counting never moves stock; approval turns decided differences into movements.
/// </summary>
public sealed class CountSession : BaseEntity
{
    public const int NoteMaxLength = 500;

    private readonly List<CountLine> _lines = [];
    private readonly List<CountScan> _scans = [];

    private CountSession()
    {
    }

    public Guid LocationId { get; private set; }

    public bool IncludeChildren { get; private set; }

    public CountStatus Status { get; private set; }

    public Guid StartedBy { get; private set; }

    public DateTime StartedAt { get; private set; }

    public Guid? ApprovedBy { get; private set; }

    public DateTime? ApprovedAt { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Shared reference of the movements created by the approval.</summary>
    public Guid? ApprovalReferenceId { get; private set; }

    /// <summary>Last scan sequence; every scan bumps it, so concurrent scans conflict (xmin) instead of racing.</summary>
    public int ScanSequence { get; private set; }

    public IReadOnlyList<CountLine> Lines => _lines;

    public IReadOnlyList<CountScan> Scans => _scans;

    public bool IsOpen => Status is CountStatus.Counting or CountStatus.Reviewing;

    /// <summary>Starts a count with one expected line per snapshot entry.</summary>
    public static CountSession Start(
        Location location,
        bool includeChildren,
        string? note,
        IEnumerable<ExpectedStock> expected,
        Guid userId,
        DateTime now)
    {
        if (!location.IsActive)
        {
            throw new DomainException(CountErrors.LocationInactive, "Pasif lokasyonda sayım başlatılamaz.", "locationId");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > NoteMaxLength)
        {
            throw new DomainException(CountErrors.NoteTooLong, $"Not en fazla {NoteMaxLength} karakter olabilir.", "note");
        }

        var session = new CountSession
        {
            LocationId = location.Id,
            IncludeChildren = includeChildren,
            Status = CountStatus.Counting,
            StartedBy = userId,
            StartedAt = now,
            Note = trimmed,
        };
        session._lines.AddRange(expected.Select(e =>
            CountLine.Expected(session.Id, e.LocationId, e.ProductId, e.ItemId, e.Quantity)));
        return session;
    }

    /// <summary>
    /// Whether two scopes share a location: the same root, or one includes children and contains the other.
    /// Paths are materialized location paths ("/root/…/id/").
    /// </summary>
    public static bool ScopesOverlap(string pathA, bool includeChildrenA, string pathB, bool includeChildrenB) =>
        pathA == pathB
        || (includeChildrenA && pathB.StartsWith(pathA, StringComparison.Ordinal))
        || (includeChildrenB && pathA.StartsWith(pathB, StringComparison.Ordinal));

    /// <summary>Whether <paramref name="location"/> is in the scope rooted at <paramref name="scopeRoot"/>.</summary>
    public bool Covers(Location scopeRoot, Location location)
    {
        if (scopeRoot.Id != LocationId)
        {
            throw new ArgumentException("Not the session's location.", nameof(scopeRoot));
        }

        return location.Id == LocationId
            || (IncludeChildren && location.Path.StartsWith(scopeRoot.Path, StringComparison.Ordinal));
    }

    /// <summary>Counts an item where it was scanned (the context location).</summary>
    public CountScanResult ScanItem(Location scopeRoot, Location context, Item item, Guid userId, DateTime now)
    {
        EnsureCanScan(scopeRoot, context);

        var here = _lines.SingleOrDefault(l => l.ItemId == item.Id && l.LocationId == context.Id);
        if (here is { IsCounted: true })
        {
            return new CountScanResult(CountScanOutcome.AlreadyCounted, here, null);
        }

        // Expected here → counted. Anywhere else (another shelf in scope, or out of scope) → unexpected here;
        // a matching expected line elsewhere is then treated as found during review.
        var outcome = here?.LineType == CountLineType.Expected ? CountScanOutcome.Counted : CountScanOutcome.Unexpected;
        var line = here ?? AddUnexpected(context, item.ProductId, item.Id);
        var scan = AddScan(line, 1, userId, now);
        return new CountScanResult(outcome, line, scan);
    }

    /// <summary>Adds a counted quantity of a quantity product at the context location.</summary>
    public CountScanResult ScanQuantity(
        Location scopeRoot,
        Location context,
        Product product,
        decimal quantity,
        Guid userId,
        DateTime now)
    {
        EnsureCanScan(scopeRoot, context);

        if (product.TrackingType == TrackingType.Serialized)
        {
            throw new DomainException(
                CountErrors.ScanItemLabel,
                $"{product.Name} tekil takipli: adetin kendi etiketini (ITM-…) okutun.",
                "code");
        }

        if (quantity <= 0)
        {
            throw new DomainException(CountErrors.InvalidQuantity, "Miktar sıfırdan büyük olmalıdır.", "quantity");
        }

        var here = _lines.SingleOrDefault(l => l.ItemId == null && l.ProductId == product.Id && l.LocationId == context.Id);
        if (here is null && !product.IsActive)
        {
            throw new DomainException(CountErrors.ProductInactive, "Pasif ürün sayıma eklenemez.", "code");
        }

        var outcome = here?.LineType == CountLineType.Expected ? CountScanOutcome.Counted : CountScanOutcome.Unexpected;
        var line = here ?? AddUnexpected(context, product.Id, null);
        var scan = AddScan(line, quantity, userId, now);
        return new CountScanResult(outcome, line, scan);
    }

    /// <summary>Removes the line's last scan; an unexpected line without scans disappears.</summary>
    public void UndoLastScan(Guid lineId)
    {
        EnsureStatus(CountStatus.Counting, CountErrors.NotCounting, "Sayım incelemeye gönderildi; okutma geri alınamaz.");

        var line = _lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new DomainException(CountErrors.LineNotFound, "Sayım satırı bulunamadı.", "lineId");
        var last = _scans.Where(s => s.CountLineId == lineId).MaxBy(s => s.Sequence)
            ?? throw new DomainException(CountErrors.NothingToUndo, "Bu satırda geri alınacak okutma yok.", "lineId");

        _scans.Remove(last);
        Recount(line);
        if (line.LineType == CountLineType.Unexpected && !line.IsCounted)
        {
            _lines.Remove(line);
        }

        ScanSequence++;
    }

    /// <summary>Locks the count for review.</summary>
    public void Submit() =>
        Transition(CountStatus.Reviewing, CountStatus.Counting);

    public void Cancel()
    {
        if (!IsOpen)
        {
            throw new DomainException(CountErrors.InvalidTransition, $"{Label(Status)} sayım iptal edilemez.");
        }

        Status = CountStatus.Cancelled;
    }

    /// <summary>Records the approval; the decided movements are created by <see cref="CountApprovalService"/>.</summary>
    internal void Approve(IReadOnlyDictionary<Guid, CountResolution> resolutions, Guid userId, DateTime now, Guid referenceId)
    {
        Transition(CountStatus.Approved, CountStatus.Reviewing);
        foreach (var line in _lines)
        {
            line.Resolve(resolutions.TryGetValue(line.Id, out var resolution) ? resolution : null);
        }

        ApprovedBy = userId;
        ApprovedAt = now;
        ApprovalReferenceId = referenceId;
    }

    internal void EnsureReviewing() =>
        EnsureStatus(CountStatus.Reviewing, CountErrors.InvalidTransition, $"Sadece incelemedeki sayım onaylanabilir; bu sayım {Label(Status)}.");

    public static string Label(CountStatus status) => status switch
    {
        CountStatus.Counting => "sayılıyor",
        CountStatus.Reviewing => "incelemede",
        CountStatus.Approved => "onaylandı",
        CountStatus.Cancelled => "iptal edildi",
        _ => status.ToString(),
    };

    /// <summary>Counting and the context location is in scope.</summary>
    public void EnsureCanScan(Location scopeRoot, Location context)
    {
        EnsureStatus(CountStatus.Counting, CountErrors.NotCounting, "Sayım incelemeye gönderildi; okutma yapılamaz.");
        if (!Covers(scopeRoot, context))
        {
            throw new DomainException(
                CountErrors.LocationOutOfScope,
                $"{context.Name} bu sayımın kapsamında değil.",
                "locationId");
        }
    }

    private CountLine AddUnexpected(Location context, Guid productId, Guid? itemId)
    {
        var line = CountLine.Unexpected(Id, context.Id, productId, itemId);
        _lines.Add(line);
        return line;
    }

    private CountScan AddScan(CountLine line, decimal quantity, Guid userId, DateTime now)
    {
        ScanSequence++;
        var scan = CountScan.Create(Id, line.Id, ScanSequence, quantity, userId, now);
        _scans.Add(scan);
        Recount(line);
        return scan;
    }

    private void Recount(CountLine line)
    {
        var scans = _scans.Where(s => s.CountLineId == line.Id).ToList();
        line.SetCounted(scans.Count == 0 ? null : scans.Sum(s => s.Quantity));
    }

    private void Transition(CountStatus to, CountStatus from)
    {
        if (Status != from)
        {
            throw new DomainException(
                CountErrors.InvalidTransition,
                $"Sayım {Label(Status)}; {Label(to)} durumuna geçemez.");
        }

        Status = to;
    }

    private void EnsureStatus(CountStatus expected, string code, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(code, message);
        }
    }
}
