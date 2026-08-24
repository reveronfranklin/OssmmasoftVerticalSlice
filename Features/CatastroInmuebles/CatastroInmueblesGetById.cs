using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroInmuebles;

public record CatastroInmueblesGetByIdQuery(long CodigoInmueble);

public class CatastroInmueblesGetByIdHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CatastroInmuebleDetalleResponse>> HandleAsync(CatastroInmueblesGetByIdQuery query)
    {
        if (query.CodigoInmueble <= 0)
        {
            return Failure("El código del inmueble debe ser mayor que cero.");
        }

        if (!CatastroInmueblesDb.TryGetEmpresa(config, out var empresa, out var empresaError))
        {
            return Failure(empresaError);
        }

        using var cn = connectionDB.GetCatConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Failure($"Error técnico al abrir conexión CAT: {ex.Message}");
        }

        using var cmd = new OracleCommand("CAT.SP_CAT_INM_GET_ID", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };
        cmd.Parameters.Add("p_CodigoInmueble", OracleDbType.Int64).Value = query.CodigoInmueble;
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var message = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            CatastroInmuebleDetalleResponse? data = null;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    data = CatastroInmueblesDb.MapDetalle(reader);
                }
            }

            var dbMessage = CatastroInmueblesDb.GetMessage(message);
            var isValid = CatastroInmueblesDb.IsSuccessMessage(dbMessage) && data is not null;
            return new ResultDto<CatastroInmuebleDetalleResponse>(data!)
            {
                IsValid = isValid,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return Failure($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<CatastroInmuebleDetalleResponse> Failure(string message) =>
        new(null!) { IsValid = false, Message = message };
}

[ApiController]
[Authorize]
[Route("api/CatastroInmuebles")]
public class CatastroInmueblesGetByIdController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("getById")]
    public async Task<IActionResult> GetById(CatastroInmueblesGetByIdQuery query)
    {
        var result = await new CatastroInmueblesGetByIdHandler(connectionDB, config).HandleAsync(query);
        return Ok(result);
    }
}
