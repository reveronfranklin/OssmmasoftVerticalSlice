using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntSaldosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntSaldoResponse>>> HandleAsync(CntSaldoGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntSaldoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_SAL_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoPeriodo);
            cmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoMayor);
            cmd.Parameters.Add("p_CODIGO_AUX", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoAuxiliar);
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntSaldoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntSaldoMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntSaldoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntSaldoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SaveCntSaldoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntSaldoSaveCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoPeriodo <= 0 || value.CodigoMayor <= 0 || value.CodigoAuxiliar <= 0)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "Periodo, mayor y auxiliar son requeridos." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            var isCreate = !value.CodigoSaldo.HasValue || value.CodigoSaldo.Value <= 0;
            using var cmd = new OracleCommand(isCreate ? "CNT.SP_CNT_SAL_INS" : "CNT.SP_CNT_SAL_UPD", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };

            if (!isCreate)
            {
                cmd.Parameters.Add("p_CODIGO_SALDO", OracleDbType.Int32).Value = value.CodigoSaldo!.Value;
            }

            CntSaldoBinder.AddSaveParameters(cmd, value, validation.Empresa);
            OracleParameter? pCodigo = isCreate
                ? cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output)
                : null;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = isCreate ? CntDb.GetIntOutput(pCodigo!) : value.CodigoSaldo!.Value;

            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class DeleteCntSaldoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntSaldoDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        return await CntCatalogAdminSupport.ExecuteDeleteAsync(connectionDB, "CNT.SP_CNT_SAL_DEL", value.CodigoSaldo, "p_CODIGO_SALDO", validation.Empresa);
    }
}
