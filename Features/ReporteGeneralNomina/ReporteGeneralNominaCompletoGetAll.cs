using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.ReporteGeneralNomina;

// Request
public record ReporteGeneralNominaCompletoGetAllQuery(
    int p_tipo_nomina,
    int codigo_empresa,
    DateTime p_fecha_pago,
    int p_tipo_generacion,
    int? p_codigo_periodo,
    string? p_cedula
);

// Response
public record GetReporteGeneralNominaCompletoGetAllResponse(
    GetReporteGeneralNominaPeriodoGetByCodigoResponse? Periodo,
    List<GetReporteGeneralNominaGetAllResponse> General,
    List<GetReporteGeneralNominaDetalleGetAllResponse> Detalle,
    List<GetReporteGeneralNominaFirmaGetAllResponse> Firma
);

public class GetReporteGeneralNominaCompletoGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<GetReporteGeneralNominaCompletoGetAllResponse>> HandleAsync(ReporteGeneralNominaCompletoGetAllQuery value)
    {
        var generalHandler = new GetReporteGeneralNominaGetAllHandler(_connectionDB);
        var detalleHandler = new GetReporteGeneralNominaDetalleGetAllHandler(_connectionDB);
        var firmaHandler = new GetReporteGeneralNominaFirmaGetAllHandler(_connectionDB);
        var periodoHandler = new GetReporteGeneralNominaPeriodoGetByCodigoHandler(_connectionDB);
        GetReporteGeneralNominaPeriodoGetByCodigoResponse? periodo = null;
        var periodoRecords = 0;

        if (value.p_codigo_periodo.HasValue)
        {
            var periodoResult = await periodoHandler.HandleAsync(new ReporteGeneralNominaPeriodoGetByCodigoQuery(
                value.p_codigo_periodo.Value
            ));

            if (!periodoResult.IsValid)
            {
                return BuildInvalidResult(periodoResult.Message);
            }

            periodo = periodoResult.Data;
            periodoRecords = periodoResult.CantidadRegistros;
        }

        var effectiveValue = BuildEffectiveQuery(value, periodo);

        if (!TryBuildReporteParameters(effectiveValue, out var reporteParameters, out var parameterError))
        {
            return BuildInvalidResult(parameterError ?? "Parametros invalidos para el reporte.");
        }

        var generalResult = await generalHandler.HandleAsync(new ReporteGeneralNominaGetAllQuery(
            reporteParameters.FromTable1,
            reporteParameters.FromTable2,
            effectiveValue.p_tipo_nomina,
            effectiveValue.p_fecha_pago,
            effectiveValue.codigo_empresa,
            reporteParameters.Where
        ));

        if (!generalResult.IsValid)
        {
            return BuildInvalidResult(generalResult.Message);
        }

        var detalleResult = await detalleHandler.HandleAsync(new ReporteGeneralNominaDetalleGetAllQuery(
            reporteParameters.FromTable1,
            reporteParameters.FromTable2,
            effectiveValue.p_tipo_nomina,
            effectiveValue.codigo_empresa,
            effectiveValue.p_fecha_pago,
            reporteParameters.Where,
            reporteParameters.Cedula
        ));

        if (!detalleResult.IsValid)
        {
            return BuildInvalidResult(detalleResult.Message);
        }

        var firmaResult = await firmaHandler.HandleAsync();

        if (!firmaResult.IsValid)
        {
            return BuildInvalidResult(firmaResult.Message);
        }

        var response = new GetReporteGeneralNominaCompletoGetAllResponse(
            periodo,
            generalResult.Data ?? new List<GetReporteGeneralNominaGetAllResponse>(),
            detalleResult.Data ?? new List<GetReporteGeneralNominaDetalleGetAllResponse>(),
            firmaResult.Data ?? new List<GetReporteGeneralNominaFirmaGetAllResponse>()
        );

        return new ResultDto<GetReporteGeneralNominaCompletoGetAllResponse>(response)
        {
            Data = response,
            CantidadRegistros = periodoRecords + generalResult.CantidadRegistros + detalleResult.CantidadRegistros + firmaResult.CantidadRegistros,
            IsValid = true,
            Message = "Success"
        };
    }

    private static ReporteGeneralNominaCompletoGetAllQuery BuildEffectiveQuery(
        ReporteGeneralNominaCompletoGetAllQuery value,
        GetReporteGeneralNominaPeriodoGetByCodigoResponse? periodo)
    {
        if (periodo?.FechaNomina is null || value.p_tipo_generacion is not (2 or 3))
        {
            return value;
        }

        return value with { p_fecha_pago = periodo.FechaNomina.Value.Date };
    }

    private static ResultDto<GetReporteGeneralNominaCompletoGetAllResponse> BuildInvalidResult(string message)
    {
        return new ResultDto<GetReporteGeneralNominaCompletoGetAllResponse>(
            new GetReporteGeneralNominaCompletoGetAllResponse(
                null,
                new List<GetReporteGeneralNominaGetAllResponse>(),
                new List<GetReporteGeneralNominaDetalleGetAllResponse>(),
                new List<GetReporteGeneralNominaFirmaGetAllResponse>()
            ))
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }

    private static bool TryBuildReporteParameters(
        ReporteGeneralNominaCompletoGetAllQuery value,
        out ReporteGeneralNominaRuntimeParameters parameters,
        out string? errorMessage)
    {
        parameters = new ReporteGeneralNominaRuntimeParameters(string.Empty, string.Empty, string.Empty, string.Empty);
        errorMessage = null;

        if (value.p_tipo_nomina <= 0)
        {
            errorMessage = "El parametro p_tipo_nomina debe ser mayor que cero.";
            return false;
        }

        if (value.codigo_empresa <= 0)
        {
            errorMessage = "El parametro codigo_empresa debe ser mayor que cero.";
            return false;
        }

        if (value.p_tipo_generacion is < 1 or > 3)
        {
            errorMessage = "El parametro p_tipo_generacion debe ser 1, 2 o 3.";
            return false;
        }

        if (value.p_tipo_generacion is 2 or 3 && !value.p_codigo_periodo.HasValue)
        {
            errorMessage = "El parametro p_codigo_periodo es obligatorio para p_tipo_generacion 2 y 3.";
            return false;
        }

        if (value.p_codigo_periodo.HasValue && value.p_codigo_periodo.Value <= 0)
        {
            errorMessage = "El parametro p_codigo_periodo debe ser mayor que cero.";
            return false;
        }

        var cleanCedulaValue = value.p_cedula?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanCedulaValue) &&
            (cleanCedulaValue.Any(ch => ch == '\'' || ch == ';') ||
             cleanCedulaValue.Contains("--") ||
             cleanCedulaValue.Contains("/*") ||
             cleanCedulaValue.Contains("*/")))
        {
            errorMessage = "El parametro p_cedula contiene caracteres no permitidos.";
            return false;
        }

        var fromTable1 = value.p_tipo_generacion == 3
            ? "RH_HISTORICO_NOMINA RTN"
            : "RH_TMP_NOMINA RTN";

        var fromTable2 = value.p_tipo_generacion == 3
            ? "RH_HISTORICO_PERSONAL_CARGO RVPC"
            : "RH_V_PERSONAL_CARGO RVPC";

        var filters = new List<string>
        {
            "RTN.CODIGO_TIPO_NOMINA = RVPC.CODIGO_TIPO_NOMINA"
        };

        if (value.p_tipo_generacion == 3)
        {
            filters.Add($"RVPC.FECHA_NOMINA = DATE '{value.p_fecha_pago:yyyy-MM-dd}'");
            filters.Add("RTN.CODIGO_PERIODO = RVPC.CODIGO_PERIODO");
        }

        if (value.p_codigo_periodo.HasValue)
        {
            filters.Add($"RTN.CODIGO_PERIODO = {value.p_codigo_periodo.Value}");
        }

        var where = string.Join(" AND ", filters);
        var cedula = string.IsNullOrWhiteSpace(cleanCedulaValue)
            ? string.Empty
            : $"RVPC.CEDULA = '{cleanCedulaValue}'";

        parameters = new ReporteGeneralNominaRuntimeParameters(fromTable1, fromTable2, where, cedula);
        return true;
    }
}

internal record ReporteGeneralNominaRuntimeParameters(
    string FromTable1,
    string FromTable2,
    string Where,
    string Cedula
);

// Endpoint
[ApiController]
[Route("api/ReporteGeneralNominaCompletoGetAll")]
public class ReporteGeneralNominaCompletoGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteGeneralNominaCompletoGetAllQuery value)
    {
        try
        {
            var handler = new GetReporteGeneralNominaCompletoGetAllHandler(_connectionDB);
            var result = await handler.HandleAsync(value);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error interno en el servidor",
                detail = ex.Message
            });
        }
    }
}
