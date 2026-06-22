using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReportePersonal;

public static class ReportePersonalPdfGenerator
{
    private const string ReportTitle = "LISTADO DE PERSONAL";

    public static byte[] Generate(
        IReadOnlyCollection<ReportePersonalResponse> data,
        ReportePersonalGetAllQuery query,
        IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoPath = Path.Combine(environment.ContentRootPath, "Assets", "Reports", "logoLeft.jpeg");
        var generatedAt = DateTime.Now;
        var culture = CultureInfo.GetCultureInfo("es-VE");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(text => text.FontSize(8));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(72).Height(45).Element(element =>
                        {
                            if (File.Exists(logoPath))
                            {
                                element.Image(logoPath).FitArea();
                            }
                            else
                            {
                                element.Text(string.Empty);
                            }
                        });

                        row.RelativeItem().Column(title =>
                        {
                            title.Item().AlignCenter().Text(ReportTitle).Bold().FontSize(14);
                            title.Item().AlignCenter().Text($"Fecha: {generatedAt:dd/MM/yyyy HH:mm}");
                            title.Item().AlignCenter().Text(BuildFilterDescription(query));
                        });

                        row.ConstantItem(72).AlignRight().Text(text =>
                        {
                            text.Span("Pagina ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });

                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(2.4f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(2.5f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(2.3f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.2f);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header, "Cedula");
                        HeaderCell(header, "Nombre");
                        HeaderCell(header, "Ingreso");
                        HeaderCell(header, "Departamento");
                        HeaderCell(header, "Codigo");
                        HeaderCell(header, "Cargo");
                        HeaderCell(header, "Sueldo");
                        HeaderCell(header, "Status");
                        HeaderCell(header, "Nomina");
                    });

                    foreach (var item in data)
                    {
                        BodyCell(table, item.Cedula);
                        BodyCell(table, item.Nombre);
                        BodyCell(table, item.FechaIngreso?.ToString("dd/MM/yyyy", culture) ?? string.Empty);
                        BodyCell(table, item.Departamento);
                        BodyCell(table, item.Codigo);
                        BodyCell(table, item.Cargo);
                        BodyCell(table, item.Sueldo.ToString("N2", culture), alignRight: true);
                        BodyCell(table, item.DescripcionStatus);
                        BodyCell(table, item.TipoNomina);
                    }
                });

                page.Footer().AlignRight().Text($"Total registros: {data.Count}").FontSize(8);
            });
        }).GeneratePdf();
    }

    private static string BuildFilterDescription(ReportePersonalGetAllQuery query)
    {
        var tipoNomina = query.CodigoTipoNomina.HasValue && query.CodigoTipoNomina.Value > 0
            ? query.CodigoTipoNomina.Value.ToString(CultureInfo.InvariantCulture)
            : "Todos";

        var status = string.IsNullOrWhiteSpace(query.Status) ? "Todos" : query.Status.Trim();

        return $"Tipo nomina: {tipoNomina} | Status: {status}";
    }

    private static void HeaderCell(TableCellDescriptor table, string text)
    {
        table.Cell()
            .Background(Colors.Grey.Lighten3)
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(4)
            .Text(text)
            .Bold();
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell()
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(3);

        var descriptor = cell.Text(text ?? string.Empty);

        if (alignRight)
        {
            descriptor.AlignRight();
        }
    }
}

[ApiController]
[Route("api/ReportePersonal")]
public class ReportePersonalPdfController(
    ConnectionDB _connectionDB,
    IConfiguration _config,
    IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> GeneratePdf(ReportePersonalGetAllQuery value)
    {
        var handler = new ReportePersonalGetAllHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        if (!result.IsValid || result.Data is null)
        {
            return Ok(result);
        }

        var pdfBytes = ReportePersonalPdfGenerator.Generate(result.Data, value, _environment);
        var fileName = BuildFileName(value);

        return File(pdfBytes, "application/pdf", fileName);
    }

    private static string BuildFileName(ReportePersonalGetAllQuery value)
    {
        var tipoNomina = value.CodigoTipoNomina.HasValue && value.CodigoTipoNomina.Value > 0
            ? value.CodigoTipoNomina.Value.ToString(CultureInfo.InvariantCulture)
            : "todos";

        return $"reporte-personal-{tipoNomina}-{DateTime.Now:yyyyMMddHHmmss}.pdf";
    }
}
