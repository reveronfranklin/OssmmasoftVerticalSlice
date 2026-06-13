using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntPeriodosAdminHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntPeriodoAdminResponse>>> HandleAsync(CntPeriodoAdminGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogView);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntPeriodoAdminResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_PER_ADM_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_ANO_PERIODO", OracleDbType.Int32).Value = CntDb.DbValue(value.AnoPeriodo);
            cmd.Parameters.Add("p_SOLO_ABIERTOS", OracleDbType.Int32).Value = value.SoloAbiertos ? 1 : 0;
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntPeriodoAdminResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntPeriodoMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntPeriodoAdminResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntPeriodoAdminResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SaveCntPeriodoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntPeriodoSaveCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (string.IsNullOrWhiteSpace(value.NombrePeriodo) || value.AnoPeriodo <= 0 || value.NumeroPeriodo <= 0)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "Nombre, ano y numero de periodo son requeridos." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            var isCreate = !value.CodigoPeriodo.HasValue || value.CodigoPeriodo.Value <= 0;
            using var cmd = new OracleCommand(isCreate ? "CNT.SP_CNT_PER_INS" : "CNT.SP_CNT_PER_UPD", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };

            if (!isCreate)
            {
                cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo!.Value;
            }

            CntPeriodoBinder.AddSaveParameters(cmd, value, validation.Empresa);
            OracleParameter? pCodigo = isCreate
                ? cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output)
                : null;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = isCreate ? CntDb.GetIntOutput(pCodigo!) : value.CodigoPeriodo!.Value;

            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class DeleteCntPeriodoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntPeriodoDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        return await CntCatalogAdminSupport.ExecuteDeleteAsync(connectionDB, "CNT.SP_CNT_PER_DEL", value.CodigoPeriodo, "p_CODIGO_PERIODO", validation.Empresa);
    }
}

public class GenerateCntPeriodoYearHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntPeriodoGenerateYearCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.CatalogAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        if (value.AnoPeriodo < 1900 || value.AnoPeriodo > 9999)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "El ano del periodo no es valido." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_PER_GEN_YEAR", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_ANO_PERIODO", OracleDbType.Int32).Value = value.AnoPeriodo;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD_OUT", OracleDbType.Int32, ParameterDirection.Output);
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
