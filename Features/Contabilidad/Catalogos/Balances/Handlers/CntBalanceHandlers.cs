using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntBalancesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntBalanceResponse>>> HandleAsync(CntBalanceGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntBalanceResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_BAL_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_RUBRO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoRubro);
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntBalanceResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntBalanceMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntBalanceResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntBalanceResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SaveCntBalanceHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntBalanceSaveCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (string.IsNullOrWhiteSpace(value.NumeroBalance) || string.IsNullOrWhiteSpace(value.Denominacion))
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "El numero y la denominacion son requeridos." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            var isCreate = !value.CodigoBalance.HasValue || value.CodigoBalance.Value <= 0;
            using var cmd = new OracleCommand(isCreate ? "CNT.SP_CNT_BAL_INS" : "CNT.SP_CNT_BAL_UPD", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };

            if (!isCreate)
            {
                cmd.Parameters.Add("p_CODIGO_BALANCE", OracleDbType.Int32).Value = value.CodigoBalance!.Value;
            }

            cmd.Parameters.Add("p_NUMERO_BALANCE", OracleDbType.Varchar2).Value = value.NumeroBalance.Trim();
            cmd.Parameters.Add("p_DENOMINACION", OracleDbType.Varchar2).Value = value.Denominacion.Trim();
            cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Descripcion);
            cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra1);
            cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra2);
            cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra3);
            cmd.Parameters.Add("p_CODIGO_RUBRO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoRubro);
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            OracleParameter? pCodigo = isCreate
                ? cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output)
                : null;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = isCreate ? CntDb.GetIntOutput(pCodigo!) : value.CodigoBalance!.Value;

            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class DeleteCntBalanceHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntBalanceDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        return await CntCatalogAdminSupport.ExecuteDeleteAsync(connectionDB, "CNT.SP_CNT_BAL_DEL", value.CodigoBalance, "p_CODIGO_BALANCE", validation.Empresa);
    }
}
