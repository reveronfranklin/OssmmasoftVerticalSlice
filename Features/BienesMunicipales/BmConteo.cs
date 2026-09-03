using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmConteo")]
public class BmConteoController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONTEO_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapConteo));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmConteoUpsertRequest request)
    {
        return Ok(await MutateConteoAsync("BMC.SP_BM_CONTEO_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmConteoUpsertRequest request)
    {
        return Ok(await MutateConteoAsync("BMC.SP_BM_CONTEO_UPD", request));
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(BmConteoDeleteRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONTEO_DEL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapConteo));
    }

    [HttpPost("CerrarConteo")]
    public async Task<IActionResult> CerrarConteo(BmConteoCerrarRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONTEO_CERRAR", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_Comentario", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Comentario);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapConteo));
    }

    private async Task<ResultDto<List<BmConteoResponse>>> MutateConteoAsync(string procedureName, BmConteoUpsertRequest request)
    {
        var validationError = ValidateUpsert(request);
        if (validationError is not null)
        {
            return BmDb.InvalidList<BmConteoResponse>(validationError);
        }

        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmConteoResponse>(error);
        }

        int? cantidadConteos = null;
        if (procedureName.EndsWith("_INS", StringComparison.Ordinal))
        {
            var cantidadResult = await GetCantidadConteosAsync(empresa, request.ConteoId);
            if (!cantidadResult.IsValid)
            {
                return BmDb.InvalidList<BmConteoResponse>(cantidadResult.Message);
            }

            cantidadConteos = cantidadResult.Data;
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return BmDb.InvalidList<BmConteoResponse>(openError);

        if (procedureName.EndsWith("_INS", StringComparison.Ordinal))
        {
            var selectedIcps = request.ListIcpSeleccionado!
                .Where(item => item.CodigoIcp > 0)
                .Select(item => item.CodigoIcp)
                .Distinct()
                .ToArray();
            var targetValidation = await ValidateOpenIcpsAsync(
                cn,
                "BMC.BM_DIR_BIEN",
                selectedIcps,
                "BMC destino");
            if (targetValidation is not null)
            {
                return BmDb.InvalidList<BmConteoResponse>(targetValidation);
            }
        }

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Titulo);
        cmd.Parameters.Add("p_Comentario", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Comentario);
        cmd.Parameters.Add("p_CodigoPersonaResp", OracleDbType.Int32).Value = request.CodigoPersonaResponsable;
        cmd.Parameters.Add("p_ConteoId", OracleDbType.Int32).Value = request.ConteoId;
        cmd.Parameters.Add("p_Fecha", OracleDbType.Date).Value = BmDb.DbValue(request.Fecha);
        cmd.Parameters.Add("p_CodigosIcp", OracleDbType.Varchar2).Value = BmDb.DbValue(BmDb.ToIcpCsv(request.ListIcpSeleccionado));
        if (cantidadConteos.HasValue)
        {
            cmd.Parameters.Add("p_CantidadConteos", OracleDbType.Int32).Value = cantidadConteos.Value;
        }
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        try
        {
            return await BmDb.ExecuteListAsync(cmd, MapConteo);
        }
        catch (Exception ex)
        {
            return BmDb.InvalidList<BmConteoResponse>($"Error tecnico al ejecutar {procedureName}: {ex.Message}");
        }
    }

    private async Task<ResultDto<int>> GetCantidadConteosAsync(int empresa, int conteoId)
    {
        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = openError };
        }

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_DESC_GET_TIT", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_TituloId", OracleDbType.Int32).Value = 7;
        cmd.Parameters.Add("p_DescripcionId", OracleDbType.Int32).Value = conteoId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        try
        {
            var result = await BmDb.ExecuteListAsync(cmd, reader => reader.SafeGetString("DESCRIPCION"));
            var descripcion = result.Data?.SingleOrDefault();
            if (!result.IsValid || !int.TryParse(descripcion, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cantidad) || cantidad < 1)
            {
                return new ResultDto<int>(0)
                {
                    IsValid = false,
                    Message = result.IsValid ? "Cantidad de conteos invalida." : result.Message
                };
            }

            return new ResultDto<int>(cantidad) { IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = $"Error tecnico al consultar cantidad de conteos: {ex.Message}" };
        }
    }

    private static async Task<string?> ValidateOpenIcpsAsync(
        OracleConnection cn,
        string tableName,
        IReadOnlyCollection<int> selectedIcps,
        string connectionName)
    {
        var parameters = selectedIcps.Select((_, index) => $":p{index}").ToArray();
        using var cmd = new OracleCommand(
            $"SELECT DISTINCT CODIGO_ICP FROM {tableName} WHERE CODIGO_ICP IN ({string.Join(",", parameters)})",
            cn)
        {
            BindByName = true
        };
        var index = 0;
        foreach (var codigoIcp in selectedIcps)
        {
            cmd.Parameters.Add($"p{index++}", OracleDbType.Int32).Value = codigoIcp;
        }

        var found = new HashSet<int>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        var missing = selectedIcps.Where(codigoIcp => !found.Contains(codigoIcp)).OrderBy(value => value).ToArray();
        return missing.Length == 0
            ? null
            : $"Los ICP {string.Join(", ", missing)} no existen en BM_DIR_BIEN de {connectionName}. Ejecute la replica y seleccione nuevamente los ICP.";
    }

    private static string? ValidateUpsert(BmConteoUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo))
        {
            return "El titulo del conteo es requerido.";
        }

        if (request.CodigoPersonaResponsable <= 0)
        {
            return "Debe seleccionar una persona responsable.";
        }

        if (request.ConteoId <= 0)
        {
            return "Debe seleccionar la cantidad de conteos.";
        }

        if (request.ListIcpSeleccionado is null || request.ListIcpSeleccionado.All(icp => icp.CodigoIcp <= 0))
        {
            return "Debe seleccionar al menos un ICP.";
        }

        return null;
    }

    internal static BmConteoResponse MapConteo(IDataReader reader)
    {
        var fecha = BmDb.GetDate(reader, "FECHA");
        var totalCantidad = reader.SafeGetInt32("TOTAL_CANTIDAD");
        var totalContada = reader.SafeGetInt32("TOTAL_CANTIDAD_CONTADA");
        var totalDiferencia = reader.SafeGetInt32("TOTAL_DIFERENCIA");
        var codigo = reader.SafeGetInt32("CODIGO_BM_CONTEO");

        return new BmConteoResponse(
            codigo,
            reader.SafeGetString("TITULO"),
            reader.SafeGetString("COMENTARIO"),
            reader.SafeGetInt32("CODIGO_PERSONA_RESPONSABLE"),
            reader.SafeGetString("NOMBRE_PERSONA_RESPONSABLE"),
            reader.SafeGetInt32("CONTEO_ID"),
            fecha,
            BmDb.ToDateString(fecha),
            BmDb.ToFechaDto(fecha),
            new List<BmConteoDetalleResumenResponse>
            {
                new(codigo, reader.SafeGetInt32("CONTEO"), totalCantidad, totalContada, totalDiferencia)
            },
            BmDb.ToDateString(fecha),
            totalCantidad,
            totalContada,
            totalDiferencia
        );
    }
}
