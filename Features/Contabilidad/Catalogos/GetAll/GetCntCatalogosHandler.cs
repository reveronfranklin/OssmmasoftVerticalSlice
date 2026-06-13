using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntCatalogosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntCatalogResponse>>> HandleAsync(CntCatalogGetAllQuery value)
    {
        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntCatalogResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            var tituloId = value.Catalogo?.ToLowerInvariant() switch
            {
                "tipos-comprobante" or "tipos" => 5,
                "origenes-comprobante" or "origenes" => 1,
                _ => 0
            };

            if (tituloId == 0)
            {
                return new ResultDto<List<CntCatalogResponse>>(null!) { Data = null, IsValid = false, Message = "Catalogo CNT no soportado." };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CAT_DESC_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_TITULO_ID", OracleDbType.Int32).Value = tituloId;
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntCatalogResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new CntCatalogResponse(
                        reader.SafeGetInt32("ID"),
                        reader.SafeGetString("CODIGO"),
                        reader.SafeGetString("DESCRIPCION"),
                        reader.SafeGetString("EXTRA1"),
                        reader.SafeGetString("EXTRA2"),
                        reader.SafeGetString("EXTRA3")));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntCatalogResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntCatalogResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntPeriodosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntPeriodoResponse>>> HandleAsync(CntPeriodoGetAllQuery value)
    {
        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntPeriodoResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_PER_GET_OPEN", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_SOLO_ABIERTOS", OracleDbType.Int32).Value = value.SoloAbiertos ? 1 : 0;
            cmd.Parameters.Add("p_FECHA", OracleDbType.Date).Value = CntDb.DbValue(value.Fecha);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntPeriodoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new CntPeriodoResponse(
                        reader.SafeGetInt32("CODIGO_PERIODO"),
                        reader.SafeGetString("NOMBRE_PERIODO"),
                        CntDb.SafeGetDate(reader, "FECHA_DESDE"),
                        CntDb.SafeGetDate(reader, "FECHA_HASTA"),
                        reader.SafeGetInt32("ANO_PERIODO"),
                        reader.SafeGetInt32("NUMERO_PERIODO"),
                        reader.SafeGetInt32("CERRADO") == 1));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntPeriodoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntPeriodoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SearchCntMayoresHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntMayorResponse>>> HandleAsync(CntMayorSearchQuery value)
    {
        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntMayorResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_MAY_SEARCH", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32).Value = value.PageSize <= 0 ? 20 : value.PageSize;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntMayorResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new CntMayorResponse(
                        reader.SafeGetInt32("CODIGO_MAYOR"),
                        reader.SafeGetString("NUMERO_MAYOR"),
                        reader.SafeGetString("DENOMINACION"),
                        reader.SafeGetString("DESCRIPCION"),
                        reader.SafeGetString("COLUMNA_BALANCE")));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntMayorResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntMayorResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SearchCntAuxiliaresHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntAuxiliarResponse>>> HandleAsync(CntAuxiliarSearchQuery value)
    {
        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntAuxiliarResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_AUX_SEARCH", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoMayor);
            cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32).Value = value.PageSize <= 0 ? 20 : value.PageSize;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntAuxiliarResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new CntAuxiliarResponse(
                        reader.SafeGetInt32("CODIGO_AUXILIAR"),
                        reader.SafeGetInt32("CODIGO_MAYOR"),
                        reader.SafeGetString("SEGMENTO1"),
                        reader.SafeGetString("SEGMENTO2"),
                        reader.SafeGetString("DENOMINACION"),
                        reader.SafeGetString("DESCRIPCION")));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntAuxiliarResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntAuxiliarResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
