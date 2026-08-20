using OssmmasoftVerticalSlice.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteRelacionRetencionIva;

/// <summary>
/// Relacion de Retenciones de IVA por periodos. Requerimiento 22.
///
/// Reproduce el layout de <c>ADM_RELACION_RETENCION_IVA_OP2.rdf</c> segun el PDF
/// de muestra: titulo, el periodo consultado como subtitulo, y por cada
/// comprobante un bloque con su cabecera (comprobante, fecha, estatus, orden de
/// pago, proveedor) seguido de la tabla de sus documentos. Al cierre, un unico
/// TOTAL alineado bajo la columna Monto Retenido.
///
/// **El subtitulo es el rango de parametros, no el periodo fiscal de los datos.**
/// Es lo que hacia <c>CF_PERIODOFormula</c>: si las dos fechas coinciden imprime
/// una sola. La columna PERIODO_FISCAL del query legado ("Año:2026 Mes:Julio")
/// no se migro porque no aparece en ninguna parte del PDF de muestra.
///
/// **El RIF del proveedor no se imprime**, igual que en el reporte legado: el
/// query lo calculaba -y aqui se conserva en el DTO, para el contrato de
/// frontend- pero el layout V2 solo muestra el nombre. Si se quiere en el papel,
/// es una linea en <see cref="CabeceraComprobante"/>.
/// </summary>
public static class ReporteRelacionRetIvaPdfGenerator
{
    private const string Titulo = "RELACION DE RETENCIONES POR IVA";

    // Anchos fijos de las columnas de importe. Son constantes y no relativos a
    // proposito: es lo que mantiene las columnas alineadas entre un bloque de
    // comprobante y el siguiente, que es como se lee el reporte legado.
    private const float AnchoFecha = 55f;
    private const float AnchoMonto = 84f;
    private const float AnchoExento = 80f;
    private const float AnchoAlicuota = 32f;
    private const float AnchoRetenido = 88f;

    // Cuantos documentos puede tener un comprobante para que su bloque se
    // mantenga en una sola pagina. En Letter horizontal caben unas 40 filas de
    // detalle; 25 deja margen para nombres de proveedor que ocupen dos lineas.
    private const int MaxDocumentosBloqueJunto = 25;

    public static byte[] Generate(
        IReadOnlyList<ReporteRelacionRetIvaComprobante> comprobantes,
        ReporteRelacionRetIvaQuery query,
        ReportPrintContext printContext)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var culture = CultureInfo.GetCultureInfo("es-VE");
        var periodo = ConstruirPeriodo(query, culture);
        var totalGeneral = comprobantes.Sum(c => c.TotalRetenido);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(14);
                page.DefaultTextStyle(style => style.FontSize(6.5f));

                page.Header().Element(element => Encabezado(element, periodo, query.Estatus));

                page.Content().PaddingTop(8).Column(col =>
                {
                    foreach (var comprobante in comprobantes)
                    {
                        var bloque = col.Item().PaddingBottom(4);

                        // El bloque se mantiene junto para que la cabecera de un
                        // comprobante no quede al pie de una pagina con sus
                        // importes en la siguiente. Pero solo cuando cabe: a
                        // ShowEntire() no se le puede dar contenido mas alto que
                        // una pagina -lanza DocumentLayoutException-, y un
                        // comprobante con decenas de documentos es exactamente
                        // ese caso. Cuando no cabe se deja fluir; la tabla de
                        // detalle repite su propia cabecera de columnas en cada
                        // pagina, asi que las filas siguen siendo legibles.
                        if (comprobante.Documentos.Count <= MaxDocumentosBloqueJunto)
                        {
                            bloque = bloque.ShowEntire();
                        }

                        bloque.Column(contenido =>
                        {
                            contenido.Item().Element(e => CabeceraComprobante(e, comprobante, culture));
                            contenido.Item().Element(e => TablaDocumentos(e, comprobante, culture));
                        });
                    }

                    col.Item().PaddingTop(8).Element(e => TotalGeneral(e, totalGeneral, culture));
                });

                page.Footer().PaddingTop(4).Element(element =>
                    ReportPdfFooter.Build(element, printContext, 6f));
            });
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------------
    // Encabezado
    // ------------------------------------------------------------------------

    private static void Encabezado(IContainer container, string periodo, string? estatus)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(Titulo).Bold().FontSize(11);
            col.Item().AlignCenter().Text(periodo).FontSize(8);

            // Solo se imprime cuando se filtro: en el caso normal -todos los
            // estatus- una linea que diga "TODOS" es ruido.
            var etiqueta = EtiquetaEstatus(estatus);

            if (!string.IsNullOrEmpty(etiqueta))
            {
                col.Item().AlignCenter().Text($"ESTATUS: {etiqueta}").FontSize(7);
            }
        });
    }

    // ------------------------------------------------------------------------
    // Cabecera de un comprobante
    // ------------------------------------------------------------------------

    private static void CabeceraComprobante(
        IContainer container, ReporteRelacionRetIvaComprobante comprobante, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(110);
                columns.ConstantColumn(62);
                columns.ConstantColumn(70);
                columns.ConstantColumn(62);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                Cabecera(header, "Comprobante", centrar: true);
                Cabecera(header, "Fecha", centrar: true);
                Cabecera(header, "Estatus", centrar: true);
                Cabecera(header, "Orden Pago", centrar: true);
                Cabecera(header, "Proveedor");
            });

            Celda(table, comprobante.NumeroComprobante, centrar: true, negrita: true);
            Celda(table, Fecha(comprobante.Fecha, culture), centrar: true);
            Celda(table, comprobante.EstatusDescripcion, centrar: true);
            Celda(table, comprobante.NumeroOrdenPago, centrar: true);
            Celda(table, comprobante.NombreProveedor);
        });
    }

    // ------------------------------------------------------------------------
    // Documentos de un comprobante
    // ------------------------------------------------------------------------

    private static void TablaDocumentos(
        IContainer container, ReporteRelacionRetIvaComprobante comprobante, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(AnchoFecha);
                columns.ConstantColumn(AnchoMonto);
                columns.ConstantColumn(AnchoMonto);
                columns.ConstantColumn(AnchoExento);
                columns.ConstantColumn(AnchoAlicuota);
                columns.ConstantColumn(AnchoMonto);
                columns.ConstantColumn(AnchoRetenido);
            });

            table.Header(header =>
            {
                Cabecera(header, "Nro. Factura");
                Cabecera(header, "Fecha", centrar: true);
                Cabecera(header, "Monto del Documento", derecha: true);
                Cabecera(header, "Base Imponible", derecha: true);
                Cabecera(header, "Monto exento IVA", derecha: true);
                Cabecera(header, "%", centrar: true);
                Cabecera(header, "Impuesto IVA", derecha: true);
                Cabecera(header, "Monto Retenido", derecha: true);
            });

            foreach (var documento in comprobante.Documentos)
            {
                // Cuando el tipo de documento no es factura, el reporte legado
                // deja la celda en blanco -es lo que hace ADM_F_TIPO_DOCUMENTO-.
                // Se cae al numero de documento para no imprimir una fila de
                // importes sin ninguna referencia que la identifique.
                var referencia = string.IsNullOrWhiteSpace(documento.NumeroFactura)
                    ? documento.NumeroDocumento
                    : documento.NumeroFactura;

                Celda(table, referencia);
                Celda(table, Fecha(documento.FechaDocumento, culture), centrar: true);
                Celda(table, Monto(documento.MontoDocumento, culture), derecha: true);
                Celda(table, Monto(documento.BaseImponible, culture), derecha: true);
                Celda(table, Monto(documento.MontoImpuestoExento, culture), derecha: true);
                Celda(table, documento.Alicuota, centrar: true);
                Celda(table, Monto(documento.MontoImpuesto, culture), derecha: true);
                Celda(table, Monto(documento.MontoRetenidoNeto, culture), derecha: true);
            }
        });
    }

    // ------------------------------------------------------------------------
    // Total de cierre, alineado bajo la columna Monto Retenido
    // ------------------------------------------------------------------------

    private static void TotalGeneral(IContainer container, decimal total, CultureInfo culture)
    {
        container.Row(row =>
        {
            row.RelativeItem();
            row.ConstantItem(90).AlignRight().Text("TOTAL").Bold().FontSize(8);
            // BorderTop antes de AlignRight: al reves la linea se dibujaria solo
            // sobre el ancho del texto, no sobre la columna.
            row.ConstantItem(AnchoRetenido).BorderTop(0.7f).BorderColor(Colors.Black)
                .PaddingTop(2).AlignRight().Text(Monto(total, culture)).Bold().FontSize(8);
        });
    }

    // ------------------------------------------------------------------------
    // Periodo consultado (CF_PERIODOFormula del reporte legado)
    // ------------------------------------------------------------------------

    private static string ConstruirPeriodo(ReporteRelacionRetIvaQuery query, CultureInfo culture)
    {
        var desde = query.FechaDesde;
        var hasta = query.FechaHasta;

        if (!desde.HasValue && !hasta.HasValue)
        {
            return string.Empty;
        }

        if (desde.HasValue && hasta.HasValue && desde.Value.Date == hasta.Value.Date)
        {
            return Fecha(desde, culture);
        }

        return $"{Fecha(desde, culture)} - {Fecha(hasta, culture)}";
    }

    private static string EtiquetaEstatus(string? estatus)
    {
        return (estatus ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "AP" => "APROBADO",
            "PE" => "PENDIENTE",
            "AN" => "ANULADO",
            _ => string.Empty
        };
    }

    // ------------------------------------------------------------------------
    // Utilidades de tabla
    // ------------------------------------------------------------------------

    private static void Cabecera(
        TableCellDescriptor header, string texto, bool centrar = false, bool derecha = false)
    {
        var celda = header.Cell().Border(0.5f).BorderColor(Colors.Grey.Darken1)
            .Background(Colors.Grey.Lighten3).Padding(2).AlignMiddle();

        if (derecha)
        {
            celda.AlignRight().Text(texto).SemiBold().FontSize(6f);

            return;
        }

        if (centrar)
        {
            celda.AlignCenter().Text(texto).SemiBold().FontSize(6f);

            return;
        }

        celda.Text(texto).SemiBold().FontSize(6f);
    }

    // La alineacion se aplica ANTES de Text(): el contenedor queda consumido al
    // escribir el texto, asi que encadenarla despues no tiene efecto.
    private static void Celda(
        TableDescriptor table, string? texto, bool centrar = false, bool derecha = false, bool negrita = false)
    {
        var celda = table.Cell().Border(0.4f).BorderColor(Colors.Grey.Medium).Padding(2).MinHeight(9);
        var contenido = texto ?? string.Empty;

        if (derecha)
        {
            Escribir(celda.AlignRight(), contenido, negrita);

            return;
        }

        if (centrar)
        {
            Escribir(celda.AlignCenter(), contenido, negrita);

            return;
        }

        Escribir(celda, contenido, negrita);
    }

    private static void Escribir(IContainer celda, string contenido, bool negrita)
    {
        var texto = celda.Text(contenido).FontSize(6.2f);

        if (negrita)
        {
            texto.SemiBold();
        }
    }

    private static string Fecha(DateTime? valor, CultureInfo culture) =>
        valor.HasValue ? valor.Value.ToString("dd/MM/yyyy", culture) : string.Empty;

    private static string Monto(decimal valor, CultureInfo culture) =>
        valor.ToString("N2", culture);
}
