using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

public record BmReportColumn<T>(string Header, Func<T, object?> Value, bool AlignRight = false);

public static class BmReportesExport
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static FileContentResult BuildPdfFile(ControllerBase controller, string prefix, byte[] bytes)
    {
        var fileName = BuildFileName(prefix, "pdf");
        controller.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return controller.File(bytes, "application/pdf", enableRangeProcessing: true);
    }

    public static FileContentResult BuildExcelFile(ControllerBase controller, string prefix, byte[] bytes)
    {
        var fileName = BuildFileName(prefix, "xlsx");
        return controller.File(bytes, ExcelContentType, fileName);
    }

    public static byte[] GeneratePdf<T>(
        string title,
        string filterDescription,
        IReadOnlyCollection<T> items,
        IReadOnlyList<BmReportColumn<T>> columns)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(style => style.FontSize(6.2f));

                page.Header().Column(column =>
                {
                    column.Item().Text(title).Bold().FontSize(13).AlignCenter();
                    column.Item().PaddingTop(3).Text(filterDescription).FontSize(7).AlignCenter();
                    column.Item().PaddingTop(3).AlignRight().Text($"Fecha emision: {DateTime.Now:dd/MM/yyyy hh:mm tt}").FontSize(7);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(definition =>
                    {
                        foreach (var _ in columns)
                        {
                            definition.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var column in columns)
                        {
                            header.Cell()
                                .Border(0.5f)
                                .Background(Colors.Grey.Lighten3)
                                .Padding(2)
                                .AlignCenter()
                                .AlignMiddle()
                                .Text(column.Header)
                                .Bold()
                                .FontSize(5.8f);
                        }
                    });

                    foreach (var item in items)
                    {
                        foreach (var column in columns)
                        {
                            var cell = table.Cell()
                                .BorderBottom(0.25f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Padding(1.5f)
                                .MinHeight(10);

                            var text = FormatValue(column.Value(item));
                            if (column.AlignRight)
                            {
                                cell.AlignRight().Text(text).FontSize(5.7f);
                            }
                            else
                            {
                                cell.Text(text).FontSize(5.7f);
                            }
                        }
                    }

                    if (items.Count == 0)
                    {
                        table.Cell().ColumnSpan((uint)columns.Count).Border(0.5f).Padding(8).AlignCenter()
                            .Text("Sin registros para los filtros seleccionados").Bold();
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Registros: {items.Count:N0}").Bold();
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

    public static byte[] GenerateExcel<T>(
        string sheetName,
        IReadOnlyCollection<T> items,
        IReadOnlyList<BmReportColumn<T>> columns)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", BuildContentTypes());
            AddEntry(archive, "_rels/.rels", BuildRootRelationships());
            AddEntry(archive, "xl/workbook.xml", BuildWorkbook(sheetName));
            AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheet(items, columns));
        }

        return memory.ToArray();
    }

    public static IReadOnlyList<BmReportColumn<BmReportePlacaResponse>> PlacaColumns()
    {
        return
        [
            new("Codigo Bien", x => x.CodigoBien, true),
            new("Placa", x => x.NumeroPlaca),
            new("Articulo", x => x.Articulo),
            new("Especificacion", x => x.Especificacion),
            new("Valor Inicial", x => x.ValorInicial, true),
            new("Valor Actual", x => x.ValorActual, true),
            new("Tipo Movimiento", x => x.TipoMovimiento),
            new("Fecha Movimiento", x => x.FechaMovimiento),
            new("Codigo ICP", x => x.CodigoIcp, true),
            new("Unidad Ejecutora", x => x.UnidadEjecutora),
            new("Responsable", x => x.ResponsableBien),
            new("Estado", x => x.EstadoOperativo)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmReporteLoteResponse>> LoteColumns()
    {
        return
        [
            new("Codigo Bien", x => x.CodigoBien, true),
            new("Placa", x => x.NumeroPlaca),
            new("Lote", x => x.NumeroLote),
            new("Articulo", x => x.Articulo),
            new("Fecha Ins", x => x.FechaIns),
            new("Fecha Compra", x => x.FechaCompra),
            new("Valor Inicial", x => x.ValorInicial, true),
            new("Valor Actual", x => x.ValorActual, true),
            new("Codigo ICP", x => x.CodigoIcp, true),
            new("Unidad", x => x.UnidadEjecutora),
            new("Responsable", x => x.ResponsableBien)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmReporteFichaResponse>> FichaColumns()
    {
        return
        [
            new("Seccion", x => x.Seccion),
            new("Referencia", x => x.Referencia),
            new("Descripcion", x => x.Descripcion),
            new("Fecha", x => x.Fecha),
            new("Unidad", x => x.Unidad),
            new("Observacion", x => x.Observacion)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmReporteSolicitudResponse>> SolicitudColumns()
    {
        return
        [
            new("Codigo", x => x.CodigoSolMovBien, true),
            new("Solicitud", x => x.NumeroSolicitud),
            new("Placa", x => x.NumeroPlaca),
            new("Articulo", x => x.Articulo),
            new("Tipo", x => x.TipoMovimiento),
            new("Movimiento", x => x.TipoMovimientoDescripcion),
            new("Fecha", x => x.FechaMovimiento),
            new("Aprobado", x => x.Aprobado ? "SI" : "NO"),
            new("Unidad", x => x.UnidadEjecutora),
            new("Concepto", x => x.ConceptoMovimiento),
            new("Nota", x => x.NotaIncidencia)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmProcesoMasivoResponse>> ProcesoMasivoColumns()
    {
        return
        [
            new("Proceso", x => x.CodigoProcesoMasivo, true),
            new("Detalle", x => x.CodigoProcesoMasivoDet, true),
            new("Placa", x => x.NumeroPlaca),
            new("Articulo", x => x.Articulo),
            new("Dir Origen", x => x.CodigoDirOrigen, true),
            new("ICP Origen", x => x.CodigoIcpOrigen, true),
            new("Dir Destino", x => x.CodigoDirDestino, true),
            new("Unidad Destino", x => x.UnidadDestino),
            new("Estado", x => x.Estado),
            new("Mensaje", x => x.Mensaje),
            new("Movimiento", x => x.CodigoMovBien, true)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmReporteUbicacionResponse>> UbicacionColumns()
    {
        return
        [
            new("Codigo ICP", x => x.CodigoIcp, true),
            new("Unidad Ejecutora", x => x.UnidadEjecutora),
            new("Codigo Dir Bien", x => x.CodigoDirBien, true),
            new("Direccion", x => x.Direccion),
            new("Total Bienes", x => x.TotalBienes, true),
            new("Valor Total", x => x.ValorTotal, true)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmMovimientoResponse>> MovimientoColumns()
    {
        return
        [
            new("Codigo Mov", x => x.CodigoMovBien, true),
            new("Codigo Bien", x => x.CodigoBien, true),
            new("Placa", x => x.NumeroPlaca),
            new("Articulo", x => x.Articulo),
            new("Tipo", x => x.TipoMovimiento),
            new("Descripcion", x => x.TipoMovimientoDescripcion),
            new("Fecha", x => x.FechaMovimiento),
            new("Codigo ICP", x => x.CodigoIcp, true),
            new("Unidad Ejecutora", x => x.UnidadEjecutora),
            new("Concepto", x => x.ConceptoMovimiento)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmReporteConteoDifResponse>> ConteoDifColumns()
    {
        return
        [
            new("Codigo Conteo", x => x.CodigoBmConteo, true),
            new("Codigo Detalle", x => x.CodigoBmConteoDetalle, true),
            new("Conteo", x => x.Conteo, true),
            new("Codigo ICP", x => x.CodigoIcp, true),
            new("Unidad Trabajo", x => x.UnidadTrabajo),
            new("Placa", x => x.NumeroPlaca),
            new("Articulo", x => x.Articulo),
            new("Cantidad", x => x.Cantidad, true),
            new("Contada", x => x.CantidadContada, true),
            new("Diferencia", x => x.Diferencia, true),
            new("Comentario", x => x.Comentario)
        ];
    }

    public static IReadOnlyList<BmReportColumn<BmReporteConteoHistResponse>> ConteoHistColumns()
    {
        return
        [
            new("Codigo Conteo", x => x.CodigoBmConteo, true),
            new("Titulo", x => x.Titulo),
            new("Fecha", x => x.Fecha),
            new("Fecha Cierre", x => x.FechaCierre),
            new("Cantidad", x => x.TotalCantidad, true),
            new("Contada", x => x.TotalCantidadContada, true),
            new("Diferencia", x => x.TotalDiferencia, true),
            new("Comentario", x => x.Comentario)
        ];
    }

    private static string BuildFileName(string prefix, string extension)
    {
        return $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}.{extension}";
    }

    private static string BuildContentTypes()
    {
        return """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """;
    }

    private static string BuildRootRelationships()
    {
        return """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """;
    }

    private static string BuildWorkbook(string sheetName)
    {
        return $$"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="{{SecurityElement.Escape(TrimSheetName(sheetName))}}" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """;
    }

    private static string BuildWorkbookRelationships()
    {
        return """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """;
    }

    private static string BuildWorksheet<T>(IReadOnlyCollection<T> items, IReadOnlyList<BmReportColumn<T>> columns)
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        builder.Append("<row r=\"1\">");

        for (var i = 0; i < columns.Count; i++)
        {
            AppendTextCell(builder, 1, i + 1, columns[i].Header);
        }

        builder.Append("</row>");

        var rowNumber = 2;
        foreach (var item in items)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowNumber}\">");
            for (var i = 0; i < columns.Count; i++)
            {
                var value = columns[i].Value(item);
                if (value is int or long or decimal or double or float)
                {
                    AppendNumberCell(builder, rowNumber, i + 1, value);
                }
                else
                {
                    AppendTextCell(builder, rowNumber, i + 1, FormatValue(value));
                }
            }

            builder.Append("</row>");
            rowNumber++;
        }

        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static void AppendTextCell(StringBuilder builder, int row, int column, string value)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{GetColumnName(column)}{row}\" t=\"inlineStr\"><is><t>");
        builder.Append(SecurityElement.Escape(value) ?? string.Empty);
        builder.Append("</t></is></c>");
    }

    private static void AppendNumberCell(StringBuilder builder, int row, int column, object value)
    {
        var number = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
        builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{GetColumnName(column)}{row}\"><v>{number}</v></c>");
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            decimal number => number.ToString("N2", CultureInfo.GetCultureInfo("es-VE")),
            double number => number.ToString("N2", CultureInfo.GetCultureInfo("es-VE")),
            float number => number.ToString("N2", CultureInfo.GetCultureInfo("es-VE")),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string GetColumnName(int column)
    {
        var dividend = column;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string TrimSheetName(string sheetName)
    {
        var value = string.IsNullOrWhiteSpace(sheetName) ? "Reporte" : sheetName.Trim();
        return value.Length <= 31 ? value : value[..31];
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content.TrimStart());
    }
}
