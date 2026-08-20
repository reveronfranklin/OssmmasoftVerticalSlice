using OssmmasoftVerticalSlice.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteChequesPeriodoMotivo;

/// <summary>
/// Relacion de Cheques Emitidos Por Periodos (con Motivo). Requerimiento 23.
///
/// Reproduce el layout de <c>ADM_PERIODOS_CHEQUES_MOTIVO1.rdf</c> segun el PDF de
/// muestra: una seccion por banco/cuenta que arranca en pagina nueva, con el
/// titulo y el encabezado del banco repetidos en cada pagina de la seccion, la
/// tabla de cheques con su bloque de motivo debajo de cada fila, y al cierre de
/// la seccion el recuadro de totales con dos columnas -TOTAL BANCO y TOTAL
/// GENERAL-.
///
/// **El TOTAL GENERAL se repite en cada seccion y es el del reporte completo**,
/// no un acumulado parcial. Es lo que hacia el reporte legado con sus columnas de
/// resumen a nivel de reporte (<c>CS_TOTAL_VALIDOS</c> y compania), y se
/// comprueba en el PDF de muestra: los tres grupos imprimen el mismo
/// 44 / 208.234.142,39, que es la suma de los tres subtotales.
/// </summary>
public static class ReporteChequesMotivoPdfGenerator
{
    private const string Titulo = "RELACION DE CHEQUES EMITIDOS DURANTE EL PERIODO";

    private const float AnchoFecha = 60f;
    private const float AnchoDocumento = 92f;
    private const float AnchoStatus = 44f;
    private const float AnchoMonto = 100f;

    public static byte[] Generate(
        IReadOnlyList<ReporteChequesMotivoGrupo> grupos,
        ReporteChequesMotivoQuery query,
        ReportPrintContext printContext)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var culture = CultureInfo.GetCultureInfo("es-VE");
        var periodo = ConstruirPeriodo(query, culture);

        var general = new Totales(
            grupos.Sum(g => g.CantidadValidos),
            grupos.Sum(g => g.MontoValidos),
            grupos.Sum(g => g.CantidadAnulados),
            grupos.Sum(g => g.MontoAnulados));

        return Document.Create(container =>
        {
            // Una seccion por banco/cuenta: es lo que produce el salto de pagina
            // entre cuentas -como en el PDF de muestra- sin calcular alturas a
            // mano, y lo que hace que el encabezado del banco se repita en las
            // paginas de continuacion de esa misma cuenta.
            foreach (var grupo in grupos)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(14);
                    page.DefaultTextStyle(style => style.FontSize(6.8f));

                    page.Header().Element(e => Encabezado(e, grupo, periodo, query));
                    page.Content().PaddingTop(6).Column(col =>
                    {
                        col.Item().Element(e => TablaCheques(e, grupo, culture));
                        col.Item().PaddingTop(10).Element(e => Totalizacion(e, grupo, general, culture));
                    });
                    page.Footer().PaddingTop(4).Element(e =>
                        ReportPdfFooter.Build(e, printContext, 6f));
                });
            }
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------------
    // Encabezado, repetido en cada pagina de la seccion
    // ------------------------------------------------------------------------

    private static void Encabezado(
        IContainer container,
        ReporteChequesMotivoGrupo grupo,
        string periodo,
        ReporteChequesMotivoQuery query)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(Titulo).Bold().FontSize(10);
            col.Item().AlignCenter().Text(periodo).FontSize(8);

            var filtros = ConstruirFiltros(query);

            if (!string.IsNullOrEmpty(filtros))
            {
                col.Item().AlignCenter().Text(filtros).FontSize(6.5f);
            }

            col.Item().PaddingTop(8).AlignCenter().Text("DATOS DEL BANCO").SemiBold().FontSize(8);

            // "BANESCO CUENTA Nro. 0134...". El legado imprimia "CUENTA N" con el
            // signo de ordinal; se usa "Nro." para no depender de la codificacion
            // del caracter, igual que en el resto de reportes del slice.
            col.Item().PaddingTop(2).Text(
                $"{grupo.NombreBanco} CUENTA Nro. {grupo.NumeroCuenta}").SemiBold().FontSize(8);
        });
    }

    // ------------------------------------------------------------------------
    // Tabla de cheques de un banco/cuenta
    // ------------------------------------------------------------------------

    private static void TablaCheques(
        IContainer container, ReporteChequesMotivoGrupo grupo, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(AnchoFecha);
                columns.ConstantColumn(AnchoDocumento);
                columns.ConstantColumn(AnchoStatus);
                columns.RelativeColumn();
                columns.ConstantColumn(AnchoMonto);
            });

            // Solo una regla bajo los titulos, como el reporte legado: una tabla
            // con todos los bordes competiria visualmente con los bloques de
            // motivo, que son parrafos largos.
            table.Header(header =>
            {
                Cabecera(header, "FECHA", centrar: true);
                Cabecera(header, "Nro. DOCUMENTO", centrar: true);
                Cabecera(header, "STATUS", centrar: true);
                Cabecera(header, "BENEFICIARIO");
                Cabecera(header, "MONTO Bs.", derecha: true);
            });

            foreach (var item in grupo.Items)
            {
                table.Cell().PaddingVertical(1).AlignCenter()
                    .Text(Fecha(item.FechaCheque, culture)).FontSize(6.5f);
                table.Cell().PaddingVertical(1).AlignCenter()
                    .Text(item.NumeroDocumento).FontSize(6.5f);
                table.Cell().PaddingVertical(1).AlignCenter()
                    .Text(item.EstatusDescripcion).FontSize(6.5f);
                table.Cell().PaddingVertical(1)
                    .Text(item.Beneficiario).FontSize(6.5f);
                table.Cell().PaddingVertical(1).AlignRight()
                    .Text(Monto(item.Monto, culture)).FontSize(6.5f);

                if (string.IsNullOrWhiteSpace(item.Motivo))
                {
                    continue;
                }

                // El motivo arranca bajo BENEFICIARIO y se extiende hasta el
                // borde, igual que en el PDF de muestra: es un parrafo largo
                // -motivo del cheque, referencia de la orden de pago y partidas
                // imputadas- y darle solo el ancho de una columna lo dejaria
                // ilegible.
                table.Cell().ColumnSpan(3);
                table.Cell().ColumnSpan(2).PaddingBottom(3).PaddingLeft(2)
                    .Text(item.Motivo).FontSize(6f).LineHeight(1.1f);
            }
        });
    }

    // ------------------------------------------------------------------------
    // Recuadro de totales: subtotal del banco y total general del reporte
    // ------------------------------------------------------------------------

    private sealed record Totales(
        int CantidadValidos, decimal MontoValidos, int CantidadAnulados, decimal MontoAnulados);

    private static void Totalizacion(
        IContainer container,
        ReporteChequesMotivoGrupo grupo,
        Totales general,
        CultureInfo culture)
    {
        var banco = new Totales(
            grupo.CantidadValidos, grupo.MontoValidos, grupo.CantidadAnulados, grupo.MontoAnulados);

        container.AlignRight().Width(430).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(150).AlignCenter().Text("TOTAL BANCO").SemiBold().FontSize(7.5f);
                row.ConstantItem(150).AlignCenter().Text("TOTAL GENERAL").SemiBold().FontSize(7.5f);
            });

            col.Item().BorderTop(0.7f).BorderColor(Colors.Black).PaddingTop(2)
                .Element(e => LineaTotal(e, "CHEQUES VALIDOS:",
                    banco.CantidadValidos, banco.MontoValidos,
                    general.CantidadValidos, general.MontoValidos, culture));

            col.Item().Element(e => LineaTotal(e, "CHEQUES ANULADOS:",
                banco.CantidadAnulados, banco.MontoAnulados,
                general.CantidadAnulados, general.MontoAnulados, culture));
        });
    }

    private static void LineaTotal(
        IContainer container,
        string etiqueta,
        int cantidadBanco,
        decimal montoBanco,
        int cantidadGeneral,
        decimal montoGeneral,
        CultureInfo culture)
    {
        container.PaddingVertical(1).Row(row =>
        {
            row.RelativeItem().AlignRight().PaddingRight(6)
                .Text(etiqueta).SemiBold().FontSize(7f);

            row.ConstantItem(40).AlignRight()
                .Text(cantidadBanco.ToString(CultureInfo.InvariantCulture)).FontSize(7f);
            row.ConstantItem(110).AlignRight()
                .Text(Monto(montoBanco, culture)).FontSize(7f);

            row.ConstantItem(40).AlignRight()
                .Text(cantidadGeneral.ToString(CultureInfo.InvariantCulture)).FontSize(7f);
            row.ConstantItem(110).AlignRight()
                .Text(Monto(montoGeneral, culture)).FontSize(7f);
        });
    }

    // ------------------------------------------------------------------------
    // Periodo y filtros aplicados
    // ------------------------------------------------------------------------

    /// <summary>
    /// Subtitulo del reporte. Replica <c>CF_MENBRETE</c>, que concatenaba el
    /// titulo con el rango de fechas.
    /// </summary>
    private static string ConstruirPeriodo(ReporteChequesMotivoQuery query, CultureInfo culture)
    {
        var desde = Fecha(query.FechaDesde, culture);
        var hasta = Fecha(query.FechaHasta, culture);

        if (desde.Length == 0 && hasta.Length == 0)
        {
            return string.Empty;
        }

        return desde == hasta ? desde : $"{desde} - {hasta}";
    }

    /// <summary>
    /// Los filtros opcionales que si se aplicaron. Quien recibe el PDF impreso
    /// tiene que poder saber con que se genero, porque el papel no lleva la
    /// pantalla de parametros adjunta. Los que no se usaron no se mencionan: una
    /// linea que diga "banco: todos" es ruido.
    /// </summary>
    private static string ConstruirFiltros(ReporteChequesMotivoQuery query)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.NumeroCuenta))
        {
            partes.Add($"CUENTA: {query.NumeroCuenta.Trim()}");
        }

        var status = (query.Status ?? string.Empty).Trim().ToUpperInvariant();

        if (status is "AP" or "AN")
        {
            partes.Add($"STATUS: {(status == "AP" ? "APROBADO" : "ANULADO")}");
        }

        if (query.CodigoProveedor is int proveedor && proveedor > 0)
        {
            partes.Add($"PROVEEDOR: {proveedor.ToString(CultureInfo.InvariantCulture)}");
        }

        return string.Join("   -   ", partes);
    }

    // ------------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------------

    private static void Cabecera(
        TableCellDescriptor header, string texto, bool centrar = false, bool derecha = false)
    {
        var celda = header.Cell().BorderBottom(0.7f).BorderColor(Colors.Black)
            .PaddingBottom(2).PaddingTop(4);

        if (derecha)
        {
            celda.AlignRight().Text(texto).SemiBold().FontSize(6.8f);

            return;
        }

        if (centrar)
        {
            celda.AlignCenter().Text(texto).SemiBold().FontSize(6.8f);

            return;
        }

        celda.Text(texto).SemiBold().FontSize(6.8f);
    }

    private static string Fecha(DateTime? valor, CultureInfo culture) =>
        valor.HasValue ? valor.Value.ToString("dd/MM/yyyy", culture) : string.Empty;

    private static string Monto(decimal valor, CultureInfo culture) =>
        valor.ToString("N2", culture);
}
