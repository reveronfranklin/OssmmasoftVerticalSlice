using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class CloneCntDescriptivasHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntCloneDescriptivasResponse>> HandleAsync(CntCloneDescriptivasCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<CntCloneDescriptivasResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CLONE_DES", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_EMPRESA_ORIGEN", OracleDbType.Int32).Value = value.EmpresaOrigen;
            cmd.Parameters.Add("p_EMPRESA_DESTINO", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            var pTitulos = cmd.Parameters.Add("p_TITULOS", OracleDbType.Int32, ParameterDirection.Output);
            var pDescriptivas = cmd.Parameters.Add("p_DESCRIPTIVAS", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var response = new CntCloneDescriptivasResponse(CntDb.GetIntOutput(pTitulos), CntDb.GetIntOutput(pDescriptivas));

            return new ResultDto<CntCloneDescriptivasResponse>(response) { Data = isSuccess ? response : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntCloneDescriptivasResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class CloneCntPlanCuentasHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntClonePlanCuentasResponse>> HandleAsync(CntClonePlanCuentasCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<CntClonePlanCuentasResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CLONE_PLAN", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_EMPRESA_ORIGEN", OracleDbType.Int32).Value = value.EmpresaOrigen;
            cmd.Parameters.Add("p_EMPRESA_DESTINO", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            var pRubros = cmd.Parameters.Add("p_RUBROS", OracleDbType.Int32, ParameterDirection.Output);
            var pBalances = cmd.Parameters.Add("p_BALANCES", OracleDbType.Int32, ParameterDirection.Output);
            var pMayores = cmd.Parameters.Add("p_MAYORES", OracleDbType.Int32, ParameterDirection.Output);
            var pAuxiliares = cmd.Parameters.Add("p_AUXILIARES", OracleDbType.Int32, ParameterDirection.Output);
            var pRelPuc = cmd.Parameters.Add("p_REL_PUC", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var response = new CntClonePlanCuentasResponse(
                CntDb.GetIntOutput(pRubros),
                CntDb.GetIntOutput(pBalances),
                CntDb.GetIntOutput(pMayores),
                CntDb.GetIntOutput(pAuxiliares),
                CntDb.GetIntOutput(pRelPuc));

            return new ResultDto<CntClonePlanCuentasResponse>(response) { Data = isSuccess ? response : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntClonePlanCuentasResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
