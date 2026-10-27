using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockTracker.Application.Labels;

namespace StockTracker.Infrastructure.Labels;

/// <summary>
/// Label PDFs with fixed millimetre geometry (print at 100 % / actual size).
/// Each sheet page is laid out cell by cell, so the page count is exact.
/// </summary>
public sealed class QuestPdfLabelRenderer : ILabelRenderer
{
    /// <summary>Paper and cell geometry in millimetres.</summary>
    public sealed record Layout(
        float PageWidth,
        float PageHeight,
        int Columns,
        int Rows,
        float CellWidth,
        float CellHeight,
        float MarginLeft,
        float MarginTop,
        float QrSize,
        float Padding);

    public static readonly IReadOnlyDictionary<LabelTemplate, Layout> Layouts = new Dictionary<LabelTemplate, Layout>
    {
        [LabelTemplate.Sheet3x8] = new(210, 297, 3, 8, 70, 37, 0, 0.5f, 28, 2.5f),
        [LabelTemplate.Sheet2x4] = new(210, 297, 2, 4, 105, 74, 0, 0.5f, 50, 4),
        [LabelTemplate.Sheet5x13] = new(210, 297, 5, 13, 38, 21, 10, 12, 17, 1.5f),
        [LabelTemplate.Thermal50x30] = new(50, 30, 1, 1, 50, 30, 0, 0, 24, 2),
    };

    static QuestPdfLabelRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(LabelTemplate template, IReadOnlyList<LabelData> labels, int startPosition, string baseUrl)
    {
        var layout = Layouts[template];
        var perPage = layout.Columns * layout.Rows;

        // Empty cells first (a half-used sheet), then the labels.
        var cells = Enumerable.Repeat<LabelData?>(null, startPosition - 1).Concat(labels).ToList();
        var pages = cells.Chunk(perPage).ToList();

        return Document.Create(document =>
        {
            foreach (var page in pages)
            {
                document.Page(p =>
                {
                    p.Size(layout.PageWidth, layout.PageHeight, Unit.Millimetre);
                    p.MarginLeft(layout.MarginLeft, Unit.Millimetre);
                    p.MarginTop(layout.MarginTop, Unit.Millimetre);
                    p.MarginRight(0);
                    p.MarginBottom(0);
                    p.DefaultTextStyle(t => t.FontFamily("Lato").FontColor(Colors.Black));
                    p.Content().Column(column =>
                    {
                        for (var row = 0; row < layout.Rows; row++)
                        {
                            var rowCells = page.Skip(row * layout.Columns).Take(layout.Columns).ToList();
                            if (rowCells.Count == 0)
                            {
                                break;
                            }

                            column.Item().Height(layout.CellHeight, Unit.Millimetre).Row(r =>
                            {
                                foreach (var cell in rowCells)
                                {
                                    r.ConstantItem(layout.CellWidth, Unit.Millimetre).Element(c =>
                                    {
                                        if (cell is not null)
                                        {
                                            Cell(c, template, layout, cell, baseUrl);
                                        }
                                    });
                                }
                            });
                        }
                    });
                });
            }
        }).GeneratePdf();
    }

    private static void Cell(IContainer container, LabelTemplate template, Layout layout, LabelData label, string baseUrl)
    {
        var qr = QrCodes.Png(QrCodes.Content(baseUrl, label.Code));
        container.Padding(layout.Padding, Unit.Millimetre).Row(row =>
        {
            row.ConstantItem(layout.QrSize, Unit.Millimetre)
                .AlignMiddle()
                .Height(layout.QrSize, Unit.Millimetre)
                .Image(qr)
                .FitArea();
            row.ConstantItem(layout.Padding, Unit.Millimetre);
            row.RelativeItem().AlignMiddle().Column(text => Text(text, template, label));
        });
    }

    private static void Text(ColumnDescriptor text, LabelTemplate template, LabelData label)
    {
        switch (template)
        {
            case LabelTemplate.Sheet3x8:
                text.Item().Text(label.Code).FontSize(9).Bold();
                text.Item().PaddingTop(1).Text(label.Title).FontSize(8).ClampLines(2);
                if (label.Detail is not null)
                {
                    text.Item().PaddingTop(1).Text(label.Detail).FontSize(7).ClampLines(1);
                }

                break;

            case LabelTemplate.Sheet2x4:
                text.Item().Text(label.Code).FontSize(11).Bold();
                text.Item().PaddingTop(2).Text(label.Title).FontSize(18).Bold().ClampLines(2);
                if (label.Detail is not null)
                {
                    text.Item().PaddingTop(2).Text(label.Detail).FontSize(9).ClampLines(2);
                }

                break;

            case LabelTemplate.Sheet5x13:
                text.Item().Text(label.Code).FontSize(6).Bold();
                break;

            default:
                text.Item().Text(label.Code).FontSize(8).Bold();
                text.Item().PaddingTop(1).Text(label.Title).FontSize(7).ClampLines(1);
                break;
        }
    }
}
