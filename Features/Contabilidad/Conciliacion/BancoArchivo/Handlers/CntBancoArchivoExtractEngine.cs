using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntBancoArchivoExtractEngine
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static CntBancoArchivoExtractResponse Extract(CntBancoArchivoExtractCommand value, CntBancoArchivoFormatConfig? config = null)
    {
        var tipoFormato = NormalizeFormat(config?.TipoFormato ?? value.TipoFormato);
        if (tipoFormato == "PDF_TEXTO")
        {
            return ExtractPdfText(value, config);
        }

        var rows = tipoFormato switch
        {
            "CSV_TXT" => ParseDelimited(GetTextContent(value), config?.Delimiter),
            "TEXTO_DELIMITADO" => ParseDelimited(value.TextoPegado ?? string.Empty, config?.Delimiter),
            "TEXTO_LIBRE" => ParseFreeTextLines(!string.IsNullOrWhiteSpace(value.TextoPegado) ? value.TextoPegado : GetTextContent(value)),
            "XLSX" => ParseXlsx(GetBinaryContent(value), config?.SheetName),
            _ => throw new InvalidOperationException($"Formato {value.TipoFormato} no soportado por el extractor.")
        };

        var result = RowsToMovements(tipoFormato, rows, config);

        return result;
    }

    private static string NormalizeFormat(string value) => (value ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "CSV" => "CSV_TXT",
        "TXT" => "CSV_TXT",
        "CSV_TXT" => "CSV_TXT",
        "XLS" => "XLSX",
        "XLSX" => "XLSX",
        "PDF" => "PDF_TEXTO",
        "PDF_TEXTO" => "PDF_TEXTO",
        "TEXTO" => "TEXTO_DELIMITADO",
        "TEXTO_DELIMITADO" => "TEXTO_DELIMITADO",
        "TEXTO_LIBRE" => "TEXTO_LIBRE",
        "LIBRE" => "TEXTO_LIBRE",
        var format => format
    };

    private static string GetTextContent(CntBancoArchivoExtractCommand value)
    {
        if (!string.IsNullOrWhiteSpace(value.TextoPegado))
        {
            return value.TextoPegado;
        }

        var bytes = GetBinaryContent(value);

        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] GetBinaryContent(CntBancoArchivoExtractCommand value)
    {
        if (string.IsNullOrWhiteSpace(value.ContenidoBase64))
        {
            throw new InvalidOperationException("Debe enviar contenido para extraer movimientos.");
        }

        var content = value.ContenidoBase64;
        var commaIndex = content.IndexOf(',');
        if (commaIndex >= 0)
        {
            content = content[(commaIndex + 1)..];
        }

        return Convert.FromBase64String(content);
    }

    private static List<string[]> ParseDelimited(string content, string? configuredDelimiter)
    {
        var lines = content
            .Replace("\uFEFF", string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return [];
        }

        var delimiter = !string.IsNullOrEmpty(configuredDelimiter)
            ? configuredDelimiter[0]
            : CountDelimiter(lines[0], ';') >= CountDelimiter(lines[0], ',') ? ';' : ',';

        return lines.Select(line => SplitDelimitedLine(line, delimiter)).ToList();
    }

    private static CntBancoArchivoExtractResponse ExtractPdfText(CntBancoArchivoExtractCommand value, CntBancoArchivoFormatConfig? config)
    {
        var pages = ExtractPdfTextPages(GetBinaryContent(value));
        var text = string.Join(Environment.NewLine, pages.Select(page => page.Texto));
        if (string.IsNullOrWhiteSpace(text))
        {
            return new CntBancoArchivoExtractResponse(
                "PDF_TEXTO",
                0,
                1,
                0m,
                [],
                [new CntBancoArchivoExtractError(0, "archivo", "El PDF no contiene texto seleccionable. Use OCR.", value.NombreArchivo ?? string.Empty)],
                null,
                pages);
        }

        var rows = ShouldUseDelimitedPdfParsing(text, config)
            ? ParseDelimited(text, config?.Delimiter)
            : ParsePdfTextLines(text);

        var result = RowsToMovements("PDF_TEXTO", rows, config);

        return result with
        {
            TextoExtraido = text,
            PaginasTexto = pages
        };
    }

    private static List<CntBancoArchivoExtractPage> ExtractPdfTextPages(byte[] content)
    {
        var pages = new List<CntBancoArchivoExtractPage>();
        using var document = PdfDocument.Open(content);

        foreach (var page in document.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = page.Text;
            }

            pages.Add(new CntBancoArchivoExtractPage(page.Number, text.Trim()));
        }

        return pages;
    }

    private static bool ShouldUseDelimitedPdfParsing(string text, CntBancoArchivoFormatConfig? config)
    {
        if (!string.IsNullOrWhiteSpace(config?.Delimiter))
        {
            return true;
        }

        return text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => CountDelimiter(line, ';') >= 5 || CountDelimiter(line, '\t') >= 5);
    }

    private static List<string[]> ParsePdfTextLines(string text)
    {
        return ParseUnstructuredMovementLines(text);
    }

    private static List<string[]> ParseFreeTextLines(string text)
    {
        return ParseUnstructuredMovementLines(text);
    }

    private static List<string[]> ParseUnstructuredMovementLines(string text)
    {
        var rows = new List<string[]>();
        var lines = text
            .Replace("\uFEFF", string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Regex.Replace(line.Trim(), @"\s+", " "))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        foreach (var line in lines)
        {
            var row = TryParsePdfMovementLine(line);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static string[]? TryParsePdfMovementLine(string line)
    {
        const string datePattern = @"(?<date>\d{1,2}[\/-]\d{1,2}[\/-]\d{2,4}|\d{4}[\/-]\d{1,2}[\/-]\d{1,2})";
        const string amountPattern = @"(?<amount>-?\d{1,3}(?:[.,]\d{3})*[.,]\d{2}|-?\d+[.,]\d{2})";
        var match = Regex.Match(line, $@"{datePattern}\s+(?<body>.+?)\s+{amountPattern}\s*$");
        if (!match.Success)
        {
            return null;
        }

        var date = match.Groups["date"].Value;
        var body = match.Groups["body"].Value.Trim();
        var amount = match.Groups["amount"].Value;
        var parts = body.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var number = parts.Length > 1 ? parts[0] : string.Empty;
        var description = parts.Length > 1 ? parts[1] : body;
        var transactionType = DetectTransactionType(body, amount);

        if (description.Contains("saldo", StringComparison.OrdinalIgnoreCase)
            || description.Contains("fecha", StringComparison.OrdinalIgnoreCase)
            || description.Contains("descripcion", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return [date, string.IsNullOrWhiteSpace(number) ? date : number, transactionType.TipoId.ToString(Invariant), transactionType.Tipo, description, amount];
    }

    private static (int TipoId, string Tipo) DetectTransactionType(string body, string amount)
    {
        if (amount.TrimStart().StartsWith("-", StringComparison.Ordinal))
        {
            return (1, "DEBITO");
        }

        if (body.Contains("credito", StringComparison.OrdinalIgnoreCase)
            || body.Contains("crédito", StringComparison.OrdinalIgnoreCase)
            || body.Contains("abono", StringComparison.OrdinalIgnoreCase)
            || body.Contains("deposito", StringComparison.OrdinalIgnoreCase)
            || body.Contains("depósito", StringComparison.OrdinalIgnoreCase)
            || body.Contains("transferencia recibida", StringComparison.OrdinalIgnoreCase))
        {
            return (2, "CREDITO");
        }

        if (body.Contains("debito", StringComparison.OrdinalIgnoreCase)
            || body.Contains("débito", StringComparison.OrdinalIgnoreCase)
            || body.Contains("cargo", StringComparison.OrdinalIgnoreCase)
            || body.Contains("pago", StringComparison.OrdinalIgnoreCase)
            || body.Contains("retiro", StringComparison.OrdinalIgnoreCase)
            || body.Contains("comision", StringComparison.OrdinalIgnoreCase)
            || body.Contains("comisión", StringComparison.OrdinalIgnoreCase))
        {
            return (1, "DEBITO");
        }

        return (1, "BANCO");
    }

    private static int CountDelimiter(string line, char delimiter) => line.Count(character => character == delimiter);

    private static string[] SplitDelimitedLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (character == delimiter && !inQuotes)
            {
                cells.Add(CleanCell(current.ToString()));
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        cells.Add(CleanCell(current.ToString()));

        return cells.ToArray();
    }

    private static string CleanCell(string value) => value.Trim().Trim('"').Trim();

    private static List<string[]> ParseXlsx(byte[] content, string? configuredSheet)
    {
        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = FindSheetEntry(archive, configuredSheet);

        if (sheetEntry is null)
        {
            throw new InvalidOperationException("El archivo XLS/XLSX no contiene hojas legibles.");
        }

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<string[]>();

        foreach (var row in document.Descendants(ns + "row"))
        {
            var cells = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var columnIndex = GetColumnIndex(reference);
                cells[columnIndex] = GetCellValue(cell, ns, sharedStrings);
            }

            if (cells.Count == 0)
            {
                continue;
            }

            var maxIndex = cells.Keys.Max();
            var values = Enumerable.Range(0, maxIndex + 1)
                .Select(index => cells.TryGetValue(index, out var value) ? value : string.Empty)
                .ToArray();

            rows.Add(values);
        }

        return rows;
    }

    private static ZipArchiveEntry? FindSheetEntry(ZipArchive archive, string? configuredSheet)
    {
        if (!string.IsNullOrWhiteSpace(configuredSheet) && int.TryParse(configuredSheet, out var sheetNumber) && sheetNumber > 0)
        {
            var configuredEntry = archive.GetEntry($"xl/worksheets/sheet{sheetNumber}.xml");
            if (configuredEntry is not null)
            {
                return configuredEntry;
            }
        }

        return archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? archive.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToList();
    }

    private static int GetColumnIndex(string reference)
    {
        var letters = new string(reference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        var index = 0;

        foreach (var letter in letters)
        {
            index = index * 26 + (letter - 'A' + 1);
        }

        return Math.Max(index - 1, 0);
    }

    private static string GetCellValue(XElement cell, XNamespace ns, List<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;

        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)).Trim();
        }

        var raw = cell.Element(ns + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(raw, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedIndex].Trim();
        }

        return raw.Trim();
    }

    private static CntBancoArchivoExtractResponse RowsToMovements(string tipoFormato, List<string[]> rows, CntBancoArchivoFormatConfig? config)
    {
        var startIndex = Math.Max((config?.StartRow ?? 1) - 1, 0);
        var rowsFromStart = rows.Skip(startIndex).ToList();
        var dataRows = config?.HasHeader == true || (config?.HasHeader is null && HasHeader(rowsFromStart.FirstOrDefault()))
            ? rowsFromStart.Skip(1).ToList()
            : rowsFromStart;
        var header = (config?.HasHeader == true || (config?.HasHeader is null && HasHeader(rowsFromStart.FirstOrDefault())))
            ? rowsFromStart.FirstOrDefault()
            : null;
        var lines = new List<CntBancoArchivoDetalleLineCommand>();
        var errors = new List<CntBancoArchivoExtractError>();
        var lineNumber = startIndex + (header is null ? 0 : 1);
        var mapping = config?.Mapping ?? CntBancoArchivoMapping.Default;

        foreach (var row in dataRows)
        {
            lineNumber++;
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            try
            {
                lines.Add(MapRow(tipoFormato, row, lineNumber, mapping, header, config is not null));
            }
            catch (Exception ex)
            {
                errors.Add(new CntBancoArchivoExtractError(lineNumber, "fila", ex.Message, string.Join(" | ", row)));
            }
        }

        var confidence = CalculateAverageConfidence(lines, errors.Count);

        return new CntBancoArchivoExtractResponse(tipoFormato, lines.Count, errors.Count, confidence, lines, errors);
    }

    private static decimal CalculateAverageConfidence(List<CntBancoArchivoDetalleLineCommand> lines, int errorCount)
    {
        var total = lines.Count + errorCount;
        if (total == 0)
        {
            return 0m;
        }

        var lineConfidence = lines.Count == 0
            ? 0m
            : lines.Average(line => line.Confianza ?? 0.8m);
        var successRatio = lines.Count / (decimal)total;

        return Math.Round(lineConfidence * successRatio, 4);
    }

    private static bool HasHeader(string[]? row) =>
        row?.Any(cell => cell.Contains("fecha", StringComparison.OrdinalIgnoreCase)
            || cell.Contains("monto", StringComparison.OrdinalIgnoreCase)
            || cell.Contains("numero", StringComparison.OrdinalIgnoreCase)
            || cell.Contains("descrip", StringComparison.OrdinalIgnoreCase)) == true;

    private static CntBancoArchivoDetalleLineCommand MapRow(string tipoFormato, string[] row, int index, CntBancoArchivoMapping mapping, string[]? header, bool hasConfiguredFormat)
    {
        var fecha = ParseDate(GetMappedValue(row, header, mapping.Fecha));
        var tipoIdValue = GetMappedValue(row, header, mapping.TipoId);
        var tipoId = int.TryParse(tipoIdValue, NumberStyles.Integer, Invariant, out var parsedTipoId) ? parsedTipoId : 1;
        var tipoValue = GetMappedValue(row, header, mapping.Tipo);
        var descripcionValue = GetMappedValue(row, header, mapping.Descripcion);
        var numeroValue = GetMappedValue(row, header, mapping.Numero);
        var montoValue = GetMappedValue(row, header, mapping.Monto);
        var monto = ParseAmount(!string.IsNullOrWhiteSpace(montoValue) ? montoValue : descripcionValue);
        var advertencias = new List<string>();
        var confianza = GetBaseConfidence(tipoFormato, hasConfiguredFormat);

        if (string.IsNullOrWhiteSpace(numeroValue))
        {
            confianza -= 0.08m;
            advertencias.Add("Numero asignado automaticamente.");
        }

        if (string.IsNullOrWhiteSpace(tipoIdValue))
        {
            confianza -= 0.04m;
            advertencias.Add("Tipo ID asignado por defecto.");
        }

        if (string.IsNullOrWhiteSpace(tipoValue))
        {
            confianza -= 0.04m;
            advertencias.Add("Tipo asignado por defecto.");
        }

        if (string.IsNullOrWhiteSpace(descripcionValue))
        {
            confianza -= 0.06m;
            advertencias.Add("Descripcion asignada por defecto.");
        }

        if (tipoFormato == "PDF_TEXTO" && !hasConfiguredFormat)
        {
            advertencias.Add("Fila extraida por patron de texto PDF.");
        }

        if (tipoFormato == "TEXTO_LIBRE")
        {
            advertencias.Add("Fila extraida por heuristica de texto libre.");
        }

        confianza = Math.Clamp(confianza, 0m, 1m);

        return new CntBancoArchivoDetalleLineCommand(
            fecha,
            string.IsNullOrWhiteSpace(numeroValue) ? index.ToString(Invariant) : numeroValue,
            tipoId,
            string.IsNullOrWhiteSpace(tipoValue) ? "BANCO" : tipoValue,
            string.IsNullOrWhiteSpace(descripcionValue) ? (string.IsNullOrWhiteSpace(tipoValue) ? "Movimiento bancario" : tipoValue) : descripcionValue,
            monto,
            Math.Round(confianza, 4),
            advertencias);
    }

    private static decimal GetBaseConfidence(string tipoFormato, bool hasConfiguredFormat) => tipoFormato switch
    {
        "CSV_TXT" => hasConfiguredFormat ? 0.98m : 0.9m,
        "XLSX" => hasConfiguredFormat ? 0.98m : 0.9m,
        "TEXTO_DELIMITADO" => hasConfiguredFormat ? 0.95m : 0.82m,
        "TEXTO_LIBRE" => 0.68m,
        "PDF_TEXTO" => hasConfiguredFormat ? 0.86m : 0.74m,
        _ => 0.75m
    };

    private static string GetValue(string[] row, int index) => index >= 0 && index < row.Length ? row[index].Trim() : string.Empty;

    private static string GetMappedValue(string[] row, string[]? header, CntBancoArchivoFieldMap fieldMap)
    {
        if (fieldMap.Index.HasValue)
        {
            return GetValue(row, fieldMap.Index.Value);
        }

        if (!string.IsNullOrWhiteSpace(fieldMap.Name) && header is not null)
        {
            var index = Array.FindIndex(header, cell => cell.Trim().Equals(fieldMap.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                return GetValue(row, index);
            }
        }

        return string.Empty;
    }

    private static DateTime ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Fecha requerida.");
        }

        if (double.TryParse(value, NumberStyles.Number, Invariant, out var serial) && serial > 20000 && serial < 80000)
        {
            return DateTime.FromOADate(serial).Date;
        }

        var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "d-M-yyyy" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact.Date;
        }

        if (DateTime.TryParse(value, new CultureInfo("es-VE"), DateTimeStyles.None, out var parsed))
        {
            return parsed.Date;
        }

        throw new InvalidOperationException($"Fecha invalida: {value}.");
    }

    private static decimal ParseAmount(string value)
    {
        var cleanValue = (value ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (string.IsNullOrWhiteSpace(cleanValue))
        {
            throw new InvalidOperationException("Monto requerido.");
        }

        if (cleanValue.Contains(','))
        {
            cleanValue = cleanValue.Replace(".", string.Empty).Replace(",", ".");
        }

        if (decimal.TryParse(cleanValue, NumberStyles.Number | NumberStyles.AllowLeadingSign, Invariant, out var amount))
        {
            return amount;
        }

        throw new InvalidOperationException($"Monto invalido: {value}.");
    }
}

internal record CntBancoArchivoFormatConfig(
    string TipoFormato,
    string? Delimiter,
    bool? HasHeader,
    int StartRow,
    string? SheetName,
    CntBancoArchivoMapping Mapping)
{
    public static CntBancoArchivoFormatConfig FromDatabase(
        string tipoFormato,
        string? delimiter,
        bool hasHeader,
        int startRow,
        string? sheetName,
        string? mappingJson) =>
        new(
            tipoFormato,
            delimiter,
            hasHeader,
            startRow <= 0 ? 1 : startRow,
            sheetName,
            CntBancoArchivoMapping.FromJson(mappingJson));
}

internal record CntBancoArchivoMapping(
    CntBancoArchivoFieldMap Fecha,
    CntBancoArchivoFieldMap Numero,
    CntBancoArchivoFieldMap TipoId,
    CntBancoArchivoFieldMap Tipo,
    CntBancoArchivoFieldMap Descripcion,
    CntBancoArchivoFieldMap Monto)
{
    public static CntBancoArchivoMapping Default { get; } = new(
        new CntBancoArchivoFieldMap(0, null),
        new CntBancoArchivoFieldMap(1, null),
        new CntBancoArchivoFieldMap(2, null),
        new CntBancoArchivoFieldMap(3, null),
        new CntBancoArchivoFieldMap(4, null),
        new CntBancoArchivoFieldMap(5, null));

    public static CntBancoArchivoMapping FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new CntBancoArchivoMapping(
            ReadMap(root, "fecha", Default.Fecha),
            ReadMap(root, "numero", Default.Numero),
            ReadMap(root, "tipoId", Default.TipoId),
            ReadMap(root, "tipo", Default.Tipo),
            ReadMap(root, "descripcion", Default.Descripcion),
            ReadMap(root, "monto", Default.Monto));
    }

    private static CntBancoArchivoFieldMap ReadMap(JsonElement root, string property, CntBancoArchivoFieldMap fallback)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var index))
        {
            return new CntBancoArchivoFieldMap(index, null);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            return int.TryParse(text, out var parsedIndex)
                ? new CntBancoArchivoFieldMap(parsedIndex, null)
                : new CntBancoArchivoFieldMap(null, text);
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            int? objectIndex = null;
            string? objectName = null;
            if (value.TryGetProperty("index", out var indexValue) && indexValue.TryGetInt32(out var parsedObjectIndex))
            {
                objectIndex = parsedObjectIndex;
            }
            if (value.TryGetProperty("name", out var nameValue))
            {
                objectName = nameValue.GetString();
            }

            return new CntBancoArchivoFieldMap(objectIndex, objectName);
        }

        return fallback;
    }
}

internal record CntBancoArchivoFieldMap(int? Index, string? Name);
