using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntConciliacionesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntConciliacionResponse>>> HandleAsync(CntConciliacionGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntConciliacionResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionBinder.AddGetParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntConciliacionResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntConciliacionMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntConciliacionResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntConciliacionResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntConciliacionByIdHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntConciliacionResponse>> HandleAsync(CntConciliacionGetByIdQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<CntConciliacionResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoConciliacion <= 0)
        {
            return new ResultDto<CntConciliacionResponse>(null!) { Data = null, IsValid = false, Message = "CodigoConciliacion es requerido." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_GET_ID", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            CntConciliacionResponse? conciliacion = null;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    conciliacion = CntConciliacionMapper.Map(reader);
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<CntConciliacionResponse>(conciliacion!) { Data = isSuccess ? conciliacion : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntConciliacionResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class CreateCntConciliacionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntConciliacionCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoPeriodo <= 0 || value.CodigoCuentaBanco <= 0)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "Periodo y cuenta bancaria son requeridos." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_INS", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionBinder.AddCreateParameters(cmd, value, validation.Empresa);
            var pCodigoOut = cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = CntDb.GetIntOutput(pCodigoOut);
            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class PrecloseCntConciliacionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntConciliacionPrecloseCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoConciliacion <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "CodigoConciliacion es requerido." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_PRE", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionBinder.AddPrecloseParameters(cmd, value, validation.Empresa);
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

public class CloseCntConciliacionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntConciliacionCloseCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoConciliacion <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "CodigoConciliacion es requerido." };
        }

        if (value.ForzarDiferencia)
        {
            var forcePermission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ConciliacionForceClose);
            if (!forcePermission.IsValid)
            {
                return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = forcePermission.Message };
            }
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_CLOSE", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionBinder.AddCloseParameters(cmd, value, validation.Empresa);
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

public class ReverseCntConciliacionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntConciliacionReverseCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoConciliacion <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "CodigoConciliacion es requerido." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_REV", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionBinder.AddReverseParameters(cmd, value, validation.Empresa);
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
