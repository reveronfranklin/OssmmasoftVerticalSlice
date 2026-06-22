using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteBm1;

public static class ReporteBm1PdfGenerator
{
    private const string ReportTitle = "REPORTE BM1";

    public static byte[] Generate(
        IReadOnlyCollection<ReporteBm1ItemResponse> items,
        ReporteBm1GetAllQuery query,
        IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var totalCantidad = items.Sum(item => item.Cantidad);
        var totalValor = items.Sum(item => item.ValorActual);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(14);
                page.DefaultTextStyle(style => style.FontSize(5.2f));

                page.Header().Element(element => BuildHeader(element, query, logoBytes));
                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.8f);
                        columns.ConstantColumn(24);
                        columns.ConstantColumn(26);
                        columns.ConstantColumn(26);
                        columns.ConstantColumn(32);
                        columns.ConstantColumn(28);
                        columns.ConstantColumn(50);
                        columns.ConstantColumn(52);
                        columns.RelativeColumn(1.35f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.05f);
                        columns.RelativeColumn(1.25f);
                        columns.ConstantColumn(45);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header, "UNIDAD");
                        HeaderCell(header, "GRP");
                        HeaderCell(header, "NIV 1");
                        HeaderCell(header, "NIV 2");
                        HeaderCell(header, "LOTE");
                        HeaderCell(header, "CANT");
                        HeaderCell(header, "PLACA");
                        HeaderCell(header, "VALOR");
                        HeaderCell(header, "ARTICULO");
                        HeaderCell(header, "ESPECIFICACION");
                        HeaderCell(header, "SERVICIO");
                        HeaderCell(header, "RESPONSABLE");
                        HeaderCell(header, "FECHA");
                    });

                    foreach (var item in items)
                    {
                        BodyCell(table, item.UnidadTrabajo);
                        BodyCell(table, item.CodigoGrupo, alignCenter: true);
                        BodyCell(table, item.CodigoNivel1, alignCenter: true);
                        BodyCell(table, item.CodigoNivel2, alignCenter: true);
                        BodyCell(table, item.NumeroLote, alignCenter: true);
                        BodyCell(table, item.Cantidad.ToString(CultureInfo.InvariantCulture), alignRight: true);
                        BodyCell(table, item.NumeroPlaca);
                        BodyCell(table, FormatAmount(item.ValorActual, culture), alignRight: true);
                        BodyCell(table, item.Articulo);
                        BodyCell(table, item.Especificacion);
                        BodyCell(table, item.Servicio);
                        BodyCell(table, item.ResponsableBien);
                        BodyCell(table, FormatDate(item.FechaMovimiento), alignCenter: true);
                    }

                    if (items.Count == 0)
                    {
                        EmptyCell(table, "Sin registros para los filtros seleccionados", 13);
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Registros: {items.Count:N0} | Cantidad: {totalCantidad:N0} | Valor actual: {FormatAmount(totalValor, culture)}").Bold();
                    row.ConstantItem(95).AlignRight().Text(text =>
                    {
                        text.Span("Pagina ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void BuildHeader(IContainer container, ReporteBm1GetAllQuery query, byte[]? logoBytes)
    {
        container.Row(row =>
        {
            row.ConstantItem(110).Height(38).Element(element =>
            {
                if (logoBytes is not null)
                {
                    element.AlignLeft().Image(logoBytes).FitArea();
                }
                else
                {
                    element.AlignLeft().Text("LOGO").Bold().FontSize(8);
                }
            });

            row.RelativeItem().AlignCenter().Column(column =>
            {
                column.Item().Text(ReportTitle).Bold().FontSize(12);
                column.Item().PaddingTop(2).Text(BuildFilterText(query)).FontSize(7);
            });

            row.ConstantItem(120).AlignRight().Column(column =>
            {
                column.Item().Text($"FECHA EMISION: {DateTime.Now:dd/MM/yyyy}").Bold().FontSize(7);
                column.Item().Text($"HORA: {DateTime.Now:hh:mm tt}").FontSize(7);
            });
        });
    }

    private static string BuildFilterText(ReporteBm1GetAllQuery query)
    {
        var fechaDesde = query.FechaDesde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        var fechaHasta = query.FechaHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        var icps = ReporteBm1Db.ToCsv(query.CodigosIcp);
        return string.IsNullOrWhiteSpace(icps)
            ? $"Fecha desde: {fechaDesde} | Fecha hasta: {fechaHasta} | ICP: Todos"
            : $"Fecha desde: {fechaDesde} | Fecha hasta: {fechaHasta} | ICP: {icps}";
    }

    private static void HeaderCell(TableCellDescriptor table, string text)
    {
        table.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().AlignMiddle()
            .Text(text).Bold().FontSize(4.8f);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false, bool alignCenter = false)
    {
        var cell = table.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten1).Padding(1.5f).MinHeight(10);

        if (alignRight)
        {
            cell.AlignRight().Text(text).FontSize(4.8f);
            return;
        }

        if (alignCenter)
        {
            cell.AlignCenter().Text(text).FontSize(4.8f);
            return;
        }

        cell.Text(text).FontSize(4.8f);
    }

    private static void EmptyCell(TableDescriptor table, string text, uint colSpan)
    {
        table.Cell().ColumnSpan(colSpan).Border(0.5f).Padding(8).AlignCenter().Text(text).Bold();
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string FormatAmount(decimal value, CultureInfo culture)
    {
        return value.ToString("N2", culture);
    }

    private static byte[]? TryReadReportAsset(IWebHostEnvironment environment, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "ReportAssets", fileName),
            Path.Combine(environment.ContentRootPath, "Assets", fileName),
            Path.Combine(environment.WebRootPath ?? string.Empty, "images", fileName)
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        return null;
    }
}
