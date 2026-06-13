using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntRelacionDocumentosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntRelacionDocumentoResponse>>> HandleAsync(CntRelacionDocumentoGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntRelacionDocumentoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_REL_DOC_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_TIPO_DOC_ID", OracleDbType.Int32).Value = CntDb.DbValue(value.TipoDocumentoId);
            cmd.Parameters.Add("p_TIPO_TRANS_ID", OracleDbType.Int32).Value = CntDb.DbValue(value.TipoTransaccionId);
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntRelacionDocumentoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntRelacionDocumentoMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntRelacionDocumentoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntRelacionDocumentoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SaveCntRelacionDocumentoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntRelacionDocumentoSaveCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (value.TipoDocumentoId <= 0 || value.TipoTransaccionId <= 0)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "Tipo de documento y tipo de transaccion son requeridos." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            var isCreate = !value.CodigoRelacionDocumento.HasValue || value.CodigoRelacionDocumento.Value <= 0;
            using var cmd = new OracleCommand(isCreate ? "CNT.SP_CNT_REL_DOC_INS" : "CNT.SP_CNT_REL_DOC_UPD", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };

            if (!isCreate)
            {
                cmd.Parameters.Add("p_CODIGO_REL_DOC", OracleDbType.Int32).Value = value.CodigoRelacionDocumento!.Value;
            }

            CntRelacionDocumentoBinder.AddSaveParameters(cmd, value, validation.Empresa);
            OracleParameter? pCodigo = isCreate
                ? cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output)
                : null;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = isCreate ? CntDb.GetIntOutput(pCodigo!) : value.CodigoRelacionDocumento!.Value;

            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class DeleteCntRelacionDocumentoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntRelacionDocumentoDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        return await CntCatalogAdminSupport.ExecuteDeleteAsync(connectionDB, "CNT.SP_CNT_REL_DOC_DEL", value.CodigoRelacionDocumento, "p_CODIGO_REL_DOC", validation.Empresa);
    }
}
