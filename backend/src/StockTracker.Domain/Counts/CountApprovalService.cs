using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;

namespace StockTracker.Domain.Counts;

/// <summary>Stock the approval may change, loaded by the caller and tracked for saving.</summary>
public interface ICountApprovalStock
{
    Product Product(Guid id);

    Location Location(Guid id);

    Item Item(Guid id);

    /// <summary>The balance of (product, location); created empty when missing.</summary>
    StockBalance Balance(Guid productId, Guid locationId);
}

/// <summary>
/// Turns the approver's decisions into movements (decision table of the plan):
/// quantity difference → Adjustment, lost item → Out (Lost), unexpected in-stock item → Transfer.
/// All movements share one reference; the caller saves them with the session in one transaction.
/// </summary>
public sealed class CountApprovalService(StockMovementService movements)
{
    public const string LostNote = "Sayımda bulunamadı.";

    public IReadOnlyList<StockMovement> Approve(
        CountSession session,
        IReadOnlyList<CountReviewLine> review,
        IReadOnlyDictionary<Guid, CountResolution> decisions,
        ICountApprovalStock stock,
        MovementContext context)
    {
        session.EnsureReviewing();
        Validate(review, decisions);

        var ledger = new List<StockMovement>();
        foreach (var line in review.Where(l => l.Options.Count > 0))
        {
            var decision = decisions[line.Line.Id];
            try
            {
                if (Apply(line, decision, stock, context) is { } movement)
                {
                    ledger.Add(movement);
                }
            }
            catch (DomainException e)
            {
                throw e.WithTarget(Target(line.Line.Id));
            }
        }

        session.Approve(decisions, context.UserId, context.OccurredAt, context.ReferenceId);
        return ledger;
    }

    private StockMovement? Apply(CountReviewLine line, CountResolution decision, ICountApprovalStock stock, MovementContext context)
    {
        var l = line.Line;
        switch (decision)
        {
            case CountResolution.ApplyDifference:
            case CountResolution.AcceptZero:
                return movements.AdjustQuantity(
                    stock.Product(l.ProductId),
                    stock.Location(l.LocationId),
                    stock.Balance(l.ProductId, l.LocationId),
                    line.Difference,
                    context);
            case CountResolution.MarkLost:
                return movements.DisposeItem(
                    stock.Item(l.ItemId!.Value),
                    stock.Product(l.ProductId),
                    stock.Location(l.LocationId),
                    MovementReason.Lost,
                    LostNote,
                    context);
            case CountResolution.MoveHere:
                return movements.TransferItem(
                    stock.Item(l.ItemId!.Value),
                    stock.Product(l.ProductId),
                    stock.Location(l.LocationId),
                    context);
            default:
                return null;
        }
    }

    private static void Validate(IReadOnlyList<CountReviewLine> review, IReadOnlyDictionary<Guid, CountResolution> decisions)
    {
        var byId = review.ToDictionary(l => l.Line.Id);
        foreach (var (lineId, decision) in decisions)
        {
            if (!byId.TryGetValue(lineId, out var line) || !line.Options.Contains(decision))
            {
                throw new DomainException(
                    CountErrors.InvalidResolution,
                    "Bu satır için bu karar verilemez.",
                    Target(lineId));
            }
        }

        var undecided = review.Where(l => l.Options.Count > 0 && !decisions.ContainsKey(l.Line.Id)).ToList();
        if (undecided.Count > 0)
        {
            throw new DomainException(
                CountErrors.UndecidedLines,
                $"{undecided.Count} fark satırı için karar verilmedi.",
                Target(undecided[0].Line.Id));
        }
    }

    private static string Target(Guid lineId) => $"decisions[{lineId}]";
}
