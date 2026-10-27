using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Reports;

/// <summary>Turkish texts written into report cells (the UI has its own labels).</summary>
public static class ReportLabels
{
    // Turkey has used UTC+3 all year since 2016 (same as BusinessDay).
    private static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);

    /// <summary>UTC from the database → Istanbul wall-clock time for the cell.</summary>
    public static DateTime Istanbul(DateTime utc) => DateTime.SpecifyKind(utc + IstanbulOffset, DateTimeKind.Unspecified);

    public static DateTime? Istanbul(DateTime? utc) => utc is { } value ? Istanbul(value) : null;

    public static string Tracking(TrackingType type) => type == TrackingType.Serialized ? "Tekil" : "Miktar bazlı";

    public static string Unit(Unit unit) => unit switch
    {
        Domain.Catalog.Unit.Piece => "Adet",
        Domain.Catalog.Unit.Kg => "Kg",
        Domain.Catalog.Unit.Meter => "Metre",
        Domain.Catalog.Unit.Liter => "Litre",
        Domain.Catalog.Unit.Package => "Paket",
        Domain.Catalog.Unit.Box => "Kutu",
        _ => unit.ToString(),
    };

    public static string Movement(MovementType type) => type switch
    {
        MovementType.In => "Giriş",
        MovementType.Out => "Çıkış",
        MovementType.Transfer => "Transfer",
        MovementType.Adjustment => "Sayım düzeltmesi",
        MovementType.Assign => "Zimmet",
        MovementType.Return => "İade",
        MovementType.StatusChange => "Durum değişikliği",
        _ => type.ToString(),
    };

    public static string? Reason(MovementReason? reason) => reason switch
    {
        MovementReason.Consumed => "Tüketildi",
        MovementReason.Damaged => "Hasarlı",
        MovementReason.Lost => "Kayıp",
        MovementReason.Other => "Diğer",
        _ => null,
    };

    public static string YesNo(bool value) => value ? "Evet" : "Hayır";
}
