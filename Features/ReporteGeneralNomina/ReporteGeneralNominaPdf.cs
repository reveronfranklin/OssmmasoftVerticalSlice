using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteGeneralNomina;

public record ReporteGeneralNominaPdfQuery(
    int p_tipo_nomina,
    DateTime p_fecha_pago,
    int p_tipo_generacion,
    int? p_codigo_periodo,
    string? p_cedula,
    int? codigo_empresa = null
);

public static class ReporteGeneralNominaPdfGenerator
{
    private const string ReportTitle = "REPORTE GENERAL DE NOMINA";

    public static byte[] Generate(
        GetReporteGeneralNominaCompletoGetAllResponse data,
        ReporteGeneralNominaPdfQuery query,
        IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var offices = BuildOfficeGroups(data.Detalle);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(text => text.FontSize(6.5f));

                page.Header().Element(element => BuildHeader(element, data.Periodo, query, logoBytes));

                page.Content().PaddingTop(10).Column(content =>
                {
                    content.Item().Element(element => BuildConceptSummary(element, data.General, culture));

                    foreach (var office in offices)
                    {
                        content.Item().PageBreak();
                        content.Item().Element(element => BuildOfficeSection(element, office, culture));
                    }

                    if (offices.Count > 0)
                    {
                        content.Item().PageBreak();
                        content.Item().Element(element => BuildGeneralTotals(element, offices, culture));
                    }

                    if (data.Firma.Count > 0)
                    {
                        content.Item().PageBreak();
                        content.Item().Element(element => BuildSignatures(element, data.Firma));
                    }
                });
            });
        }).GeneratePdf();
    }

    private static void BuildHeader(
        IContainer container,
        GetReporteGeneralNominaPeriodoGetByCodigoResponse? periodo,
        ReporteGeneralNominaPdfQuery query,
        byte[]? logoBytes)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(140).Height(45).Element(element =>
                {
                    if (logoBytes is not null)
                    {
                        element.AlignLeft().AlignMiddle().Image(logoBytes).FitArea();
                    }
                    else
                    {
                        element.AlignLeft().AlignMiddle().Text("LOGO").Bold().FontSize(8);
                    }
                });

                row.RelativeItem().AlignCenter().Column(title =>
                {
                    title.Item().Text(ReportTitle).Bold().FontSize(11);
                    title.Item().PaddingTop(3).Text(BuildPayrollDescription(periodo, query)).Bold().FontSize(8);
                    title.Item().PaddingTop(2).Text(BuildFilterDescription(query)).FontSize(7);
                });

                row.ConstantItem(120).AlignRight().DefaultTextStyle(style => style.Bold().FontSize(8)).Text(text =>
                {
                    text.Span("Pagina ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });
    }

    private static void BuildConceptSummary(
        IContainer container,
        IReadOnlyCollection<GetReporteGeneralNominaGetAllResponse> concepts,
        CultureInfo culture)
    {
        var visibleConcepts = concepts
            .Where(item => !string.IsNullOrWhiteSpace(item.RNumeroConcepto))
            .ToList();

        var totalAssignments = visibleConcepts.Sum(item => item.RAsignacion);
        var totalDeductions = visibleConcepts.Sum(item => item.RDeduccion);
        var totalGeneral = totalAssignments - totalDeductions;
        var deductibleAmount = concepts.Where(item => item.RDeducible == 1).Sum(item => item.RAsignacion);
        var netPayable = totalGeneral - deductibleAmount;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(45);
                columns.RelativeColumn();
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
            });

            table.Header(header =>
            {
                HeaderCell(header, "No");
                HeaderCell(header, "CONCEPTO");
                HeaderCell(header, "ASIGNACIONES");
                HeaderCell(header, "DEDUCCIONES");
                HeaderCell(header, "GENERAL");
            });

            foreach (var item in visibleConcepts)
            {
                BodyCell(table, item.RNumeroConcepto, alignCenter: true);
                BodyCell(table, item.RDenominacionConcepto);
                BodyCell(table, FormatAmount(item.RAsignacion, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.RDeduccion, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.RMontoVisible, culture), alignRight: true);
            }

            TotalLabelCell(table, "TOTALES");
            TotalAmountCell(table, FormatAmount(totalAssignments, culture));
            TotalAmountCell(table, FormatAmount(totalDeductions, culture));
            TotalAmountCell(table, FormatAmount(totalGeneral, culture));

            TotalLabelCell(table, "DEDUCIBLE");
            TotalAmountCell(table, FormatAmount(deductibleAmount, culture));
            TotalAmountCell(table, FormatAmount(0, culture));
            TotalAmountCell(table, string.Empty);

            TotalLabelCell(table, "TOTAL ASIGNACIONES");
            TotalAmountCell(table, FormatAmount(netPayable, culture));
            TotalAmountCell(table, FormatAmount(totalDeductions, culture));
            TotalAmountCell(table, string.Empty);
        });
    }

    private static void BuildOfficeSection(IContainer container, PayrollOfficeGroup office, CultureInfo culture)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(180);
                    columns.RelativeColumn();
                });

                HeaderCell(table, "CODIGO DEL DEPARTAMENTO");
                HeaderCell(table, "DENOMINACION DEL DEPARTAMENTO");
                BodyCell(table, office.OfficeCode);
                BodyCell(table, office.OfficeDenomination);
            });

            foreach (var employee in office.Employees)
            {
                column.Item().PaddingTop(8).Element(element => BuildEmployeeSection(element, employee, culture));
            }

            column.Item().PaddingTop(8).Element(element => BuildOfficeTotals(element, office, culture));
        });
    }

    private static void BuildEmployeeSection(IContainer container, PayrollEmployeeGroup employee, CultureInfo culture)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(55);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn();
                    columns.ConstantColumn(45);
                    columns.ConstantColumn(55);
                    columns.ConstantColumn(65);
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(95);
                });

                HeaderCell(table, "CEDULA");
                HeaderCell(table, "NOMBRE Y APELLIDO");
                HeaderCell(table, "CARGO");
                HeaderCell(table, "CODIGO");
                HeaderCell(table, "INGRESO");
                HeaderCell(table, "SUELDO");
                HeaderCell(table, "BANCO");
                HeaderCell(table, "No DE CUENTA");

                BodyCell(table, employee.IdCard, alignCenter: true);
                BodyCell(table, employee.Name);
                BodyCell(table, employee.JobTitle);
                BodyCell(table, employee.PersonCode, alignCenter: true);
                BodyCell(table, FormatDate(employee.HireDate), alignCenter: true);
                BodyCell(table, FormatAmount(employee.Salary, culture), alignRight: true);
                BodyCell(table, employee.Bank);
                BodyCell(table, employee.AccountNo);
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(35);
                    columns.ConstantColumn(35);
                    columns.ConstantColumn(35);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.ConstantColumn(75);
                    columns.ConstantColumn(75);
                    columns.ConstantColumn(75);
                });

                HeaderCell(table, "TIPO");
                HeaderCell(table, "No");
                HeaderCell(table, "%");
                HeaderCell(table, "CONCEPTO");
                HeaderCell(table, "COMPLEMENTO");
                HeaderCell(table, "ASIGNACIONES");
                HeaderCell(table, "DEDUCCIONES");
                HeaderCell(table, "ACUMULADOS");

                foreach (var concept in employee.Concepts)
                {
                    BodyCell(table, concept.TransactionType, alignCenter: true);
                    BodyCell(table, concept.Number, alignCenter: true);
                    BodyCell(table, concept.Percentage == 0 ? string.Empty : concept.Percentage.ToString("N2", culture), alignRight: true);
                    BodyCell(table, concept.Denomination);
                    BodyCell(table, concept.Complement);
                    BodyCell(table, concept.Assignment > 0 ? FormatAmount(concept.Assignment, culture) : string.Empty, alignRight: true);
                    BodyCell(table, concept.Deduction > 0 ? FormatAmount(concept.Deduction, culture) : string.Empty, alignRight: true);
                    BodyCell(table, string.Empty);
                }

                TotalLabelCell(table, "TOTALES");
                TotalAmountCell(table, FormatAmount(employee.TotalAssignment, culture));
                TotalAmountCell(table, FormatAmount(employee.TotalDeduction, culture));
                TotalAmountCell(table, string.Empty);

                TotalLabelCell(table, "NETO A COBRAR");
                TotalAmountCell(table, FormatAmount(employee.TotalAssignment - employee.TotalDeduction, culture));
                TotalAmountCell(table, string.Empty);
                TotalAmountCell(table, string.Empty);
            });
        });
    }

    private static void BuildOfficeTotals(IContainer container, PayrollOfficeGroup office, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(60);
                columns.RelativeColumn();
                columns.ConstantColumn(80);
            });

            BodyCell(table, "Personal Activo");
            BodyCell(table, office.ActiveCount.ToString(CultureInfo.InvariantCulture), alignRight: true);
            BodyCell(table, $"Total Asignaciones: {office.OfficeCode}");
            BodyCell(table, FormatAmount(office.TotalAssignment, culture), alignRight: true);

            BodyCell(table, "Personal de Permiso");
            BodyCell(table, office.PermissionCount.ToString(CultureInfo.InvariantCulture), alignRight: true);
            BodyCell(table, $"Total Deducciones: {office.OfficeCode}");
            BodyCell(table, FormatAmount(office.TotalDeduction, culture), alignRight: true);

            BodyCell(table, "Personal de Reposo");
            BodyCell(table, office.SickLeaveCount.ToString(CultureInfo.InvariantCulture), alignRight: true);
            BodyCell(table, string.Empty);
            BodyCell(table, string.Empty);

            TotalLabelCell(table, "Personal de Vacaciones");
            TotalAmountCell(table, office.VacationCount.ToString(CultureInfo.InvariantCulture));
            TotalLabelCell(table, $"Total Neto: {office.OfficeCode}");
            TotalAmountCell(table, FormatAmount(office.TotalAssignment - office.TotalDeduction, culture));
        });
    }

    private static void BuildGeneralTotals(IContainer container, IReadOnlyCollection<PayrollOfficeGroup> offices, CultureInfo culture)
    {
        var totalAssignment = offices.Sum(item => item.TotalAssignment);
        var totalDeduction = offices.Sum(item => item.TotalDeduction);

        container.Column(column =>
        {
            column.Item()
                .Background(Colors.Grey.Lighten3)
                .Padding(3)
                .AlignCenter()
                .Text("RESUMEN GENERAL DE NOMINA")
                .Bold()
                .FontSize(8);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                    columns.RelativeColumn();
                    columns.ConstantColumn(90);
                });

                BodyCell(table, "Total General Personal Activo");
                BodyCell(table, offices.Sum(item => item.ActiveCount).ToString(CultureInfo.InvariantCulture), alignRight: true);
                BodyCell(table, "Total General Asignaciones");
                BodyCell(table, FormatAmount(totalAssignment, culture), alignRight: true);

                BodyCell(table, "Total General Personal de Permiso");
                BodyCell(table, offices.Sum(item => item.PermissionCount).ToString(CultureInfo.InvariantCulture), alignRight: true);
                BodyCell(table, "Total General Deducciones");
                BodyCell(table, FormatAmount(totalDeduction, culture), alignRight: true);

                BodyCell(table, "Total General Personal de Reposo");
                BodyCell(table, offices.Sum(item => item.SickLeaveCount).ToString(CultureInfo.InvariantCulture), alignRight: true);
                BodyCell(table, string.Empty);
                BodyCell(table, string.Empty);

                TotalLabelCell(table, "Total General Personal de Vacaciones");
                TotalAmountCell(table, offices.Sum(item => item.VacationCount).ToString(CultureInfo.InvariantCulture));
                TotalLabelCell(table, "Total General Neto");
                TotalAmountCell(table, FormatAmount(totalAssignment - totalDeduction, culture));
            });
        });
    }

    private static void BuildSignatures(IContainer container, IReadOnlyCollection<GetReporteGeneralNominaFirmaGetAllResponse> signatures)
    {
        container.Column(column =>
        {
            foreach (var group in signatures.GroupBy(item => string.IsNullOrWhiteSpace(item.DescripcionOficina) ? "GERENCIA GENERAL" : item.DescripcionOficina.Trim()))
            {
                column.Item()
                    .PaddingTop(8)
                    .Background(Colors.Grey.Lighten3)
                    .Padding(3)
                    .AlignCenter()
                    .Text(group.Key)
                    .Bold()
                    .FontSize(8);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.ConstantColumn(70);
                        columns.RelativeColumn();
                        columns.ConstantColumn(150);
                    });

                    foreach (var signature in group.OrderBy(item => item.Orden, StringComparer.OrdinalIgnoreCase))
                    {
                        BodyCell(table, $"Elaborado: {signature.Nombre}");
                        BodyCell(table, signature.Apellido);
                        BodyCell(table, signature.Cedula, alignCenter: true);
                        BodyCell(table, signature.DescripcionCargo);
                        BodyCell(table, "____________________________\nFirma", alignCenter: true);
                    }
                });
            }
        });
    }

    private static List<PayrollOfficeGroup> BuildOfficeGroups(IReadOnlyCollection<GetReporteGeneralNominaDetalleGetAllResponse> details)
    {
        return details
            .GroupBy(item => new
            {
                OfficeCode = string.IsNullOrWhiteSpace(item.CodigoOficina) ? "SIN CODIGO" : item.CodigoOficina.Trim(),
                OfficeDenomination = string.IsNullOrWhiteSpace(item.Denominacion) ? "SIN DENOMINACION" : item.Denominacion.Trim()
            })
            .OrderBy(group => group.Key.OfficeCode)
            .Select(group =>
            {
                var employees = group
                    .GroupBy(item => item.Cedula)
                    .OrderBy(employeeGroup => employeeGroup.First().Nombre)
                    .Select(employeeGroup =>
                    {
                        var first = employeeGroup.First();
                        var concepts = employeeGroup
                            .OrderBy(item => item.NumeroConcepto)
                            .Select(item => new PayrollConcept(
                                item.TipoMovConcepto,
                                item.NumeroConcepto,
                                item.Porcentaje,
                                item.DenominacionConcepto,
                                item.ComplementoConcepto,
                                item.Asignacion,
                                item.Deduccion))
                            .ToList();

                        return new PayrollEmployeeGroup(
                            first.Cedula,
                            first.Nombre,
                            first.DenominacionCargo,
                            first.CodigoPersona.ToString(CultureInfo.InvariantCulture),
                            first.FechaIngreso,
                            first.Monto,
                            first.Banco,
                            first.NoCuenta,
                            first.Activos > 0,
                            first.Permisos > 0,
                            first.Reposos > 0,
                            first.Vacaciones > 0,
                            concepts);
                    })
                    .ToList();

                return new PayrollOfficeGroup(group.Key.OfficeCode, group.Key.OfficeDenomination, employees);
            })
            .ToList();
    }

    private static string BuildPayrollDescription(GetReporteGeneralNominaPeriodoGetByCodigoResponse? periodo, ReporteGeneralNominaPdfQuery query)
    {
        if (periodo is null)
        {
            return $"Tipo nomina {query.p_tipo_nomina} - Fecha {query.p_fecha_pago:dd/MM/yyyy}";
        }

        var date = periodo.FechaNomina ?? query.p_fecha_pago;
        return $"{periodo.DescripcionTipoNomina} - {periodo.DescripcionPeriodo} {date:dd/MM/yyyy}";
    }

    private static string BuildFilterDescription(ReporteGeneralNominaPdfQuery query)
    {
        var cedula = string.IsNullOrWhiteSpace(query.p_cedula) ? "Todos" : query.p_cedula.Trim();
        return $"Tipo generacion: {query.p_tipo_generacion} | Periodo: {query.p_codigo_periodo?.ToString(CultureInfo.InvariantCulture) ?? "N/A"} | Cedula: {cedula}";
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

    private static string FormatAmount(decimal value, CultureInfo culture)
    {
        return value.ToString("N2", culture);
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static void HeaderCell(TableDescriptor table, string text)
    {
        table.Cell()
            .Border(0.5f)
            .Background(Colors.Grey.Lighten3)
            .Padding(2)
            .AlignCenter()
            .Text(text)
            .Bold()
            .FontSize(6.5f);
    }

    private static void HeaderCell(TableCellDescriptor table, string text)
    {
        table.Cell()
            .Border(0.5f)
            .Background(Colors.Grey.Lighten3)
            .Padding(2)
            .AlignCenter()
            .Text(text)
            .Bold()
            .FontSize(6.5f);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignCenter = false, bool alignRight = false)
    {
        var cell = table.Cell()
            .BorderBottom(0.25f)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingHorizontal(2)
            .PaddingVertical(1);

        if (alignCenter)
        {
            cell = cell.AlignCenter();
        }
        else if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text ?? string.Empty).FontSize(6.5f);
    }

    private static void TotalLabelCell(TableDescriptor table, string text)
    {
        table.Cell()
            .ColumnSpan(2)
            .BorderTop(0.5f)
            .PaddingHorizontal(2)
            .PaddingVertical(2)
            .AlignRight()
            .Text(text)
            .Bold()
            .FontSize(6.5f);
    }

    private static void TotalAmountCell(TableDescriptor table, string text)
    {
        table.Cell()
            .BorderTop(0.5f)
            .PaddingHorizontal(2)
            .PaddingVertical(2)
            .AlignRight()
            .Text(text ?? string.Empty)
            .Bold()
            .FontSize(6.5f);
    }

    private record PayrollConcept(
        string TransactionType,
        string Number,
        decimal Percentage,
        string Denomination,
        string Complement,
        decimal Assignment,
        decimal Deduction);

    private record PayrollEmployeeGroup(
        string IdCard,
        string Name,
        string JobTitle,
        string PersonCode,
        DateTime? HireDate,
        decimal Salary,
        string Bank,
        string AccountNo,
        bool IsActive,
        bool IsPermission,
        bool IsSickLeave,
        bool IsVacation,
        IReadOnlyCollection<PayrollConcept> Concepts)
    {
        public decimal TotalAssignment => Concepts.Sum(item => item.Assignment);
        public decimal TotalDeduction => Concepts.Sum(item => item.Deduction);
    }

    private record PayrollOfficeGroup(
        string OfficeCode,
        string OfficeDenomination,
        IReadOnlyCollection<PayrollEmployeeGroup> Employees)
    {
        public decimal TotalAssignment => Employees.Sum(item => item.TotalAssignment);
        public decimal TotalDeduction => Employees.Sum(item => item.TotalDeduction);
        public int ActiveCount => Employees.Count(item => item.IsActive);
        public int PermissionCount => Employees.Count(item => item.IsPermission);
        public int SickLeaveCount => Employees.Count(item => item.IsSickLeave);
        public int VacationCount => Employees.Count(item => item.IsVacation);
    }
}

[ApiController]
[Route("api/ReporteGeneralNomina")]
public class ReporteGeneralNominaPdfController(
    ConnectionDB _connectionDB,
    IConfiguration _config,
    IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> GeneratePdf(ReporteGeneralNominaPdfQuery value)
    {
        if (!TryResolveEmpresa(value.codigo_empresa, out var codigoEmpresa, out var empresaError))
        {
            return Ok(new ResultDto<string>(string.Empty)
            {
                IsValid = false,
                Message = empresaError ?? "EmpresaConfig invalido."
            });
        }

        var handler = new GetReporteGeneralNominaCompletoGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReporteGeneralNominaCompletoGetAllQuery(
            value.p_tipo_nomina,
            codigoEmpresa,
            value.p_fecha_pago,
            value.p_tipo_generacion,
            value.p_codigo_periodo,
            value.p_cedula));

        if (!result.IsValid || result.Data is null)
        {
            return Ok(result);
        }

        var pdfBytes = ReporteGeneralNominaPdfGenerator.Generate(result.Data, value, _environment);
        return File(pdfBytes, "application/pdf", BuildFileName(value));
    }

    private bool TryResolveEmpresa(int? requestedEmpresa, out int empresa, out string? errorMessage)
    {
        empresa = requestedEmpresa.GetValueOrDefault();
        errorMessage = null;

        if (empresa > 0)
        {
            return true;
        }

        var empresaString = _config["settings:EmpresaConfig"];
        if (string.IsNullOrWhiteSpace(empresaString))
        {
            errorMessage = "Configuracion 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(empresaString, out empresa) || empresa <= 0)
        {
            errorMessage = "EmpresaConfig debe ser un numero valido.";
            return false;
        }

        return true;
    }

    private static string BuildFileName(ReporteGeneralNominaPdfQuery value)
    {
        var periodo = value.p_codigo_periodo.HasValue
            ? value.p_codigo_periodo.Value.ToString(CultureInfo.InvariantCulture)
            : "sin-periodo";

        return $"reporte-general-nomina-{value.p_tipo_nomina}-{periodo}-{DateTime.Now:yyyyMMddHHmmss}.pdf";
    }
}
