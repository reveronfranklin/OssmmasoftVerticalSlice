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

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var generatedAt = DateTime.Now;
        var culture = CultureInfo.GetCultureInfo("es-VE");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(text => text.FontSize(7));

                page.Header().Column(column =>
                {
                    column.Item().Border(0.75f).Padding(6).Column(header =>
                    {
                        header.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Republica Bolivariana de Venezuela").Bold().FontSize(8);
                            row.RelativeItem().AlignRight().Text($"FECHA DE EMISION: {generatedAt:dd/MM/yyyy}").Bold().FontSize(8);
                        });

                        header.Item().PaddingTop(8).Row(row =>
                        {
                            row.ConstantItem(120).Height(54).Element(element =>
                            {
                                if (logoBytes is not null)
                                {
                                    element.AlignCenter().AlignMiddle().Image(logoBytes).FitArea();
                                }
                                else
                                {
                                    element.AlignCenter().AlignMiddle().Text("LOGO").Bold().FontSize(8);
                                }
                            });

                            row.RelativeItem().AlignCenter().PaddingTop(18).Text(ReportTitle).Bold().FontSize(10);

                            row.ConstantItem(120).AlignRight().PaddingTop(18).DefaultTextStyle(style => style.Bold().FontSize(8)).Text(text =>
                            {
                                text.Span("Pagina ");
                                text.CurrentPageNumber();
                                text.Span(" de ");
                                text.TotalPages();
                            });
                        });

                        header.Item().PaddingTop(4).AlignCenter().Text(BuildFilterDescription(query)).FontSize(7);
                    });
                });

                page.Content().PaddingTop(12).Column(content =>
                {
                    var departments = data
                        .GroupBy(item => string.IsNullOrWhiteSpace(item.Departamento) ? "SIN UNIDAD EJECUTORA" : item.Departamento.Trim())
                        .OrderBy(group => group.Key)
                        .ToList();

                    foreach (var department in departments)
                    {
                        content.Item().Element(element => BuildDepartmentHeader(element, department.Key));
                        content.Item().Element(element => BuildPersonnelTable(element, department.ToList(), culture));
                        content.Item().Element(element => BuildDepartmentTotals(element, department.Count(), department.Sum(item => item.Sueldo), culture));
                    }

                    content.Item().PaddingTop(18).Element(element => BuildGrandTotals(element, data.Count, data.Sum(item => item.Sueldo), culture));
                });

                page.Footer().AlignRight().Text($"Total registros: {data.Count}").Bold().FontSize(8);
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

    private static byte[]? TryReadReportAsset(IWebHostEnvironment environment, string fileName)
    {
        var relativePath = Path.Combine("Assets", "Reports", fileName);
        var candidatePaths = new[]
        {
            Path.Combine(environment.ContentRootPath, relativePath),
            Path.Combine(AppContext.BaseDirectory, relativePath)
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        return null;
    }

    private static void BuildDepartmentHeader(IContainer container, string department)
    {
        container.PaddingBottom(10).Column(column =>
        {
            column.Item()
                .Border(0.75f)
                .Background(Colors.Grey.Lighten3)
                .PaddingVertical(1)
                .AlignCenter()
                .Text("UNIDAD EJECUTORA")
                .Bold()
                .FontSize(8);

            column.Item()
                .Border(0.75f)
                .Background(Colors.Grey.Lighten4)
                .PaddingVertical(1)
                .AlignCenter()
                .Text(department)
                .Bold()
                .FontSize(8);
        });
    }

    private static void BuildPersonnelTable(IContainer container, IReadOnlyCollection<ReportePersonalResponse> data, CultureInfo culture)
    {
        container.PaddingBottom(8).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60);
                columns.ConstantColumn(180);
                columns.ConstantColumn(80);
                columns.RelativeColumn();
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
            });

            table.Header(header =>
            {
                HeaderCell(header, "CEDULA");
                HeaderCell(header, "APELLIDOS Y NOMBRES");
                HeaderCell(header, "CODIGO CARGO");
                HeaderCell(header, "DENOMINACION");
                HeaderCell(header, "STATUS");
                HeaderCell(header, "SUELDO");
            });

            foreach (var item in data)
            {
                BodyCell(table, item.Cedula, alignCenter: true);
                BodyCell(table, item.Nombre);
                BodyCell(table, item.Codigo, alignCenter: true);
                BodyCell(table, item.Cargo);
                BodyCell(table, item.DescripcionStatus, alignCenter: true);
                BodyCell(table, item.Sueldo.ToString("N2", culture), alignRight: true);
            }
        });
    }

    private static void BuildDepartmentTotals(IContainer container, int totalRecords, decimal totalSalary, CultureInfo culture)
    {
        BuildTotals(container, "TOTAL UNIDAD EJECUTORA", totalRecords, totalSalary, culture, 7);
    }

    private static void BuildGrandTotals(IContainer container, int totalRecords, decimal totalSalary, CultureInfo culture)
    {
        BuildTotals(container, "GRAN TOTAL", totalRecords, totalSalary, culture, 8);
    }

    private static void BuildTotals(
        IContainer container,
        string label,
        int totalRecords,
        decimal totalSalary,
        CultureInfo culture,
        int fontSize)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(100);
                columns.ConstantColumn(90);
            });

            TotalCell(table, label, fontSize);
            TotalCell(table, $"{totalRecords} registro(s)", fontSize);
            TotalCell(table, totalSalary.ToString("N2", culture), fontSize);
        });
    }

    private static void HeaderCell(TableCellDescriptor table, string text)
    {
        table.Cell()
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(2)
            .AlignCenter()
            .Text(text)
            .Bold()
            .FontSize(7);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignCenter = false, bool alignRight = false)
    {
        var cell = table.Cell()
            .PaddingHorizontal(2)
            .PaddingVertical(2);

        if (alignCenter)
        {
            cell = cell.AlignCenter();
        }
        else if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text ?? string.Empty).FontSize(7);
    }

    private static void TotalCell(TableDescriptor table, string text, int fontSize)
    {
        table.Cell()
            .BorderTop(0.5f)
            .PaddingHorizontal(2)
            .PaddingVertical(3)
            .AlignRight()
            .Text(text)
            .Bold()
            .FontSize(fontSize);
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
