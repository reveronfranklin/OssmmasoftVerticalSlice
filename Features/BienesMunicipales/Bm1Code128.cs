namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

/// <summary>
/// Codificador Code 128 (subconjunto B) para las etiquetas de placas de Bienes Municipales.
/// Se implementa aqui en vez de agregar un paquete NuGet de codigos de barras (decision D4 del
/// requerimiento 18).
/// </summary>
/// <remarks>
/// Solo se implementa el subconjunto B. El valor a codificar es la placa compuesta
/// (CODIGO_GRUPO-CODIGO_NIVEL1-CODIGO_NIVEL2-consecutivo), formada solo por digitos y guiones, y en
/// ese patron el cambio a subconjunto C no reduce el ancho: las corridas de digitos son cortas y el
/// simbolo de cambio cuesta lo que se ahorraria. El subconjunto elegido no altera el texto que
/// devuelve el lector, solo el ancho del simbolo.
/// </remarks>
public static class Bm1Code128
{
    private const int StartCodeB = 104;
    private const int StopCode = 106;
    private const int ChecksumModulo = 103;

    /// <summary>Anchos de los 6 elementos (barra/espacio alternados) de cada simbolo 0..105, y 7 para el stop.</summary>
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    /// <summary>
    /// Devuelve true si todos los caracteres del valor son representables en el subconjunto B
    /// (ASCII 32 a 126).
    /// </summary>
    public static bool CanEncode(string? value)
    {
        return !string.IsNullOrEmpty(value) && value.All(character => character >= 32 && character <= 126);
    }

    /// <summary>
    /// Anchos de los elementos del simbolo completo, en modulos. El primer elemento es una barra y a
    /// partir de alli alternan espacio y barra.
    /// </summary>
    /// <exception cref="ArgumentException">El valor esta vacio o tiene caracteres fuera del subconjunto B.</exception>
    public static int[] GetElementWidths(string value)
    {
        if (!CanEncode(value))
        {
            throw new ArgumentException("El valor no es representable en Code 128 subconjunto B.", nameof(value));
        }

        var symbols = new List<int>(value.Length + 4) { StartCodeB };
        var checksum = StartCodeB;

        for (var index = 0; index < value.Length; index++)
        {
            var symbol = value[index] - 32;
            symbols.Add(symbol);
            checksum += symbol * (index + 1);
        }

        symbols.Add(checksum % ChecksumModulo);
        symbols.Add(StopCode);

        return symbols
            .SelectMany(symbol => Patterns[symbol].Select(width => width - '0'))
            .ToArray();
    }

    /// <summary>
    /// Representacion en modulos del simbolo: '1' es barra y '0' es espacio. Existe para poder
    /// contrastar el codificador contra vectores conocidos.
    /// </summary>
    public static string GetModules(string value)
    {
        var widths = GetElementWidths(value);
        var builder = new System.Text.StringBuilder(widths.Sum());

        for (var index = 0; index < widths.Length; index++)
        {
            builder.Append(index % 2 == 0 ? '1' : '0', widths[index]);
        }

        return builder.ToString();
    }
}
