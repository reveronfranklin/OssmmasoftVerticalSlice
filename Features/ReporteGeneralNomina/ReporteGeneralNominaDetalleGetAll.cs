using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OssmmasoftVerticalSlice.Features.ReporteGeneralNomina;

// Request
public record ReporteGeneralNominaDetalleGetAllQuery(
    string p_from_table1,
    string p_from_table2,
    int p_tipo_nomina,
    int codigo_empresa,
    DateTime p_fecha_pago,
    string p_where,
    string p_cedula
);

// Response
public record GetReporteGeneralNominaDetalleGetAllResponse(
    DateTime? FechaPeriodoNomina,
    DateTime? FechaEmisionNomina,
    decimal CodigoPeriodo,
    decimal CodigoTipoNomina,
    string CodigoOficina,
    decimal CodigoIcp,
    string Denominacion,
    string DenominacionCargo,
    string Cedula,
    string Nombre,
    string NoCuenta,
    string NumeroConcepto,
    string TipoMovConcepto,
    string DenominacionConcepto,
    string ComplementoConcepto,
    decimal Porcentaje,
    string TipoConcepto,
    decimal Monto,
    decimal Asignacion,
    decimal Deduccion,
    string Status,
    string DescripcionStatus,
    decimal Activos,
    decimal Permisos,
    decimal Vacaciones,
    decimal Reposos,
    decimal CodigoPersona,
    DateTime? FechaIngreso,
    string CargoCodigo,
    string Banco,
    decimal CodigoConcepto,
    string Modulo,
    string CodigoIdentificador
);

public class GetReporteGeneralNominaDetalleGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<GetReporteGeneralNominaDetalleGetAllResponse>>> HandleAsync(ReporteGeneralNominaDetalleGetAllQuery value)
    {
        if (!TryValidateFromFragment(value.p_from_table1, out var cleanFromTable1, out var fromTable1Error))
        {
            return BuildInvalidResult(fromTable1Error ?? "Parametro p_from_table1 invalido.");
        }

        if (!TryValidateFromFragment(value.p_from_table2, out var cleanFromTable2, out var fromTable2Error))
        {
            return BuildInvalidResult(fromTable2Error ?? "Parametro p_from_table2 invalido.");
        }

        if (!WhereClauseHelper.TryBuildCleanWhere(value.p_where, out var cleanWhere, out var whereErrorMessage))
        {
            return BuildInvalidResult(whereErrorMessage ?? "Error de sintaxis en el filtro.");
        }

        if (!TryBuildCleanOptionalCondition(value.p_cedula, out var cleanCedula, out var cedulaErrorMessage))
        {
            return BuildInvalidResult(cedulaErrorMessage ?? "Error de sintaxis en el filtro de cedula.");
        }

        if (!TryValidateSqlFragmentSafety(cleanWhere, "WHERE", out var whereSafetyError))
        {
            return BuildInvalidResult(whereSafetyError ?? "Parametro p_where invalido.");
        }

        if (!TryValidateSqlFragmentSafety(cleanCedula, "CEDULA", out var cedulaSafetyError))
        {
            return BuildInvalidResult(cedulaSafetyError ?? "Parametro p_cedula invalido.");
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_REP_GRAL_NOM_DET_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_from_table1", OracleDbType.Varchar2).Value = cleanFromTable1;
        cmd.Parameters.Add("p_from_table2", OracleDbType.Varchar2).Value = cleanFromTable2;
        cmd.Parameters.Add("p_tipo_nomina", OracleDbType.Int32).Value = value.p_tipo_nomina;
        cmd.Parameters.Add("p_codigo_empresa", OracleDbType.Int32).Value = value.codigo_empresa;
        cmd.Parameters.Add("p_fecha_pago", OracleDbType.Date).Value = value.p_fecha_pago;
        cmd.Parameters.Add("p_where", OracleDbType.Varchar2).Value =
            string.IsNullOrWhiteSpace(cleanWhere) ? (object)DBNull.Value : cleanWhere;
        cmd.Parameters.Add("p_cedula", OracleDbType.Varchar2).Value =
            string.IsNullOrWhiteSpace(cleanCedula) ? (object)DBNull.Value : cleanCedula;

        var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<GetReporteGeneralNominaDetalleGetAllResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new GetReporteGeneralNominaDetalleGetAllResponse(
                        SafeGetNullableDateTime(reader, "FECHA_PERIODO_NOMINA"),
                        SafeGetNullableDateTime(reader, "FECHA_EMISION_NOMINA"),
                        reader.SafeGetDecimal("CODIGO_PERIODO"),
                        reader.SafeGetDecimal("CODIGO_TIPO_NOMINA"),
                        GetValueAsString(reader, "CODIGO_OFICINA"),
                        reader.SafeGetDecimal("CODIGO_ICP"),
                        GetValueAsString(reader, "DENOMINACION"),
                        GetValueAsString(reader, "DENOMINACION_CARGO"),
                        GetValueAsString(reader, "CEDULA"),
                        GetValueAsString(reader, "NOMBRE"),
                        GetValueAsString(reader, "NO_CUENTA"),
                        GetValueAsString(reader, "NUMERO_CONCEPTO"),
                        GetValueAsString(reader, "TIPO_MOV_CONCEPTO"),
                        GetValueAsString(reader, "DENOMINACION_CONCEPTO"),
                        GetValueAsString(reader, "COMPLEMENTO_CONCEPTO"),
                        reader.SafeGetDecimal("PORCENTAJE"),
                        GetValueAsString(reader, "TIPO_CONCEPTO"),
                        reader.SafeGetDecimal("MONTO"),
                        reader.SafeGetDecimal("ASIGNACION"),
                        reader.SafeGetDecimal("DEDUCCION"),
                        GetValueAsString(reader, "STATUS"),
                        GetValueAsString(reader, "DESCRIPCION_STATUS"),
                        reader.SafeGetDecimal("ACTIVOS"),
                        reader.SafeGetDecimal("PERMISOS"),
                        reader.SafeGetDecimal("VACACIONES"),
                        reader.SafeGetDecimal("REPOSOS"),
                        reader.SafeGetDecimal("CODIGO_PERSONA"),
                        SafeGetNullableDateTime(reader, "FECHA_INGRESO"),
                        GetValueAsString(reader, "CARGO_CODIGO"),
                        GetValueAsString(reader, "BANCO"),
                        reader.SafeGetDecimal("CODIGO_CONCEPTO"),
                        GetValueAsString(reader, "MODULO"),
                        GetValueAsString(reader, "CODIGO_IDENTIFICADOR")
                    ));
                }
            }

            string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() ?? "Success" : "Success";
            int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString() ?? "0") : 0;
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            return new ResultDto<List<GetReporteGeneralNominaDetalleGetAllResponse>>(list)
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
            Debug.WriteLine(">>>> FILTRO CEDULA ENVIADO: " + cleanCedula);

            return new ResultDto<List<GetReporteGeneralNominaDetalleGetAllResponse>>(new List<GetReporteGeneralNominaDetalleGetAllResponse>())
            {
                Data = null,
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}. Filtro: {cleanWhere}. Cedula: {cleanCedula}"
            };
        }
    }

    private static ResultDto<List<GetReporteGeneralNominaDetalleGetAllResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<GetReporteGeneralNominaDetalleGetAllResponse>>(new List<GetReporteGeneralNominaDetalleGetAllResponse>())
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

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
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

    private static bool TryBuildCleanOptionalCondition(string? value, out string cleanValue, out string? errorMessage)
    {
        cleanValue = value?.Trim() ?? string.Empty;
        errorMessage = null;

        if (cleanValue.StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase))
        {
            cleanValue = cleanValue.Substring(6).Trim();
        }

        if (cleanValue.StartsWith("AND ", StringComparison.OrdinalIgnoreCase))
        {
            cleanValue = cleanValue.Substring(4).Trim();
        }

        if (cleanValue.StartsWith("'") && cleanValue.EndsWith("'") && cleanValue.Length > 2)
        {
            cleanValue = cleanValue.Substring(1, cleanValue.Length - 2);
        }

        if (string.IsNullOrWhiteSpace(cleanValue))
        {
            return true;
        }

        int quoteCount = cleanValue.Count(f => f == '\'');
        if (quoteCount % 2 != 0)
        {
            errorMessage = $"Error de sintaxis: Hay una comilla simple sin cerrar en el filtro de cedula: [{cleanValue}]";
            return false;
        }

        int parenthesisBalance = 0;
        foreach (char ch in cleanValue)
        {
            if (ch == '(') parenthesisBalance++;
            if (ch == ')') parenthesisBalance--;
            if (parenthesisBalance < 0) break;
        }

        if (parenthesisBalance != 0)
        {
            errorMessage = $"Error de sintaxis: parentesis desbalanceados en el filtro de cedula: [{cleanValue}]";
            return false;
        }

        if (Regex.IsMatch(cleanValue, @"^\s*(AND|OR)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(cleanValue, @"\b(AND|OR)\s*$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(cleanValue, @"(=|<>|>=|<=|>|<|LIKE|IN|BETWEEN)\s*$", RegexOptions.IgnoreCase))
        {
            errorMessage = $"Error de sintaxis: expresion incompleta en el filtro de cedula: [{cleanValue}]";
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
[Route("api/ReporteGeneralNominaDetalleGetAll")]
public class ReporteGeneralNominaDetalleGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteGeneralNominaDetalleGetAllQuery value)
    {
        try
        {
            var handler = new GetReporteGeneralNominaDetalleGetAllHandler(_connectionDB);
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
