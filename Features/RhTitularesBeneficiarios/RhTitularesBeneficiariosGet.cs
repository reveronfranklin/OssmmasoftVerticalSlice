using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhTitularesBeneficiarios;

public class RhTitularesBeneficiariosGetHandler(ConnectionDB db, IConfiguration config)
{
    public async Task<ResultDto<List<RhTitBenResponse>>> HandleAsync(RhTitBenFilter filter)
    {
        ResultDto<List<RhTitBenResponse>> Fail(string message) => new(null!) { IsValid = false, Message = message };
        if (filter.CodigoTipoNomina?.Any(n => n is null) == true)
            return Fail("Los tipos de nomina no pueden ser nulos.");
        var nominas = filter.CodigoTipoNomina?.Select(n => n.CodigoTipoNomina).Distinct().ToArray() ?? [];
        if (nominas.Length == 0 || nominas.Any(n => n <= 0) || nominas.Length > 300)
            return Fail("Seleccione entre 1 y 300 tipos de nomina validos.");
        if (!int.TryParse(config["settings:EmpresaConfig"], out var empresa) || empresa <= 0)
            return Fail("EmpresaConfig debe ser un numero valido.");
        using var cn = db.GetRhConnection();
        try { await cn.OpenAsync(); }
        catch (Exception ex) { return Fail($"Error tecnico al abrir conexion RH: {ex.Message}"); }
        try
        {
            using var cmd = new OracleCommand("RH.SP_RH_TIT_BEN_GET", cn)
            { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_NOMINAS", OracleDbType.Varchar2).Value = string.Join(",", nominas);
            cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
            cmd.Parameters.Add("p_FAMILIARES", OracleDbType.RefCursor, ParameterDirection.Output);
            cmd.Parameters.Add("p_DIRECCIONES", OracleDbType.RefCursor, ParameterDirection.Output);
            using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<RhTitBenResponse>();
            while (await reader.ReadAsync()) rows.Add(RhTitBenDb.Map(reader, rows.Count + 1));
            if (!await reader.NextResultAsync()) return Fail("No se recibieron los contactos de familiares.");
            var familiares = new List<RhTitBenFamiliar>();
            while (await reader.ReadAsync())
            {
                string T(string field) => RhTitBenDb.Text(reader, field) ?? "";
                familiares.Add(new(RhTitBenDb.Number(reader, "CODIGO_PERSONA"),
                    $"{T("NACIONALIDAD")}-{T("CEDULA_FAMILIAR")}", T("NOMBRE"), T("APELLIDO"),
                    RhTitBenDb.Date(reader, "FECHA_NACIMIENTO"), T("PARENTESCO"),
                    RhTitBenDb.Text(reader, "EMAIL"), RhTitBenDb.Text(reader, "NUMERO_COMUNICACION")));
            }
            if (!await reader.NextResultAsync()) return Fail("No se recibieron las direcciones del trabajador.");
            var direcciones = new Dictionary<int, List<RhTitBenDireccion>>();
            while (await reader.ReadAsync())
            {
                var persona = RhTitBenDb.Number(reader, "CODIGO_PERSONA");
                if (!direcciones.TryGetValue(persona, out var list)) direcciones[persona] = list = [];
                list.Add(RhTitBenDb.MapDireccion(reader));
            }
            RhTitBenDb.Enrich(rows, familiares, direcciones);
            return new(rows) { IsValid = true, Page = 1, TotalPage = 1, CantidadRegistros = rows.Count };
        }
        catch (Exception ex) { return Fail($"Error tecnico: {ex.Message}"); }
    }


}

[Authorize]
[ApiController]
[Route("api/RhTitularesBeneficiarios")]
public class RhTitularesBeneficiariosGetController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByTipoNomina")]
    public async Task<IActionResult> GetByTipoNomina(RhTitBenFilter filter)
        => Ok(await new RhTitularesBeneficiariosGetHandler(db, config).HandleAsync(filter));
}
