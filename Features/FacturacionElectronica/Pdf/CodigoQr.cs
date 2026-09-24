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
    // Lado del QR en el papel, en puntos: 64 pt = 2,26 cm. La pauta habitual
    // para un QR impreso es de 2 cm para arriba. A 52 pt (1,83 cm) la lectura
    // quedo en el limite: la verificacion de TM.8 lo leyo a 150 y 300 dpi pero
    // no a 200, que es lo que le pasa a una camara segun la distancia.
    public const float LadoPuntos = 64f;

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
