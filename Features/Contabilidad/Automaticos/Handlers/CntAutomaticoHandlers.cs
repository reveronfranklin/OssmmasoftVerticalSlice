using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class PreviewCntAutomaticoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntAutomaticPreviewResponse>> HandleAsync(CntAutomaticPreviewCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntAutomaticoSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta);
        if (!validation.IsValid)
        {
            await CntAutomaticoSupport.LogAsync(connectionDB, config, "PREVIEW", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, null, null, null, 0, 0m, 0m, value.UsuarioId, "VALIDACION", validation.Message);
            return new ResultDto<CntAutomaticPreviewResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<CntAutomaticPreviewResponse>(null!) { Data = null, IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_AUT_PREV", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntAutomaticoSupport.AddBaseParams(cmd, value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, value.UsuarioId, empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var lineas = new List<CntAutomaticLineResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    lineas.Add(CntAutomaticoSupport.MapLine(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var response = CntAutomaticoSupport.BuildPreview(value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, lineas);
            await CntAutomaticoSupport.LogAsync(connectionDB, config, "PREVIEW", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, null, null, null, lineas.Count, response.TotalDebe, response.TotalHaber, value.UsuarioId, isSuccess ? "OK" : "ERROR", message);

            return new ResultDto<CntAutomaticPreviewResponse>(response)
            {
                Data = isSuccess ? response : null,
                IsValid = isSuccess,
                Message = message
            };
        }
        catch (Exception ex)
        {
            await CntAutomaticoSupport.LogAsync(connectionDB, config, "PREVIEW", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, null, null, null, 0, 0m, 0m, value.UsuarioId, "ERROR", ex.Message);
            return new ResultDto<CntAutomaticPreviewResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class ConfirmCntAutomaticoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntAutomaticConfirmResponse>> HandleAsync(CntAutomaticConfirmCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntAutomaticoSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta);
        if (!validation.IsValid)
        {
            await CntAutomaticoSupport.LogAsync(connectionDB, config, "CONFIRM", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, value.FechaComprobante, null, null, 0, 0m, 0m, value.UsuarioId, "VALIDACION", validation.Message);
            return new ResultDto<CntAutomaticConfirmResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.FechaComprobante == default)
        {
            await CntAutomaticoSupport.LogAsync(connectionDB, config, "CONFIRM", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, value.FechaComprobante, null, null, 0, 0m, 0m, value.UsuarioId, "VALIDACION", "La fecha del comprobante es requerida.");
            return new ResultDto<CntAutomaticConfirmResponse>(null!) { Data = null, IsValid = false, Message = "La fecha del comprobante es requerida." };
        }

        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<CntAutomaticConfirmResponse>(null!) { Data = null, IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_AUT_CONF", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntAutomaticoSupport.AddBaseParams(cmd, value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, value.UsuarioId, empresa);
            cmd.Parameters.Add("p_FECHA_COMPROBANTE", OracleDbType.Date).Value = value.FechaComprobante;
            cmd.Parameters.Add("p_OBSERVACION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Observacion);
            var pCodigo = cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32, ParameterDirection.Output);
            var pNumero = cmd.Parameters.Add("p_NUMERO_COMPROBANTE", OracleDbType.Varchar2, 20, null, ParameterDirection.Output);
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD_LINEAS", OracleDbType.Int32, ParameterDirection.Output);
            var pTotalDebe = cmd.Parameters.Add("p_TOTAL_DEBE", OracleDbType.Decimal, ParameterDirection.Output);
            var pTotalHaber = cmd.Parameters.Add("p_TOTAL_HABER", OracleDbType.Decimal, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var totalDebe = CntAutomaticoSupport.GetDecimalOutput(pTotalDebe);
            var totalHaber = CntAutomaticoSupport.GetDecimalOutput(pTotalHaber);
            var response = new CntAutomaticConfirmResponse(
                CntDb.GetIntOutput(pCodigo),
                pNumero.Value?.ToString() ?? string.Empty,
                CntDb.GetIntOutput(pCantidad),
                totalDebe,
                totalHaber,
                totalDebe - totalHaber);
            await CntAutomaticoSupport.LogAsync(connectionDB, config, "CONFIRM", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, value.FechaComprobante, response.CodigoComprobante, response.NumeroComprobante, response.CantidadLineas, response.TotalDebe, response.TotalHaber, value.UsuarioId, isSuccess ? "OK" : "ERROR", message);

            return new ResultDto<CntAutomaticConfirmResponse>(response)
            {
                Data = isSuccess ? response : null,
                IsValid = isSuccess,
                Message = message
            };
        }
        catch (Exception ex)
        {
            await CntAutomaticoSupport.LogAsync(connectionDB, config, "CONFIRM", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, value.FechaComprobante, null, null, 0, 0m, 0m, value.UsuarioId, "ERROR", ex.Message);
            return new ResultDto<CntAutomaticConfirmResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

internal static class CntAutomaticoSupport
{
    private static readonly HashSet<string> AllowedOriginCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMPORDCOM",
        "ANCOMPORDC",
        "COMPCONTOB",
        "ANCOCONTOB",
        "ODPAUT",
        "ANODPAUT"
    };

    public static async Task<ResultDto<string>> ValidateAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        ClaimsPrincipal user,
        HttpRequest request,
        int usuarioId,
        int codigoPeriodo,
        int tipoComprobanteId,
        int origenId,
        DateTime fechaDesde,
        DateTime fechaHasta)
    {
        var access = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, usuarioId);
        if (!access.IsValid)
        {
            return access;
        }

        var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, usuarioId, CntSecurity.ComprobanteAutomatic);
        if (!permission.IsValid)
        {
            return permission;
        }

        if (codigoPeriodo <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El periodo es requerido." };
        }

        if (tipoComprobanteId <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El tipo de comprobante es requerido." };
        }

        if (origenId <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El origen del comprobante es requerido." };
        }

        if (!CntDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = empresaMessage };
        }

        var originValidation = await ValidateAllowedOriginAsync(connectionDB, origenId, empresa);
        if (!originValidation.IsValid)
        {
            return originValidation;
        }

        if (fechaDesde == default || fechaHasta == default)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El rango de fechas es requerido." };
        }

        if (fechaDesde.Date > fechaHasta.Date)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "La fecha desde no puede ser mayor que la fecha hasta." };
        }

        return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
    }

    private static async Task<ResultDto<string>> ValidateAllowedOriginAsync(ConnectionDB connectionDB, int origenId, int empresa)
    {
        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(@"
                SELECT CODIGO
                  FROM CNT.CNT_DESCRIPTIVAS
                 WHERE DESCRIPCION_ID = :p_ORIGEN_ID
                   AND TITULO_ID = 1
                   AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn);
            cmd.BindByName = true;
            cmd.Parameters.Add("p_ORIGEN_ID", OracleDbType.Int32).Value = origenId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

            var code = (await cmd.ExecuteScalarAsync())?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El origen del comprobante no existe para la empresa configurada." };
            }

            if (!AllowedOriginCodes.Contains(code))
            {
                return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = $"El origen {code} no esta permitido para el proceso automatico CNT." };
            }

            return new ResultDto<string>(code) { Data = code, IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    public static void AddBaseParams(OracleCommand cmd, int codigoPeriodo, int tipoComprobanteId, int origenId, DateTime fechaDesde, DateTime fechaHasta, int usuarioId, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = codigoPeriodo;
        cmd.Parameters.Add("p_TIPO_COMPROBANTE_ID", OracleDbType.Int32).Value = tipoComprobanteId;
        cmd.Parameters.Add("p_ORIGEN_ID", OracleDbType.Int32).Value = origenId;
        cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = fechaHasta;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = usuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static async Task LogAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        string operacion,
        int codigoPeriodo,
        int tipoComprobanteId,
        int origenId,
        DateTime fechaDesde,
        DateTime fechaHasta,
        DateTime? fechaComprobante,
        int? codigoComprobante,
        string? numeroComprobante,
        int cantidadLineas,
        decimal totalDebe,
        decimal totalHaber,
        int usuarioId,
        string estado,
        string? mensaje)
    {
        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out _))
            {
                return;
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_AUT_LOG_INS", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_OPERACION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(operacion);
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = codigoPeriodo;
            cmd.Parameters.Add("p_TIPO_COMPROBANTE_ID", OracleDbType.Int32).Value = tipoComprobanteId;
            cmd.Parameters.Add("p_ORIGEN_ID", OracleDbType.Int32).Value = origenId;
            cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = fechaDesde == default ? DBNull.Value : fechaDesde;
            cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = fechaHasta == default ? DBNull.Value : fechaHasta;
            cmd.Parameters.Add("p_FECHA_COMPROBANTE", OracleDbType.Date).Value = fechaComprobante.HasValue && fechaComprobante.Value != default ? fechaComprobante.Value : DBNull.Value;
            cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = codigoComprobante.HasValue ? codigoComprobante.Value : DBNull.Value;
            cmd.Parameters.Add("p_NUMERO_COMPROBANTE", OracleDbType.Varchar2).Value = CntDb.StringDbValue(numeroComprobante);
            cmd.Parameters.Add("p_CANTIDAD_LINEAS", OracleDbType.Int32).Value = cantidadLineas;
            cmd.Parameters.Add("p_TOTAL_DEBE", OracleDbType.Decimal).Value = totalDebe;
            cmd.Parameters.Add("p_TOTAL_HABER", OracleDbType.Decimal).Value = totalHaber;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = usuarioId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ESTADO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(estado);
            cmd.Parameters.Add("p_MENSAJE", OracleDbType.Varchar2).Value = CntDb.StringDbValue(mensaje);

            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // La auditoria no debe bloquear el flujo contable principal.
        }
    }

    public static CntAutomaticLineResponse MapLine(IDataReader reader)
    {
        var monto = reader.SafeGetDecimal("MONTO");
        return new CntAutomaticLineResponse(
            reader.SafeGetInt32("SECUENCIA"),
            reader.SafeGetInt32("CODIGO_MAYOR"),
            reader.SafeGetString("MAYOR"),
            reader.SafeGetInt32("CODIGO_AUXILIAR"),
            reader.SafeGetString("AUXILIAR"),
            reader.SafeGetString("REFERENCIA1"),
            reader.SafeGetString("REFERENCIA2"),
            reader.SafeGetString("REFERENCIA3"),
            reader.SafeGetString("DESCRIPCION"),
            monto,
            monto < 0 ? Math.Abs(monto) : 0m,
            monto > 0 ? monto : 0m);
    }

    public static CntAutomaticPreviewResponse BuildPreview(int codigoPeriodo, int tipoComprobanteId, int origenId, DateTime fechaDesde, DateTime fechaHasta, List<CntAutomaticLineResponse> lineas)
    {
        var totalDebe = lineas.Sum(x => x.Debe);
        var totalHaber = lineas.Sum(x => x.Haber);
        return new CntAutomaticPreviewResponse(codigoPeriodo, tipoComprobanteId, origenId, fechaDesde, fechaHasta, totalDebe, totalHaber, totalDebe - totalHaber, lineas);
    }

    public static decimal GetDecimalOutput(OracleParameter parameter)
    {
        if (parameter.Value is null || parameter.Value == DBNull.Value)
        {
            return 0m;
        }

        return Convert.ToDecimal(parameter.Value.ToString());
    }
}
