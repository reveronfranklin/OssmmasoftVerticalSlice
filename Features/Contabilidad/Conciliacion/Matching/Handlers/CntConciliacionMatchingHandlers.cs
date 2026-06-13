using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class MatchCntConciliacionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntConciliacionMatchCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoConciliacion <= 0)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "CodigoConciliacion es requerido." };
        }

        if (!value.CodigoDetalleEdoCta.HasValue && !value.CodigoDetalleLibro.HasValue)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "Debe seleccionar un movimiento de banco, libro o ambos." };
        }

        var precloseValidation = await CntConciliacionMatchingPrecloseGuard.ValidateByConciliacionAsync(connectionDB, config, value.UsuarioId, value.CodigoConciliacion, validation.Empresa);
        if (!precloseValidation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = precloseValidation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_MATCH", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionMatchingBinder.AddMatchParameters(cmd, value, validation.Empresa);
            var pCodigoTmp = cmd.Parameters.Add("p_CODIGO_TMP", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = CntDb.GetIntOutput(pCodigoTmp);
            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class UnmatchCntConciliacionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntConciliacionUnmatchCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoTmpConciliacion <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "CodigoTmpConciliacion es requerido." };
        }

        var precloseValidation = await CntConciliacionMatchingPrecloseGuard.ValidateByTemporalAsync(connectionDB, config, value.UsuarioId, value.CodigoTmpConciliacion, validation.Empresa);
        if (!precloseValidation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = precloseValidation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_UNMATCH", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionMatchingBinder.AddUnmatchParameters(cmd, value, validation.Empresa);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<string>("OK") { Data = isSuccess ? "OK" : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class MatchMultiCntConciliacionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntConciliacionMatchMultiCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoConciliacion <= 0)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "CodigoConciliacion es requerido." };
        }

        var bancoCount = value.CodigosDetalleEdoCta?.Count ?? 0;
        var libroCount = value.CodigosDetalleLibro?.Count ?? 0;
        if (bancoCount + libroCount < 2)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "Debe seleccionar al menos dos movimientos." };
        }

        var precloseValidation = await CntConciliacionMatchingPrecloseGuard.ValidateByConciliacionAsync(connectionDB, config, value.UsuarioId, value.CodigoConciliacion, validation.Empresa);
        if (!precloseValidation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = precloseValidation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_MATCH_MULTI", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionMatchingBinder.AddMatchMultiParameters(cmd, value, validation.Empresa);
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var count = CntDb.GetIntOutput(pCantidad);
            return new ResultDto<int>(count) { Data = isSuccess ? count : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

internal static class CntConciliacionMatchingPrecloseGuard
{
    public static async Task<ResultDto<string>> ValidateByConciliacionAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        int usuarioId,
        int codigoConciliacion,
        int empresa)
    {
        using var cn = connectionDB.GetCntConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(
            "SELECT COUNT(1) FROM CNT.CNT_CONCILIACIONES WHERE CODIGO_CONCILIACION = :codigo AND CODIGO_EMPRESA = :empresa AND FECHA_PRECIERRE IS NOT NULL AND FECHA_CIERRE IS NULL",
            cn);
        cmd.Parameters.Add("codigo", OracleDbType.Int32).Value = codigoConciliacion;
        cmd.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;

        return await ValidatePermissionAsync(connectionDB, config, usuarioId, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
    }

    public static async Task<ResultDto<string>> ValidateByTemporalAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        int usuarioId,
        int codigoTmpConciliacion,
        int empresa)
    {
        using var cn = connectionDB.GetCntConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(
            @"SELECT COUNT(1)
                FROM CNT.CNT_TMP_CONCILIACION tmp
                INNER JOIN CNT.CNT_CONCILIACIONES c
                   ON c.CODIGO_CONCILIACION = tmp.CODIGO_CONCILIACION
                  AND c.CODIGO_EMPRESA = tmp.CODIGO_EMPRESA
               WHERE tmp.CODIGO_TMP_CONCILIACION = :codigo
                 AND tmp.CODIGO_EMPRESA = :empresa
                 AND c.FECHA_PRECIERRE IS NOT NULL
                 AND c.FECHA_CIERRE IS NULL",
            cn);
        cmd.Parameters.Add("codigo", OracleDbType.Int32).Value = codigoTmpConciliacion;
        cmd.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;

        return await ValidatePermissionAsync(connectionDB, config, usuarioId, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
    }

    private static async Task<ResultDto<string>> ValidatePermissionAsync(ConnectionDB connectionDB, IConfiguration config, int usuarioId, int precloseCount)
    {
        if (precloseCount <= 0)
        {
            return new ResultDto<string>("OK") { Data = "OK", IsValid = true, Message = "OK" };
        }

        var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, usuarioId, CntSecurity.ConciliacionEditPreclose);
        return permission.IsValid
            ? new ResultDto<string>("OK") { Data = "OK", IsValid = true, Message = "OK" }
            : new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = permission.Message };
    }
}

public class GetCntConciliacionSuggestionsHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntConciliacionSuggestionResponse>>> HandleAsync(CntConciliacionSuggestionGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntConciliacionSuggestionResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoConciliacion <= 0)
        {
            return new ResultDto<List<CntConciliacionSuggestionResponse>>(null!) { Data = null, IsValid = false, Message = "CodigoConciliacion es requerido." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_SUG_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionMatchingBinder.AddSuggestionParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntConciliacionSuggestionResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntConciliacionMatchingMapper.MapSuggestion(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntConciliacionSuggestionResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntConciliacionSuggestionResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
