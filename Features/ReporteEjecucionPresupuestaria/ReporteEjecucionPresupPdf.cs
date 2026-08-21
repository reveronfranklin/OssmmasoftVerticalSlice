using OssmmasoftVerticalSlice.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteEjecucionPresupuestaria;

/// <summary>
/// Ejecucion Presupuestaria y Financiera del Presupuesto de Gastos.
/// Requerimiento 26 - migracion de <c>PRE_EJECUCION_POR_FECHA_FP.M4</c>.
///
/// Reproduce el layout del PDF de muestra: titulo, el periodo presupuestario y el
/// rango de fechas como subtitulo, y una seccion por imputacion presupuestaria
/// (ICP) que arranca en pagina nueva y repite su encabezado en las paginas de
/// continuacion -en el PDF de muestra el grupo <c>01-02-02-00-53</c> abarca ocho
/// paginas seguidas y todas reimprimen su encabezado-. Cada seccion cierra con
/// "TOTAL &lt;codigo-icp&gt;" y la ultima agrega el "TOTAL GENERAL" del reporte.
///
/// La tabla de cada seccion lleva las cinco columnas de codigo del Plan Unico de
/// Cuentas -"PA. GE. ES. SE. EX."-, la denominacion indentada por nivel y las
/// diez columnas numericas. **La columna BLOQUEADO del cursor no se imprime**: el
/// reporte legado no la lista entre sus diez columnas y agregarla haria que los
/// dos documentos no se puedan comparar columna a columna durante la validacion.
///
/// Los importes se imprimen en la fila de cada nivel, repetidos en los
/// descendientes, tal como salen del cursor; **los subtotales, en cambio, suman
/// solo las filas de nivel 1**, que es lo que ya resolvio
/// <see cref="ReporteEjecucionPresupTotales.De"/> en el handler. Ver el
/// encabezado de <c>ReporteEjecucionPresupGetAll.cs</c>.
/// </summary>
public static class ReporteEjecucionPresupPdfGenerator
{
    private const string Titulo = "EJECUCION PRESUPUESTARIA Y FINANCIERA DEL PRESUPUESTO DE GASTOS";
    private const string Forma = "Forma SAMI-PRE_EJECUCION";

    /// <summary>
    /// Ancho de cada columna de codigo de nivel (PA. GE. ES. SE. EX.).
    /// </summary>
    private const float AnchoCodigo = 18f;

    /// <summary>
    /// Ancho de cada una de las diez columnas numericas.
    /// </summary>
    private const float AnchoMonto = 66f;

    private const float FuenteTabla = 5.5f;
    private const float FuenteCabecera = 5.5f;

    /// <summary>
    /// Indentacion por nivel de la jerarquia, en puntos.
    /// </summary>
    private const float SangriaNivel = 5f;

    /// <summary>
    /// Etiquetas de las diez columnas numericas, en el orden del reporte legado.
    /// </summary>
    private static readonly string[] Columnas =
    [
        "PRESUPUESTADO ANUAL",
        "MODIFICACION PRESUPUESTARIA",
        "PRESUPUESTO REAL MODIFICADO",
        "COMPROMISO",
        "CAUSADO",
        "PAGADO",
        "DEUDA",
        "DISPONIBILIDAD PRESUPUESTARIA",
        "DESEMBOLSO",
        "DISPONIBILIDAD FINANCIERA"
    ];

    public static byte[] Generate(
        IReadOnlyList<ReporteEjecucionPresupGrupo> grupos,
        ReporteEjecucionPresupQuery query,
        ReportPrintContext printContext)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var culture = CultureInfo.GetCultureInfo("es-VE");
        var periodo = ConstruirPeriodo(query, culture);
        var ejercicio = ConstruirEjercicio(query);
        var filtros = ConstruirFiltros(query);
        var general = ReporteEjecucionPresupTotales.Sumar(grupos.Select(g => g.Totales));

        return Document.Create(container =>
        {
            // Una seccion por ICP: es lo que produce el salto de pagina entre
            // imputaciones -como en el PDF de muestra- sin calcular alturas a
            // mano, y lo que hace que el encabezado del grupo se repita en las
            // paginas de continuacion de ese mismo grupo.
            for (var i = 0; i < grupos.Count; i++)
            {
                var grupo = grupos[i];
                var ultimo = i == grupos.Count - 1;

                container.Page(page =>
                {
                    // Legal apaisado y no Letter como el resto de los reportes del
                    // slice: las diez columnas numericas mas las cinco de codigo y
                    // la denominacion no caben en 792 puntos sin dejar la
                    // denominacion en una columna de dos palabras por linea.
                    page.Size(PageSizes.Legal.Landscape());
                    page.Margin(14);
                    page.DefaultTextStyle(style => style.FontSize(FuenteTabla));

                    page.Header().Element(e => Encabezado(e, grupo, ejercicio, periodo, filtros));
                    page.Content().PaddingTop(6).Column(col =>
                    {
                        col.Item().Element(e => Tabla(e, grupo, culture));

                        col.Item().PaddingTop(4).Element(e => FilaTotal(
                            e, $"TOTAL {grupo.CodigoIcp}", grupo.Totales, culture, false));

                        // El total general va una sola vez, al cierre del reporte,
                        // al contrario de los reportes de cheques (requerimientos
                        // 23 y 24) que lo repiten en cada grupo.
                        if (ultimo)
                        {
                            col.Item().PaddingTop(6).Element(e => FilaTotal(
                                e, "TOTAL GENERAL", general, culture, true));
                        }
                    });
                    page.Footer().PaddingTop(4).Element(e => Pie(e, printContext));
                });
            }
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------------
    // Encabezado, repetido en cada pagina de la seccion
    // ------------------------------------------------------------------------

    private static void Encabezado(
        IContainer container,
        ReporteEjecucionPresupGrupo grupo,
        string ejercicio,
        string periodo,
        string filtros)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(Titulo).Bold().FontSize(9.5f);

            if (!string.IsNullOrEmpty(ejercicio))
            {
                col.Item().AlignCenter().Text(ejercicio).FontSize(7.5f);
            }

            if (!string.IsNullOrEmpty(periodo))
            {
                col.Item().AlignCenter().Text(periodo).FontSize(7.5f);
            }

            if (!string.IsNullOrEmpty(filtros))
            {
                col.Item().AlignCenter().Text(filtros).FontSize(6.5f);
            }

            // Encabezado del quiebre de grupo: "01-02-01-00-51 SERVICIOS DE
            // LEGISLACION DE CAPITAL HUMANO (COMISION DE CAPITAL HUMANO)".
            col.Item().PaddingTop(8).Text(
                $"{grupo.CodigoIcp} {grupo.DenominacionIcp}").SemiBold().FontSize(7.5f);
        });
    }

    // ------------------------------------------------------------------------
    // Tabla de subpartidas de un grupo ICP
    // ------------------------------------------------------------------------

    private static void Tabla(
        IContainer container, ReporteEjecucionPresupGrupo grupo, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var nivel = 0; nivel < 5; nivel++)
                {
                    columns.ConstantColumn(AnchoCodigo);
                }

                columns.RelativeColumn();

                for (var columna = 0; columna < Columnas.Length; columna++)
                {
                    columns.ConstantColumn(AnchoMonto);
                }
            });

            // Cabecera de dos niveles, como el original: "SUBPARTIDAS" abarca las
            // cinco columnas de codigo de nivel.
            table.Header(header =>
            {
                header.Cell().ColumnSpan(5).AlignCenter().PaddingBottom(1)
                    .Text("SUBPARTIDAS").SemiBold().FontSize(6f);
                header.Cell().RowSpan(2).AlignMiddle()
                    .Text("DENOMINACION").SemiBold().FontSize(6f);

                foreach (var columna in Columnas)
                {
                    header.Cell().RowSpan(2).AlignMiddle().AlignRight().PaddingRight(2)
                        .Text(columna).SemiBold().FontSize(FuenteCabecera);
                }

                foreach (var nivel in new[] { "PA.", "GE.", "ES.", "SE.", "EX." })
                {
                    header.Cell().BorderBottom(0.7f).BorderColor(Colors.Black)
                        .PaddingBottom(2).AlignCenter().Text(nivel).SemiBold().FontSize(FuenteCabecera);
                }

                // Las celdas con RowSpan(2) ya ocupan esta fila; cerrarles el borde
                // inferior exigiria once celdas mas, que QuestPDF pondria en una
                // tercera fila. La regla queda solo bajo los codigos de nivel, que
                // es donde el original la tiene.
            });

            foreach (var item in grupo.Items)
            {
                Fila(table, item, culture);
            }
        });
    }

    /// <summary>
    /// Una fila de detalle. El codigo se imprime en la columna de su nivel y la
    /// denominacion se indenta segun ese mismo nivel: asi se lee la jerarquia
    /// Partida &gt; Generica &gt; Especifica &gt; Subespecifica &gt; Nivel5 del
    /// reporte original.
    /// </summary>
    private static void Fila(
        TableDescriptor table, ReporteEjecucionPresupItem item, CultureInfo culture)
    {
        var nivel = Math.Clamp(item.Nivel, 1, 5);

        for (var columna = 1; columna <= 5; columna++)
        {
            var celda = table.Cell().PaddingVertical(0.5f).AlignCenter();

            if (columna == nivel)
            {
                celda.Text(item.CodigoNivel).FontSize(FuenteTabla);

                continue;
            }

            celda.Text(string.Empty);
        }

        table.Cell().PaddingVertical(0.5f).PaddingLeft((nivel - 1) * SangriaNivel).PaddingRight(4)
            .Text(item.DenominacionPuc).FontSize(FuenteTabla);

        Monto(table, item.Presupuestado, culture);
        Monto(table, item.Modificado, culture);
        Monto(table, item.Vigente, culture);
        Monto(table, item.Comprometido, culture);
        Monto(table, item.Causado, culture);
        Monto(table, item.Pagado, culture);
        Monto(table, item.Deuda, culture);
        Monto(table, item.Disponibilidad, culture);
        Monto(table, item.Asignacion, culture);
        Monto(table, item.DisponibilidadFinanciera, culture);
    }

    private static void Monto(TableDescriptor table, decimal valor, CultureInfo culture)
    {
        table.Cell().PaddingVertical(0.5f).PaddingRight(2).AlignRight()
            .Text(Formato(valor, culture)).FontSize(FuenteTabla);
    }

    // ------------------------------------------------------------------------
    // Subtotal de grupo y total general
    // ------------------------------------------------------------------------

    private static void FilaTotal(
        IContainer container,
        string etiqueta,
        ReporteEjecucionPresupTotales totales,
        CultureInfo culture,
        bool destacado)
    {
        var fuente = destacado ? 7f : 6.5f;

        container.BorderTop(0.7f).BorderColor(Colors.Black).PaddingTop(2).Row(row =>
        {
            row.RelativeItem().PaddingRight(6).AlignRight()
                .Text(etiqueta).SemiBold().FontSize(fuente);

            foreach (var valor in new[]
            {
                totales.Presupuestado,
                totales.Modificado,
                totales.Vigente,
                totales.Comprometido,
                totales.Causado,
                totales.Pagado,
                totales.Deuda,
                totales.Disponibilidad,
                totales.Asignacion,
                totales.DisponibilidadFinanciera
            })
            {
                row.ConstantItem(AnchoMonto).PaddingRight(2).AlignRight()
                    .Text(Formato(valor, culture)).SemiBold().FontSize(fuente);
            }
        });
    }

    // ------------------------------------------------------------------------
    // Pie
    // ------------------------------------------------------------------------

    private static void Pie(IContainer container, ReportPrintContext printContext)
    {
        container.Column(col =>
        {
            // Usuario, fecha/hora y pagina: el helper compartido del requerimiento
            // 17 imprime exactamente lo que el legado ponia en user$currentdate mas
            // el numero de pagina.
            col.Item().Element(e => ReportPdfFooter.Build(e, printContext, 6f));
            col.Item().AlignRight().Text(Forma).FontSize(5.5f).Light();
        });
    }

    // ------------------------------------------------------------------------
    // Subtitulos y filtros aplicados
    // ------------------------------------------------------------------------

    /// <summary>
    /// "PERIODO PRESUPUESTARIO ANO &lt;YYYY&gt;". El ano sale de la fecha desde,
    /// igual que el recorte de la fecha hasta que hace el handler: el reporte es de
    /// un solo ejercicio presupuestario.
    /// </summary>
    private static string ConstruirEjercicio(ReporteEjecucionPresupQuery query)
    {
        return query.FechaDesde.HasValue
            ? $"PERIODO PRESUPUESTARIO ANO {query.FechaDesde.Value.Year.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
    }

    /// <summary>
    /// "DESDE EL &lt;fecha&gt; HASTA EL &lt;fecha&gt;", como lo armaba el trigger
    /// <c>AfterPForm</c>.
    ///
    /// Repite el recorte de la fecha hasta al 31/12 del ano de la fecha desde que
    /// hizo el handler antes de consultar. Sin eso el subtitulo prometeria un rango
    /// que los datos impresos no cubren.
    /// </summary>
    private static string ConstruirPeriodo(ReporteEjecucionPresupQuery query, CultureInfo culture)
    {
        if (!query.FechaDesde.HasValue || !query.FechaHasta.HasValue)
        {
            return string.Empty;
        }

        var desde = query.FechaDesde.Value.Date;
        var hasta = query.FechaHasta.Value.Date;

        if (hasta.Year > desde.Year)
        {
            hasta = new DateTime(desde.Year, 12, 31);
        }

        return $"DESDE EL {Fecha(desde, culture)} HASTA EL {Fecha(hasta, culture)}";
    }

    /// <summary>
    /// Los filtros opcionales que si se aplicaron. Quien recibe el PDF impreso
    /// tiene que poder saber con que se genero, porque el papel no lleva la
    /// pantalla de parametros adjunta.
    ///
    /// El valor 92 se rotula "CONSOLIDADO" porque es como lo trata el stored
    /// procedure -todas las fuentes, excluyendo la 719 de los importes-, no como un
    /// filtro por la fuente numero 92.
    /// </summary>
    private static string ConstruirFiltros(ReporteEjecucionPresupQuery query)
    {
        if (query.FinanciadoId is not int financiado || financiado <= 0)
        {
            return string.Empty;
        }

        return financiado == 92
            ? "FUENTE DE FINANCIAMIENTO: CONSOLIDADO"
            : $"FUENTE DE FINANCIAMIENTO: {financiado.ToString(CultureInfo.InvariantCulture)}";
    }

    // ------------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------------

    private static string Fecha(DateTime valor, CultureInfo culture) =>
        valor.ToString("dd/MM/yyyy", culture);

    private static string Formato(decimal valor, CultureInfo culture) =>
        valor.ToString("N2", culture);
}
