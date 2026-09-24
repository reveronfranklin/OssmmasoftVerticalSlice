using QRCoder;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// TM.7, D-55. El QR del enlace publico de consulta (Art. 18.10), como SVG para
// que QuestPDF lo dibuje con .Svg(), igual que el Code128 de
// BienesMunicipales/Bm1PlacasPdf.cs.
//
// Se usa QRCoder y no un codificador propio: a diferencia de un Code128, un QR
// lleva correccion de errores Reed-Solomon y enmascarado, y equivocarse ahi
// produce un codigo que ningun telefono lee sin que nada lo avise.
public static class CodigoQr
{
    // Nivel M: tolera ~15 % de dano. Alcanza para papel impreso y deja el
    // codigo mas chico que Q o H con una URL de este largo.
    public static string Svg(string contenido)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);

        // ViewBox y no ancho/alto fijos: el tamano lo decide la plantilla.
        return new SvgQRCode(datos).GetGraphic(
            4, "#000000", "#FFFFFF", drawQuietZones: true, sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
