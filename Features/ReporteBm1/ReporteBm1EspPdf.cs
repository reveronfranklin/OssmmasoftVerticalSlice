using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteBm1;

/// <summary>
/// Formulario oficial "Inventario de Bienes Muebles BM-1". Requerimiento 27.
///
/// Reproduce el layout del reporte legado <c>BM_BM1_ESP1.rdf</c>: encabezado de
/// entidad repetido en cada pagina, una tabla de detalle por unidad de trabajo
/// con salto de pagina entre unidades, subtotal por unidad, total general y
/// bloque de firmas.
///
/// **La columna Depreciacion se imprime vacia.** No es un olvido: el analisis
/// del binario no encontro ninguna formula que la calcule, y en el PDF de
/// muestra aparece en blanco linea por linea. Es un campo del formulario legal
/// pensado para completarse a mano. Si algun dia aparece la regla, este es el
/// unico sitio que hay que tocar.
/// </summary>
public static class ReporteBm1EspPdfGenerator
{
    private const string Titulo = "INVENTARIO DE BIENES MUEBLES";

    public static byte[] Generate(
        IReadOnlyList<ReporteBm1EspUnidad> unidades,
        ReporteBm1EspEntidad entidad,
        ReporteBm1EspQuery query)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var culture = CultureInfo.GetCultureInfo("es-VE");
        var generado = DateTime.Now;
        var cantidadGeneral = unidades.Sum(u => u.Cantidad);
        var totalGeneral = unidades.Sum(u => u.Total);
        var subtitulo = ConstruirSubtitulo(query, culture);

        return Document.Create(container =>
        {
            // Una unidad por seccion de documento: es lo que produce el salto de
            // pagina entre unidades sin tener que calcular alturas a mano.
            foreach (var unidad in unidades)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(16);
                    page.DefaultTextStyle(style => style.FontSize(6.5f));

                    page.Header().Element(e => Encabezado(e, unidad.UnidadTrabajo, entidad, generado, subtitulo));
                    page.Content().PaddingTop(6).Element(e => Detalle(e, unidad, culture));
                    page.Footer().Element(e => PieDePagina(e, generado));
                });
            }

            // El cierre va en su propia pagina: el total general y las firmas
            // pertenecen al reporte completo, no a la ultima unidad, y mezclarlos
            // haria pensar que las firmas amparan solo esa.
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(16);
                page.DefaultTextStyle(style => style.FontSize(6.5f));

                page.Header().Element(e => Encabezado(e, "RESUMEN GENERAL", entidad, generado, subtitulo));
                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().AlignRight().Text($"CANTIDAD GENERAL: {cantidadGeneral}")
                        .SemiBold().FontSize(8);
                    col.Item().AlignRight().Text($"TOTAL GENERAL: {Monto(totalGeneral, culture)}")
                        .SemiBold().FontSize(8);
                    col.Item().PaddingTop(40).Element(e => Firmas(e, query.Responsable));
                });
                page.Footer().Element(e => PieDePagina(e, generado));
            });

            if (unidades.Count == 0)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(16);
                    page.Content().AlignCenter().AlignMiddle()
                        .Text("Sin bienes para los filtros seleccionados.").FontSize(10);
                });
            }
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------------
    // Encabezado de entidad, repetido en cada pagina
    // ------------------------------------------------------------------------

    private static void Encabezado(
        IContainer container, string unidad, ReporteBm1EspEntidad entidad, DateTime generado, string subtitulo)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Text(Titulo).Bold().FontSize(10);
                row.ConstantItem(60).AlignRight().Text("BM - 1").Bold().FontSize(10);
            });

            if (!string.IsNullOrWhiteSpace(subtitulo))
            {
                col.Item().AlignCenter().Text(subtitulo).FontSize(7);
            }

            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem(3).Text(t =>
                {
                    t.Span("UNIDAD DE TRABAJO O DEPENDENCIA: ").SemiBold();
                    t.Span(unidad);
                });
                row.RelativeItem(1).Text("SERVICIO:").SemiBold();
            });

            col.Item().Text(t =>
            {
                t.Span("ENTIDAD PROPIETARIA: ").SemiBold();
                t.Span(entidad.EntidadPropietaria);
            });

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("ESTADO: ").SemiBold();
                    t.Span(entidad.Estado);
                });
                row.RelativeItem().Text(t =>
                {
                    t.Span("MUNICIPIO: ").SemiBold();
                    t.Span(entidad.Municipio);
                });
            });

            col.Item().Row(row =>
            {
                row.RelativeItem(3).Text(t =>
                {
                    t.Span("DIRECCION O LUGAR: ").SemiBold();
                    t.Span(entidad.Direccion);
                });
                row.RelativeItem(1).Text(t =>
                {
                    t.Span("FECHA: ").SemiBold();
                    t.Span(generado.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                });
                row.ConstantItem(70).AlignRight().Text(t =>
                {
                    t.Span("PAGINA: ").SemiBold();
                    t.CurrentPageNumber();
                });
            });
        });
    }

    // ------------------------------------------------------------------------
    // Detalle de una unidad, con su subtotal
    // ------------------------------------------------------------------------

    private static void Detalle(IContainer container, ReporteBm1EspUnidad unidad, CultureInfo culture)
    {
        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(26);   // GRUPO
                    columns.ConstantColumn(30);   // SUB-GRUPO
                    columns.ConstantColumn(30);   // SECCION
                    columns.ConstantColumn(34);   // LOTE
                    columns.ConstantColumn(34);   // CANTIDAD
                    columns.ConstantColumn(62);   // N DE IDENTIFICACION
                    columns.RelativeColumn(1.4f); // Responsable
                    columns.RelativeColumn(2.6f); // Nombre y descripcion
                    columns.ConstantColumn(62);   // Valor unitario
                    columns.ConstantColumn(68);   // Valor total
                    columns.ConstantColumn(54);   // Depreciacion
                });

                table.Header(header =>
                {
                    Cabecera(header, "GRUPO");
                    Cabecera(header, "SUB-GRUPO");
                    Cabecera(header, "SECCION");
                    Cabecera(header, "LOTE");
                    Cabecera(header, "CANTIDAD");
                    Cabecera(header, "N DE IDENTIFICACION");
                    Cabecera(header, "RESPONSABLE");
                    Cabecera(header, "NOMBRE Y DESCRIPCION DE LOS ELEMENTOS");
                    Cabecera(header, "VALOR UNITARIO");
                    Cabecera(header, "VALOR TOTAL");
                    Cabecera(header, "DEPRECIACION");
                });

                foreach (var item in unidad.Items)
                {
                    Celda(table, item.CodigoGrupo, centrar: true);
                    Celda(table, item.CodigoNivel1, centrar: true);
                    Celda(table, item.CodigoNivel2, centrar: true);
                    Celda(table, item.NumeroLote, centrar: true);
                    Celda(table, item.Cantidad.ToString(CultureInfo.InvariantCulture), derecha: true);
                    Celda(table, item.NumeroPlaca, centrar: true);
                    Celda(table, item.ResponsableBien);
                    Celda(table, $"{item.Articulo} {item.Especificacion}".Trim());
                    Celda(table, Monto(item.ValorActual, culture), derecha: true);
                    Celda(table, Monto(item.Cantidad * item.ValorActual, culture), derecha: true);

                    // Depreciacion: sin logica en el reporte original, se imprime
                    // en blanco para que se complete a mano.
                    Celda(table, string.Empty);
                }
            });

            col.Item().PaddingTop(4).AlignRight().Text(t =>
            {
                t.Span($"CANTIDAD: {unidad.Cantidad}    ").SemiBold();
                t.Span($"TOTAL: {Monto(unidad.Total, culture)}").SemiBold();
            });
        });
    }

    private static void Firmas(IContainer container, string? responsable)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(responsable))
            {
                col.Item().PaddingBottom(20).Text(t =>
                {
                    t.Span("RESPONSABLE: ").SemiBold();
                    t.Span(responsable);
                });
            }

            col.Item().Row(row =>
            {
                foreach (var cargo in new[] { "DIRECTOR (A)", "DIRECTOR DE ADMINISTRACION", "ANALISTA DE BIENES" })
                {
                    row.RelativeItem().PaddingHorizontal(10).Column(c =>
                    {
                        c.Item().PaddingTop(24).BorderTop(0.7f).BorderColor(Colors.Black);
                        c.Item().AlignCenter().Text(cargo).FontSize(6.5f);
                    });
                }
            });

            col.Item().PaddingTop(30).Row(row =>
            {
                foreach (var cargo in new[] { "COORDINADOR DE BIENES", "GERENTE DE BIENES" })
                {
                    row.RelativeItem().PaddingHorizontal(10).Column(c =>
                    {
                        c.Item().PaddingTop(24).BorderTop(0.7f).BorderColor(Colors.Black);
                        c.Item().AlignCenter().Text(cargo).FontSize(6.5f);
                    });
                }

                // Tercera columna vacia para que las dos firmas queden alineadas
                // con las de la fila de arriba y no centradas entre ellas.
                row.RelativeItem();
            });
        });
    }

    private static void PieDePagina(IContainer container, DateTime generado)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text($"Generado el {generado:dd/MM/yyyy HH:mm}").FontSize(5.5f);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.CurrentPageNumber().FontSize(5.5f);
                t.Span(" / ").FontSize(5.5f);
                t.TotalPages().FontSize(5.5f);
            });
        });
    }

    // ------------------------------------------------------------------------
    // Subtitulo con los filtros aplicados
    //
    // El reporte legado lo componia igual: quien recibe el PDF impreso tiene que
    // poder saber con que filtros se genero, porque el papel no lleva la
    // pantalla de parametros adjunta.
    // ------------------------------------------------------------------------

    private static string ConstruirSubtitulo(ReporteBm1EspQuery query, CultureInfo culture)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.PlacaDesde) || !string.IsNullOrWhiteSpace(query.PlacaHasta))
        {
            partes.Add($"N PLACA DESDE {query.PlacaDesde ?? "..."} HASTA {query.PlacaHasta ?? "..."}");
        }

        if (query.FechaDesde.HasValue || query.FechaHasta.HasValue)
        {
            var desde = query.FechaDesde?.ToString("dd/MM/yyyy", culture) ?? "...";
            var hasta = query.FechaHasta?.ToString("dd/MM/yyyy", culture) ?? "...";
            partes.Add($"DESDE EL {desde} AL {hasta}");
        }

        return string.Join("   -   ", partes);
    }

    // ------------------------------------------------------------------------
    // Utilidades de tabla
    // ------------------------------------------------------------------------

    private static void Cabecera(TableCellDescriptor header, string texto)
    {
        header.Cell().Border(0.5f).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten3)
            .Padding(2).AlignCenter().AlignMiddle().Text(texto).Bold().FontSize(5.5f);
    }

    // La alineacion se aplica ANTES de Text(): el contenedor queda consumido al
    // escribir el texto, asi que encadenarla despues no tiene efecto.
    private static void Celda(TableDescriptor table, string? texto, bool centrar = false, bool derecha = false)
    {
        var celda = table.Cell().Border(0.4f).BorderColor(Colors.Grey.Medium).Padding(2).MinHeight(9);
        var contenido = texto ?? string.Empty;

        if (derecha)
        {
            celda.AlignRight().Text(contenido).FontSize(5.8f);

            return;
        }

        if (centrar)
        {
            celda.AlignCenter().Text(contenido).FontSize(5.8f);

            return;
        }

        celda.Text(contenido).FontSize(5.8f);
    }

    private static string Monto(decimal valor, CultureInfo culture) =>
        valor.ToString("N2", culture);
}
