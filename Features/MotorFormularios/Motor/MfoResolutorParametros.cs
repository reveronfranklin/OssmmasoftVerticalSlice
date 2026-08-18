using System.Globalization;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Un parametro ya resuelto y listo para bindear.
///
/// <c>Multiples</c> lleva todos los valores cuando el campo de origen es
/// multivalor. Un parametro simple tiene un solo elemento; uno multivalor, N.
/// Se expone como lista siempre para que el delegado de ejecucion no tenga que
/// preguntar cual de los dos casos le toco.
/// </summary>
public sealed record MfoParametroResuelto(
    string Nombre,
    string Origen,
    string TipoDato,
    IReadOnlyList<string> Multiples)
{
    public string? Texto => Multiples.Count > 0 ? Multiples[0] : null;

    public decimal? Numero =>
        Multiples.Count > 0
        && decimal.TryParse(Multiples[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d : null;

    public DateTime? Fecha =>
        Multiples.Count > 0 && MfoResolutorParametros.TryParseFecha(Multiples[0], out var f)
            ? f : null;

    public List<int> Enteros =>
        Multiples
            .Select(v => int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var i)
                ? (int?)i : null)
            .Where(i => i.HasValue)
            .Select(i => i!.Value)
            .ToList();
}

/// <summary>
/// Convierte los valores de una respuesta en los parametros que el reporte
/// espera, siguiendo el mapeo explicito de <c>MFO_REP_PARAM</c>.
///
/// Tres origenes y una regla que los atraviesa: **el cliente solo puede influir
/// en los de origen CAMPO.**
///
///   CAMPO   - el valor sale del payload, emparejado por CLAVE_CAMPO. Se empareja
///             por clave y no por CAMPO_ID porque los ids pertenecen a una
///             version concreta y cambian al publicar una nueva; la clave la
///             preserva el clonado.
///   FIJO    - VALOR_FIJO de la configuracion. El payload no lo puede tocar.
///   SISTEMA - lo resuelve el servidor: CODIGO_EMPRESA desde
///             settings:EmpresaConfig, USUARIO desde la cabecera autenticada,
///             FECHA_ACTUAL y IP_ORIGEN del contexto de la peticion.
///             **Cualquier valor que venga en el payload para uno de estos se
///             descarta sin mirarlo.** Es la segunda prueba de seguridad
///             obligatoria de la Fase 9: sin esto, un cliente manipulado pediria
///             el reporte de otra empresa.
/// </summary>
public static class MfoResolutorParametros
{
    public sealed record Resultado(
        Dictionary<string, MfoParametroResuelto> Parametros,
        List<MfoErrorValidacion> Errores)
    {
        public bool EsValido => Errores.Count == 0;

        /// <summary>
        /// Serializacion para <c>MFO_REP_EJEC.PARAMS_CLB</c>. Incluye los de
        /// origen SISTEMA ya resueltos: es lo que hace auditable una ejecucion
        /// que no guarda respuesta, y lo que permite comprobar despues con que
        /// empresa corrio.
        /// </summary>
        public string ToJson()
        {
            var plano = Parametros.Values
                .OrderBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    p => p.Nombre,
                    p => (object?)(p.Multiples.Count switch
                    {
                        0 => null,
                        1 => p.Multiples[0],
                        _ => p.Multiples
                    }));

            return JsonSerializer.Serialize(plano);
        }
    }

    /// <summary>
    /// Contexto que solo el servidor puede proveer. Ningun campo sale del
    /// request del cliente.
    /// </summary>
    public sealed record ContextoSistema(
        int CodigoEmpresa,
        string? Usuario,
        string? IpOrigen,
        DateTime Ahora);

    public static Resultado Resolver(
        IReadOnlyList<MfoRepParamResponse> definicion,
        IReadOnlyList<MfoValorRequest> valores,
        ContextoSistema sistema)
    {
        var resueltos = new Dictionary<string, MfoParametroResuelto>(StringComparer.OrdinalIgnoreCase);
        var errores = new List<MfoErrorValidacion>();

        // Los valores del payload se agrupan por clave conservando ORDEN_VAL:
        // un multivalor tiene que llegar al reporte en el orden en que el
        // usuario lo eligio, no en el que vino el JSON.
        var porClave = valores
            .Where(v => !string.IsNullOrWhiteSpace(v.Clave))
            .GroupBy(v => v.Clave, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(v => v.Fila).ThenBy(v => v.Orden)
                      .Select(v => v.Valor)
                      .Where(v => !string.IsNullOrWhiteSpace(v))
                      .Select(v => v!.Trim())
                      .ToList(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var p in definicion.OrderBy(p => p.Orden))
        {
            var origen = (p.Origen ?? string.Empty).ToUpperInvariant();
            var tipoDato = string.IsNullOrWhiteSpace(p.TipoDato) ? "TEXTO" : p.TipoDato;
            List<string> crudos;

            switch (origen)
            {
                case "CAMPO":
                    crudos = porClave.TryGetValue(p.ClaveCampo ?? string.Empty, out var delPayload)
                        ? delPayload
                        : [];
                    break;

                case "FIJO":
                    crudos = string.IsNullOrWhiteSpace(p.ValorFijo) ? [] : [p.ValorFijo.Trim()];
                    break;

                case "SISTEMA":
                    crudos = ResolverSistema(p.ClaveSistema, sistema);
                    break;

                default:
                    errores.Add(new MfoErrorValidacion(
                        p.NombreParam, 0, 0, "ORIGEN_INVALIDO",
                        $"El parametro {p.NombreParam} tiene un origen no reconocido: {p.Origen}."));
                    continue;
            }

            // El valor por defecto solo cubre la ausencia, no un valor vacio
            // deliberado en un parametro opcional.
            if (crudos.Count == 0 && !string.IsNullOrWhiteSpace(p.ValorDefecto))
            {
                crudos = [p.ValorDefecto.Trim()];
            }

            if (crudos.Count == 0)
            {
                if (p.Obligatorio)
                {
                    // El error se ancla en la CLAVE del campo, no en el nombre
                    // del parametro: es lo que permite al frontend pintarlo junto
                    // al control que lo produjo. Un parametro FIJO o SISTEMA sin
                    // valor es un fallo de configuracion, y ahi si se nombra el
                    // parametro porque no hay campo al cual apuntar.
                    var ancla = origen == "CAMPO" && !string.IsNullOrWhiteSpace(p.ClaveCampo)
                        ? p.ClaveCampo
                        : p.NombreParam;

                    errores.Add(new MfoErrorValidacion(
                        ancla, 0, 0, "REQUERIDO",
                        origen == "CAMPO"
                            ? $"Indique {p.EtiquetaCampo ?? p.NombreParam}."
                            : $"El parametro {p.NombreParam} es obligatorio y no tiene valor configurado."));
                }

                resueltos[p.NombreParam] = new MfoParametroResuelto(p.NombreParam, origen, tipoDato, []);
                continue;
            }

            var convertidos = new List<string>(crudos.Count);
            var falloConversion = false;

            foreach (var crudo in crudos)
            {
                if (!TryConvertir(crudo, tipoDato, p.Formato, out var normalizado))
                {
                    falloConversion = true;

                    errores.Add(new MfoErrorValidacion(
                        origen == "CAMPO" && !string.IsNullOrWhiteSpace(p.ClaveCampo)
                            ? p.ClaveCampo : p.NombreParam,
                        0, 0, "TIPO",
                        $"El valor '{crudo}' no es un {tipoDato.ToLowerInvariant()} valido."));

                    break;
                }

                convertidos.Add(normalizado);
            }

            resueltos[p.NombreParam] = new MfoParametroResuelto(
                p.NombreParam, origen, tipoDato, falloConversion ? [] : convertidos);
        }

        return new Resultado(resueltos, errores);
    }

    /// <summary>
    /// Dominio cerrado, el mismo que impone CK_MFO_REP_PARAM_SIS. Una clave que
    /// no este aqui devuelve vacio y, si el parametro es obligatorio, falla como
    /// error de configuracion. Nunca cae de vuelta al payload.
    /// </summary>
    private static List<string> ResolverSistema(string? clave, ContextoSistema sistema)
    {
        return (clave ?? string.Empty).ToUpperInvariant() switch
        {
            "CODIGO_EMPRESA" => [sistema.CodigoEmpresa.ToString(CultureInfo.InvariantCulture)],
            "USUARIO" => string.IsNullOrWhiteSpace(sistema.Usuario) ? [] : [sistema.Usuario],
            "FECHA_ACTUAL" => [sistema.Ahora.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)],
            "IP_ORIGEN" => string.IsNullOrWhiteSpace(sistema.IpOrigen) ? [] : [sistema.IpOrigen],
            _ => []
        };
    }

    /// <summary>
    /// Normaliza al formato canonico de cada tipo. El delegado de ejecucion
    /// recibe texto ya validado y lo convierte al tipo .NET que necesite; la
    /// conversion se hace dos veces a proposito, porque aqui el objetivo es
    /// rechazar temprano con un mensaje util, no producir el valor final.
    /// </summary>
    private static bool TryConvertir(string crudo, string? tipoDato, string? formato, out string normalizado)
    {
        normalizado = crudo;

        switch ((tipoDato ?? "TEXTO").ToUpperInvariant())
        {
            case "NUMERO":
                if (!decimal.TryParse(crudo, NumberStyles.Any, CultureInfo.InvariantCulture, out var numero))
                {
                    return false;
                }

                normalizado = numero.ToString(CultureInfo.InvariantCulture);
                return true;

            case "FECHA":
                if (!TryParseFecha(crudo, out var fecha))
                {
                    return false;
                }

                normalizado = fecha.ToString(
                    string.IsNullOrWhiteSpace(formato) ? "yyyy-MM-dd" : "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
                return true;

            default:
                return true;
        }
    }

    internal static bool TryParseFecha(string? valor, out DateTime fecha)
    {
        return DateTime.TryParseExact(valor, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy"],
                   CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
            || DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }
}
