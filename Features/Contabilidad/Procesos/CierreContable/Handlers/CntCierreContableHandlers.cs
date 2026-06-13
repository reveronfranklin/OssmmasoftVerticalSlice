using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntCierrePeriodosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntCierrePeriodoResponse>>> HandleAsync(CntCierrePeriodoGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CierreView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntCierrePeriodoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CIE_PER_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_ANO_PERIODO", OracleDbType.Int32).Value = CntDb.DbValue(value.AnoPeriodo);
            cmd.Parameters.Add("p_SOLO_PEND", OracleDbType.Int32).Value = value.SoloPendientes ? 1 : 0;
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntCierrePeriodoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntCierreContableMapper.MapPeriodo(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntCierrePeriodoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntCierrePeriodoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntCierreModificacionesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntCierreModificacionesResponse>> HandleAsync(CntCierreModificacionesQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CierreView);
        if (!validation.IsValid)
        {
            return new ResultDto<CntCierreModificacionesResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoPeriodo <= 0)
        {
            return new ResultDto<CntCierreModificacionesResponse>(null!) { Data = null, IsValid = false, Message = "CodigoPeriodo es requerido." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CIE_MOD_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var data = new CntCierreModificacionesResponse(value.CodigoPeriodo, CntDb.GetIntOutput(pCantidad));

            return new ResultDto<CntCierreModificacionesResponse>(data) { Data = isSuccess ? data : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntCierreModificacionesResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class PrecierreCntContableHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public Task<ResultDto<CntCierreActionResponse>> HandleAsync(CntCierreActionCommand value, ClaimsPrincipal user, HttpRequest request) =>
        CntCierreActionSupport.ExecuteAsync(
            connectionDB,
            config,
            user,
            request,
            value,
            CntSecurity.CierrePrecierre,
            "CNT.SP_CNT_CIE_PRE",
            "PRECIERRE");
}

public class CierreCntContableHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public Task<ResultDto<CntCierreActionResponse>> HandleAsync(CntCierreActionCommand value, ClaimsPrincipal user, HttpRequest request) =>
        CntCierreActionSupport.ExecuteAsync(
            connectionDB,
            config,
            user,
            request,
            value,
            CntSecurity.CierreCierre,
            "CNT.SP_CNT_CIE_CER",
            "CERRADO");
}

public class ReversoCntContableHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public Task<ResultDto<CntCierreActionResponse>> HandleAsync(CntCierreActionCommand value, ClaimsPrincipal user, HttpRequest request) =>
        CntCierreActionSupport.ExecuteAsync(
            connectionDB,
            config,
            user,
            request,
            value,
            CntSecurity.CierreReverso,
            "CNT.SP_CNT_CIE_REV",
            "ABIERTO");
}

internal static class CntCierreActionSupport
{
    public static async Task<ResultDto<CntCierreActionResponse>> ExecuteAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        ClaimsPrincipal user,
        HttpRequest request,
        CntCierreActionCommand value,
        string permission,
        string procedureName,
        string targetState)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, permission);
        if (!validation.IsValid)
        {
            return new ResultDto<CntCierreActionResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoPeriodo <= 0)
        {
            return new ResultDto<CntCierreActionResponse>(null!) { Data = null, IsValid = false, Message = "CodigoPeriodo es requerido." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(procedureName, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            var pSaldos = cmd.Parameters.Add(procedureName.EndsWith("_PRE", StringComparison.OrdinalIgnoreCase) ? "p_TMP_SALDOS_OUT" : "p_SALDOS_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pAnalitico = cmd.Parameters.Add(procedureName.EndsWith("_PRE", StringComparison.OrdinalIgnoreCase) ? "p_TMP_ANA_OUT" : "p_HIST_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var data = new CntCierreActionResponse(
                value.CodigoPeriodo,
                targetState,
                message,
                CntDb.GetIntOutput(pSaldos),
                CntDb.GetIntOutput(pAnalitico));

            return new ResultDto<CntCierreActionResponse>(data) { Data = isSuccess ? data : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntCierreActionResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
