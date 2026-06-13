using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntMayorAnaliticoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntMayorAnaliticoResponse>>> HandleAsync(CntMayorAnaliticoQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var access = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!access.IsValid)
            {
                return new ResultDto<List<CntMayorAnaliticoResponse>>(null!) { Data = null, IsValid = false, Message = access.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ReportView);
            if (!permission.IsValid)
            {
                return new ResultDto<List<CntMayorAnaliticoResponse>>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntMayorAnaliticoResponse>>(null!) { Data = null, IsValid = false, Message = errorMessage };
            }

            var pageSize = value.PageSize <= 0 ? 10 : value.PageSize;
            var pageNumber = value.PageNumber <= 0 ? 1 : value.PageNumber;

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_RPT_MAY_ANA", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32).Value = pageSize;
            cmd.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32).Value = pageNumber;
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoPeriodo);
            cmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoMayor);
            cmd.Parameters.Add("p_CODIGO_AUXILIAR", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoAuxiliar);
            cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = CntDb.DbValue(value.FechaDesde);
            cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = CntDb.DbValue(value.FechaHasta);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
            var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

            var list = new List<CntMayorAnaliticoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(MapMayorAnalitico(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);

            return new ResultDto<List<CntMayorAnaliticoResponse>>(list)
            {
                Data = isSuccess ? list : null,
                IsValid = isSuccess,
                Message = message,
                Page = pageNumber,
                TotalPage = CntDb.GetIntOutput(pTotalPages),
                CantidadRegistros = CntDb.GetIntOutput(pTotalRecords)
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntMayorAnaliticoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    private static CntMayorAnaliticoResponse MapMayorAnalitico(IDataReader reader)
    {
        var monto = reader.SafeGetDecimal("MONTO");

        return new CntMayorAnaliticoResponse(
            CntDb.SafeGetNullableInt(reader, "CODIGO_COMPROBANTE"),
            reader.SafeGetInt32("CODIGO_MAYOR"),
            reader.SafeGetInt32("CODIGO_AUXILIAR"),
            reader.SafeGetString("CODIGO_CUENTA"),
            reader.SafeGetString("DENOMINACION_CUENTA"),
            reader.SafeGetString("NUMERO_COMPROBANTE"),
            SafeGetNullableDate(reader, "FECHA_COMPROBANTE"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("REFERENCIA1"),
            reader.SafeGetString("REFERENCIA2"),
            monto,
            monto < 0 ? Math.Abs(monto) : 0m,
            monto > 0 ? monto : 0m,
            reader.SafeGetInt32("CODIGO_EMPRESA"));
    }

    private static DateTime? SafeGetNullableDate(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

public class GetCntMovimientoAuxiliarHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntMovimientoAuxiliarResponse>>> HandleAsync(CntMovimientoAuxiliarQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var access = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!access.IsValid)
            {
                return new ResultDto<List<CntMovimientoAuxiliarResponse>>(null!) { Data = null, IsValid = false, Message = access.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ReportView);
            if (!permission.IsValid)
            {
                return new ResultDto<List<CntMovimientoAuxiliarResponse>>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntMovimientoAuxiliarResponse>>(null!) { Data = null, IsValid = false, Message = errorMessage };
            }

            var pageSize = value.PageSize <= 0 ? 10 : value.PageSize;
            var pageNumber = value.PageNumber <= 0 ? 1 : value.PageNumber;

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_RPT_MOV_AUX", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32).Value = pageSize;
            cmd.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32).Value = pageNumber;
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoPeriodo);
            cmd.Parameters.Add("p_CODIGO_AUXILIAR", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoAuxiliar);
            cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = CntDb.DbValue(value.FechaDesde);
            cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = CntDb.DbValue(value.FechaHasta);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
            var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

            var list = new List<CntMovimientoAuxiliarResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(MapMovimientoAuxiliar(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);

            return new ResultDto<List<CntMovimientoAuxiliarResponse>>(list)
            {
                Data = isSuccess ? list : null,
                IsValid = isSuccess,
                Message = message,
                Page = pageNumber,
                TotalPage = CntDb.GetIntOutput(pTotalPages),
                CantidadRegistros = CntDb.GetIntOutput(pTotalRecords)
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntMovimientoAuxiliarResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    private static CntMovimientoAuxiliarResponse MapMovimientoAuxiliar(IDataReader reader)
    {
        var monto = reader.SafeGetDecimal("MONTO");

        return new CntMovimientoAuxiliarResponse(
            CntDb.SafeGetNullableInt(reader, "CODIGO_COMPROBANTE"),
            reader.SafeGetInt32("CODIGO_AUXILIAR"),
            reader.SafeGetString("NUMERO_CONTABLE"),
            reader.SafeGetString("NOMBRE_AUXILIAR"),
            reader.SafeGetString("NUMERO_COMPROBANTE"),
            SafeGetNullableDate(reader, "FECHA_COMPROBANTE"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("REFERENCIA1"),
            reader.SafeGetString("REFERENCIA2"),
            monto,
            monto < 0 ? Math.Abs(monto) : 0m,
            monto > 0 ? monto : 0m,
            reader.SafeGetInt32("CODIGO_EMPRESA"));
    }

    private static DateTime? SafeGetNullableDate(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}
