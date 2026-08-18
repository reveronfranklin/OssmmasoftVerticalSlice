using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Convierte el payload del cliente en los arreglos que espera
/// <c>SP_MFO_RESP_VAL_SAVE</c>, decidiendo por cada valor a que columna tipada
/// va segun <c>COLUMNA_VALOR</c> del tipo del campo.
///
/// El bindeo es de arreglos asociativos de PL/SQL, no una cadena delimitada: los
/// valores son texto escrito por el usuario y cualquier delimitador puede
/// aparecer dentro del dato. Ver 14_MFO_PKG_ARRAYS.sql.
/// </summary>
public static class MfoMapeoValores
{
    /// <summary>
    /// Los valores de tipo CLB mas largos que esto no caben en un elemento del
    /// arreglo (VARCHAR2(4000)) y viajan por el parametro CLOB dedicado.
    /// </summary>
    public const int MaxElementoArreglo = 4000;

    public sealed record Lote(
        List<MfoValorRequest> EnArreglos,
        List<MfoValorRequest> EnClob);

    /// <summary>
    /// Separa los valores que caben en los arreglos de los que necesitan el
    /// parametro CLOB. Estos ultimos se envian de uno en uno.
    /// </summary>
    public static Lote Separar(MfoDefinicionResponse definicion, IReadOnlyList<MfoValorRequest> valores)
    {
        var columnas = definicion.Secciones
            .SelectMany(s => s.Campos)
            .ToDictionary(c => c.Campo.Clave, c => c.Campo.ColumnaValor ?? "TXT",
                          StringComparer.OrdinalIgnoreCase);

        var enArreglos = new List<MfoValorRequest>();
        var enClob = new List<MfoValorRequest>();

        foreach (var v in valores)
        {
            var esClobLargo =
                columnas.TryGetValue(v.Clave, out var columna)
                && string.Equals(columna, "CLB", StringComparison.OrdinalIgnoreCase)
                && (v.Valor?.Length ?? 0) > MaxElementoArreglo;

            if (esClobLargo)
            {
                enClob.Add(v);
            }
            else
            {
                enArreglos.Add(v);
            }
        }

        return new Lote(enArreglos, enClob);
    }

    /// <summary>
    /// Agrega los siete arreglos al comando, ya convertidos al tipo que
    /// corresponde a cada campo. Un valor que no se pueda convertir llega aqui
    /// como nulo: el validador ya lo rechazo antes, asi que esto es la segunda
    /// linea, no la primera.
    /// </summary>
    public static void AgregarArreglos(
        OracleCommand cmd,
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> valores)
    {
        var columnas = definicion.Secciones
            .SelectMany(s => s.Campos)
            .ToDictionary(c => c.Campo.Clave, c => c.Campo.ColumnaValor ?? "TXT",
                          StringComparer.OrdinalIgnoreCase);

        var n = Math.Max(valores.Count, 1);

        var claves = new string[n];
        var filas = new int[n];
        var ordenes = new int[n];
        var txts = new string[n];
        var nums = new decimal?[n];
        var fecs = new DateTime?[n];
        var etis = new string[n];

        for (var i = 0; i < valores.Count; i++)
        {
            var v = valores[i];
            var columna = columnas.TryGetValue(v.Clave, out var c) ? c.ToUpperInvariant() : "TXT";

            claves[i] = v.Clave;
            filas[i] = v.Fila;
            ordenes[i] = v.Orden;
            etis[i] = v.Etiqueta ?? string.Empty;

            switch (columna)
            {
                case "NUM":
                    nums[i] = decimal.TryParse(v.Valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                        ? d : null;
                    break;

                case "FEC":
                    fecs[i] = TryParseFecha(v.Valor, out var f) ? f : null;
                    break;

                default:
                    // TXT y CLB corto comparten el arreglo de texto: el
                    // procedimiento decide en cual columna lo escribe.
                    txts[i] = v.Valor ?? string.Empty;
                    break;
            }
        }

        Agregar(cmd, "p_Claves", OracleDbType.Varchar2, claves, n, 30);
        Agregar(cmd, "p_Filas", OracleDbType.Int32, filas, n);
        Agregar(cmd, "p_Ordenes", OracleDbType.Int32, ordenes, n);
        Agregar(cmd, "p_ValoresTxt", OracleDbType.Varchar2, txts, n, MaxElementoArreglo);
        Agregar(cmd, "p_ValoresNum", OracleDbType.Decimal, nums, n);
        Agregar(cmd, "p_ValoresFec", OracleDbType.Date, fecs, n);
        Agregar(cmd, "p_Etiquetas", OracleDbType.Varchar2, etis, n, 200);
    }

    private static void Agregar(
        OracleCommand cmd, string nombre, OracleDbType tipo, Array valores, int size, int? maxLen = null)
    {
        var p = cmd.Parameters.Add(nombre, tipo);
        p.CollectionType = OracleCollectionType.PLSQLAssociativeArray;
        p.Direction = ParameterDirection.Input;
        p.Size = size;
        p.Value = valores;

        if (maxLen is int len)
        {
            // ODP.NET necesita el ancho maximo declarado de cada elemento para
            // reservar el buffer del arreglo.
            p.ArrayBindSize = Enumerable.Repeat(len, size).ToArray();
        }
    }

    private static bool TryParseFecha(string? valor, out DateTime fecha)
    {
        return DateTime.TryParseExact(valor, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"],
                   CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
            || DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }
}
