using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntAuxiliaresHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntAuxiliarAdminResponse>>> HandleAsync(CntAuxiliarGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntAuxiliarAdminResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_AUX_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoMayor);
            cmd.Parameters.Add("p_SOLO_VIGENTES", OracleDbType.Int32).Value = value.SoloVigentes ? 1 : 0;
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntAuxiliarAdminResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntAuxiliarAdminMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntAuxiliarAdminResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntAuxiliarAdminResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SaveCntAuxiliarHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntAuxiliarSaveCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (value.CodigoMayor <= 0 || string.IsNullOrWhiteSpace(value.Denominacion))
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "El mayor y la denominacion son requeridos." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            var isCreate = !value.CodigoAuxiliar.HasValue || value.CodigoAuxiliar.Value <= 0;
            using var cmd = new OracleCommand(isCreate ? "CNT.SP_CNT_AUX_INS" : "CNT.SP_CNT_AUX_UPD", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };

            if (!isCreate)
            {
                cmd.Parameters.Add("p_CODIGO_AUXILIAR", OracleDbType.Int32).Value = value.CodigoAuxiliar!.Value;
            }

            CntAuxiliarCommandBinder.AddSaveParameters(cmd, value, validation.Empresa);
            OracleParameter? pCodigo = isCreate
                ? cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output)
                : null;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = isCreate ? CntDb.GetIntOutput(pCodigo!) : value.CodigoAuxiliar!.Value;

            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class DeleteCntAuxiliarHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntAuxiliarDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        return await CntCatalogAdminSupport.ExecuteDeleteAsync(connectionDB, "CNT.SP_CNT_AUX_DEL", value.CodigoAuxiliar, "p_CODIGO_AUXILIAR", validation.Empresa);
    }
}

public class GetCntAuxiliarUsedByHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntAuxiliarUsedByQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogView);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_AUX_USED", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_AUXILIAR", OracleDbType.Int32).Value = value.CodigoAuxiliar;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD", OracleDbType.Int32, ParameterDirection.Output);
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
