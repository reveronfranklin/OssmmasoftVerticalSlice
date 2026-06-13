using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntEstadosCuentaHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntEstadoCuentaResponse>>> HandleAsync(CntEstadosCuentaGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntEstadoCuentaResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_EDO_CTA_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntEstadosCuentaBinder.AddGetParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntEstadoCuentaResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntEstadosCuentaMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntEstadoCuentaResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntEstadoCuentaResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntEstadoCuentaDetallesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntEstadoCuentaDetalleResponse>>> HandleAsync(CntEstadoCuentaDetalleGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntEstadoCuentaDetalleResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_EDO_CTA_DET_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntEstadosCuentaBinder.AddDetailParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntEstadoCuentaDetalleResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntEstadosCuentaMapper.MapDetalle(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntEstadoCuentaDetalleResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntEstadoCuentaDetalleResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
