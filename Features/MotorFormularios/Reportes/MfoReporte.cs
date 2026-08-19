using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Modo "parametros de reporte": el formulario deja de guardar una respuesta de
/// negocio y pasa a alimentar los parametros de un reporte existente.
///
/// El endpoint que importa es <c>ejecutar</c>. Su orden no es negociable, porque
/// cada paso protege al siguiente:
///
///   1. resolver el reporte y comprobar el permiso;
///   2. validar el payload con el mismo motor que valida una respuesta;
///   3. resolver los parametros -los de origen SISTEMA los pone el servidor-;
///   4. buscar la clave en la lista blanca; si no esta, no se invoca nada;
///   5. ejecutar acotado por TIMEOUT_SEG y MAX_FILAS;
///   6. registrar en MFO_REP_EJEC pase lo que pase;
///   7. devolver el PDF para el visor, sin descarga forzada.
///
/// Como en el resto del repositorio, el fallo esperado viaja en
/// <c>ResultDto.IsValid = false</c> y el exito devuelve el archivo. El frontend
/// distingue por el Content-Type, igual que ya hace con api/ReporteBm1/pdf.
/// </summary>
[ApiController]
[Route("api/MfoReporte")]
public class MfoReporteController(
    ConnectionDB connectionDB,
    IConfiguration config,
    IWebHostEnvironment ambiente,
    MfoDefinicionCache cache) : ControllerBase
{
    /// <summary>
    /// Tope cuando MFO_REPORTE.TIMEOUT_SEG viene nulo. Un reporte sin limite
    /// puede dejar una conexion colgada indefinidamente.
    /// </summary>
    private const int TimeoutPorDefecto = 120;

    private string? Usuario => Request.Headers["X-Usuario"].FirstOrDefault();

    private string? IpOrigen => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ------------------------------------------------------------------------
    // Consulta
    // ------------------------------------------------------------------------

    /// <summary>
    /// Reportes de un formulario con su mapeo de parametros y sus columnas.
    /// Una sola llamada: la pantalla necesita las tres cosas a la vez y pedirlas
    /// por separado seria tres oportunidades de que lleguen incoherentes.
    /// </summary>
    [HttpGet("getByFormulario")]
    public async Task<IActionResult> GetByFormulario(
        [FromQuery] int? formularioId,
        [FromQuery] string? alias,
        [FromQuery] bool soloActivos = true)
    {
        var detalle = await CargarDetalleAsync(formularioId, alias, null, soloActivos);
        if (!detalle.IsValid || detalle.Data is null)
        {
            return Ok(detalle);
        }

        // El permiso se comprueba sobre el formulario dueño, que sale de la
        // consulta: comprobarlo sobre el alias que envia el cliente permitiria
        // pedir los reportes de un formulario cerrado mandando el alias de uno
        // abierto.
        var primero = detalle.Data.Reportes.FirstOrDefault();
        if (primero is not null)
        {
            var permiso = await MfoAutorizacion.PuedeAsync(
                connectionDB, primero.FormularioId, MfoAutorizacion.Ver, Usuario);

            if (!permiso.Permitido)
            {
                return Ok(MfoDb.Invalid<MfoReporteDetalle>(permiso.Mensaje));
            }

            // El selector no debe ofrecer lo que el usuario no puede ejecutar:
            // dejarlo visible solo produce un error al pulsar Generar.
            var permitidos = new List<MfoReporteResponse>();

            foreach (var r in detalle.Data.Reportes)
            {
                var puede = await MfoAutorizacion.PuedeEjecutarReporteAsync(
                    connectionDB, r.FormularioId, r.ReporteId, Usuario);

                if (puede.Permitido) permitidos.Add(r);
            }

            if (permitidos.Count != detalle.Data.Reportes.Count)
            {
                var ids = permitidos.Select(r => r.ReporteId).ToHashSet();

                var filtrado = new MfoReporteDetalle(
                    permitidos,
                    detalle.Data.Parametros.Where(p => ids.Contains(p.ReporteId)).ToList(),
                    detalle.Data.Columnas.Where(c => ids.Contains(c.ReporteId)).ToList());

                return Ok(new ResultDto<MfoReporteDetalle>(filtrado)
                {
                    Data = filtrado,
                    IsValid = true,
                    Message = string.Empty,
                    CantidadRegistros = permitidos.Count
                });
            }
        }

        return Ok(detalle);
    }

    /// <summary>
    /// Claves de reporte habilitadas en el backend.
    ///
    /// El diseñador las ofrece en una lista y no como texto libre, por la misma
    /// razon que los catalogos: una clave escrita a mano no resolveria nunca, y
    /// el usuario no tendria forma de saber por que. Que la lista sea corta y
    /// venga del codigo es la parte visible de la restriccion, no un descuido:
    /// habilitar un reporte nuevo requiere despliegue.
    ///
    /// No expone nada sensible -son constantes del codigo- y por eso no exige
    /// permiso, igual que api/MfoCatalogo/catalogos.
    /// </summary>
    [HttpGet("registrados")]
    public IActionResult Registrados()
    {
        var claves = MfoRegistroReportes.ClavesRegistradas().ToList();

        return Ok(new ResultDto<List<string>>(claves)
        {
            Data = claves,
            IsValid = true,
            Message = string.Empty,
            CantidadRegistros = claves.Count
        });
    }

    // ------------------------------------------------------------------------
    // Ejecucion
    // ------------------------------------------------------------------------

    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar(MfoReporteEjecutarRequest request)
    {
        if (!MfoDb.TryGetEmpresa(config, out var empresa, out var errorEmpresa))
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(errorEmpresa));
        }

        var detalle = await CargarDetalleAsync(null, null, request.ReporteId, soloActivos: true);
        if (!detalle.IsValid || detalle.Data is null)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(detalle.Message));
        }

        var reporte = detalle.Data.Reportes.FirstOrDefault();
        if (reporte is null)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(
                "El reporte indicado no existe o esta inactivo."));
        }

        // Generar un reporte es extraer datos del formulario, asi que el permiso
        // que se exige es EXPORTAR y no LLENAR.
        //
        // Limite conocido y documentado: MFO_PERMISO es por formulario, no por
        // reporte, asi que quien pueda exportar un formulario puede ejecutar
        // todos sus reportes. Distinguir por reporte necesitaria una tabla de
        // permisos propia; queda anotado en el PLAN como deuda de esta fase.
        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, reporte.FormularioId, MfoAutorizacion.Exportar, Usuario);

        if (!permiso.Permitido)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(permiso.Mensaje));
        }

        // Segundo eje: poder exportar el formulario no implica poder ejecutar
        // todos sus reportes. Sin acotacion los hereda todos; con ella, solo los
        // asignados.
        var permisoReporte = await MfoAutorizacion.PuedeEjecutarReporteAsync(
            connectionDB, reporte.FormularioId, reporte.ReporteId, Usuario);

        if (!permisoReporte.Permitido)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(permisoReporte.Mensaje));
        }

        var valores = request.Valores ?? [];

        // Misma validacion que un envio: un formulario de parametros tiene
        // reglas y condiciones como cualquier otro, y saltarselas aqui abriria
        // por la puerta de atras justo lo que el motor cierra por la de delante.
        var definicion = await cache.ObtenerAsync(connectionDB, null, reporte.Alias);
        if (!definicion.IsValid || definicion.Data is null)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(definicion.Message));
        }

        var errores = MfoValidador.ValidarEnvio(definicion.Data, valores);
        if (errores.Count > 0)
        {
            return Ok(ErroresDeValidacion(errores));
        }

        var sistema = new MfoResolutorParametros.ContextoSistema(
            empresa, Usuario, IpOrigen, DateTime.Now);

        var resolucion = MfoResolutorParametros.Resolver(detalle.Data.Parametros, valores, sistema);
        if (!resolucion.EsValido)
        {
            return Ok(ErroresDeValidacion(resolucion.Errores));
        }

        var paramsJson = resolucion.ToJson();

        // Persistencia condicional. Con REGISTRA_EJEC='S' la ejecucion queda
        // ademas como MFO_RESPUESTA consultable desde la bandeja; con 'N' la
        // trazabilidad la da MFO_REP_EJEC.PARAMS_CLB, que se escribe igual.
        int? respuestaId = null;
        if (reporte.RegistraEjec)
        {
            var persistida = await PersistirRespuestaAsync(reporte, definicion.Data, valores);
            if (!persistida.IsValid)
            {
                return Ok(MfoDb.InvalidList<MfoErrorValidacion>(persistida.Message));
            }

            respuestaId = persistida.Data;
        }

        var timeout = reporte.TimeoutSeg is int t && t > 0 ? t : TimeoutPorDefecto;
        var cronometro = Stopwatch.StartNew();
        MfoResultadoEjecucion resultado;

        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout)))
        {
            var contexto = new MfoContextoEjecucion(
                connectionDB, config, ambiente, reporte, resolucion.Parametros, cts.Token);

            try
            {
                // WaitAsync acota lo que espera la peticion, no lo que tarda la
                // consulta en Oracle: el comando sigue corriendo del lado del
                // servidor hasta que termine. Es una mitigacion parcial y honesta
                // -evita dejar al usuario colgado- pero no sustituye a un
                // OracleCommand.CommandTimeout dentro de cada reporte registrado.
                resultado = await MfoRegistroReportes.EjecutarAsync(contexto).WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                resultado = MfoResultadoEjecucion.Error(
                    $"El reporte supero el tiempo maximo de {timeout} segundos. Refine los parametros.");
            }
            catch (Exception ex)
            {
                resultado = MfoResultadoEjecucion.Error($"Error tecnico al generar el reporte: {ex.Message}");
            }
        }

        cronometro.Stop();

        // La bitacora se escribe siempre, tambien cuando fallo. Un reporte que
        // se degrada o que revienta por timeout es exactamente el caso que hay
        // que poder ver despues.
        await RegistrarEjecucionAsync(
            reporte.ReporteId, respuestaId, empresa, (int)cronometro.ElapsedMilliseconds,
            resultado.Filas, resultado.Resultado, resultado.Mensaje, paramsJson);

        if (resultado.Documento is null || resultado.Documento.Length == 0)
        {
            return Ok(new ResultDto<List<MfoErrorValidacion>>([])
            {
                Data = [],
                IsValid = false,
                Message = string.IsNullOrWhiteSpace(resultado.Mensaje)
                    ? "El reporte no produjo ningun documento."
                    : resultado.Mensaje
            });
        }

        // TRUNCADO devuelve el PDF igual: el usuario pidio un reporte y lo
        // recibe, pero tiene que saber que esta incompleto. Va en cabeceras para
        // no romper el flujo del visor.
        Response.Headers.Append("X-Mfo-Resultado", resultado.Resultado);
        Response.Headers.Append("X-Mfo-Filas", resultado.Filas.ToString());

        if (!string.IsNullOrWhiteSpace(resultado.Mensaje))
        {
            Response.Headers.Append("X-Mfo-Mensaje", resultado.Mensaje);
        }

        var nombre = string.IsNullOrWhiteSpace(resultado.NombreArchivo)
            ? "reporte.pdf"
            : resultado.NombreArchivo;

        // inline, no attachment: el repositorio muestra los PDF en el visor
        // existente y no fuerza descargas.
        Response.Headers.ContentDisposition = $"inline; filename=\"{nombre}\"";

        return File(resultado.Documento, "application/pdf", enableRangeProcessing: true);
    }

    // ------------------------------------------------------------------------
    // Configuracion (consumida por la pestaña Reportes del diseñador, Fase 9.4)
    // ------------------------------------------------------------------------

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert(MfoReporteUpsertRequest request)
    {
        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, request.FormularioId, MfoAutorizacion.Disenar, Usuario);

        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_UPSERT", cn);
        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = MfoDb.DbValue(request.ReporteId);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = request.FormularioId;
        cmd.Parameters.Add("p_Clave", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Clave);
        cmd.Parameters.Add("p_Nombre", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Nombre);
        cmd.Parameters.Add("p_Descripcion", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Descripcion);
        cmd.Parameters.Add("p_TipoEjec", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.TipoEjec);
        cmd.Parameters.Add("p_ClaveRegistro", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveRegistro);
        cmd.Parameters.Add("p_TituloReporte", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.TituloReporte);
        cmd.Parameters.Add("p_Orientacion", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Orientacion);
        cmd.Parameters.Add("p_MaxFilas", OracleDbType.Int32).Value = MfoDb.DbValue(request.MaxFilas);
        cmd.Parameters.Add("p_TimeoutSeg", OracleDbType.Int32).Value = MfoDb.DbValue(request.TimeoutSeg);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_Activo", OracleDbType.Char).Value = MfoDb.DbFlag(request.Activo);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    /// <summary>
    /// Retira un reporte. Si tiene ejecuciones registradas se inactiva en vez de
    /// borrarse: <c>Data</c> devuelve 1 cuando hubo borrado fisico y 0 cuando
    /// solo se inactivo, para que la pantalla pueda decir cual de las dos cosas
    /// paso.
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(MfoIdRequest request)
    {
        var formularioId = await ResolverFormularioDeReporteAsync(request.Id);
        if (formularioId is null)
        {
            return Ok(MfoDb.Invalid<int>("El reporte indicado no existe."));
        }

        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, formularioId.Value, MfoAutorizacion.Disenar, Usuario);

        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_DELETE", cn);
        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Eliminado", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_Eliminado"));
    }

    [HttpPost("param/upsert")]
    public async Task<IActionResult> ParamUpsert(MfoRepParamUpsertRequest request)
    {
        var formularioId = await ResolverFormularioDeReporteAsync(request.ReporteId);
        if (formularioId is null)
        {
            return Ok(MfoDb.Invalid<int>("El reporte indicado no existe."));
        }

        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, formularioId.Value, MfoAutorizacion.Disenar, Usuario);

        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_PARAM_UPSERT", cn);
        cmd.Parameters.Add("p_RepParamId", OracleDbType.Int32).Value = MfoDb.DbValue(request.RepParamId);
        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = request.ReporteId;
        cmd.Parameters.Add("p_NombreParam", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.NombreParam);
        cmd.Parameters.Add("p_Origen", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Origen);
        cmd.Parameters.Add("p_ClaveCampo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveCampo);
        cmd.Parameters.Add("p_ValorFijo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ValorFijo);
        cmd.Parameters.Add("p_ClaveSistema", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveSistema);
        cmd.Parameters.Add("p_TipoDato", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.TipoDato);
        cmd.Parameters.Add("p_Formato", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Formato);
        cmd.Parameters.Add("p_Obligatorio", OracleDbType.Char).Value = MfoDb.DbFlag(request.Obligatorio);
        cmd.Parameters.Add("p_ValorDefecto", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ValorDefecto);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    [HttpPost("param/delete")]
    public async Task<IActionResult> ParamDelete(MfoIdRequest request)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        var formularioId = await ResolverFormularioDeParamAsync(cn, request.Id);
        if (formularioId is null)
        {
            return Ok(MfoDb.Invalid<int>("El parametro indicado no existe."));
        }

        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, formularioId.Value, MfoAutorizacion.Disenar, Usuario);

        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_PARAM_DEL", cn);
        cmd.Parameters.Add("p_RepParamId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd));
    }

    [HttpPost("columna/upsert")]
    public async Task<IActionResult> ColumnaUpsert(MfoRepColumnaUpsertRequest request)
    {
        var formularioId = await ResolverFormularioDeReporteAsync(request.ReporteId);
        if (formularioId is null)
        {
            return Ok(MfoDb.Invalid<int>("El reporte indicado no existe."));
        }

        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, formularioId.Value, MfoAutorizacion.Disenar, Usuario);

        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_COL_UPSERT", cn);
        cmd.Parameters.Add("p_RepColumnaId", OracleDbType.Int32).Value = MfoDb.DbValue(request.RepColumnaId);
        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = request.ReporteId;
        cmd.Parameters.Add("p_NombreCol", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.NombreCol);
        cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Titulo);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_AnchoRel", OracleDbType.Int32).Value = request.AnchoRel;
        cmd.Parameters.Add("p_Alineacion", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Alineacion);
        cmd.Parameters.Add("p_Formato", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Formato);
        cmd.Parameters.Add("p_Totalizar", OracleDbType.Char).Value = MfoDb.DbFlag(request.Totalizar);
        cmd.Parameters.Add("p_Agrupar", OracleDbType.Char).Value = MfoDb.DbFlag(request.Agrupar);
        cmd.Parameters.Add("p_Visible", OracleDbType.Char).Value = MfoDb.DbFlag(request.Visible);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    // ------------------------------------------------------------------------
    // Bitacora
    // ------------------------------------------------------------------------

    /// <summary>
    /// Bitacora paginada.
    ///
    /// Sin <c>FormularioId</c> la consulta se acota al usuario en curso. La
    /// alternativa -devolver la bitacora completa de la empresa- seria un
    /// endpoint administrativo, y MFO_PERMISO no tiene una accion que
    /// represente "administrar el motor" contra la cual comprobarlo. Antes que
    /// inventar un permiso implicito, se acota.
    /// </summary>
    [HttpPost("ejecuciones")]
    public async Task<IActionResult> Ejecuciones(MfoRepEjecFilterRequest request)
    {
        if (!MfoDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(MfoDb.InvalidList<MfoRepEjecResponse>(error));
        }

        var usuarioFiltro = request.Usuario;

        if (request.FormularioId is int formularioId)
        {
            var permiso = await MfoAutorizacion.PuedeAsync(
                connectionDB, formularioId, MfoAutorizacion.Ver, Usuario);

            if (!permiso.Permitido)
            {
                return Ok(MfoDb.InvalidList<MfoRepEjecResponse>(permiso.Mensaje));
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Usuario))
            {
                return Ok(MfoDb.InvalidList<MfoRepEjecResponse>(
                    "Indique el formulario o consulte con un usuario identificado."));
            }

            usuarioFiltro = Usuario;
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoRepEjecResponse>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_EJEC_LIST", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = MfoDb.DbValue(request.FormularioId);
        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = MfoDb.DbValue(request.ReporteId);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(usuarioFiltro);
        cmd.Parameters.Add("p_Resultado", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Resultado);
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = MfoDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = MfoDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalPage", OracleDbType.Int32, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteListAsync(cmd, MapEjecucion, request.Page, request.PageSize));
    }

    /// <summary>
    /// Ultimas ejecuciones del usuario en curso, con sus parametros, para poder
    /// recargarlas en el formulario. Siempre del usuario autenticado: devolver
    /// las de otro expondria con que filtros consulta.
    /// </summary>
    [HttpPost("ultimos")]
    public async Task<IActionResult> Ultimos(MfoRepUltimosRequest request)
    {
        if (string.IsNullOrWhiteSpace(Usuario))
        {
            return Ok(MfoDb.InvalidList<MfoRepUltimoResponse>(
                "Se requiere un usuario identificado."));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoRepUltimoResponse>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_ULTIMOS", cn);
        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = MfoDb.DbValue(request.ReporteId);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = MfoDb.DbValue(request.FormularioId);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = Usuario;
        cmd.Parameters.Add("p_Cantidad", OracleDbType.Int32).Value = request.Cantidad;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteListAsync(cmd, MapUltimo));
    }

    // ------------------------------------------------------------------------
    // Apoyo
    // ------------------------------------------------------------------------

    private async Task<ResultDto<MfoReporteDetalle>> CargarDetalleAsync(
        int? formularioId, string? alias, int? reporteId, bool soloActivos)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return MfoDb.Invalid<MfoReporteDetalle>(openError);

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_GET_BY_FORM", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = MfoDb.DbValue(formularioId);
        cmd.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = MfoDb.DbValue(alias);
        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = MfoDb.DbValue(reporteId);
        cmd.Parameters.Add("p_SoloActivos", OracleDbType.Char).Value = MfoDb.DbFlag(soloActivos);
        cmd.Parameters.Add("p_Reportes", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Parametros", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Columnas", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var reportes = new List<MfoReporteResponse>();
        var parametros = new List<MfoRepParamResponse>();
        var columnas = new List<MfoRepColumnaResponse>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                reportes.Add(MapReporte(reader));
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    parametros.Add(MapParametro(reader));
                }
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    columnas.Add(MapColumna(reader));
                }
            }
        }
        catch (OracleException ex)
        {
            return MfoDb.Invalid<MfoReporteDetalle>($"Error de base de datos ({ex.Number}): {ex.Message}");
        }

        var message = MfoDb.GetMessage(pMessage);
        var isSuccess = MfoDb.IsSuccessMessage(message);
        var data = new MfoReporteDetalle(reportes, parametros, columnas);

        return new ResultDto<MfoReporteDetalle>(data)
        {
            Data = isSuccess ? data : null,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message,
            CantidadRegistros = reportes.Count
        };
    }

    private async Task<int?> ResolverFormularioDeReporteAsync(int reporteId)
    {
        using var cn = connectionDB.GetMfoConnection();
        if (await MfoDb.TryOpenAsync(cn) is not null) return null;

        using var cmd = new OracleCommand(
            "SELECT FORMULARIO_ID FROM MFO.MFO_REPORTE WHERE REPORTE_ID = :p_ReporteId", cn)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = reporteId;

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? reader.SafeGetInt32("FORMULARIO_ID") : null;
    }

    private static async Task<int?> ResolverFormularioDeParamAsync(OracleConnection cn, int repParamId)
    {
        using var cmd = new OracleCommand(
            @"SELECT R.FORMULARIO_ID
                FROM MFO.MFO_REP_PARAM P
                JOIN MFO.MFO_REPORTE R ON R.REPORTE_ID = P.REPORTE_ID
               WHERE P.REP_PARAM_ID = :p_RepParamId", cn)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_RepParamId", OracleDbType.Int32).Value = repParamId;

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? reader.SafeGetInt32("FORMULARIO_ID") : null;
    }

    /// <summary>
    /// Crea la respuesta, guarda los valores y la envia. Solo se usa cuando
    /// <c>REGISTRA_EJEC='S'</c>. El envio se hace antes de ejecutar el reporte
    /// para que <c>MFO_REP_EJEC.RESPUESTA_ID</c> apunte a una respuesta ya
    /// sellada, incluso si el reporte falla despues.
    /// </summary>
    private async Task<ResultDto<int>> PersistirRespuestaAsync(
        MfoReporteResponse reporte,
        MfoDefinicionResponse definicion,
        List<MfoValorRequest> valores)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return MfoDb.Invalid<int>(openError);

        int respuestaId;

        using (var crear = MfoDb.StoredProcedure("SP_MFO_RESP_CREATE", cn))
        {
            crear.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = reporte.Alias;
            crear.Parameters.Add("p_ClaveIdem", OracleDbType.Varchar2).Value = DBNull.Value;
            crear.Parameters.Add("p_EntidadRef", OracleDbType.Varchar2).Value = "REPORTE";
            crear.Parameters.Add("p_ClaveRef", OracleDbType.Varchar2).Value = reporte.Clave;
            crear.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
            crear.Parameters.Add("p_IpOrigen", OracleDbType.Varchar2).Value = MfoDb.DbValue(IpOrigen);
            var pResp = crear.Parameters.Add("p_RespuestaId", OracleDbType.Int32, ParameterDirection.Output);
            crear.Parameters.Add("p_VersionId", OracleDbType.Int32, ParameterDirection.Output);
            var pMsg = crear.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await crear.ExecuteNonQueryAsync();

            var mensaje = MfoDb.GetMessage(pMsg);
            if (!MfoDb.IsSuccessMessage(mensaje))
            {
                return MfoDb.Invalid<int>(mensaje);
            }

            respuestaId = MfoDb.GetIntOutput(pResp);
        }

        var lote = MfoMapeoValores.Separar(definicion, valores);

        using (var guardar = MfoDb.StoredProcedure("SP_MFO_RESP_VAL_SAVE", cn))
        {
            guardar.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = respuestaId;
            MfoMapeoValores.AgregarArreglos(guardar, definicion, lote.EnArreglos);
            guardar.Parameters.Add("p_ClobClave", OracleDbType.Varchar2).Value = DBNull.Value;
            guardar.Parameters.Add("p_ClobFila", OracleDbType.Int32).Value = DBNull.Value;
            guardar.Parameters.Add("p_ClobOrden", OracleDbType.Int32).Value = DBNull.Value;
            guardar.Parameters.Add("p_ClobValor", OracleDbType.Clob).Value = DBNull.Value;
            guardar.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
            guardar.Parameters.Add("p_Guardados", OracleDbType.Int32, ParameterDirection.Output);
            guardar.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var guardado = await MfoDb.ExecuteScalarAsync(guardar, "p_Guardados");
            if (!guardado.IsValid)
            {
                return MfoDb.Invalid<int>(guardado.Message);
            }
        }

        // Los textos largos que no caben en el arreglo van de uno en uno. Un
        // formulario de parametros rara vez los tiene, pero el camino existe y
        // omitirlo dejaria valores silenciosamente fuera.
        foreach (var largo in lote.EnClob)
        {
            using var clob = MfoDb.StoredProcedure("SP_MFO_RESP_VAL_SAVE", cn);
            clob.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = respuestaId;
            MfoMapeoValores.AgregarArreglos(clob, definicion, []);
            clob.Parameters.Add("p_ClobClave", OracleDbType.Varchar2).Value = MfoDb.DbValue(largo.Clave);
            clob.Parameters.Add("p_ClobFila", OracleDbType.Int32).Value = largo.Fila;
            clob.Parameters.Add("p_ClobOrden", OracleDbType.Int32).Value = largo.Orden;
            clob.Parameters.Add("p_ClobValor", OracleDbType.Clob).Value =
                largo.Valor is null ? DBNull.Value : largo.Valor;
            clob.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
            clob.Parameters.Add("p_Guardados", OracleDbType.Int32, ParameterDirection.Output);
            clob.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var parcial = await MfoDb.ExecuteScalarAsync(clob, "p_Guardados");
            if (!parcial.IsValid)
            {
                return MfoDb.Invalid<int>(parcial.Message);
            }
        }

        using (var enviar = MfoDb.StoredProcedure("SP_MFO_RESP_SUBMIT", cn))
        {
            enviar.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = respuestaId;
            enviar.Parameters.Add("p_Snapshot", OracleDbType.Clob).Value = JsonSerializer.Serialize(valores);
            enviar.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
            enviar.Parameters.Add("p_IpOrigen", OracleDbType.Varchar2).Value = MfoDb.DbValue(IpOrigen);
            enviar.Parameters.Add("p_CampoError", OracleDbType.Varchar2, 30, null, ParameterDirection.Output);
            var pMsg = enviar.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await enviar.ExecuteNonQueryAsync();

            var mensaje = MfoDb.GetMessage(pMsg);
            if (!MfoDb.IsSuccessMessage(mensaje))
            {
                return MfoDb.Invalid<int>(mensaje);
            }
        }

        return new ResultDto<int>(respuestaId) { Data = respuestaId, IsValid = true, Message = string.Empty };
    }

    /// <summary>
    /// Escribe la bitacora. No propaga fallos: si el registro falla, el usuario
    /// ya tiene su reporte y perder la bitacora no debe convertirse en perder el
    /// resultado. El procedimiento es AUTONOMOUS_TRANSACTION justamente para
    /// que este escritura no dependa de nada mas.
    /// </summary>
    private async Task RegistrarEjecucionAsync(
        int reporteId, int? respuestaId, int empresa, int milisegundos,
        int filas, string resultado, string mensaje, string paramsJson)
    {
        try
        {
            using var cn = connectionDB.GetMfoConnection();
            if (await MfoDb.TryOpenAsync(cn) is not null) return;

            using var cmd = MfoDb.StoredProcedure("SP_MFO_REP_EJEC_INS", cn);
            cmd.Parameters.Add("p_ReporteId", OracleDbType.Int32).Value = reporteId;
            cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = MfoDb.DbValue(respuestaId);
            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
            cmd.Parameters.Add("p_Milisegundos", OracleDbType.Int32).Value = milisegundos;
            cmd.Parameters.Add("p_Filas", OracleDbType.Int32).Value = filas;
            cmd.Parameters.Add("p_Resultado", OracleDbType.Varchar2).Value = resultado;
            cmd.Parameters.Add("p_Mensaje", OracleDbType.Varchar2).Value = MfoDb.DbValue(mensaje);
            cmd.Parameters.Add("p_Params", OracleDbType.Clob).Value = paramsJson ?? string.Empty;
            cmd.Parameters.Add("p_IpOrigen", OracleDbType.Varchar2).Value = MfoDb.DbValue(IpOrigen);
            cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Deliberadamente silencioso: ver el resumen del metodo.
        }
    }

    private static ResultDto<List<MfoErrorValidacion>> ErroresDeValidacion(List<MfoErrorValidacion> errores)
    {
        return new ResultDto<List<MfoErrorValidacion>>(errores)
        {
            Data = errores,
            IsValid = false,
            Message = $"Los parametros tienen {errores.Count} error(es).",
            CantidadRegistros = errores.Count
        };
    }

    // ------------------------------------------------------------------------
    // Mapeos
    // ------------------------------------------------------------------------

    private static MfoReporteResponse MapReporte(IDataReader r)
    {
        var claveRegistro = r.SafeGetString("CLAVE_REGISTRO");

        return new MfoReporteResponse(
            r.SafeGetInt32("REPORTE_ID"),
            r.SafeGetInt32("FORMULARIO_ID"),
            r.SafeGetString("ALIAS"),
            r.SafeGetString("CLAVE"),
            r.SafeGetString("NOMBRE"),
            r.SafeGetString("DESCRIPCION"),
            r.SafeGetString("TIPO_EJEC"),
            claveRegistro,
            r.SafeGetString("TITULO_REPORTE"),
            r.SafeGetString("ORIENTACION"),
            MfoDb.ToNullableInt(r, "MAX_FILAS"),
            MfoDb.ToNullableInt(r, "TIMEOUT_SEG"),
            r.SafeGetInt32("ORDEN"),
            MfoDb.ToBool(r, "ACTIVO"),
            r.SafeGetString("MODO_USO"),
            MfoDb.ToBool(r, "REGISTRA_EJEC"),
            r.SafeGetInt32("PARAMETROS"),
            r.SafeGetInt32("COLUMNAS"),
            MfoRegistroReportes.EstaRegistrado(claveRegistro));
    }

    private static MfoRepParamResponse MapParametro(IDataReader r) => new(
        r.SafeGetInt32("REP_PARAM_ID"),
        r.SafeGetInt32("REPORTE_ID"),
        r.SafeGetString("NOMBRE_PARAM"),
        r.SafeGetString("ORIGEN"),
        r.SafeGetInt32("CAMPO_ID"),
        r.SafeGetString("CLAVE_CAMPO"),
        r.SafeGetString("ETIQUETA_CAMPO"),
        r.SafeGetString("VALOR_FIJO"),
        r.SafeGetString("CLAVE_SISTEMA"),
        r.SafeGetString("TIPO_DATO"),
        r.SafeGetString("FORMATO"),
        MfoDb.ToBool(r, "OBLIGATORIO"),
        r.SafeGetString("VALOR_DEFECTO"),
        r.SafeGetInt32("ORDEN"));

    private static MfoRepColumnaResponse MapColumna(IDataReader r) => new(
        r.SafeGetInt32("REP_COLUMNA_ID"),
        r.SafeGetInt32("REPORTE_ID"),
        r.SafeGetString("NOMBRE_COL"),
        r.SafeGetString("TITULO"),
        r.SafeGetInt32("ORDEN"),
        r.SafeGetInt32("ANCHO_REL"),
        r.SafeGetString("ALINEACION"),
        r.SafeGetString("FORMATO"),
        MfoDb.ToBool(r, "TOTALIZAR"),
        MfoDb.ToBool(r, "AGRUPAR"),
        MfoDb.ToBool(r, "VISIBLE"));

    private static MfoRepEjecResponse MapEjecucion(IDataReader r) => new(
        r.SafeGetInt32("REP_EJEC_ID"),
        r.SafeGetInt32("REPORTE_ID"),
        r.SafeGetString("CLAVE_REPORTE"),
        r.SafeGetString("REPORTE"),
        r.SafeGetInt32("FORMULARIO_ID"),
        r.SafeGetString("ALIAS"),
        r.SafeGetString("FORMULARIO"),
        r.SafeGetInt32("RESPUESTA_ID"),
        r.SafeGetString("USUARIO"),
        MfoDb.GetDate(r, "FECHA_INICIO"),
        r.SafeGetInt32("MILISEGUNDOS"),
        r.SafeGetInt32("FILAS"),
        r.SafeGetString("RESULTADO"),
        r.SafeGetString("MENSAJE"),
        r.SafeGetString("IP_ORIGEN"),
        MfoDb.ToBool(r, "TIENE_PARAMS"));

    private static MfoRepUltimoResponse MapUltimo(IDataReader r) => new(
        r.SafeGetInt32("REP_EJEC_ID"),
        r.SafeGetInt32("REPORTE_ID"),
        r.SafeGetString("CLAVE_REPORTE"),
        r.SafeGetString("REPORTE"),
        r.SafeGetInt32("FORMULARIO_ID"),
        r.SafeGetString("ALIAS"),
        MfoDb.GetDate(r, "FECHA_INICIO"),
        r.SafeGetInt32("MILISEGUNDOS"),
        r.SafeGetInt32("FILAS"),
        r.SafeGetString("RESULTADO"),
        r.SafeGetString("MENSAJE"),
        r.SafeGetString("PARAMS_CLB"));
}
