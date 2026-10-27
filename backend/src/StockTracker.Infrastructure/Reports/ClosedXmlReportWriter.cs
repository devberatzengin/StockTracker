using ClosedXML.Excel;
using StockTracker.Application.Reports;

namespace StockTracker.Infrastructure.Reports;

/// <summary>
/// One worksheet per sheet: bold frozen header, auto filter, fixed column widths
/// (no font measuring, so no system fonts are needed in the container).
/// </summary>
public sealed class ClosedXmlReportWriter : IReportWriter
{
    public byte[] Write(IReadOnlyList<ReportSheet> sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var sheet in sheets)
        {
            var ws = workbook.Worksheets.Add(sheet.Name);
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                var column = sheet.Columns[c];
                ws.Cell(1, c + 1).Value = column.Header;
                ws.Column(c + 1).Width = column.Width;
                ws.Column(c + 1).Style.NumberFormat.Format = column.Type switch
                {
                    ReportColumnType.DateTime => "dd.mm.yyyy hh:mm",
                    ReportColumnType.Date => "dd.mm.yyyy",
                    ReportColumnType.Number => "#,##0.###",
                    _ => "@",
                };
            }

            for (var r = 0; r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                for (var c = 0; c < row.Length; c++)
                {
                    ws.Cell(r + 2, c + 1).Value = ToCell(row[c]);
                }
            }

            var header = ws.Row(1);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECEF");
            ws.SheetView.FreezeRows(1);
            ws.Range(1, 1, Math.Max(1, sheet.Rows.Count + 1), sheet.Columns.Count).SetAutoFilter();
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static XLCellValue ToCell(object? value) => value switch
    {
        null => Blank.Value,
        string text => text,
        decimal number => number,
        int number => number,
        DateTime time => time,
        DateOnly day => day.ToDateTime(TimeOnly.MinValue),
        bool flag => flag,
        _ => value.ToString(),
    };
}
