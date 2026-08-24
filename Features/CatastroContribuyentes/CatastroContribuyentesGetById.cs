using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroContribuyentes;

public record CatastroContribuyentesGetByIdQuery(long CodigoContribuyente);

public class CatastroContribuyentesGetByIdHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CatastroContribuyenteDetalleResponse>> HandleAsync(CatastroContribuyentesGetByIdQuery query)
    {
        if (query.CodigoContribuyente <= 0) return Failure("El código del contribuyente debe ser mayor que cero.");
        if (!CatastroContribuyentesDb.TryGetEmpresa(config, out var empresa, out var error)) return Failure(error);
        using var cn = connectionDB.GetRmConnection();
        try { await cn.OpenAsync(); } catch (Exception ex) { return Failure($"Error técnico al abrir conexión RM: {ex.Message}"); }

        using var cmd = new OracleCommand("RM.SP_RM_CONTRI_GET_ID", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_CodigoContribuyente", OracleDbType.Int64).Value = query.CodigoContribuyente;
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Contribuyente", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Direcciones", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Comunicaciones", OracleDbType.RefCursor, ParameterDirection.Output);
        var message = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        try
        {
            CatastroContribuyenteResponse? contributor = null;
            var addresses = new List<CatastroContribuyenteDireccionResponse>();
            var communications = new List<CatastroContribuyenteComunicacionResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync()) contributor = CatastroContribuyentesDb.MapContribuyente(reader);
                if (await reader.NextResultAsync()) while (await reader.ReadAsync()) addresses.Add(new(Convert.ToInt64(reader["CODIGO_DIRECCION"]), CatastroContribuyentesDb.Long(reader, "PAIS_ID"), CatastroContribuyentesDb.Long(reader, "ESTADO_ID"), CatastroContribuyentesDb.Long(reader, "MUNICIPIO_ID"), CatastroContribuyentesDb.Long(reader, "CIUDAD_ID"), CatastroContribuyentesDb.Long(reader, "PARROQUIA_ID"), CatastroContribuyentesDb.Long(reader, "SECTOR_ID"), CatastroContribuyentesDb.String(reader, "VIALIDAD"), CatastroContribuyentesDb.String(reader, "VIVIENDA"), CatastroContribuyentesDb.String(reader, "NUMERO_UNIDAD"), CatastroContribuyentesDb.String(reader, "COMPLEMENTO_DIR"), CatastroContribuyentesDb.Long(reader, "PRINCIPAL") == 1));
                if (await reader.NextResultAsync()) while (await reader.ReadAsync()) communications.Add(new(Convert.ToInt64(reader["CODIGO_COMUNICACION"]), Convert.ToInt64(reader["TIPO_COMUNICACION_ID"]), CatastroContribuyentesDb.String(reader, "CODIGO_AREA"), CatastroContribuyentesDb.String(reader, "LINEA_COMUNICACION") ?? string.Empty, CatastroContribuyentesDb.String(reader, "EXTENSION"), CatastroContribuyentesDb.Long(reader, "PRINCIPAL") == 1));
            }
            var dbMessage = CatastroContribuyentesDb.Message(message);
            var valid = contributor is not null && CatastroContribuyentesDb.Success(dbMessage);
            var data = contributor is null ? null : new CatastroContribuyenteDetalleResponse(contributor, addresses, communications);
            return new(data!) { IsValid = valid, Message = dbMessage };
        }
        catch (Exception ex) { return Failure($"Error técnico: {ex.Message}"); }
    }
    private static ResultDto<CatastroContribuyenteDetalleResponse> Failure(string message) => new(null!) { IsValid = false, Message = message };
}

[ApiController]
[Authorize]
[Route("api/CatastroContribuyentes")]
public class CatastroContribuyentesGetByIdController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("getById")]
    public async Task<IActionResult> GetById(CatastroContribuyentesGetByIdQuery query) => Ok(await new CatastroContribuyentesGetByIdHandler(connectionDB, config).HandleAsync(query));
}
