using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

/// <summary>
/// Genera el PDF de etiquetas de placas de Bienes Municipales, una etiqueta por pagina.
/// Reproduce la etiqueta que producia el sistema anterior en
/// BM_V_BM1Service.GenerateMultipleFont (requerimiento 18).
/// </summary>
public static class Bm1PlacasPdfGenerator
{
    private const float PageWidthPoints = 170f;
    private const float PageHeightPoints = 85f;
    private const float BarcodeWidthPoints = 105f;

    // El sistema anterior reservaba 16 puntos para el bloque completo del simbolo: las barras mas el
    // numero de placa legible debajo (Barcode128.SetBaseline(10)). Aqui se reparte ese mismo alto
    // entre las barras y la linea de texto, para no crecer la etiqueta.
    private const float BarcodeHeightPoints = 11f;
    private const float BarcodeTextFontSize = 5f;

    private static readonly string[] ArticulosEnMinuscula =
        ["El", "La", "Los", "Las", "Un", "Una", "Unos", "Unas", "Y", "De", "Del", "E", "Al"];

    public static byte[] Generate(IReadOnlyCollection<Bm1Response> items, Bm1PlacasImagenes imagenes)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var escudoBytes = imagenes.Escudo;
        var logoBytes = imagenes.Logo;

        // Se comparte una sola instancia de Image por asset en vez de pasar el byte[] en cada etiqueta.
        // A diferencia del resto de reportes, aqui cada placa es una Page independiente, asi que pasar
        // los bytes incrusta el escudo y el logotipo una vez por pagina: 55 placas pesaban 1,9 MB en vez
        // de 100 KB, y el visor no lograba mostrar un PDF de ese tamano.
        var escudo = escudoBytes is null ? null : Image.FromBinaryData(escudoBytes);
        var logo = logoBytes is null ? null : Image.FromBinaryData(logoBytes);

        // Mismo orden que usaba CreateBardCodeMultiple en el sistema anterior.
        var placas = items.OrderBy(item => item.UnidadTrabajo, StringComparer.OrdinalIgnoreCase).ToList();

        return Document.Create(container =>
        {
            foreach (var placa in placas)
            {
                container.Page(page =>
                {
                    page.Size(PageWidthPoints, PageHeightPoints, Unit.Point);
                    page.Margin(3, Unit.Point);
                    page.DefaultTextStyle(style => style.FontSize(6));

                    page.Content().Column(column =>
                    {
                        column.Item().Element(element => BuildHeader(element, placa, escudo, logo));

                        column.Item().PaddingTop(2).AlignCenter().Text("Bienes Municipales").Bold().FontSize(8);

                        column.Item().PaddingTop(1).AlignCenter()
                            .Element(element => BuildBarcode(element, placa.PlacaBarra));

                        column.Item().AlignCenter().Text("Concejo Municipal de Chacao").Bold().FontSize(7);

                        column.Item().AlignCenter().Text(CapitalizarAPA(placa.UnidadTrabajo)).Bold().FontSize(6);
                    });
                });
            }
        }).GeneratePdf();
    }

    private static void BuildHeader(IContainer container, Bm1Response placa, Image? escudo, Image? logo)
    {
        container.Height(30).Row(row =>
        {
            row.ConstantItem(35).Element(element =>
            {
                if (escudo is not null)
                {
                    element.AlignLeft().AlignMiddle().Image(escudo).FitArea();
                }
            });

            row.RelativeItem().AlignCenter().AlignBottom()
                .Text(FormatDate(placa.FechaMovimiento)).Bold().FontSize(6);

            row.ConstantItem(58).Element(element =>
            {
                if (logo is not null)
                {
                    element.AlignRight().AlignMiddle().Image(logo).FitArea();
                }
            });
        });
    }

    /// <summary>
    /// Dibuja el simbolo Code 128 como SVG: las coordenadas van en modulos enteros y el escalado a
    /// puntos lo hace el renderizador, de modo que la etiqueta queda descrita por un solo elemento
    /// en vez de un centenar de cajas de layout.
    /// Debajo de las barras va el numero de placa en claro, igual que en el sistema anterior: si el
    /// lector falla o la etiqueta se raya, el bien se sigue identificando a simple vista.
    /// Si el valor no es codificable, imprime el texto de la placa para que la etiqueta siga siendo
    /// identificable en vez de salir en blanco.
    /// </summary>
    private static void BuildBarcode(IContainer container, string value)
    {
        var texto = value ?? string.Empty;

        if (!Bm1Code128.CanEncode(texto))
        {
            container.Text(texto).Bold().FontSize(7);
            return;
        }

        container.Width(BarcodeWidthPoints).Column(column =>
        {
            column.Item().Height(BarcodeHeightPoints).Svg(BuildBarcodeSvg(texto));

            column.Item().AlignCenter().Text(texto).FontSize(BarcodeTextFontSize);
        });
    }

    private static string BuildBarcodeSvg(string value)
    {
        var widths = Bm1Code128.GetElementWidths(value);
        var totalModules = widths.Sum();

        // El lienzo se declara con la misma proporcion que el contenedor destino, para que el
        // escalado uniforme del renderizador de SVG no deforme ni funda las barras.
        var svgHeight = (totalModules * BarcodeHeightPoints / BarcodeWidthPoints)
            .ToString("0.###", CultureInfo.InvariantCulture);

        var builder = new System.Text.StringBuilder();
        builder.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{totalModules}\" height=\"{svgHeight}\" viewBox=\"0 0 {totalModules} {svgHeight}\">");

        var offset = 0;
        for (var index = 0; index < widths.Length; index++)
        {
            // Los elementos en indice par son barras; los impares, espacios.
            if (index % 2 == 0)
            {
                builder.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{offset}\" y=\"0\" width=\"{widths[index]}\" height=\"{svgHeight}\" fill=\"#000000\"/>");
            }

            offset += widths[index];
        }

        builder.Append("</svg>");

        return builder.ToString();
    }

    /// <summary>
    /// Capitaliza la unidad de trabajo dejando los articulos en minuscula, igual que
    /// BM_V_BM1Service.CapitalizarAPA del sistema anterior.
    /// </summary>
    private static string CapitalizarAPA(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var textInfo = CultureInfo.GetCultureInfo("en-US").TextInfo;
        var palabras = textInfo.ToTitleCase(value.ToLowerInvariant()).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', palabras.Select(palabra =>
            ArticulosEnMinuscula.Contains(palabra, StringComparer.Ordinal) ? palabra.ToLowerInvariant() : palabra));
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
    }

    /// <summary>
    /// Devuelve el primero de los nombres indicados que exista entre los directorios de assets de
    /// reportes, o null si no hay ninguno. Mismo orden de busqueda que ReporteBm1Pdf.
    /// </summary>
    internal static byte[]? TryReadReportAsset(IWebHostEnvironment environment, params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var candidates = new[]
            {
                Path.Combine(environment.ContentRootPath, "Assets", "Reports", fileName),
                Path.Combine(environment.ContentRootPath, "ReportAssets", fileName),
                Path.Combine(environment.ContentRootPath, "Assets", fileName),
                Path.Combine(environment.WebRootPath ?? string.Empty, "images", fileName)
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    return File.ReadAllBytes(path);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Las dos imagenes de la etiqueta, ya en memoria. Cualquiera puede venir en null: en ese caso su
/// recuadro queda vacio y la etiqueta se emite igual.
/// </summary>
public record Bm1PlacasImagenes(byte[]? Escudo, byte[]? Logo);

/// <summary>
/// Resuelve las dos imagenes de la etiqueta igual que el sistema anterior: la clave de
/// <c>SIS.OSS_CONFIG</c> guarda el nombre del archivo y este se lee de la carpeta
/// <c>settings:BmFiles</c> del servidor.
/// </summary>
/// <remarks>
/// Si la carpeta no esta montada o la clave no existe -por ejemplo en una maquina de desarrollo sin
/// acceso al recurso compartido de Windows-, se cae a los assets versionados de
/// <c>Assets/Reports</c>. Son equivalentes pero no identicos: el logotipo versionado es la version a
/// color y el del servidor es la monocroma, asi que una etiqueta a color indica que la carpeta no se
/// esta alcanzando. Nunca se propaga el fallo: una placa sin logotipo se sigue pudiendo imprimir y
/// leer.
/// </remarks>
public static class Bm1PlacasImagenesLoader
{
    private const string ClaveEscudo = "ESCUDO_CHACAO";
    private const string ClaveLogo = "LOGO_CHACAO";

    public static async Task<Bm1PlacasImagenes> LoadAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        IWebHostEnvironment environment)
    {
        var carpeta = BmDb.GetBmFilesPath(config);
        var separador = config["settings:SeparatorPatch"];

        var escudo =
            await TryReadFromConfigAsync(connectionDB, carpeta, separador, ClaveEscudo)
            ?? Bm1PlacasPdfGenerator.TryReadReportAsset(environment, "escudoChacao.png", "escudoChacao.jpeg");

        // No se usa logoLeft.jpeg como respaldo del escudo: es el logotipo, no el escudo, y ponerlo a
        // la izquierda repetiria la misma imagen en los dos extremos de la etiqueta.
        var logo =
            await TryReadFromConfigAsync(connectionDB, carpeta, separador, ClaveLogo)
            ?? Bm1PlacasPdfGenerator.TryReadReportAsset(environment, "logoChacao.jpeg", "logoChacao.png");

        return new Bm1PlacasImagenes(escudo, logo);
    }

    private static async Task<byte[]?> TryReadFromConfigAsync(
        ConnectionDB connectionDB,
        string carpeta,
        string? separador,
        string clave)
    {
        try
        {
            var fileName = await ReadValorAsync(connectionDB, clave);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            // El sistema anterior concatenaba la carpeta y el valor tal cual, y el valor puede traer ya
            // el separador de Windows. Se toma solo el nombre para no seguir rutas que vengan en la
            // configuracion, y se combina con la ruta del servidor.
            var soloNombre = Path.GetFileName(fileName.Replace('\\', '/').Trim());
            if (string.IsNullOrWhiteSpace(soloNombre))
            {
                return null;
            }

            var path = string.IsNullOrEmpty(separador)
                ? Path.Combine(carpeta, soloNombre)
                : $"{carpeta.TrimEnd('\\', '/')}{separador}{soloNombre}";

            return File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
        }
        catch
        {
            // Un fallo de BD o de disco no puede impedir que se impriman las placas.
            return null;
        }
    }

    private static async Task<string?> ReadValorAsync(ConnectionDB connectionDB, string clave)
    {
        using var cn = connectionDB.GetSisConnection();
        var openError = await BmDb.TryOpenAsync(cn, "SIS");
        if (openError is not null)
        {
            return null;
        }

        using var cmd = BmDb.StoredProcedure("SIS.SP_OSS_CONFIG_GET_VALOR", cn);
        cmd.Parameters.Add("p_Clave", OracleDbType.Varchar2).Value = clave;
        var pValor = cmd.Parameters.Add("p_Valor", OracleDbType.Varchar2, 100, null, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        await cmd.ExecuteNonQueryAsync();

        if (!BmDb.IsSuccessMessage(BmDb.GetMessage(pMessage)))
        {
            return null;
        }

        return pValor.Value == DBNull.Value ? null : pValor.Value?.ToString();
    }
}
