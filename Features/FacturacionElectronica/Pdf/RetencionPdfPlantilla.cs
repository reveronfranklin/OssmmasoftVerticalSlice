using OssmmasoftVerticalSlice.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// La cabecera del comprobante, tal como se imprime. Art. 11.
public record RetencionImpresionCabecera(
    long Id,
    string Numeracion,
    string Periodo,
    string PeriodoFormato,
    string FechaEmision8d,
    string HoraEmision,
    string FechaEntrega8d,
    string AgenteRif,
    string AgenteRazonSocial,
    string AgenteDomicilio,
    string ProveedorRif,
    string ProveedorRazonSocial,
    string ProveedorDomicilio,
    string ProveedorCorreo,
    decimal TotalDocumentos,
    decimal TotalBase,
    decimal TotalImpuesto,
    decimal TotalRetenido,
    string ImprentaRif,
    string ImprentaRazonSocial,
    string ImprentaProvidencia,
    bool EsPrueba);

// Una factura retenida. Numerales 11.5, 11.6 y 11.8.
public record RetencionImpresionDetalle(
    int Orden,
    string DocumentoNumero,
    string DocumentoControl,
    string DocumentoFecha8d,
    decimal MontoTotal,
    decimal BaseImponible,
    decimal ImpuestoCausado,
    decimal MontoRetenido,
    decimal Porcentaje);

public record RetencionImpresion(RetencionImpresionCabecera Cabecera, List<RetencionImpresionDetalle> Detalle);

// T7.1 - representacion grafica del comprobante de retencion. Art. 11.
//
// PLANTILLA PROPIA Y NO UN CASO MAS DE DocumentoPdfPlantilla, por la misma razon
// por la que tiene tabla propia (D-37): no es un FED_DOCUMENTO. No tiene
// denominacion del Art. 7.1, no tiene numero de control, no tiene adquiriente
// sino proveedor, y su numeracion es el formato de catorce caracteres del 11.1
// en vez de la del 7.2. De los quince numerales del Art. 7 no comparte ninguno:
// comparte el Art. 31, que es tipografia, y el Art. 12, que es la leyenda.
//
// Meterlo en la otra plantilla habria significado nueve condicionales sobre un
// documento que no comparte campos.
public static class RetencionPdfPlantilla
{
    private const float PuntosImprenta = 6.5f;
    private const float PuntosAgente = 9f;
    private const float PuntosNumeracion = 11f;
    private const float PuntosCuerpo = 8.5f;
    private const float PuntosDenominacion = 15f;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-VE");

    public const string SqlRetencionParaImprimir = @"
        SELECT r.ID, r.NUMERACION, r.PERIODO, r.EMITIDO_EN, r.ENTREGADO_EN,
               r.AGENTE_RIF, r.AGENTE_RAZON_SOCIAL, COALESCE(r.AGENTE_DOMICILIO, '') AS AGENTE_DOMICILIO,
               r.PROVEEDOR_RIF, r.PROVEEDOR_RAZON_SOCIAL,
               COALESCE(r.PROVEEDOR_DOMICILIO, '') AS PROVEEDOR_DOMICILIO,
               COALESCE(r.PROVEEDOR_CORREO, '')    AS PROVEEDOR_CORREO,
               r.TOTAL_DOCUMENTOS, r.TOTAL_BASE, r.TOTAL_IMPUESTO, r.TOTAL_RETENIDO,
               COALESCE(r.IMPRENTA_RIF, '')          AS IMPRENTA_RIF,
               COALESCE(r.IMPRENTA_RAZON_SOCIAL, '') AS IMPRENTA_RAZON_SOCIAL,
               COALESCE(r.IMPRENTA_PROVIDENCIA, '')  AS IMPRENTA_PROVIDENCIA,
               r.ES_PRUEBA
        FROM FED.FED_RETENCION r
        WHERE r.ID = @retencion_id;";

    public const string SqlDetalleParaImprimir = @"
        SELECT ORDEN, DOCUMENTO_NUMERO, DOCUMENTO_CONTROL, DOCUMENTO_FECHA,
               MONTO_TOTAL, BASE_IMPONIBLE, IMPUESTO_CAUSADO, MONTO_RETENIDO, PORCENTAJE
        FROM FED.FED_RETENCION_DETALLE
        WHERE RETENCION_ID = @retencion_id
        ORDER BY ORDEN;";

    public static RetencionImpresionCabecera MapCabecera(IDataReader reader)
    {
        DateTime emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));
        int ordinalEntrega = reader.GetOrdinal("entregado_en");

        // 11.3 pide fecha de emision Y DE ENTREGA. La de entrega no existe cuando
        // el documento se emite; hasta que se registre, el papel lo dice en vez de
        // inventar una fecha.
        string entrega = reader.IsDBNull(ordinalEntrega)
            ? string.Empty
            : FacturaFormato.FechaOchoDigitos(reader.GetDateTime(ordinalEntrega));

        string periodo = reader.SafeGetString("periodo");

        return new RetencionImpresionCabecera(
            reader.SafeGetInt64("id"),
            reader.SafeGetString("numeracion"),
            periodo,
            RetencionDb.FormatearPeriodo(periodo),
            FacturaFormato.FechaOchoDigitos(emitidoEn),
            FacturaFormato.HoraConMeridiano(emitidoEn),
            entrega,
            reader.SafeGetString("agente_rif"),
            reader.SafeGetString("agente_razon_social"),
            reader.SafeGetString("agente_domicilio"),
            reader.SafeGetString("proveedor_rif"),
            reader.SafeGetString("proveedor_razon_social"),
            reader.SafeGetString("proveedor_domicilio"),
            reader.SafeGetString("proveedor_correo"),
            reader.SafeGetDecimal("total_documentos"),
            reader.SafeGetDecimal("total_base"),
            reader.SafeGetDecimal("total_impuesto"),
            reader.SafeGetDecimal("total_retenido"),
            reader.SafeGetString("imprenta_rif"),
            reader.SafeGetString("imprenta_razon_social"),
            reader.SafeGetString("imprenta_providencia"),
            reader.SafeGetBoolean("es_prueba"));
    }

    public static RetencionImpresionDetalle MapDetalle(IDataReader reader) => new(
        reader.SafeGetInt32("orden"),
        reader.SafeGetString("documento_numero"),
        reader.SafeGetString("documento_control"),
        FacturaFormato.FechaOchoDigitos(reader.GetDateTime(reader.GetOrdinal("documento_fecha"))),
        reader.SafeGetDecimal("monto_total"),
        reader.SafeGetDecimal("base_imponible"),
        reader.SafeGetDecimal("impuesto_causado"),
        reader.SafeGetDecimal("monto_retenido"),
        reader.SafeGetDecimal("porcentaje"));

    public static byte[] Generar(RetencionImpresion comprobante, byte[]? logoImprenta = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var c = comprobante.Cabecera;

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
                    columna.Item().Element(e => Partes(e, c));
                    columna.Item().PaddingTop(10).Element(e => Detalle(e, comprobante.Detalle));
                    columna.Item().PaddingTop(10).Element(e => Totales(e, c));
                    columna.Item().PaddingTop(12).Element(e => Leyendas(e, c));
                });

                page.Footer().Element(e => PieImprenta(e, c, logoImprenta));
            });
        }).GeneratePdf();
    }

    // 11.1, 11.3 y 11.7.
    private static void Encabezado(IContainer container, RetencionImpresionCabecera c)
    {
        container.Row(fila =>
        {
            fila.RelativeItem().Column(columna =>
            {
                columna.Item().Text(c.AgenteRazonSocial).FontSize(PuntosAgente).Bold();
                columna.Item().Text($"RIF: {c.AgenteRif}").FontSize(PuntosAgente);

                if (c.AgenteDomicilio.Length > 0)
                {
                    columna.Item().Text(c.AgenteDomicilio).FontSize(PuntosAgente);
                }
            });

            fila.ConstantItem(230).Column(columna =>
            {
                columna.Item().AlignRight()
                    .Text("COMPROBANTE DE RETENCIÓN DE IVA").FontSize(PuntosDenominacion).Bold();

                // 11.1 - catorce caracteres, AAAAMMSSSSSSSS. Es la UNICA
                // identificacion del documento: no hay numero de control.
                columna.Item().PaddingTop(3).AlignRight()
                    .Text($"N° {c.Numeracion}").FontSize(PuntosNumeracion).Bold();

                // 11.7 - periodo de imposicion.
                columna.Item().AlignRight()
                    .Text($"Período de imposición: {c.PeriodoFormato}").FontSize(PuntosAgente);

                // 11.3 - fecha de emision Y de entrega.
                columna.Item().PaddingTop(3).AlignRight()
                    .Text($"Emisión: {c.FechaEmision8d}  {c.HoraEmision}").FontSize(PuntosAgente);

                columna.Item().AlignRight().Text(c.FechaEntrega8d.Length > 0
                    ? $"Entrega: {c.FechaEntrega8d}"
                    : "Entrega: pendiente de registro (Art. 11.3)").FontSize(PuntosAgente);
            });
        });
    }

    // 11.2 y 11.4. El correo del proveedor se imprime porque el numeral lo nombra
    // expresamente junto al nombre, el RIF y el domicilio.
    private static void Partes(IContainer container, RetencionImpresionCabecera c)
    {
        container.Row(fila =>
        {
            fila.RelativeItem().Border(0.5f).Padding(6).Column(columna =>
            {
                columna.Item().Text("AGENTE DE RETENCIÓN · Art. 11.2").FontSize(PuntosCuerpo).Bold();
                columna.Item().Text(c.AgenteRazonSocial).FontSize(PuntosCuerpo);
                columna.Item().Text($"RIF: {c.AgenteRif}").FontSize(PuntosCuerpo);

                if (c.AgenteDomicilio.Length > 0)
                {
                    columna.Item().Text(c.AgenteDomicilio).FontSize(PuntosCuerpo);
                }
            });

            fila.ConstantItem(10);

            fila.RelativeItem().Border(0.5f).Padding(6).Column(columna =>
            {
                columna.Item().Text("PROVEEDOR · Art. 11.4").FontSize(PuntosCuerpo).Bold();
                columna.Item().Text(c.ProveedorRazonSocial).FontSize(PuntosCuerpo);
                columna.Item().Text($"RIF: {c.ProveedorRif}").FontSize(PuntosCuerpo);

                if (c.ProveedorDomicilio.Length > 0)
                {
                    columna.Item().Text(c.ProveedorDomicilio).FontSize(PuntosCuerpo);
                }

                if (c.ProveedorCorreo.Length > 0)
                {
                    columna.Item().Text($"Correo: {c.ProveedorCorreo}").FontSize(PuntosCuerpo);
                }
            });
        });
    }

    // 11.5, 11.6 y 11.8, que son POR DOCUMENTO RETENIDO.
    private static void Detalle(IContainer container, List<RetencionImpresionDetalle> filas)
    {
        container.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(20);
                columnas.ConstantColumn(60);
                columnas.RelativeColumn(2);
                columnas.ConstantColumn(55);
                columnas.RelativeColumn(1);
                columnas.RelativeColumn(1);
                columnas.RelativeColumn(1);
                columnas.ConstantColumn(38);
                columnas.RelativeColumn(1);
            });

            tabla.Header(cabecera =>
            {
                Encabezado(cabecera, "#");
                Encabezado(cabecera, "Fecha");
                Encabezado(cabecera, "N° control · 11.5");
                Encabezado(cabecera, "N° doc · 11.6");
                Encabezado(cabecera, "Total · 11.8", true);
                Encabezado(cabecera, "Base · 11.8", true);
                Encabezado(cabecera, "Impuesto · 11.8", true);
                Encabezado(cabecera, "%", true);
                Encabezado(cabecera, "Retenido · 11.8", true);
            });

            foreach (var f in filas)
            {
                Celda(tabla, f.Orden.ToString());
                Celda(tabla, f.DocumentoFecha8d);
                Celda(tabla, f.DocumentoControl);
                Celda(tabla, f.DocumentoNumero);
                Celda(tabla, Numero(f.MontoTotal), true);
                Celda(tabla, Numero(f.BaseImponible), true);
                Celda(tabla, Numero(f.ImpuestoCausado), true);
                Celda(tabla, Porcentaje(f.Porcentaje), true);
                Celda(tabla, Numero(f.MontoRetenido), true);
            }
        });
    }

    // Los mismos cuatro montos del 11.8, agregados. Son la suma del desglose.
    private static void Totales(IContainer container, RetencionImpresionCabecera c)
    {
        container.AlignRight().Width(260).Column(columna =>
        {
            columna.Item().Element(e => Linea(e, "Total de los documentos", Numero(c.TotalDocumentos)));
            columna.Item().Element(e => Linea(e, "Base imponible", Numero(c.TotalBase)));
            columna.Item().Element(e => Linea(e, "Impuesto causado", Numero(c.TotalImpuesto)));
            columna.Item().PaddingTop(3).Element(e =>
                Linea(e, "TOTAL RETENIDO · Art. 11.8", Numero(c.TotalRetenido), true));
        });
    }

    private static void Leyendas(IContainer container, RetencionImpresionCabecera c)
    {
        container.Column(columna =>
        {
            columna.Item().Text(FacturaFormato.LeyendaProvidencia).FontSize(PuntosCuerpo).Italic();

            // Lo que sorprende a quien viene de una factura, dicho en el papel.
            columna.Item().PaddingTop(4)
                .Text("Este documento no lleva número de control: el Artículo 11 no remite a los numerales 4 "
                      + "y 5 del Artículo 7. El número de control que aparece en el desglose es el de la "
                      + "factura retenida (Art. 11.5).")
                .FontSize(PuntosImprenta).Italic();

            if (c.EsPrueba)
            {
                columna.Item().PaddingTop(6).Border(1).Padding(4)
                    .Text("DOCUMENTO DE PRUEBA · SIN VALIDEZ FISCAL. Falta la autorización del SENIAT (Art. 11.9).")
                    .FontSize(PuntosCuerpo).Bold();
            }
        });
    }

    // 11.9 - los datos de la imprenta digital autorizada. Art. 31: minimo 6 pt.
    private static void PieImprenta(IContainer container, RetencionImpresionCabecera c, byte[]? logo)
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
                    ? $"Imprenta digital (Art. 11.9): {c.ImprentaRazonSocial} · RIF {c.ImprentaRif}"
                    : "Imprenta digital (Art. 11.9): datos pendientes de la autorización del SENIAT";

                columna.Item().Text(imprenta).FontSize(PuntosImprenta);

                if (c.ImprentaProvidencia.Length > 0)
                {
                    columna.Item().Text($"Providencia de autorización: {c.ImprentaProvidencia}")
                        .FontSize(PuntosImprenta);
                }
            });
        });
    }

    private static void Encabezado(TableCellDescriptor fila, string texto, bool derecha = false)
    {
        var celda = fila.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingHorizontal(2);
        (derecha ? celda.AlignRight() : celda).Text(texto).FontSize(PuntosImprenta).Bold();
    }

    private static void Celda(TableDescriptor tabla, string texto, bool derecha = false)
    {
        var celda = tabla.Cell().BorderBottom(0.25f).PaddingVertical(2).PaddingHorizontal(2);
        (derecha ? celda.AlignRight() : celda).Text(texto).FontSize(PuntosCuerpo);
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

    // El porcentaje de retencion es 75 o 100 en la practica, y "75,00 %" no entra
    // en su columna: se partia en dos lineas. Se imprime sin decimales cuando es
    // entero, que ademas es como lo nombra la normativa del agente de retencion.
    private static string Porcentaje(decimal valor) =>
        valor == decimal.Truncate(valor)
            ? valor.ToString("0", Cultura) + "%"
            : valor.ToString("0.##", Cultura) + "%";
}
