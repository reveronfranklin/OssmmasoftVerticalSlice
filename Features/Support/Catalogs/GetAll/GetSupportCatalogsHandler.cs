using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Support;

public class GetSupportCatalogsHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<SupportCatalogResponse>>> HandleAsync(SupportCatalogGetAllQuery value)
    {
        try
        {
            if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<SupportCatalogResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            var procedure = value.Catalogo?.ToLowerInvariant() switch
            {
                "types" or "tipos" => "SIS.SP_SOP_TYPE_GET_ALL",
                "priorities" or "prioridades" => "SIS.SP_SOP_PRIOR_GET_ALL",
                "statuses" or "estados" => "SIS.SP_SOP_STATUS_GET_ALL",
                "modules" or "modulos" => "SIS.SP_SOP_MODULE_GET_ALL",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(procedure))
            {
                return new ResultDto<List<SupportCatalogResponse>>(null!) { IsValid = false, Message = "Catalogo no soportado." };
            }

            using var cn = connectionDB.GetSisConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            var list = new List<SupportCatalogResponse>();

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new SupportCatalogResponse(
                        reader.SafeGetInt32("ID"),
                        reader.SafeGetString("NOMBRE"),
                        reader.SafeGetString("DESCRIPCION"),
                        reader.SafeGetInt32("ORDEN"),
                        SupportDb.SafeGetFlag(reader, "ACTIVO")
                    ));
                }
            }

            var message = SupportDb.GetMessage(pMessage);
            var isSuccess = SupportDb.IsSuccessMessage(message);
            return new ResultDto<List<SupportCatalogResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<SupportCatalogResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
