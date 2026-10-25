using FluentValidation;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;

namespace StockTracker.Application.Inventory;

public sealed record CreatedItemDto(Guid Id, string Code, Guid ProductId, string? SerialNumber);

/// <summary>Result of a stock command: the shared reference of its movements and any new items.</summary>
public sealed record MovementResultDto(Guid ReferenceId, int MovementCount, IReadOnlyList<CreatedItemDto> CreatedItems);

/// <summary>
/// A line moving stock out of a location: either an item, or a quantity of a product at a source location.
/// </summary>
public sealed record StockLine(Guid? ItemId, Guid? ProductId, Guid? FromLocationId, decimal? Quantity);

public static class MovementLimits
{
    public const int MaxLines = 100;
}

internal sealed class StockLineValidator : AbstractValidator<StockLine>
{
    public StockLineValidator()
    {
        RuleFor(x => x)
            .Must(l => l.ItemId is not null
                ? l.ProductId is null && l.FromLocationId is null && l.Quantity is null
                : l.ProductId is not null && l.FromLocationId is not null && l.Quantity is not null)
            .WithMessage("Satır ya bir adet (itemId) ya da ürün, kaynak lokasyon ve miktar içermelidir.")
            .OverridePropertyName("itemId");

        RuleFor(x => x.Quantity!.Value)
            .GreaterThan(0).WithMessage("Miktar sıfırdan büyük olmalıdır.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true).WithMessage("Miktar en fazla 3 ondalık basamak içerebilir.")
            .OverridePropertyName("quantity")
            .When(x => x.Quantity is not null);
    }
}

internal static class MovementCommandHelpers
{
    /// <summary>Attributes domain errors of line <paramref name="index"/> to <c>lines[index].field</c>.</summary>
    public static T Line<T>(int index, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DomainException e)
        {
            throw e.WithTarget($"lines[{index}].{e.Target ?? "productId"}");
        }
    }

    public static DomainException LineError(int index, string field, string code, string message) =>
        new(code, message, $"lines[{index}].{field}");

    public static async Task<Location> LoadTargetAsync(ILocationRepository locations, Guid id, CancellationToken cancellationToken) =>
        await locations.GetByIdAsync(id, cancellationToken)
        ?? throw new DomainException(InventoryErrors.LocationNotFound, "Hedef lokasyon bulunamadı.", "toLocationId");

    public static async Task<Dictionary<Guid, Product>> LoadProductsAsync(
        IProductRepository products,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken) =>
        (await products.GetByIdsAsync([.. ids.Distinct()], cancellationToken)).ToDictionary(p => p.Id);

    public static async Task<Dictionary<Guid, Location>> LoadLocationsAsync(
        ILocationRepository locations,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken) =>
        (await locations.GetByIdsAsync([.. ids.Distinct()], cancellationToken)).ToDictionary(l => l.Id);

    public static Product Product(Dictionary<Guid, Product> products, Guid id, int index) =>
        products.GetValueOrDefault(id)
        ?? throw LineError(index, "productId", InventoryErrors.ProductNotFound, "Ürün bulunamadı.");

    public static Location Source(Dictionary<Guid, Location> locations, Guid id, int index) =>
        locations.GetValueOrDefault(id)
        ?? throw LineError(index, "fromLocationId", InventoryErrors.LocationNotFound, "Kaynak lokasyon bulunamadı.");

    public static void EnsureDistinctItems(IReadOnlyList<StockLine> lines)
    {
        var seen = new HashSet<Guid>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].ItemId is { } itemId && !seen.Add(itemId))
            {
                throw LineError(i, "itemId", InventoryErrors.InvalidMovement, "Aynı adet bir işlemde birden fazla kez kullanılamaz.");
            }
        }
    }

    /// <summary>
    /// Loads the balances for the keys and returns one tracked instance per (product, location),
    /// creating empty balances on demand so several lines can share them.
    /// </summary>
    public static async Task<BalanceSet> LoadBalancesAsync(
        IStockBalanceRepository balances,
        IReadOnlyCollection<(Guid ProductId, Guid LocationId)> keys,
        CancellationToken cancellationToken) =>
        new(balances, await balances.GetAsync(keys, cancellationToken));
}

internal sealed class BalanceSet(IStockBalanceRepository repository, IEnumerable<StockBalance> existing)
{
    private readonly Dictionary<(Guid, Guid), StockBalance> _balances =
        existing.ToDictionary(b => (b.ProductId, b.LocationId));

    public StockBalance? Find(Guid productId, Guid locationId) => _balances.GetValueOrDefault((productId, locationId));

    public StockBalance GetOrCreate(Guid productId, Guid locationId)
    {
        if (!_balances.TryGetValue((productId, locationId), out var balance))
        {
            balance = StockBalance.Empty(productId, locationId);
            repository.Add(balance);
            _balances[(productId, locationId)] = balance;
        }

        return balance;
    }
}
