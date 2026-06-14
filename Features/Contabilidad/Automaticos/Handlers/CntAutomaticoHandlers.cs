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

            var preview = await ExecutePreviewAsync(value, empresa, retryAfterObjectRefresh: true);
            var lineas = preview.Lineas;
            var message = preview.Message;
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

    private async Task<(List<CntAutomaticLineResponse> Lineas, string Message)> ExecutePreviewAsync(CntAutomaticPreviewCommand value, int empresa, bool retryAfterObjectRefresh)
    {
        try
        {
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

            return (lineas, CntDb.GetMessage(pMessage));
        }
        catch (OracleException ex) when (ex.Number == 8103 && retryAfterObjectRefresh)
        {
            OracleConnection.ClearAllPools();
            return await ExecutePreviewAsync(value, empresa, retryAfterObjectRefresh: false);
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

        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<CntAutomaticConfirmResponse>(null!) { Data = null, IsValid = false, Message = errorMessage };
            }

            var results = new List<CntAutomaticDailyConfirmResponse>();
            var current = value.FechaDesde.Date;
            var end = value.FechaHasta.Date;

            while (current <= end)
            {
                var replace = await CntAutomaticoSupport.DeleteExistingAutomaticForDayAsync(
                    connectionDB,
                    config,
                    value.CodigoPeriodo,
                    value.TipoComprobanteId,
                    value.OrigenId,
                    current,
                    value.UsuarioId,
                    empresa);

                if (!replace.IsValid)
                {
                    results.Add(new CntAutomaticDailyConfirmResponse(
                        current,
                        0,
                        string.Empty,
                        0,
                        0m,
                        0m,
                        0m,
                        "ERROR",
                        replace.Message));

                    await CntAutomaticoSupport.LogAsync(connectionDB, config, "CONFIRM_DIA", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, current, current, current, null, null, 0, 0m, 0m, value.UsuarioId, "ERROR", replace.Message);
                    current = current.AddDays(1);
                    continue;
                }

                var daily = await CntAutomaticoSupport.ConfirmDayAsync(
                    connectionDB,
                    value.CodigoPeriodo,
                    value.TipoComprobanteId,
                    value.OrigenId,
                    current,
                    value.UsuarioId,
                    empresa,
                    value.Observacion,
                    replace.Data == "REEMPLAZADO");

                results.Add(daily);

                await CntAutomaticoSupport.LogAsync(
                    connectionDB,
                    config,
                    "CONFIRM_DIA",
                    value.CodigoPeriodo,
                    value.TipoComprobanteId,
                    value.OrigenId,
                    current,
                    current,
                    current,
                    daily.CodigoComprobante > 0 ? daily.CodigoComprobante : null,
                    string.IsNullOrWhiteSpace(daily.NumeroComprobante) ? null : daily.NumeroComprobante,
                    daily.CantidadLineas,
                    daily.TotalDebe,
                    daily.TotalHaber,
                    value.UsuarioId,
                    daily.Estado,
                    daily.Mensaje);

                current = current.AddDays(1);
            }

            var generated = results.Where(x => x.Estado is "GENERADO" or "REEMPLAZADO").ToList();
            var totalDebe = generated.Sum(x => x.TotalDebe);
            var totalHaber = generated.Sum(x => x.TotalHaber);
            var response = new CntAutomaticConfirmResponse(
                generated.Count,
                results.Count(x => x.Estado == "SIN_LINEAS"),
                results.Count(x => x.Estado == "ERROR"),
                generated.Sum(x => x.CantidadLineas),
                totalDebe,
                totalHaber,
                totalDebe - totalHaber,
                results);

            var isSuccess = response.CantidadErrores == 0;
            var message = isSuccess
                ? $"Proceso automatico finalizado. Comprobantes generados: {response.CantidadComprobantes}."
                : $"Proceso automatico finalizado con {response.CantidadErrores} error(es).";

            await CntAutomaticoSupport.LogAsync(connectionDB, config, "CONFIRM", value.CodigoPeriodo, value.TipoComprobanteId, value.OrigenId, value.FechaDesde, value.FechaHasta, null, null, null, response.TotalLineas, response.TotalDebe, response.TotalHaber, value.UsuarioId, isSuccess ? "OK" : "ERROR", message);

            return new ResultDto<CntAutomaticConfirmResponse>(response)
            {
                Data = response,
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
        "COMP",
        "ANCOMP",
        "COMPORDCOM",
        "ANCOMPORDC",
        "COMPCONTOB",
        "ANCOCONTOB",
        "ODPAUT",
        "ANODPAUT",
        "CHEAUT",
        "CHEPROAUT",
        "CHERETAUT",
        "CHERETDAUT",
        "RETENFTDT",
        "ANCHEAUT"
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

        var periodValidation = await ValidatePeriodRangeAsync(connectionDB, codigoPeriodo, empresa, fechaDesde, fechaHasta);
        if (!periodValidation.IsValid)
        {
            return periodValidation;
        }

        return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
    }

    private static async Task<ResultDto<string>> ValidatePeriodRangeAsync(ConnectionDB connectionDB, int codigoPeriodo, int empresa, DateTime fechaDesde, DateTime fechaHasta)
    {
        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(@"
                SELECT FECHA_DESDE,
                       FECHA_HASTA,
                       FECHA_CIERRE
                  FROM CNT.CNT_PERIODOS
                 WHERE CODIGO_PERIODO = :p_CODIGO_PERIODO
                   AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn);
            cmd.BindByName = true;
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = codigoPeriodo;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El periodo seleccionado no existe." };
            }

            var periodoDesde = CntDb.SafeGetDate(reader, "FECHA_DESDE").Date;
            var periodoHasta = CntDb.SafeGetDate(reader, "FECHA_HASTA").Date;
            var fechaCierre = CntDb.SafeGetNullableDate(reader, "FECHA_CIERRE");

            if (fechaCierre.HasValue)
            {
                return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El periodo seleccionado esta cerrado." };
            }

            if (fechaDesde.Date < periodoDesde || fechaHasta.Date > periodoHasta)
            {
                return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "La fecha desde y fecha hasta deben estar dentro del periodo seleccionado." };
            }

            return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = ex.Message };
        }
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
                   AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = :p_CODIGO_EMPRESA)", cn);
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

    public static async Task<ResultDto<string>> DeleteExistingAutomaticForDayAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        int codigoPeriodo,
        int tipoComprobanteId,
        int origenId,
        DateTime fechaComprobante,
        int usuarioId,
        int empresa)
    {
        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();

            var existing = new List<(int CodigoComprobante, string Extra1)>();
            using (var query = new OracleCommand(@"
                SELECT CODIGO_COMPROBANTE,
                       NVL(EXTRA1, ' ') EXTRA1
                  FROM CNT.CNT_COMPROBANTES
                 WHERE CODIGO_PERIODO = :p_CODIGO_PERIODO
                   AND TIPO_COMPROBANTE_ID = :p_TIPO_COMPROBANTE_ID
                   AND NVL(ORIGEN_ID, 0) = :p_ORIGEN_ID
                   AND TRUNC(FECHA_COMPROBANTE) = TRUNC(:p_FECHA_COMPROBANTE)
                   AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn))
            {
                query.BindByName = true;
                query.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = codigoPeriodo;
                query.Parameters.Add("p_TIPO_COMPROBANTE_ID", OracleDbType.Int32).Value = tipoComprobanteId;
                query.Parameters.Add("p_ORIGEN_ID", OracleDbType.Int32).Value = origenId;
                query.Parameters.Add("p_FECHA_COMPROBANTE", OracleDbType.Date).Value = fechaComprobante.Date;
                query.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

                using var reader = await query.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existing.Add((reader.SafeGetInt32("CODIGO_COMPROBANTE"), reader.SafeGetString("EXTRA1").Trim()));
                }
            }

            if (existing.Count == 0)
            {
                return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
            }

            if (existing.Any(x => !string.Equals(x.Extra1, "AUTOMATICO", StringComparison.OrdinalIgnoreCase)))
            {
                return new ResultDto<string>(string.Empty)
                {
                    Data = null,
                    IsValid = false,
                    Message = $"Existe un comprobante manual para la fecha {fechaComprobante:yyyy-MM-dd}. No se reemplaza."
                };
            }

            foreach (var item in existing)
            {
                using var deleteDetails = new OracleCommand(@"
                    DELETE FROM CNT.CNT_DETALLE_COMPROBANTE
                     WHERE CODIGO_COMPROBANTE = :p_CODIGO_COMPROBANTE
                       AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn);
                deleteDetails.BindByName = true;
                deleteDetails.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = item.CodigoComprobante;
                deleteDetails.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
                await deleteDetails.ExecuteNonQueryAsync();

                using var deleteHeader = new OracleCommand(@"
                    DELETE FROM CNT.CNT_COMPROBANTES
                     WHERE CODIGO_COMPROBANTE = :p_CODIGO_COMPROBANTE
                       AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn);
                deleteHeader.BindByName = true;
                deleteHeader.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = item.CodigoComprobante;
                deleteHeader.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
                await deleteHeader.ExecuteNonQueryAsync();

                await LogAsync(connectionDB, config, "REEMPLAZO_DIA", codigoPeriodo, tipoComprobanteId, origenId, fechaComprobante.Date, fechaComprobante.Date, fechaComprobante.Date, item.CodigoComprobante, null, 0, 0m, 0m, usuarioId, "REEMPLAZADO", "Comprobante automatico existente eliminado para regeneracion.");
            }

            return new ResultDto<string>("REEMPLAZADO") { IsValid = true, Message = "Comprobante automatico previo reemplazado." };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    public static async Task<CntAutomaticDailyConfirmResponse> ConfirmDayAsync(
        ConnectionDB connectionDB,
        int codigoPeriodo,
        int tipoComprobanteId,
        int origenId,
        DateTime dia,
        int usuarioId,
        int empresa,
        string? observacion,
        bool wasReplaced,
        bool retryAfterObjectRefresh = true)
    {
        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_AUT_CONF", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            AddBaseParams(cmd, codigoPeriodo, tipoComprobanteId, origenId, dia.Date, dia.Date, usuarioId, empresa);
            cmd.Parameters.Add("p_FECHA_COMPROBANTE", OracleDbType.Date).Value = dia.Date;
            cmd.Parameters.Add("p_OBSERVACION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(observacion);
            var pCodigo = cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32, ParameterDirection.Output);
            var pNumero = cmd.Parameters.Add("p_NUMERO_COMPROBANTE", OracleDbType.Varchar2, 20, null, ParameterDirection.Output);
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD_LINEAS", OracleDbType.Int32, ParameterDirection.Output);
            var pTotalDebe = cmd.Parameters.Add("p_TOTAL_DEBE", OracleDbType.Decimal, ParameterDirection.Output);
            var pTotalHaber = cmd.Parameters.Add("p_TOTAL_HABER", OracleDbType.Decimal, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var codigo = CntDb.GetIntOutput(pCodigo);
            var numero = pNumero.Value?.ToString() ?? string.Empty;
            var cantidad = CntDb.GetIntOutput(pCantidad);
            var totalDebe = GetDecimalOutput(pTotalDebe);
            var totalHaber = GetDecimalOutput(pTotalHaber);
            var isSuccess = CntDb.IsSuccessMessage(message);

            if (!isSuccess && message.Contains("no genero lineas", StringComparison.OrdinalIgnoreCase))
            {
                return new CntAutomaticDailyConfirmResponse(dia.Date, 0, string.Empty, 0, 0m, 0m, 0m, "SIN_LINEAS", message);
            }

            if (!isSuccess)
            {
                return new CntAutomaticDailyConfirmResponse(dia.Date, codigo, numero, cantidad, totalDebe, totalHaber, totalDebe - totalHaber, "ERROR", message);
            }

            return new CntAutomaticDailyConfirmResponse(dia.Date, codigo, numero, cantidad, totalDebe, totalHaber, totalDebe - totalHaber, wasReplaced ? "REEMPLAZADO" : "GENERADO", message);
        }
        catch (OracleException ex) when (ex.Number == 8103 && retryAfterObjectRefresh)
        {
            OracleConnection.ClearAllPools();
            return await ConfirmDayAsync(connectionDB, codigoPeriodo, tipoComprobanteId, origenId, dia, usuarioId, empresa, observacion, wasReplaced, retryAfterObjectRefresh: false);
        }
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
