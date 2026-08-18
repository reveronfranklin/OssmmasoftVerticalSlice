using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Captura de respuestas. Es el endpoint generico del motor: acepta pares
/// clave/valor arbitrarios, y por eso toda la validacion del servidor esta antes
/// de tocar la base.
///
/// Dos momentos con exigencias distintas:
///   saveValores -> solo validacion estructural. Un borrador se guarda a medias
///                  por definicion; exigir los obligatorios haria imposible el
///                  autoguardado.
///   submit      -> estructural + reglas + condiciones, y ademas la regla UNICO
///                  dentro del procedimiento, que es la unica que necesita la
///                  base.
/// </summary>
[ApiController]
[Route("api/MfoRespuesta")]
public class MfoRespuestaController(
    ConnectionDB connectionDB,
    IConfiguration config,
    MfoDefinicionCache cache) : ControllerBase
{
    private string? Usuario => Request.Headers["X-Usuario"].FirstOrDefault();

    private string? IpOrigen => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Resuelve el formulario dueño de una respuesta y comprueba el permiso.
    /// Se hace por respuesta y no por alias porque el cliente manda un id: si la
    /// comprobacion se hiciera sobre el alias que el cliente envia, bastaria con
    /// mandar el alias de un formulario abierto para tocar la respuesta de otro.
    /// </summary>
    private async Task<MfoAutorizacion.Resultado> PuedeSobreRespuestaAsync(int respuestaId, string accion)
    {
        var formularioId = await ResolverFormularioAsync(respuestaId);
        if (formularioId is null)
        {
            return new MfoAutorizacion.Resultado(false, "La respuesta indicada no existe.");
        }

        return await MfoAutorizacion.PuedeAsync(connectionDB, formularioId.Value, accion, Usuario);
    }

    private async Task<int?> ResolverFormularioAsync(int respuestaId)
    {
        using var cn = connectionDB.GetMfoConnection();
        if (await MfoDb.TryOpenAsync(cn) is not null) return null;

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_GET_BY_ID", cn);
        cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = respuestaId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Valores", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? reader.SafeGetInt32("FORMULARIO_ID") : null;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(MfoRespuestaCreateRequest request)
    {
        var definicion = await cache.ObtenerAsync(connectionDB, null, request.Alias);
        if (!definicion.IsValid || definicion.Data is null)
        {
            return Ok(MfoDb.Invalid<MfoRespuestaCreada>(definicion.Message));
        }

        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, definicion.Data.FormularioId, MfoAutorizacion.Llenar, Usuario);
        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<MfoRespuestaCreada>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<MfoRespuestaCreada>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_CREATE", cn);
        cmd.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Alias);
        cmd.Parameters.Add("p_ClaveIdem", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveIdem);
        cmd.Parameters.Add("p_EntidadRef", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.EntidadRef);
        cmd.Parameters.Add("p_ClaveRef", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveRef);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_IpOrigen", OracleDbType.Varchar2).Value = MfoDb.DbValue(IpOrigen);
        var pResp = cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32, ParameterDirection.Output);
        var pVer = cmd.Parameters.Add("p_VersionId", OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        await cmd.ExecuteNonQueryAsync();

        var message = MfoDb.GetMessage(pMessage);
        var isSuccess = MfoDb.IsSuccessMessage(message);
        var data = new MfoRespuestaCreada(MfoDb.GetIntOutput(pResp), MfoDb.GetIntOutput(pVer));

        return Ok(new ResultDto<MfoRespuestaCreada>(data)
        {
            Data = isSuccess ? data : null,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message
        });
    }

    [HttpPost("saveValores")]
    public async Task<IActionResult> SaveValores(MfoRespuestaSaveRequest request)
    {
        return await GuardarAsync(request.RespuestaId, request.Valores, esEnvio: false);
    }

    /// <summary>
    /// Guarda y envia en una sola llamada. El payload que llega aqui es el que
    /// se sella en SNAPSHOT_CLB: se escribe crudo, tal como llego, y la
    /// aplicacion nunca lo lee. Es evidencia forense por si el mapeo a EAV
    /// alguna vez tiene un fallo.
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(MfoRespuestaSubmitRequest request)
    {
        var guardado = await GuardarAsync(request.RespuestaId, request.Valores, esEnvio: true);

        if (guardado is not OkObjectResult ok || ok.Value is not ResultDto<List<MfoErrorValidacion>> res
            || !res.IsValid)
        {
            return guardado;
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoErrorValidacion>(openError));

        var snapshot = JsonSerializer.Serialize(request.Valores);

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_SUBMIT", cn);
        cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = request.RespuestaId;
        cmd.Parameters.Add("p_Snapshot", OracleDbType.Clob).Value = snapshot;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_IpOrigen", OracleDbType.Varchar2).Value = MfoDb.DbValue(IpOrigen);
        var pCampo = cmd.Parameters.Add("p_CampoError", OracleDbType.Varchar2, 30, null, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        await cmd.ExecuteNonQueryAsync();

        var message = MfoDb.GetMessage(pMessage);
        if (MfoDb.IsSuccessMessage(message))
        {
            return Ok(new ResultDto<List<MfoErrorValidacion>>([])
            {
                Data = [], IsValid = true, Message = string.Empty
            });
        }

        // La regla UNICO devuelve el campo culpable: se traduce al mismo formato
        // de error por campo que produce el validador, para que el frontend no
        // tenga dos caminos distintos para pintar un error.
        var claveError = pCampo.Value == DBNull.Value ? null : pCampo.Value?.ToString();
        var errores = string.IsNullOrWhiteSpace(claveError)
            ? new List<MfoErrorValidacion>()
            : [new MfoErrorValidacion(claveError, 0, 0, "UNICO", message)];

        return Ok(new ResultDto<List<MfoErrorValidacion>>(errores)
        {
            Data = errores,
            IsValid = false,
            Message = message
        });
    }

    /// <summary>
    /// Camino comun de guardado. Devuelve la lista de errores de validacion en
    /// <c>Data</c>: cada uno con su clave, fila y ocurrencia, para que el
    /// frontend los pinte junto al control que los produjo y no en un toast.
    /// </summary>
    private async Task<IActionResult> GuardarAsync(
        int respuestaId, List<MfoValorRequest> valores, bool esEnvio)
    {
        valores ??= [];

        var permiso = await PuedeSobreRespuestaAsync(respuestaId, MfoAutorizacion.Llenar);
        if (!permiso.Permitido)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(permiso.Mensaje));
        }

        // La definicion se resuelve por la version de la respuesta, no por la
        // vigente del formulario: una respuesta abierta con la version 1 se
        // sigue validando con la version 1 aunque ya se haya publicado la 2.
        var versionId = await ResolverVersionAsync(respuestaId);
        if (versionId is null)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>("La respuesta indicada no existe."));
        }

        var definicion = await cache.ObtenerAsync(connectionDB, versionId, null);
        if (!definicion.IsValid || definicion.Data is null)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(definicion.Message));
        }

        var errores = esEnvio
            ? MfoValidador.ValidarEnvio(definicion.Data, valores)
            : MfoValidador.ValidarBorrador(definicion.Data, valores);

        if (errores.Count > 0)
        {
            return Ok(new ResultDto<List<MfoErrorValidacion>>(errores)
            {
                Data = errores,
                IsValid = false,
                Message = $"La respuesta tiene {errores.Count} error(es) de validacion.",
                CantidadRegistros = errores.Count
            });
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoErrorValidacion>(openError));

        var lote = MfoMapeoValores.Separar(definicion.Data, valores);

        var resultado = await EjecutarGuardadoAsync(cn, respuestaId, definicion.Data, lote.EnArreglos, null);
        if (!resultado.IsValid)
        {
            return Ok(MfoDb.InvalidList<MfoErrorValidacion>(resultado.Message));
        }

        // Los textos largos van de uno en uno: no caben en un elemento del
        // arreglo asociativo.
        foreach (var largo in lote.EnClob)
        {
            var parcial = await EjecutarGuardadoAsync(cn, respuestaId, definicion.Data, [], largo);
            if (!parcial.IsValid)
            {
                return Ok(MfoDb.InvalidList<MfoErrorValidacion>(parcial.Message));
            }
        }

        return Ok(new ResultDto<List<MfoErrorValidacion>>([])
        {
            Data = [], IsValid = true, Message = string.Empty
        });
    }

    private async Task<ResultDto<int>> EjecutarGuardadoAsync(
        OracleConnection cn,
        int respuestaId,
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> enArreglos,
        MfoValorRequest? clob)
    {
        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_VAL_SAVE", cn);
        cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = respuestaId;

        MfoMapeoValores.AgregarArreglos(cmd, definicion, enArreglos);

        cmd.Parameters.Add("p_ClobClave", OracleDbType.Varchar2).Value = MfoDb.DbValue(clob?.Clave);
        cmd.Parameters.Add("p_ClobFila", OracleDbType.Int32).Value = MfoDb.DbValue(clob?.Fila);
        cmd.Parameters.Add("p_ClobOrden", OracleDbType.Int32).Value = MfoDb.DbValue(clob?.Orden);
        cmd.Parameters.Add("p_ClobValor", OracleDbType.Clob).Value =
            clob?.Valor is null ? DBNull.Value : clob.Valor;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Guardados", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return await MfoDb.ExecuteScalarAsync(cmd, "p_Guardados");
    }

    private async Task<int?> ResolverVersionAsync(int respuestaId)
    {
        using var cn = connectionDB.GetMfoConnection();
        if (await MfoDb.TryOpenAsync(cn) is not null) return null;

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_GET_BY_ID", cn);
        cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = respuestaId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Valores", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? reader.SafeGetInt32("VERSION_ID") : null;
    }

    [HttpGet("getById")]
    public async Task<IActionResult> GetById([FromQuery] int id)
    {
        var permiso = await PuedeSobreRespuestaAsync(id, MfoAutorizacion.Ver);
        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<MfoRespuestaDetalle>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<MfoRespuestaDetalle>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_GET_BY_ID", cn);
        cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Valores", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        MfoRespuestaResponse? sobre = null;
        var valores = new List<MfoValorResponse>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                sobre = new MfoRespuestaResponse(
                    reader.SafeGetInt32("RESPUESTA_ID"), reader.SafeGetInt32("VERSION_ID"),
                    reader.SafeGetInt32("FORMULARIO_ID"), reader.SafeGetString("ALIAS"),
                    reader.SafeGetString("FORMULARIO"), reader.SafeGetInt32("VERSION_NUMERO"),
                    reader.SafeGetString("ESTADO"), reader.SafeGetString("CLAVE_IDEM"),
                    reader.SafeGetString("ENTIDAD_REF"), reader.SafeGetString("CLAVE_REF"),
                    reader.SafeGetString("USUARIO_LLENA"), MfoDb.GetDate(reader, "FECHA_INICIO"),
                    MfoDb.GetDate(reader, "FECHA_ENVIO"), reader.SafeGetString("MOTIVO_ANULA"));
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    valores.Add(new MfoValorResponse(
                        reader.SafeGetInt32("VALOR_ID"), reader.SafeGetInt32("CAMPO_ID"),
                        reader.SafeGetString("CLAVE_CAMPO"), reader.SafeGetInt32("FILA"),
                        reader.SafeGetInt32("ORDEN_VAL"), reader.SafeGetString("COLUMNA_VALOR"),
                        reader.SafeGetString("VALOR_TEXTO"), reader.SafeGetString("ETIQUETA_VAL")));
                }
            }
        }

        var message = MfoDb.GetMessage(pMessage);
        var isSuccess = MfoDb.IsSuccessMessage(message) && sobre is not null;
        var data = new MfoRespuestaDetalle(sobre!, valores);

        return Ok(new ResultDto<MfoRespuestaDetalle>(data)
        {
            Data = isSuccess ? data : null,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message
        });
    }

    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(MfoRespuestaSearchRequest request)
    {
        if (!MfoDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(MfoDb.InvalidList<MfoRespuestaListItem>(error));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoRespuestaListItem>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_SEARCH", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Alias);
        cmd.Parameters.Add("p_Estado", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Estado);
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = MfoDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = MfoDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Usuario);
        cmd.Parameters.Add("p_EntidadRef", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.EntidadRef);
        cmd.Parameters.Add("p_ClaveRef", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveRef);
        cmd.Parameters.Add("p_ClaveCampo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveCampo);
        cmd.Parameters.Add("p_ValorTexto", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ValorTexto);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalPage", OracleDbType.Int32, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteListAsync(cmd, r => new MfoRespuestaListItem(
            r.SafeGetInt32("RESPUESTA_ID"), r.SafeGetInt32("VERSION_ID"), r.SafeGetInt32("FORMULARIO_ID"),
            r.SafeGetString("ALIAS"), r.SafeGetString("FORMULARIO"), r.SafeGetInt32("VERSION_NUMERO"),
            r.SafeGetString("ESTADO"), r.SafeGetString("USUARIO_LLENA"),
            MfoDb.GetDate(r, "FECHA_INICIO"), MfoDb.GetDate(r, "FECHA_ENVIO"),
            r.SafeGetString("ENTIDAD_REF"), r.SafeGetString("CLAVE_REF"),
            r.SafeGetString("MOTIVO_ANULA"), r.SafeGetInt32("VALORES")),
            request.Page, request.PageSize));
    }

    [HttpPost("anular")]
    public async Task<IActionResult> Anular(MfoRespuestaAnularRequest request)
    {
        var permiso = await PuedeSobreRespuestaAsync(request.RespuestaId, MfoAutorizacion.Anular);
        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_ANULAR", cn);
        cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = request.RespuestaId;
        cmd.Parameters.Add("p_Motivo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Motivo);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd));
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(MfoIdRequest request)
    {
        var permiso = await PuedeSobreRespuestaAsync(request.Id, MfoAutorizacion.Llenar);
        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_DELETE", cn);
        cmd.Parameters.Add("p_RespuestaId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_AdjuntosHuerfanos", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_AdjuntosHuerfanos"));
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(MfoRespuestaExportRequest request)
    {
        if (!MfoDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(MfoDb.InvalidList<MfoExportItem>(error));
        }

        // La exportacion sin alias sacaria datos de todos los formularios de la
        // empresa de una vez, y no hay un formulario contra el cual comprobar el
        // permiso. Se exige el alias.
        if (string.IsNullOrWhiteSpace(request.Alias))
        {
            return Ok(MfoDb.InvalidList<MfoExportItem>("Indique el formulario a exportar."));
        }

        var definicionExp = await cache.ObtenerAsync(connectionDB, null, request.Alias);
        if (!definicionExp.IsValid || definicionExp.Data is null)
        {
            return Ok(MfoDb.InvalidList<MfoExportItem>(definicionExp.Message));
        }

        var permisoExp = await MfoAutorizacion.PuedeAsync(
            connectionDB, definicionExp.Data.FormularioId, MfoAutorizacion.Exportar, Usuario);
        if (!permisoExp.Permitido)
        {
            return Ok(MfoDb.InvalidList<MfoExportItem>(permisoExp.Mensaje));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoExportItem>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_RESP_EXPORT", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Alias);
        cmd.Parameters.Add("p_Estado", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Estado);
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = MfoDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = MfoDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteListAsync(cmd, r => new MfoExportItem(
            r.SafeGetString("ALIAS"), r.SafeGetInt32("RESPUESTA_ID"), r.SafeGetInt32("VERSION_ID"),
            r.SafeGetInt32("VERSION_NUMERO"), r.SafeGetString("ESTADO"), r.SafeGetString("USUARIO_LLENA"),
            MfoDb.GetDate(r, "FECHA_INICIO"), MfoDb.GetDate(r, "FECHA_ENVIO"),
            r.SafeGetString("ENTIDAD_REF"), r.SafeGetString("CLAVE_REF"),
            r.SafeGetString("CLAVE_CAMPO"), r.SafeGetString("ETIQUETA"),
            r.SafeGetInt32("FILA"), r.SafeGetInt32("ORDEN_VAL"), r.SafeGetString("TIPO_CAMPO"),
            r.SafeGetString("VALOR"), r.SafeGetString("ETIQUETA_VAL"))));
    }
}

public record MfoRespuestaCreada(int RespuestaId, int VersionId);
