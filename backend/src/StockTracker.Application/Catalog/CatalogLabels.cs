using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Catalog;

/// <summary>Turkish unit names for server-written texts (notifications); the UI has its own labels.</summary>
public static class CatalogLabels
{
    public static string Unit(Unit unit) => unit switch
    {
        Domain.Catalog.Unit.Piece => "adet",
        Domain.Catalog.Unit.Kg => "kg",
        Domain.Catalog.Unit.Meter => "metre",
        Domain.Catalog.Unit.Liter => "litre",
        Domain.Catalog.Unit.Package => "paket",
        Domain.Catalog.Unit.Box => "kutu",
        _ => unit.ToString(),
    };
}
