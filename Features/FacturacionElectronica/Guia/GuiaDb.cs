using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Un renglon de la guia de despacho. Art. 10.4.
//
// NO TIENE PRECIO NI ALICUOTA, y esa ausencia es el articulo, no un olvido: el
// Art. 10.2 remite a los numerales 2, 3, 4, 5, 6 y 14 del Art. 7, y el precio
// vive en el 7.8, que no esta remitido. El 10.4 no completa al 7.8: lo reemplaza
// por "la descripcion de los bienes que se trasladan, senalando su capacidad,
// peso o volumen".
//
// LOS TRES CAMPOS DE MEDIDA LLEVAN VALOR POR DEFECTO AUNQUE EL 10.4 LOS EXIJA, y
// la razon es quien tiene que dar la mala noticia. Sin defecto, un renglon que
// llegue sin medida lo rechaza el enlazado del modelo con un error de framework,
// y quien integra recibe un RFC 9110 en vez de "el numeral exige senalar
// capacidad, peso o volumen". El requisito no se relaja: lo hace cumplir
// GuiaValidador, que es donde vive el Art. 29.4.
public record GuiaRenglonCommand(
    string Descripcion,
    decimal Cantidad,
    string MedidaTipo = "",
    decimal MedidaValor = 0,
    string MedidaUnidad = "",
    string Codigo = "");

// La solicitud de emision de una guia de despacho.
//
// Receptor y no adquiriente (10.5), y el nombre importa: el Art. 7.7 admite
// cedula o pasaporte cuando el adquiriente es persona natural que no requiere la
// factura, y el 10.5 NO tiene esa alternativa. Pide nombre y RIF. Se persiste en
// las columnas ADQ_* de FED_DOCUMENTO, que son el mismo lugar del documento con
// otro nombre legal (D-41).
public record GuiaEmitirCommand(
    long EmisorId,
    List<GuiaRenglonCommand> Renglones,
    string MotivoTraslado = "",
    string Destino = "",
    string ReceptorNombre = "",
    string ReceptorRif = "",
    string Serie = "",
    string NumeracionExterna = "",
    string UsuarioIns = "",
    string ClaveIdempotencia = "");

// Lo que devuelve la emision de una guia.
//
// Trae numeroControl -a diferencia del comprobante de retencion- porque el Art.
// 10.2 SI remite a los numerales 4 y 5 del Art. 7. Y no trae totales, porque no
// remite al 11, 12 ni 13.
public record GuiaEmitidaResponse(
    long DocumentoId,
    string Numeracion,
    string NumeracionConSerie,
    string NumeroControl,
    string NumeroControlTexto,
    string RangoNumerosControl,
    string Denominacion,
    string FechaEmision8d,
    string HoraEmision,
    string FechaAsignacion8d,
    string ReceptorNombre,
    string ReceptorRif,
    string MotivoTraslado,
    string Destino,
    int CantidadRenglones,

    // Art. 10.3. Va en la respuesta y no la arma la pantalla: es un literal que
    // fija la norma, igual que la denominacion.
    string LeyendaSinCreditoFiscal,
    string LeyendaProvidencia,
    bool EsPrueba,
    string MotivoPrueba,
    bool YaExistia);

// SQL del subdominio de guias de despacho. Fase 6.
public static class GuiaDb
{
    public const string SqlGuiaInsert = @"
        INSERT INTO FED.FED_GUIA_DESPACHO
            (DOCUMENTO_ID, MOTIVO_TRASLADO, DESTINO, USUARIO_INS)
        VALUES
            (@documento_id, @motivo_traslado, @destino, @usuario_ins);";

    // Los datos propios del Art. 10 de un documento ya emitido. Se lee por
    // separado y no por JOIN en el listado general porque solo un tipo de
    // documento los tiene.
    //
    // Trae tambien el receptor y la cuenta de renglones porque la respuesta de la
    // guia los lleva y la del documento generico no: FacturaEmitidaResponse esta
    // hecha para la factura, que informa totales y no receptor.
    public const string SqlGuiaPorDocumento = @"
        SELECT g.MOTIVO_TRASLADO, g.DESTINO,
               d.ADQ_NOMBRE, d.ADQ_RIF,
               (SELECT COUNT(*) FROM FED.FED_DOCUMENTO_DETALLE r WHERE r.DOCUMENTO_ID = d.ID)
                   AS CANTIDAD_RENGLONES
        FROM FED.FED_GUIA_DESPACHO g
        JOIN FED.FED_DOCUMENTO d ON d.ID = g.DOCUMENTO_ID
        WHERE g.DOCUMENTO_ID = @documento_id;";

    // NO HAY SqlGuiaGetAll, y es a proposito: la guia se lista con los otros
    // tres documentos en facturaGetAll, porque comparte con ellos la numeracion y
    // el numero de control. Un listado propio duplicaria esa consulta para
    // filtrar por un tipo que el listado general ya sabe filtrar.

    // Las tres unidades que el numeral nombra, y nada mas. Es el CHECK de la
    // tabla escrito en C# para poder rechazar con un mensaje que cite el numeral
    // en vez de dejar que reviente el motor.
    public static readonly string[] MedidasValidas = ["capacidad", "peso", "volumen"];

    // El renglon de una guia expresado como renglon de documento, para poder
    // reusar el motor de emision sin duplicarlo.
    //
    // Precio y alicuota van en CERO y no es una conversion perezosa: es lo que el
    // Art. 10.2 obliga al no remitir al 7.8 ni al 7.11. El validador ya rechazo
    // cualquier intento de traer montos, asi que aca no hay nada que perder.
    public static FacturaRenglonCommand ComoRenglon(GuiaRenglonCommand renglon) => new(
        renglon.Descripcion,
        renglon.Cantidad,
        Precio: 0,
        Alicuota: 0,
        Exento: false,
        Codigo: renglon.Codigo,
        MedidaTipo: renglon.MedidaTipo,
        MedidaValor: renglon.MedidaValor,
        MedidaUnidad: renglon.MedidaUnidad);

    public static string TextoMedida(GuiaRenglonCommand renglon) =>
        $"{renglon.MedidaTipo}: {renglon.MedidaValor:0.####} {renglon.MedidaUnidad}".Trim();

    public static GuiaDatos MapGuia(IDataReader reader) => new(
        reader.SafeGetString("motivo_traslado"),
        reader.SafeGetString("destino"),
        reader.SafeGetString("adq_nombre"),
        reader.SafeGetString("adq_rif"),
        (int)reader.SafeGetInt64("cantidad_renglones"));
}

public record GuiaDatos(
    string MotivoTraslado,
    string Destino,
    string ReceptorNombre,
    string ReceptorRif,
    int CantidadRenglones);
