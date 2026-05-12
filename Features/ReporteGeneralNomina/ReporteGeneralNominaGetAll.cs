using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OssmmasoftVerticalSlice.Features.ReporteGeneralNomina;

// Request
public record ReporteGeneralNominaGetAllQuery(
    string p_from_table1,
    string p_from_table2,
    int p_tipo_nomina,
    DateTime p_fecha_pago,
    int codigo_empresa,
    string p_where
);

// Response
public record GetReporteGeneralNominaGetAllResponse(
    string RTipoConcepto,
    string RNumeroConcepto,
    string RDenominacionConcepto,
    decimal RAsignacion,
    decimal RDeduccion,
    decimal RMontoVisible,
    decimal RMonto,
    decimal RDeducible
);

public class GetReporteGeneralNominaGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<GetReporteGeneralNominaGetAllResponse>>> HandleAsync(ReporteGeneralNominaGetAllQuery value)
    {
        if (!TryValidateFromFragment(value.p_from_table1, out var cleanFromTable1, out var fromTable1Error))
        {
            return BuildInvalidResult(fromTable1Error ?? "Parametro p_from_table1 invalido.");
        }

        if (!TryValidateFromFragment(value.p_from_table2, out var cleanFromTable2, out var fromTable2Error))
        {
            return BuildInvalidResult(fromTable2Error ?? "Parametro p_from_table2 invalido.");
        }

        if (!WhereClauseHelper.TryBuildCleanWhere(value.p_where, out var cleanWhere, out var errorMessage))
        {
            return BuildInvalidResult(errorMessage ?? "Error de sintaxis en el filtro.");
        }

        if (!TryValidateSqlFragmentSafety(cleanWhere, "WHERE", out var whereSafetyError))
        {
            return BuildInvalidResult(whereSafetyError ?? "Parametro p_where invalido.");
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_REP_GRAL_NOMINA_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_from_table1", OracleDbType.Varchar2).Value = cleanFromTable1;
        cmd.Parameters.Add("p_from_table2", OracleDbType.Varchar2).Value = cleanFromTable2;
        cmd.Parameters.Add("p_tipo_nomina", OracleDbType.Int32).Value = value.p_tipo_nomina;
        cmd.Parameters.Add("p_fecha_pago", OracleDbType.Date).Value = value.p_fecha_pago;
        cmd.Parameters.Add("p_codigo_empresa", OracleDbType.Int32).Value = value.codigo_empresa;
        cmd.Parameters.Add("p_where", OracleDbType.Varchar2).Value =
            string.IsNullOrWhiteSpace(cleanWhere) ? (object)DBNull.Value : cleanWhere;

        var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<GetReporteGeneralNominaGetAllResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var R_TIPO_CONCEPTO = reader.SafeGetString("R_TIPO_CONCEPTO");
                    var R_NUMERO_CONCEPTO = reader.SafeGetString("R_NUMERO_CONCEPTO");
                    var R_DENOMINACION_CONCEPTO = reader.SafeGetString("R_DENOMINACION_CONCEPTO");
                
                    list.Add(new GetReporteGeneralNominaGetAllResponse(
                        GetValueAsString(reader, "R_TIPO_CONCEPTO"),
                        GetValueAsString(reader, "R_NUMERO_CONCEPTO"),
                        GetValueAsString(reader, "R_DENOMINACION_CONCEPTO"),
                        reader.SafeGetDecimal("R_ASIGNACION"),
                        reader.SafeGetDecimal("R_DEDUCCION"),
                        reader.SafeGetDecimal("R_MONTO_VISIBLE"),
                        reader.SafeGetDecimal("R_MONTO"),
                        reader.SafeGetDecimal("R_DEDUCIBLE")
                    ));
                }
            }

            string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() ?? "Success" : "Success";
            int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString() ?? "0") : 0;
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            return new ResultDto<List<GetReporteGeneralNominaGetAllResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = dbTotalRecords,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (OracleException ex)
        {
            Debug.WriteLine(">>>> ORACLE ERROR: " + ex.Message);
            Debug.WriteLine(">>>> FROM TABLE 1: " + cleanFromTable1);
            Debug.WriteLine(">>>> FROM TABLE 2: " + cleanFromTable2);
            Debug.WriteLine(">>>> FILTRO ENVIADO: " + cleanWhere);

            return new ResultDto<List<GetReporteGeneralNominaGetAllResponse>>(new List<GetReporteGeneralNominaGetAllResponse>())
            {
                Data = null,
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}. Filtro: {cleanWhere}"
            };
        }
    }

    private static ResultDto<List<GetReporteGeneralNominaGetAllResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<GetReporteGeneralNominaGetAllResponse>>(new List<GetReporteGeneralNominaGetAllResponse>())
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }

    private static string GetValueAsString(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
    }

    private static bool TryValidateFromFragment(string? value, out string cleanValue, out string? errorMessage)
    {
        cleanValue = value?.Trim() ?? string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(cleanValue))
        {
            errorMessage = "El parametro FROM no puede estar vacio.";
            return false;
        }

        if (!TryValidateSqlFragmentSafety(cleanValue, "FROM", out errorMessage))
        {
            return false;
        }

        if (!Regex.IsMatch(cleanValue, @"^[A-Za-z0-9_.$\s]+$"))
        {
            errorMessage = $"El parametro FROM contiene caracteres invalidos: [{cleanValue}]";
            return false;
        }

        return true;
    }

    private static bool TryValidateSqlFragmentSafety(string? value, string parameterName, out string? errorMessage)
    {
        var cleanValue = value?.Trim() ?? string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(cleanValue))
        {
            return true;
        }

        if (Regex.IsMatch(cleanValue, @";|--|/\*|\*/", RegexOptions.IgnoreCase))
        {
            errorMessage = $"El parametro {parameterName} contiene caracteres no permitidos: [{cleanValue}]";
            return false;
        }

        if (Regex.IsMatch(cleanValue, @"\b(DROP|DELETE|UPDATE|INSERT|MERGE|ALTER|CREATE|EXEC|EXECUTE)\b", RegexOptions.IgnoreCase))
        {
            errorMessage = $"El parametro {parameterName} contiene una palabra reservada no permitida: [{cleanValue}]";
            return false;
        }

        return true;
    }
}

// Endpoint
[ApiController]
[Route("api/ReporteGeneralNominaGetAll")]
public class ReporteGeneralNominaGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteGeneralNominaGetAllQuery value)
    {
        try
        {
            var handler = new GetReporteGeneralNominaGetAllHandler(_connectionDB);
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
