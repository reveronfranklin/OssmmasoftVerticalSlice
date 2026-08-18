using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.ReporteBm1;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Todo lo que un delegado de ejecucion necesita. Se le entrega ya resuelto:
/// el delegado no lee el request, no consulta MFO_REP_PARAM y no decide
/// permisos. Recibe parametros validados y devuelve un documento.
/// </summary>
public sealed record MfoContextoEjecucion(
    ConnectionDB Conexiones,
    IConfiguration Config,
    IWebHostEnvironment Ambiente,
    MfoReporteResponse Reporte,
    IReadOnlyDictionary<string, MfoParametroResuelto> Parametros,
    CancellationToken Cancelacion)
{
    public MfoParametroResuelto? Param(string nombre) =>
        Parametros.TryGetValue(nombre, out var p) ? p : null;
}

/// <summary>
/// Lo que produjo una ejecucion.
///
/// <c>Resultado</c> usa el dominio de <c>MFO_REP_EJEC.RESULTADO</c>:
/// OK / VACIO / TRUNCADO / ERROR. VACIO y TRUNCADO no son errores tecnicos y
/// merecen un mensaje propio al usuario -"no hubo datos con esos parametros" y
/// "se alcanzo el limite de N filas"-, porque un error generico ahi hace que la
/// gente reintente lo mismo esperando otro resultado.
/// </summary>
public sealed record MfoResultadoEjecucion(
    byte[]? Documento,
    string NombreArchivo,
    int Filas,
    string Resultado,
    string Mensaje)
{
    public static MfoResultadoEjecucion Vacio(string mensaje) =>
        new(null, string.Empty, 0, "VACIO", mensaje);

    public static MfoResultadoEjecucion Error(string mensaje) =>
        new(null, string.Empty, 0, "ERROR", mensaje);
}

/// <summary>
/// **La lista blanca de reportes ejecutables.** Es el unico punto del sistema
/// autorizado a decidir que se ejecuta.
///
/// El riesgo que esta clase existe para contener es el mas severo de todo el
/// requerimiento: un motor que ejecuta lo que dice una fila de base de datos es
/// ejecucion arbitraria configurable. <c>MFO_REPORTE.CLAVE_REGISTRO</c> no es
/// una ruta, ni un nombre de procedimiento, ni nada que se invoque: es **una
/// clave de busqueda en este diccionario**. Si no esta registrada, la ejecucion
/// falla con <c>IsValid = false</c> y no se invoca absolutamente nada.
///
/// De ahi salen las dos reglas que no se negocian:
///   1. Nunca construir el delegado a partir del texto que viene de la base
///      (nada de reflexion por nombre, nada de invocar un procedimiento cuyo
///      nombre venga de una fila).
///   2. Nunca hacer que una clave desconocida caiga en un camino "por defecto"
///      que intente adivinar. Desconocida = rechazada.
///
/// Agregar un reporte **requiere despliegue**. Es deliberado y conviene decirlo
/// en voz alta cuando se presente el modulo: lo que el motor elimina es el
/// trabajo repetido de construir la pantalla de parametros, no el control sobre
/// que codigo corre en el servidor.
/// </summary>
public static class MfoRegistroReportes
{
    private delegate Task<MfoResultadoEjecucion> Ejecutor(MfoContextoEjecucion contexto);

    private static readonly Dictionary<string, Ejecutor> Registro =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Decision 8 de la Fase 0: el primer caso va por ENDPOINT contra un
            // reporte que ya existe, para validar el mapeo de parametros de punta
            // a punta antes de construir el generador tabular generico.
            ["REPORTE_BM1_PDF"] = EjecutarReporteBm1Pdf
        };

    public static bool EstaRegistrado(string? clave)
    {
        return !string.IsNullOrWhiteSpace(clave) && Registro.ContainsKey(clave);
    }

    public static IEnumerable<string> ClavesRegistradas() => Registro.Keys.OrderBy(k => k);

    /// <summary>
    /// Punto unico de ejecucion. Comprueba la lista blanca antes de nada.
    /// </summary>
    public static async Task<MfoResultadoEjecucion> EjecutarAsync(MfoContextoEjecucion contexto)
    {
        var clave = contexto.Reporte.ClaveRegistro;

        if (!EstaRegistrado(clave))
        {
            // Mensaje deliberadamente concreto: este fallo casi siempre significa
            // "se configuro un reporte que nadie registro en el codigo", y quien
            // lo lea tiene que entender que falta un despliegue, no un permiso.
            return MfoResultadoEjecucion.Error(
                $"El reporte '{clave}' no esta registrado en el backend. " +
                "Habilitarlo requiere registrarlo en MfoRegistroReportes.cs y desplegar.");
        }

        return await Registro[clave](contexto);
    }

    // ------------------------------------------------------------------------
    // Ejecutores registrados
    // ------------------------------------------------------------------------

    /// <summary>
    /// Reporte BM1 en PDF. Reusa el handler y el generador que ya existen en
    /// <c>Features/ReporteBm1</c>: el modo reporte no reimplementa el reporte,
    /// solo sustituye el dialogo de parametros que hoy esta codificado a mano en
    /// la pantalla.
    ///
    /// <c>CodigoEmpresa</c> esta mapeado como parametro de origen SISTEMA y por
    /// eso aparece en PARAMS_CLB, pero este reporte no lo recibe por argumento:
    /// <c>ReporteBm1GetAllHandler</c> lo resuelve el mismo desde
    /// settings:EmpresaConfig. El mapeo se mantiene igual porque es lo que hace
    /// auditable con que empresa corrio, y porque demuestra el punto que importa:
    /// un CodigoEmpresa enviado por el cliente no llega a la consulta por ninguna
    /// via.
    /// </summary>
    private static async Task<MfoResultadoEjecucion> EjecutarReporteBm1Pdf(MfoContextoEjecucion contexto)
    {
        var desde = contexto.Param("FechaDesde")?.Fecha;
        var hasta = contexto.Param("FechaHasta")?.Fecha;
        var icps = contexto.Param("CodigosIcp")?.Enteros ?? [];

        var query = new ReporteBm1GetAllQuery(desde, hasta, icps.Count > 0 ? icps : null);

        var handler = new ReporteBm1GetAllHandler(contexto.Conexiones, contexto.Config);
        var datos = await handler.HandleAsync(query);

        if (!datos.IsValid || datos.Data is null)
        {
            return MfoResultadoEjecucion.Error(datos.Message);
        }

        if (datos.Data.Count == 0)
        {
            return MfoResultadoEjecucion.Vacio(
                "El reporte no devolvio datos con esos parametros.");
        }

        var items = datos.Data;
        var resultado = "OK";
        var mensaje = string.Empty;

        // MAX_FILAS es un corte de seguridad, no un filtro: el usuario tiene que
        // enterarse de que esta viendo un resultado incompleto, porque si no
        // tomara decisiones sobre datos truncados creyendolos completos.
        if (contexto.Reporte.MaxFilas is int max && max > 0 && items.Count > max)
        {
            items = items.Take(max).ToList();
            resultado = "TRUNCADO";
            mensaje = $"Se alcanzo el limite de {max} filas. Refine los parametros.";
        }

        var bytes = ReporteBm1PdfGenerator.Generate(items, query, contexto.Ambiente);

        return new MfoResultadoEjecucion(
            bytes,
            $"ReporteBm1_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            items.Count,
            resultado,
            mensaje);
    }
}
