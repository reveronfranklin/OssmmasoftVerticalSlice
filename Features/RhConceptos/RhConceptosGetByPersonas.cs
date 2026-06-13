using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhConceptos;

public record RhConceptosGetByPersonasQuery(
    int CodigoPersona,
    DateTime Desde,
    DateTime Hasta,
    List<RhConceptosTipoNominaFilter>? CodigoTipoNomina,
    bool SinRestriccionFecha = false
);

public record RhConceptosTipoNominaFilter(int CodigoTipoNomina);

public record RhConceptosByPersonasResponse(
    int CodigoConcepto,
    string Codigo,
    int CodigoTipoNomina,
    string TipoNominaDescripcion,
    string Denominacion,
    string Descripcion,
    string TipoConcepto,
    int ModuloId,
    string ModuloDescripcion,
    int CodigoPuc,
    string CodigoPucConcat,
    string Status,
    int FrecuenciaId,
    string FrecuenciaDescripcion,
    int Dedusible,
    int Automatico,
    int IdModeloCalculo,
    string Extra1
);

public class RhConceptosGetByPersonasHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<RhConceptosByPersonasResponse>>> HandleAsync(
        RhConceptosGetByPersonasQuery query)
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

        if (!query.SinRestriccionFecha && query.CodigoPersona <= 0 && query.Hasta.Date > query.Desde.Date.AddYears(1))
        {
            return BuildInvalidResult("El rango de fechas no puede ser mayor a un año.");
        }

        using var cn = _connectionDB.GetRhConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<List<RhConceptosByPersonasResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión RH: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("RH.SP_RH_CONCEPTOS_BY_PERSONA_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_codigo_persona", OracleDbType.Int32).Value = query.CodigoPersona;
        cmd.Parameters.Add("p_desde", OracleDbType.Date).Value = query.Desde.Date;
        cmd.Parameters.Add("p_hasta", OracleDbType.Date).Value = query.Hasta.Date;
        cmd.Parameters.Add("p_codigos_tipo_nomina", OracleDbType.Varchar2).Value = DbValue(ToCsv(query.CodigoTipoNomina));
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<RhConceptosByPersonasResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new RhConceptosByPersonasResponse(
                        reader.SafeGetInt32("CODIGO_CONCEPTO"),
                        reader.SafeGetString("CODIGO"),
                        reader.SafeGetInt32("CODIGO_TIPO_NOMINA"),
                        reader.SafeGetString("TIPO_NOMINA_DESCRIPCION"),
                        reader.SafeGetString("DENOMINACION"),
                        reader.SafeGetString("DESCRIPCION"),
                        reader.SafeGetString("TIPO_CONCEPTO"),
                        reader.SafeGetInt32("MODULO_ID"),
                        reader.SafeGetString("MODULO_DESCRIPCION"),
                        reader.SafeGetInt32("CODIGO_PUC"),
                        reader.SafeGetString("CODIGO_PUC_CONCAT"),
                        reader.SafeGetString("STATUS"),
                        reader.SafeGetInt32("FRECUENCIA_ID"),
                        reader.SafeGetString("FRECUENCIA_DESCRIPCION"),
                        reader.SafeGetInt32("DEDUSIBLE"),
                        reader.SafeGetInt32("AUTOMATICO"),
                        reader.SafeGetInt32("ID_MODELO_CALCULO"),
                        reader.SafeGetString("EXTRA1")
                    ));
                }
            }

            var dbMessage = GetMessage(pMessage);
            var isSuccess = IsSuccessMessage(dbMessage);
            var totalRecords = GetIntOutput(pTotalRecords);

            return new ResultDto<List<RhConceptosByPersonasResponse>>(list)
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
            return new ResultDto<List<RhConceptosByPersonasResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }

    private static ResultDto<List<RhConceptosByPersonasResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<RhConceptosByPersonasResponse>>(new List<RhConceptosByPersonasResponse>())
        {
            Data = new List<RhConceptosByPersonasResponse>(),
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

    private static string ToCsv(List<RhConceptosTipoNominaFilter>? values)
    {
        if (values is null)
        {
            return string.Empty;
        }

        return string.Join(",", values
            .Select(value => value.CodigoTipoNomina)
            .Where(value => value > 0)
            .Distinct());
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }
}

[ApiController]
[Route("api/RhConceptos")]
public class RhConceptosGetByPersonasController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetConceptosByPersonas")]
    public async Task<IActionResult> GetConceptosByPersonas(RhConceptosGetByPersonasQuery value)
    {
        var handler = new RhConceptosGetByPersonasHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
