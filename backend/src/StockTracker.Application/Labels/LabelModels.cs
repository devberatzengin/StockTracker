namespace StockTracker.Application.Labels;

/// <summary>Label sheets of the plan (sizes in millimetres are fixed in the renderer).</summary>
public enum LabelTemplate
{
    /// <summary>A4, 3 × 8 labels of 70 × 37 mm: products and items.</summary>
    Sheet3x8,

    /// <summary>A4, 2 × 4 labels of 105 × 74 mm: shelves, cabinets, rooms.</summary>
    Sheet2x4,

    /// <summary>A4, 5 × 13 labels of 38 × 21 mm: small products (QR and code only).</summary>
    Sheet5x13,

    /// <summary>Thermal roll, one 50 × 30 mm label per page.</summary>
    Thermal50x30,
}

/// <param name="Title">Product or location name.</param>
/// <param name="Detail">Serial number of an item, or the last two levels of a location's parent path.</param>
public sealed record LabelData(string Code, string Title, string? Detail);

public static class LabelSheets
{
    public const int MaxLabels = 500;

    /// <summary>Labels per page; a thermal roll has one per "page".</summary>
    public static int CellsPerPage(LabelTemplate template) => template switch
    {
        LabelTemplate.Sheet3x8 => 24,
        LabelTemplate.Sheet2x4 => 8,
        LabelTemplate.Sheet5x13 => 65,
        _ => 1,
    };

    public static bool IsSheet(LabelTemplate template) => template != LabelTemplate.Thermal50x30;

    /// <param name="startPosition">1-based cell of the first label; earlier cells stay empty (half-used sheets).</param>
    public static int PageCount(LabelTemplate template, int labelCount, int startPosition) =>
        labelCount == 0 ? 0 : (int)Math.Ceiling((labelCount + startPosition - 1) / (double)CellsPerPage(template));
}

public sealed record LabelFile(byte[] Content, string FileName);

public interface ILabelRenderer
{
    /// <param name="baseUrl">QR content is "{baseUrl}/s/{code}".</param>
    byte[] Render(LabelTemplate template, IReadOnlyList<LabelData> labels, int startPosition, string baseUrl);
}

/// <summary>PUBLIC_BASE_URL: the address QR labels open (the app as phones reach it).</summary>
public interface IPublicUrls
{
    string? BaseUrl { get; }
}
