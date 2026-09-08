using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T7.1 y T7.2 - representacion grafica de los cuatro documentos que viven en
// FED_DOCUMENTO: factura, nota de debito, nota de credito y guia de despacho.
//
// EL ART. 31 MANDA SOBRE EL DISENO, y por eso los tamanos son constantes con
// nombre y no numeros sueltos repartidos por el archivo: los datos de la
// imprenta digital no pueden bajar de 6 puntos, y los del emisor y el numero de
// control no pueden bajar de 8. Un cambio de maquetado que achique una fuente
// no es un ajuste estetico: es un incumplimiento, y el Art. 34.1 lo convierte en
// causal de revocatoria de la autorizacion de la imprenta.
//
// Por eso ademas T7.2 se verifica MIDIENDO EL PDF con PdfPig, no leyendo este
// archivo. Que la constante diga 8 no prueba que la letra salga en 8.
//
// UN SOLO ARCHIVO PARA CUATRO DOCUMENTOS, y no cuatro plantillas. Comparten los
// numerales 1 a 6 y el 14 y 15 del Art. 7, que son la mitad del papel. Lo que
// cambia son bloques: la nota agrega la referencia del Art. 23, la guia quita
// los montos y agrega la medida del 10.4. Cuatro plantillas serian cuatro
// lugares donde arreglar el mismo numeral.
//
// El comprobante de retencion NO esta aca: no es un FED_DOCUMENTO, no lleva
// numero de control y sus numerales son los del Art. 11. Tiene su propia
// plantilla, por la misma razon por la que tiene su propia tabla (D-37).
public static class DocumentoPdfPlantilla
{
    // Art. 31 - los minimos de la norma. NO BAJAR DE ACA.
    public const float PuntosMinimosImprenta = 6f;
    public const float PuntosMinimosEmisor = 8f;

    // Lo que se usa realmente, con margen sobre el minimo. El margen es
    // deliberado: si un renderizador redondea hacia abajo, 8.0 exacto podria
    // medir 7.98 y quedar por debajo del minimo legal.
    private const float PuntosImprenta = 6.5f;
    private const float PuntosEmisor = 9f;
    private const float PuntosControl = 10f;
    private const float PuntosCuerpo = 8.5f;
    private const float PuntosDenominacion = 16f;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-VE");

    // El logo va como parametro y no lo lee la plantilla: dibujar no es leer
    // del disco. Nulo cuando el archivo no esta, y el papel sale igual de valido:
    // el Art. 7.14 pide razon social, RIF y providencia de la imprenta, no su
    // logo.
    public static byte[] Generar(DocumentoImpresion documento, byte[]? logoImprenta = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var c = documento.Cabecera;
        bool esGuia = c.TipoDocumento == "entrega";
        bool esNota = FacturacionElectronicaDb.TiposNota.Contains(c.TipoDocumento);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontSize(PuntosCuerpo));

                page.Header().Element(e => Encabezado(e, c));

                page.Content().PaddingTop(10).Column(columna =>
                {
                    columna.Item().Element(e => Adquiriente(e, c, esGuia));

                    if (esNota)
                    {
                        columna.Item().PaddingTop(8).Element(e => ReferenciaNota(e, c));
                    }

                    if (esGuia && c.GuiaMotivoTraslado.Length > 0)
                    {
                        columna.Item().PaddingTop(8).Element(e => MotivoTraslado(e, c));
                    }

                    columna.Item().PaddingTop(10).Element(e => Renglones(e, documento.Renglones, esGuia));

                    if (!esGuia)
                    {
                        columna.Item().PaddingTop(10).Element(e => Totales(e, c, documento.Impuestos));
                    }

                    columna.Item().PaddingTop(12).Element(e => Leyendas(e, c, esGuia));
                });

                page.Footer().Element(e => PieImprenta(e, c, logoImprenta));
            });
        }).GeneratePdf();
    }

    // Numerales 7.1, 7.2, 7.3, 7.4, 7.5 y 7.6. Los del emisor y el numero de
    // control van en PuntosEmisor y PuntosControl por el Art. 31.
    private static void Encabezado(IContainer container, DocumentoImpresionCabecera c)
    {
        container.Row(fila =>
        {
            fila.RelativeItem().Column(columna =>
            {
                // 7.3 - nombre, domicilio fiscal y RIF del emisor.
                columna.Item().Text(c.EmisorRazonSocial).FontSize(PuntosEmisor).Bold();
                columna.Item().Text($"RIF: {c.EmisorRif}").FontSize(PuntosEmisor);

                if (c.EmisorDomicilio.Length > 0)
                {
                    columna.Item().Text(c.EmisorDomicilio).FontSize(PuntosEmisor);
                }
            });

            fila.ConstantItem(210).Column(columna =>
            {
                // 7.1 - la denominacion, tal como la fija la norma.
                columna.Item().AlignRight().Text(c.Denominacion).FontSize(PuntosDenominacion).Bold();

                // 7.2 - numeracion consecutiva y unica.
                columna.Item().AlignRight().Text($"N° {c.NumeracionConSerie}").FontSize(PuntosEmisor).Bold();

                // 7.4 - el numero de control. Es lo que aporta la imprenta y por
                // eso el Art. 31 lo agrupa con los datos del emisor: 8 puntos.
                if (c.NumeroControl.Length > 0)
                {
                    columna.Item().PaddingTop(3).AlignRight()
                        .Text($"N° de Control {c.NumeroControl}").FontSize(PuntosControl).Bold();

                    // 7.5 - el total de numeros asignados, "desde ... hasta ...".
                    columna.Item().AlignRight().Text(c.RangoNumerosControl).FontSize(PuntosEmisor);
                }

                // 7.6 - fecha en ocho digitos y hora con a.m./p.m. Llega
                // formateada del backend: aca no se rearma.
                columna.Item().PaddingTop(3).AlignRight()
                    .Text($"Emisión: {c.FechaEmision8d}  {c.HoraEmision}").FontSize(PuntosEmisor);

                // 7.15 - fecha de asignacion, que es un dato distinto del 7.6.
                columna.Item().AlignRight()
                    .Text($"Asignación: {c.FechaAsignacion8d}").FontSize(PuntosEmisor);
            });
        });
    }

    // 7.7 para la factura y la nota; 10.5 para la guia, que pide RECEPTOR y no
    // admite cedula ni pasaporte en lugar del RIF.
    private static void Adquiriente(IContainer container, DocumentoImpresionCabecera c, bool esGuia)
    {
        container.Border(0.5f).Padding(6).Column(columna =>
        {
            columna.Item().Text(esGuia ? "RECEPTOR · Art. 10.5" : "ADQUIRIENTE · Art. 7.7")
                .FontSize(PuntosCuerpo).Bold();

            columna.Item().Text(c.AdqNombre).FontSize(PuntosCuerpo);

            if (c.AdqRif.Length > 0)
            {
                columna.Item().Text($"RIF: {c.AdqRif}").FontSize(PuntosCuerpo);
            }

            // La cedula o pasaporte solo aparece donde el 7.7 la admite. En una
            // guia no se imprime aunque viniera: el 10.5 no la contempla.
            if (!esGuia && c.AdqDocumentoId.Length > 0)
            {
                columna.Item().Text($"C.I./Pasaporte: {c.AdqDocumentoId}").FontSize(PuntosCuerpo);
            }
        });
    }

    // Art. 23 de la SNAT/2011/00071: la nota debe hacer referencia a la fecha,
    // numero y monto de la factura que soporto la operacion.
    private static void ReferenciaNota(IContainer container, DocumentoImpresionCabecera c)
    {
        container.Border(0.5f).Padding(6).Column(columna =>
        {
            columna.Item().Text("DOCUMENTO QUE SE CORRIGE · Art. 23").FontSize(PuntosCuerpo).Bold();

            columna.Item().Text(FacturaFormato.ReferenciaOriginal(
                c.NotaOrigenFecha8d, c.NotaOrigenNumeracion, c.NotaOrigenTotal,
                c.NotaOrigenMoneda.Length > 0 ? c.NotaOrigenMoneda : "VES")).FontSize(PuntosCuerpo);

            if (c.NotaMotivo.Length > 0)
            {
                columna.Item().Text($"Motivo: {c.NotaMotivo}").FontSize(PuntosCuerpo);
            }
        });
    }

    // No sale de un numeral: sale del encabezado del Art. 10, que solo admite
    // este documento para traslados que no representen ventas (D-43).
    private static void MotivoTraslado(IContainer container, DocumentoImpresionCabecera c)
    {
        container.Border(0.5f).Padding(6).Column(columna =>
        {
            columna.Item().Text("MOTIVO DEL TRASLADO").FontSize(PuntosCuerpo).Bold();
            columna.Item().Text(c.GuiaMotivoTraslado).FontSize(PuntosCuerpo);

            if (c.GuiaDestino.Length > 0)
            {
                columna.Item().Text($"Destino: {c.GuiaDestino}").FontSize(PuntosCuerpo);
            }
        });
    }

    // 7.8 para la factura y la nota: descripcion, cantidad, precio y la marca
    // (E) de lo exento. 10.4 para la guia: descripcion y medida, SIN precio.
    private static void Renglones(IContainer container, List<DocumentoImpresionRenglon> renglones, bool esGuia)
    {
        container.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(24);
                columnas.RelativeColumn(4);
                columnas.ConstantColumn(50);

                if (esGuia)
                {
                    columnas.RelativeColumn(2);
                }
                else
                {
                    columnas.ConstantColumn(60);
                    columnas.ConstantColumn(40);
                    columnas.ConstantColumn(70);
                }
            });

            tabla.Header(cabecera =>
            {
                Celda(cabecera, "#", true);
                Celda(cabecera, esGuia ? "Descripción del bien · 10.4" : "Descripción · 7.8", true);
                Celda(cabecera, "Cant.", true);

                if (esGuia)
                {
                    Celda(cabecera, "Capacidad, peso o volumen · 10.4", true);
                }
                else
                {
                    Celda(cabecera, "Precio", true, true);
                    Celda(cabecera, "Alíc.", true, true);
                    Celda(cabecera, "Total", true, true);
                }
            });

            foreach (var r in renglones)
            {
                // 7.8 - la marca (E) del exento va junto a la descripcion, no en
                // una columna aparte: asi lo pide el numeral.
                string descripcion = r.Exento && !esGuia ? $"{r.Descripcion} (E)" : r.Descripcion;

                if (r.Codigo.Length > 0)
                {
                    descripcion = $"[{r.Codigo}] {descripcion}";
                }

                Celda(tabla, r.Orden.ToString());
                Celda(tabla, descripcion);
                Celda(tabla, Numero(r.Cantidad));

                if (esGuia)
                {
                    Celda(tabla, r.MedidaTipo.Length > 0
                        ? $"{r.MedidaTipo}: {Numero(r.MedidaValor)} {r.MedidaUnidad}"
                        : string.Empty);
                }
                else
                {
                    Celda(tabla, Numero(r.Precio), false, true);
                    Celda(tabla, FacturaFormato.PorcentajeAlicuota(r.Alicuota), false, true);
                    Celda(tabla, Numero(r.TotalRenglon), false, true);
                }

                // 7.9 y 7.10 - bienes entregados en una prestacion de servicios, y
                // los ajustes al precio con su descripcion y su valor. Van como
                // linea secundaria del renglon para no inventarles columnas que la
                // mayoria de los documentos deja vacias.
                if (r.BienesEntregados.Length > 0)
                {
                    NotaDeRenglon(tabla, $"Bienes entregados (7.9): {r.BienesEntregados}", esGuia);
                }

                if (r.AjusteDescripcion.Length > 0 || r.AjusteValor != 0)
                {
                    NotaDeRenglon(tabla, $"Ajuste (7.10): {r.AjusteDescripcion} {Numero(r.AjusteValor)}", esGuia);
                }
            }
        });
    }

    // 7.11, 7.12 y 7.13 - base por alicuota, IVA por alicuota y valor total.
    private static void Totales(
        IContainer container, DocumentoImpresionCabecera c, List<DocumentoImpresionImpuesto> impuestos)
    {
        container.AlignRight().Width(250).Column(columna =>
        {
            foreach (var i in impuestos)
            {
                string tasa = FacturaFormato.PorcentajeAlicuota(i.Alicuota);
                columna.Item().Element(e => Linea(e, $"Base imponible {tasa} · 7.11", Numero(i.BaseImponible)));
                columna.Item().Element(e => Linea(e, $"IVA {tasa} · 7.12", Numero(i.MontoIva)));
            }

            // El exento se informa aparte y no como una alicuota de cero: es lo
            // que pide el 7.11 y lo que evita que parezca gravado al 0 %.
            if (c.TotalExento != 0)
            {
                columna.Item().Element(e => Linea(e, "Total exento · 7.11", Numero(c.TotalExento)));
            }

            columna.Item().PaddingTop(3).Element(e =>
                Linea(e, $"VALOR TOTAL · 7.13 ({c.Moneda})", Numero(c.TotalGeneral), true));

            // Art. 13.14 de la 00071: una operacion en moneda extranjera expresa
            // AMBOS montos y el tipo de cambio.
            if (c.Moneda.Length > 0 && c.Moneda != "VES")
            {
                columna.Item().Element(e => Linea(e, $"Tipo de cambio · 13.14", Numero(c.TasaCambio)));
                columna.Item().Element(e => Linea(e, $"Total en {c.Moneda} · 13.14", Numero(c.TotalMoneda)));
            }
        });
    }

    private static void Leyendas(IContainer container, DocumentoImpresionCabecera c, bool esGuia)
    {
        container.Column(columna =>
        {
            // Art. 12 - todos los documentos indican que se emiten conforme a la
            // Providencia. Sin excepcion (RF-B.4.1).
            columna.Item().Text(FacturaFormato.LeyendaProvidencia).FontSize(PuntosCuerpo).Italic();

            // Art. 10.3 - solo la guia, y es literal.
            if (esGuia)
            {
                columna.Item().PaddingTop(4)
                    .Text(FacturaFormato.LeyendaSinCreditoFiscal).FontSize(PuntosCuerpo).Bold();
            }

            // Mientras no haya providencia de autorizacion, el documento no tiene
            // validez fiscal y el papel tiene que decirlo. Entregarlo sin este
            // aviso seria el dano real.
            if (c.EsPrueba)
            {
                columna.Item().PaddingTop(6).Border(1).Padding(4)
                    .Text("DOCUMENTO DE PRUEBA · SIN VALIDEZ FISCAL. Falta la autorización del SENIAT (Art. 7.14).")
                    .FontSize(PuntosCuerpo).Bold();
            }
        });
    }

    // 7.14 - razon social y RIF de la imprenta digital, mas la nomenclatura y
    // fecha de su providencia. Art. 31: no menos de 6 puntos.
    //
    // EL LOGO DE LA IMPRENTA VA EN EL PIE Y NO EN EL ENCABEZADO, y no es una
    // decision de maquetado. El encabezado identifica al EMISOR -numerales 7.1,
    // 7.2 y 7.3-, y estampar ahi el logo de Ossmmasoft haria parecer que el
    // documento lo emite la imprenta. El Art. 7.14 le da a la imprenta su lugar,
    // que es este, y el Art. 31 le da su tamano, que es el mas chico del papel.
    private static void PieImprenta(IContainer container, DocumentoImpresionCabecera c, byte[]? logo)
    {
        container.BorderTop(0.5f).PaddingTop(4).Row(fila =>
        {
            if (logo is not null)
            {
                fila.ConstantItem(34).AlignMiddle().Height(14).Image(logo).FitArea();
                fila.ConstantItem(6);
            }

            fila.RelativeItem().Column(columna =>
            {
                string imprenta = c.ImprentaRazonSocial.Length > 0
                    ? $"Imprenta digital: {c.ImprentaRazonSocial} · RIF {c.ImprentaRif}"
                    : "Imprenta digital: datos pendientes de la autorización del SENIAT";

                columna.Item().Text(imprenta).FontSize(PuntosImprenta);

                if (c.ImprentaProvidencia.Length > 0)
                {
                    columna.Item().Text($"Providencia de autorización: {c.ImprentaProvidencia}")
                        .FontSize(PuntosImprenta);
                }
            });
        });
    }

    // ------------------------------------------------------------------
    // Piezas de maquetado
    // ------------------------------------------------------------------

    private static void Celda(TableDescriptor tabla, string texto, bool encabezado = false, bool derecha = false)
    {
        var celda = tabla.Cell().BorderBottom(0.25f).PaddingVertical(2).PaddingHorizontal(3);
        var alineada = derecha ? celda.AlignRight() : celda;
        var t = alineada.Text(texto).FontSize(PuntosCuerpo);

        if (encabezado)
        {
            t.Bold();
        }
    }

    private static void Celda(TableCellDescriptor fila, string texto, bool encabezado = false, bool derecha = false)
    {
        var celda = fila.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingHorizontal(3);
        var alineada = derecha ? celda.AlignRight() : celda;
        var t = alineada.Text(texto).FontSize(PuntosCuerpo);

        if (encabezado)
        {
            t.Bold();
        }
    }

    private static void NotaDeRenglon(TableDescriptor tabla, string texto, bool esGuia)
    {
        tabla.Cell();
        tabla.Cell().ColumnSpan(esGuia ? 3u : 5u).PaddingLeft(3).PaddingBottom(2)
            .Text(texto).FontSize(PuntosImprenta).Italic();
    }

    private static void Linea(IContainer container, string etiqueta, string valor, bool fuerte = false)
    {
        container.Row(fila =>
        {
            var izquierda = fila.RelativeItem().Text(etiqueta).FontSize(PuntosCuerpo);
            var derecha = fila.ConstantItem(90).AlignRight().Text(valor).FontSize(PuntosCuerpo);

            if (fuerte)
            {
                izquierda.Bold();
                derecha.Bold();
            }
        });
    }

    private static string Numero(decimal valor) => valor.ToString("N2", Cultura);
}
