using System.Globalization;
using OssmmasoftVerticalSlice.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OssmmasoftVerticalSlice.Features.ReporteRelacionCompOpPagos;

public static class ReporteRelacionCompOpPagosPdfGenerator
{
    // Proporciones medidas sobre COP_2026: compromiso, descripcion, importe,
    // numero/fecha/importe de orden y numero/fecha/importe de cheque.
    private static readonly float[] Anchos = [78, 48, 210, 72, 36, 48, 72, 48, 48, 72];

    public static byte[] Generate(CopReporte reporte, ReporteRelacionCompOpPagosQuery query,
        ReportPrintContext contexto, IWebHostEnvironment? ambiente = null)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var cultura = CultureInfo.GetCultureInfo("es-VE");
        var logoPath = Path.Combine(ambiente?.ContentRootPath ?? AppContext.BaseDirectory, "Assets", "Reports", "logoLeft.jpeg");
        var logo = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;
        string Fecha(DateTime? f) => f?.ToString("dd/MM/yyyy", cultura) ?? string.Empty;
        string Monto(decimal m) => m.ToString("N2", cultura);

        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter.Landscape());
            pagina.MarginLeft(18); pagina.MarginRight(42);
            pagina.MarginTop(12); pagina.MarginBottom(50);
            pagina.DefaultTextStyle(s => s.FontSize(7.5f));
            pagina.Header().Column(c =>
            {
                c.Item().Height(84).CornerRadius(9).Border(0.65f).PaddingHorizontal(7).Column(encabezado =>
                {
                    encabezado.Item().Height(74).Layers(l =>
                    {
                        l.PrimaryLayer().AlignCenter().AlignMiddle().Column(titulo =>
                        {
                            titulo.Item().AlignCenter().Text("RELACI\u00d3N DE COMPROMISOS CON ORDEN DE PAGO Y CHEQUE").Bold().FontSize(9.5f);
                            titulo.Item().AlignCenter().Text($"DESDE {Fecha(query.FechaDesde)} HASTA {Fecha(query.FechaHasta)}").FontSize(7);
                            if (query.CodigosProveedores is { Count: > 0 })
                                titulo.Item().AlignCenter().Text($"Proveedores seleccionados: {query.CodigosProveedores.Distinct().Count()}").FontSize(6);
                        });
                        if (logo is not null)
                            l.Layer().PaddingLeft(30).PaddingTop(15).Width(54).Height(25).Image(logo).FitArea();
                    });
                    encabezado.Item().Text(string.Join(" / ", reporte.Items.Select(i => i.Entidad).Where(e => e.Length > 0).Distinct())).FontSize(5);
                });
                c.Item().Element(CabeceraTabla);
            });

            pagina.Content().CornerRadius(9).Border(0.65f).Layers(l =>
            {
                // Las lineas verticales llenan el area del reporte como en el
                // original, incluso al final de una pagina con pocos registros.
                l.Layer().Svg(size =>
                {
                    var acumulado = 0f;
                    var lineas = new System.Text.StringBuilder();
                    foreach (var ancho in Anchos.Take(Anchos.Length - 1))
                    {
                        acumulado += ancho;
                        var x = size.Width * acumulado / Anchos.Sum();
                        lineas.Append(FormattableString.Invariant($"<line x1='{x}' y1='0' x2='{x}' y2='{size.Height}' stroke='black' stroke-width='0.5'/>"));
                    }
                    return FormattableString.Invariant($"<svg xmlns='http://www.w3.org/2000/svg' width='{size.Width}' height='{size.Height}'>{lineas}</svg>");
                });
                l.PrimaryLayer().ExtendVertical().Column(c =>
                {
                    foreach (var item in reporte.Items)
                    {
                        var hijos = new List<(CopOrden? Orden, CopPago? Pago)>();
                        foreach (var orden in item.Ordenes)
                        {
                            if (orden.Pagos.Count == 0) hijos.Add((orden, null));
                            else
                            {
                                hijos.Add((orden, orden.Pagos[0]));
                                hijos.AddRange(orden.Pagos.Skip(1).Select(p => ((CopOrden?)null, (CopPago?)p)));
                            }
                        }
                        if (hijos.Count == 0) hijos.Add((null, null));
                        c.Item().EnsureSpace(45).Table(t =>
                        {
                            Columnas(t);
                            t.Cell().Element(Celda).Text(item.NumeroCompromiso);
                            t.Cell().Element(Celda).Text(Fecha(item.FechaCompromiso));
                            t.Cell().Element(Celda).BorderBottom(0.4f).Column(d =>
                            {
                                d.Item().Text(item.CedRif);
                                d.Item().Text(item.NombreProveedor);
                                d.Item().Text(item.Motivo);
                                d.Item().Text(item.Partida);
                            });
                            t.Cell().Element(Celda).AlignRight().Text(Monto(item.MontoCompromiso));
                            ImprimirHijo(t, hijos[0].Orden, hijos[0].Pago);
                            foreach (var hijo in hijos.Skip(1))
                            {
                                // Referencia del padre sin repetir su importe.
                                t.Cell().Element(Celda).Text(item.NumeroCompromiso).FontSize(6);
                                t.Cell().Element(Celda).Text(string.Empty);
                                t.Cell().Element(Celda).Text(item.Partida).FontSize(6);
                                t.Cell().Element(Celda).Text(string.Empty);
                                ImprimirHijo(t, hijo.Orden, hijo.Pago);
                            }
                        });
                    }
                    c.Item().EnsureSpace(34).PaddingTop(4).Table(t =>
                    {
                        Columnas(t);
                        t.Cell().ColumnSpan(3).Element(Celda).AlignRight().Text($"TOTAL {reporte.Items.Count} COMPROMISO POR Bs.").SemiBold();
                        t.Cell().Element(Celda).AlignRight().Text(Monto(reporte.TotalComprometido)).SemiBold();
                        t.Cell().ColumnSpan(2).Element(Celda).AlignRight().Text("TOTAL").SemiBold();
                        t.Cell().Element(Celda).AlignRight().Text(reporte.Items.Any(i => i.Ordenes.Count > 0) ? Monto(reporte.TotalOrdenado) : string.Empty).SemiBold();
                        t.Cell().ColumnSpan(2).Element(Celda).AlignRight().Text("TOTAL").SemiBold();
                        t.Cell().Element(Celda).AlignRight().Text(reporte.Items.Any(i => i.Ordenes.Any(o => o.Pagos.Count > 0)) ? Monto(reporte.TotalPagado) : string.Empty).SemiBold();
                        t.Cell().ColumnSpan(10).PaddingHorizontal(3).Text($"Compromisos unicos: {reporte.CantidadCompromisos}").FontSize(5.5f);
                    });
                });
            });
            pagina.Footer().PaddingTop(2).Column(c =>
            {
                c.Item().Element(e => ReportPdfFooter.Build(e, contexto, 5));
                c.Item().AlignRight().Text("Forma: Ossmmasoft-ADM_RELACION_COMPROMISO_OP_CHA").FontSize(5);
            });
        })).GeneratePdf();

        void ImprimirHijo(TableDescriptor t, CopOrden? orden, CopPago? pago)
        {
            t.Cell().Element(Celda).Text(orden?.NumeroOrdenPago ?? string.Empty);
            t.Cell().Element(Celda).Text(Fecha(orden?.FechaOrdenPago));
            t.Cell().Element(Celda).AlignRight().Text(orden is null ? string.Empty : Monto(orden.MontoOrdenPago));
            t.Cell().Element(Celda).Text(pago?.NumeroCheque ?? string.Empty);
            t.Cell().Element(Celda).Text(Fecha(pago?.FechaCheque));
            t.Cell().Element(Celda).AlignRight().Text(pago is null ? string.Empty : Monto(pago.MontoCheque));
        }
    }

    private static void Columnas(TableDescriptor t) => t.ColumnsDefinition(c =>
    {
        foreach (var ancho in Anchos) c.RelativeColumn(ancho);
    });

    private static void CabeceraTabla(IContainer c) => c.CornerRadius(9).Background(Colors.Grey.Lighten2).Border(0.65f).Table(t =>
    {
        Columnas(t);
        t.Cell().Row(1).Column(1).ColumnSpan(2).Element(Titulo).Text("COMPROMISOS");
        t.Cell().Row(1).Column(3).RowSpan(2).Element(Titulo).Text("C.I. / R.I.F. / PROVEEDOR / MOTIVO / PARTIDA(S)");
        t.Cell().Row(1).Column(4).RowSpan(2).Element(Titulo).Text("MONTO");
        t.Cell().Row(1).Column(5).ColumnSpan(3).Element(Titulo).Text("ORDEN DE PAGO");
        t.Cell().Row(1).Column(8).ColumnSpan(3).Element(Titulo).Text("CHEQUE");
        foreach (var (columna, texto) in new (uint, string)[] { (1, "N\u00daMERO"), (2, "FECHA"), (5, "N\u00daMERO"), (6, "FECHA"), (7, "MONTO"), (8, "N\u00daMERO"), (9, "FECHA"), (10, "MONTO") })
            t.Cell().Row(2).Column(columna).Element(Titulo).Text(texto);
    });

    private static IContainer Titulo(IContainer c) => c.BorderRight(0.45f).BorderBottom(0.4f).MinHeight(11).PaddingHorizontal(1).AlignMiddle().AlignCenter().DefaultTextStyle(s => s.FontSize(6.5f).Bold());
    private static IContainer Celda(IContainer c) => c.PaddingHorizontal(2).PaddingVertical(1);
}
