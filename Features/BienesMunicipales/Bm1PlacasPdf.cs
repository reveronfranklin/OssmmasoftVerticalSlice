using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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
    private const float BarcodeHeightPoints = 16f;

    private static readonly string[] ArticulosEnMinuscula =
        ["El", "La", "Los", "Las", "Un", "Una", "Unos", "Unas", "Y", "De", "Del", "E", "Al"];

    public static byte[] Generate(IReadOnlyCollection<Bm1Response> items, IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        // El sistema anterior tomaba estas dos imagenes de OSS_CONFIG, claves ESCUDO_CHACAO (el escudo
        // del municipio, a la izquierda) y LOGO_CHACAO (el logotipo institucional, a la derecha). Aqui
        // se leen de los assets de reportes del vertical slice. No se usa logoLeft.jpeg como respaldo
        // del escudo: es el logotipo, no el escudo, y ponerlo en la izquierda repetiria la misma imagen
        // en los dos extremos de la etiqueta. Si falta alguna, su recuadro queda vacio y la etiqueta se
        // emite igual.
        var escudoBytes = TryReadReportAsset(environment, "escudoChacao.png", "escudoChacao.jpeg");
        var logoBytes = TryReadReportAsset(environment, "logoChacao.jpeg", "logoChacao.png");

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
    /// Si el valor no es codificable, imprime el texto de la placa para que la etiqueta siga siendo
    /// identificable en vez de salir en blanco.
    /// </summary>
    private static void BuildBarcode(IContainer container, string value)
    {
        if (!Bm1Code128.CanEncode(value))
        {
            container.Text(value ?? string.Empty).Bold().FontSize(7);
            return;
        }

        container.Width(BarcodeWidthPoints).Height(BarcodeHeightPoints).Svg(BuildBarcodeSvg(value));
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
    private static byte[]? TryReadReportAsset(IWebHostEnvironment environment, params string[] fileNames)
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
