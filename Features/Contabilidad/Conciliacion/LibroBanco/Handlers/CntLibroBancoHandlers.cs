using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntLibrosBancoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntLibroBancoResponse>>> HandleAsync(CntLibroBancoGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntLibroBancoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_LIB_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntLibroBancoBinder.AddGetParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntLibroBancoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntLibroBancoMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntLibroBancoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntLibroBancoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntLibroBancoDetallesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntLibroBancoDetalleResponse>>> HandleAsync(CntLibroBancoDetalleGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntLibroBancoDetalleResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_LIB_DET_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntLibroBancoBinder.AddDetailParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntLibroBancoDetalleResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntLibroBancoMapper.MapDetalle(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntLibroBancoDetalleResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntLibroBancoDetalleResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GenerateCntLibroBancoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntLibroBancoGenerateResponse>> HandleAsync(CntLibroBancoGenerateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<CntLibroBancoGenerateResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_LIB_GEN", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntLibroBancoBinder.AddGenerateParameters(cmd, value, validation.Empresa);
            var pLibros = cmd.Parameters.Add("p_CANTIDAD_LIBROS_OUT", OracleDbType.Int32, ParameterDirection.InputOutput);
            pLibros.Value = 0;
            var pDetalles = cmd.Parameters.Add("p_CANTIDAD_DET_OUT", OracleDbType.Int32, ParameterDirection.InputOutput);
            pDetalles.Value = 0;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var result = new CntLibroBancoGenerateResponse(CntDb.GetIntOutput(pLibros), CntDb.GetIntOutput(pDetalles));
            return new ResultDto<CntLibroBancoGenerateResponse>(result) { Data = isSuccess ? result : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntLibroBancoGenerateResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
