using OssmmasoftVerticalSlice.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteRelacionCompromiso;

/// <summary>
/// Relacion de Compromisos. Requerimiento 25.
///
/// Reproduce el layout de <c>ADM_RELACION_COMPROMISO.rdf</c> segun el PDF de
/// muestra: titulo, el periodo consultado como subtitulo, el nombre de la
/// entidad, una tabla plana de compromisos con cabecera de dos niveles
/// -"COMPROMISOS" agrupando NUMERO y FECHA-, y al cierre una unica linea de
/// total con la cantidad y el monto.
///
/// **Sin quiebre de grupo y sin subtotales**, al contrario de los reportes de
/// cheques (requerimientos 23 y 24): el salto de pagina es solo por cuantas
/// filas caben, y el total es uno y va al final del documento.
///
/// El pie reproduce el del reporte legado: usuario y fecha/hora de impresion mas
/// numero de pagina -que es lo que da el helper compartido del requerimiento
/// 17- y la leyenda fija de la forma a la derecha.
/// </summary>
public static class ReporteRelacionCompromisoPdfGenerator
{
    private const string Titulo = "RELACION DE COMPROMISOS";
    private const string Forma = "Forma: SAMI-ADM_RELACION_COMPROMISO_OP_CH";

    private const float AnchoNumero = 112f;
    private const float AnchoFecha = 72f;
    private const float AnchoMonto = 120f;

    public static byte[] Generate(
        IReadOnlyList<ReporteRelacionCompromisoItem> items,
        ReporteRelacionCompromisoQuery query,
        string entidad,
        ReportPrintContext printContext)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var culture = CultureInfo.GetCultureInfo("es-VE");
        var subtitulo = ConstruirSubtitulo(query, culture);
        var total = items.Sum(i => i.MontoCompromiso);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(14);
                page.DefaultTextStyle(style => style.FontSize(7f));

                page.Header().Element(e => Encabezado(e, subtitulo, entidad));
                page.Content().PaddingTop(6).Column(col =>
                {
                    col.Item().Element(e => Tabla(e, items, culture));
                    col.Item().PaddingTop(8).Element(e => Total(e, items.Count, total, culture));
                });
                page.Footer().PaddingTop(4).Element(e => Pie(e, printContext));
            });
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------------
    // Encabezado
    // ------------------------------------------------------------------------

    private static void Encabezado(IContainer container, string subtitulo, string entidad)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(Titulo).Bold().FontSize(11);

            if (!string.IsNullOrEmpty(subtitulo))
            {
                col.Item().AlignCenter().Text(subtitulo).FontSize(8);
            }

            // Donde el reporte legado ponia el membrete de SIS_MEMBRETE. Si no se
            // pudo resolver no se imprime una linea vacia.
            if (!string.IsNullOrWhiteSpace(entidad))
            {
                col.Item().PaddingTop(8).Text(entidad).SemiBold().FontSize(8);
            }
        });
    }

    // ------------------------------------------------------------------------
    // Tabla de compromisos
    // ------------------------------------------------------------------------

    private static void Tabla(
        IContainer container, IReadOnlyList<ReporteRelacionCompromisoItem> items, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(AnchoNumero);
                columns.ConstantColumn(AnchoFecha);
                columns.RelativeColumn();
                columns.ConstantColumn(AnchoMonto);
            });

            // Cabecera de dos niveles, como el original: "COMPROMISOS" abarca las
            // columnas NUMERO y FECHA.
            table.Header(header =>
            {
                header.Cell().ColumnSpan(2).AlignCenter().PaddingBottom(1)
                    .Text("COMPROMISOS").SemiBold().FontSize(7.5f);
                header.Cell().RowSpan(2).AlignMiddle().AlignCenter()
                    .Text("PROVEEDOR").SemiBold().FontSize(7.5f);
                header.Cell().RowSpan(2).AlignMiddle().AlignRight()
                    .Text("MONTO").SemiBold().FontSize(7.5f);

                header.Cell().BorderBottom(0.7f).BorderColor(Colors.Black)
                    .PaddingBottom(2).AlignCenter().Text("NUMERO").SemiBold().FontSize(7f);
                header.Cell().BorderBottom(0.7f).BorderColor(Colors.Black)
                    .PaddingBottom(2).AlignCenter().Text("FECHA").SemiBold().FontSize(7f);

                // Las dos celdas con RowSpan(2) ya ocupan esta fila; cerrar el
                // borde inferior bajo ellas exige dos celdas mas, que QuestPDF
                // colocaria en una tercera fila. Se deja la regla solo bajo
                // NUMERO y FECHA, que es donde el original la tiene.
            });

            foreach (var item in items)
            {
                table.Cell().PaddingVertical(1).PaddingRight(4)
                    .Text(item.NumeroCompromiso).FontSize(7f);
                table.Cell().PaddingVertical(1).AlignCenter()
                    .Text(Fecha(item.FechaCompromiso, culture)).FontSize(7f);
                table.Cell().PaddingVertical(1).PaddingRight(6)
                    .Text(item.NombreProveedor).FontSize(7f);
                table.Cell().PaddingVertical(1).AlignRight()
                    .Text(Monto(item.MontoCompromiso, culture)).FontSize(7f);
            }
        });
    }

    // ------------------------------------------------------------------------
    // Total de cierre
    // ------------------------------------------------------------------------

    /// <summary>
    /// Reproduce la linea del original: "TOTAL 116 COMPROMISO POR Bs.
    /// 490.687.911,95". El singular "COMPROMISO" no es un error de este codigo:
    /// es literalmente lo que imprime el reporte legado, y cambiarlo haria que
    /// los dos documentos no se puedan comparar linea a linea durante la
    /// validacion.
    /// </summary>
    private static void Total(IContainer container, int cantidad, decimal total, CultureInfo culture)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignRight().PaddingRight(8).BorderTop(0.7f).BorderColor(Colors.Black)
                .PaddingTop(3)
                .Text($"TOTAL {cantidad.ToString(CultureInfo.InvariantCulture)} COMPROMISO POR Bs.")
                .SemiBold().FontSize(8.5f);

            row.ConstantItem(AnchoMonto).BorderTop(0.7f).BorderColor(Colors.Black)
                .PaddingTop(3).AlignRight()
                .Text(Monto(total, culture)).SemiBold().FontSize(8.5f);
        });
    }

    // ------------------------------------------------------------------------
    // Pie
    // ------------------------------------------------------------------------

    private static void Pie(IContainer container, ReportPrintContext printContext)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Usuario, fecha/hora y pagina: el helper compartido del
                // requerimiento 17 imprime exactamente lo que el legado ponia en
                // user$currentdate mas el numero de pagina.
                row.RelativeItem().Element(e => ReportPdfFooter.Build(e, printContext, 6f));
            });

            col.Item().AlignRight().Text(Forma).FontSize(5.5f).Light();
        });
    }

    // ------------------------------------------------------------------------
    // Subtitulo con el periodo consultado
    // ------------------------------------------------------------------------

    /// <summary>
    /// Replica el <c>subtitulo</c> que armaba el trigger <c>AfterPForm</c>:
    /// "DESDE x HASTA y", "DESDE x" o "HASTA y" segun que fechas vengan.
    ///
    /// **Corrige un defecto del original:** cuando las dos fechas venian
    /// informadas **y eran iguales**, el legado entraba en una rama que fija los
    /// predicados de filtro pero **nunca asigna el subtitulo**, asi que una
    /// consulta de un solo dia salia sin ningun rango impreso. Aqui ese caso
    /// imprime la fecha unica.
    /// </summary>
    private static string ConstruirSubtitulo(ReporteRelacionCompromisoQuery query, CultureInfo culture)
    {
        var desde = query.FechaDesde;
        var hasta = query.FechaHasta;

        if (desde.HasValue && hasta.HasValue)
        {
            return desde.Value.Date == hasta.Value.Date
                ? Fecha(desde, culture)
                : $"DESDE {Fecha(desde, culture)} HASTA {Fecha(hasta, culture)}";
        }

        if (desde.HasValue)
        {
            return $"DESDE {Fecha(desde, culture)}";
        }

        if (hasta.HasValue)
        {
            return $"HASTA {Fecha(hasta, culture)}";
        }

        // Sin fechas el legado dejaba el subtitulo nulo, y el reporte lista el
        // presupuesto completo. Se conserva.
        return string.Empty;
    }

    private static string Fecha(DateTime? valor, CultureInfo culture) =>
        valor.HasValue ? valor.Value.ToString("dd/MM/yyyy", culture) : string.Empty;

    private static string Monto(decimal valor, CultureInfo culture) =>
        valor.ToString("N2", culture);
}
