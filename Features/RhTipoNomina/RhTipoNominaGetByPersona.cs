using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhTipoNomina;

public record RhTipoNominaGetByPersonaQuery(
    int CodigoPersona,
    DateTime Desde,
    DateTime Hasta,
    bool SinRestriccionFecha = false
);

public record RhTipoNominaByPersonaResponse(
    int CodigoTipoNomina,
    string Descripcion,
    string SiglasTipoNomina,
    int FrecuenciaPagoId,
    string FrecuenciaPago,
    decimal SueldoMinimo
);

public class RhTipoNominaGetByPersonaHandler(ConnectionDB _connectionDB, IConfiguration _configuration)
{
    public async Task<ResultDto<List<RhTipoNominaByPersonaResponse>>> HandleAsync(
        RhTipoNominaGetByPersonaQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Desde == default)
        {
            return BuildInvalidResult("Fecha Desde Invalida");
        }

        if (query.Hasta == default)
        {
            return BuildInvalidResult("Fecha Hasta Invalida");
        }

        if (query.Hasta.Date < query.Desde.Date)
        {
            return BuildInvalidResult("Fecha Hasta no puede ser menor que Fecha Desde");
        }

        if (!int.TryParse(_configuration["settings:EmpresaConfig"], out var empresa) || empresa <= 0)
            return BuildInvalidResult("EmpresaConfig debe ser un numero valido.");

        using var cn = _connectionDB.GetRhConnection();

        try
        {
            await cn.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return new ResultDto<List<RhTipoNominaByPersonaResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión RH: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("RH.SP_RH_TN_BY_PERSONA_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;
        cmd.Parameters.Add("p_empresa", OracleDbType.Int32).Value = empresa;

        cmd.Parameters.Add("p_codigo_persona", OracleDbType.Int32).Value = query.CodigoPersona;
        cmd.Parameters.Add("p_sin_fecha", OracleDbType.Int32).Value = query.SinRestriccionFecha ? 1 : 0;
        cmd.Parameters.Add("p_desde", OracleDbType.Date).Value = query.Desde.Date;
        cmd.Parameters.Add("p_hasta", OracleDbType.Date).Value = query.Hasta.Date;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<RhTipoNominaByPersonaResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(new RhTipoNominaByPersonaResponse(
                        reader.SafeGetInt32("CODIGO_TIPO_NOMINA"),
                        reader.SafeGetString("DESCRIPCION"),
                        reader.SafeGetString("SIGLAS_TIPO_NOMINA"),
                        reader.SafeGetInt32("FRECUENCIA_PAGO_ID"),
                        reader.SafeGetString("FRECUENCIA_PAGO"),
                        reader.SafeGetDecimal("SUELDO_MINIMO")
                    ));
                }
            }

            var dbMessage = GetMessage(pMessage);
            var isSuccess = IsSuccessMessage(dbMessage);
            var totalRecords = GetIntOutput(pTotalRecords);

            return new ResultDto<List<RhTipoNominaByPersonaResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = totalRecords == 0 ? list.Count : totalRecords,
                IsValid = isSuccess,
                LinkData = string.Empty,
                Message = isSuccess ? string.Empty : dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<RhTipoNominaByPersonaResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }

    private static ResultDto<List<RhTipoNominaByPersonaResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<RhTipoNominaByPersonaResponse>>(new List<RhTipoNominaByPersonaResponse>())
        {
            Data = new List<RhTipoNominaByPersonaResponse>(),
            IsValid = false,
            LinkData = string.Empty,
            Message = message,
            CantidadRegistros = 0
        };
    }

    private static string GetMessage(OracleParameter parameter, string defaultMessage = "Success")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    private static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : Convert.ToInt32(parameter.Value.ToString());
    }

    private static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }
}

[Authorize]
[ApiController]
[Route("api/RhTipoNomina")]
public class RhTipoNominaGetByPersonaController(ConnectionDB _connectionDB, IConfiguration _configuration) : ControllerBase
{
    [HttpPost]
    [Route("GetTipoNominaByCodigoPersona")]
    public async Task<IActionResult> GetTipoNominaByCodigoPersona(RhTipoNominaGetByPersonaQuery value, CancellationToken cancellationToken)
    {
        var handler = new RhTipoNominaGetByPersonaHandler(_connectionDB, _configuration);
        var result = await handler.HandleAsync(value, cancellationToken);

        return Ok(result);
    }
}
