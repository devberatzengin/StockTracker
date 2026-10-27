namespace StockTracker.Application.Reports;

public enum ReportColumnType
{
    Text,
    Number,

    /// <summary>Istanbul local date and time (dd.MM.yyyy HH:mm).</summary>
    DateTime,

    /// <summary>Calendar day (dd.MM.yyyy).</summary>
    Date,
}

/// <param name="Width">Excel column width in characters; fixed, so no font measuring is needed.</param>
public sealed record ReportColumn(string Header, ReportColumnType Type, double Width);

public sealed record ReportSheet(string Name, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<object?[]> Rows);

public sealed record ReportFile(byte[] Content, string FileName);

/// <summary>Writes sheets to an .xlsx workbook (ClosedXML).</summary>
public interface IReportWriter
{
    byte[] Write(IReadOnlyList<ReportSheet> sheets);
}

public static class ReportLimits
{
    public const int MaxRows = 50_000;
    public const string TooManyRows = "Reports.TooManyRows";

    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
