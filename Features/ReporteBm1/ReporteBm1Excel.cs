using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace OssmmasoftVerticalSlice.Features.ReporteBm1;

public static class ReporteBm1ExcelGenerator
{
    private static readonly string[] Headers =
    [
        "Unidad Trabajo",
        "Codigo Grupo",
        "Codigo Nivel 1",
        "Codigo Nivel 2",
        "Numero Lote",
        "Cantidad",
        "Numero Placa",
        "Valor Actual",
        "Articulo",
        "Especificacion",
        "Servicio",
        "Responsable Bien",
        "Fecha Movimiento"
    ];

    public static byte[] Generate(IReadOnlyCollection<ReporteBm1ItemResponse> items)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", BuildContentTypes());
            AddEntry(archive, "_rels/.rels", BuildRootRelationships());
            AddEntry(archive, "xl/workbook.xml", BuildWorkbook());
            AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheet(items));
        }

        return memory.ToArray();
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

    private static string BuildWorkbook()
    {
        return """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="BM1" sheetId="1" r:id="rId1"/>
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

    private static string BuildWorksheet(IReadOnlyCollection<ReporteBm1ItemResponse> items)
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        builder.Append("<row r=\"1\">");

        for (var i = 0; i < Headers.Length; i++)
        {
            AppendTextCell(builder, 1, i + 1, Headers[i]);
        }

        builder.Append("</row>");

        var rowNumber = 2;
        foreach (var item in items)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowNumber}\">");
            AppendTextCell(builder, rowNumber, 1, item.UnidadTrabajo);
            AppendTextCell(builder, rowNumber, 2, item.CodigoGrupo);
            AppendTextCell(builder, rowNumber, 3, item.CodigoNivel1);
            AppendTextCell(builder, rowNumber, 4, item.CodigoNivel2);
            AppendTextCell(builder, rowNumber, 5, item.NumeroLote);
            AppendNumberCell(builder, rowNumber, 6, item.Cantidad);
            AppendTextCell(builder, rowNumber, 7, item.NumeroPlaca);
            AppendNumberCell(builder, rowNumber, 8, item.ValorActual);
            AppendTextCell(builder, rowNumber, 9, item.Articulo);
            AppendTextCell(builder, rowNumber, 10, item.Especificacion);
            AppendTextCell(builder, rowNumber, 11, item.Servicio);
            AppendTextCell(builder, rowNumber, 12, item.ResponsableBien);
            AppendTextCell(builder, rowNumber, 13, item.FechaMovimiento?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty);
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

    private static void AppendNumberCell(StringBuilder builder, int row, int column, decimal value)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{GetColumnName(column)}{row}\"><v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>");
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

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content.TrimStart());
    }
}
