using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntConciliacionBancoMovimientosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntConciliacionBancoMovimientoResponse>>> HandleAsync(CntConciliacionDetalleGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        return await ExecuteAsync(value, user, request, "CNT.SP_CNT_CONC_BCO_GET", CntConciliacionDetalleMapper.MapBanco);
    }

    private async Task<ResultDto<List<CntConciliacionBancoMovimientoResponse>>> ExecuteAsync(
        CntConciliacionDetalleGetQuery value,
        ClaimsPrincipal user,
        HttpRequest request,
        string procedure,
        Func<IDataReader, CntConciliacionBancoMovimientoResponse> mapper)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntConciliacionBancoMovimientoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionDetalleBinder.AddMovimientoParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntConciliacionBancoMovimientoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(mapper(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntConciliacionBancoMovimientoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntConciliacionBancoMovimientoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntConciliacionLibroMovimientosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntConciliacionLibroMovimientoResponse>>> HandleAsync(CntConciliacionDetalleGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntConciliacionLibroMovimientoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_LIB_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionDetalleBinder.AddMovimientoParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntConciliacionLibroMovimientoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntConciliacionDetalleMapper.MapLibro(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntConciliacionLibroMovimientoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntConciliacionLibroMovimientoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntConciliacionTemporalesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntConciliacionTemporalResponse>>> HandleAsync(CntConciliacionTemporalGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntConciliacionTemporalResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CONC_TMP_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntConciliacionDetalleBinder.AddTemporalParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntConciliacionTemporalResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntConciliacionDetalleMapper.MapTemporal(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntConciliacionTemporalResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntConciliacionTemporalResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
