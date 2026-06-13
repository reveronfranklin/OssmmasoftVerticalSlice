using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhHistoricoMovimiento;

public record HistoricoTipoNominaFilter(int CodigoTipoNomina);

public record HistoricoConceptoFilter(int CodigoConcepto);

public record RhHistoricoMovimientoGetHistoricoFechaQuery(
    DateTime Desde,
    DateTime Hasta,
    string TipoQuery,
    List<HistoricoTipoNominaFilter>? CodigoTipoNomina,
    int CodigoPersona,
    List<HistoricoConceptoFilter>? CodigoConcepto,
    int Page,
    int PageSize,
    string? TipoSort,
    string? SortColumn,
    int CodigoProceso
);

public record RhHistoricoMovimientoResponse(
    int CodigoHistoricoNomina,
    int CodigoPersona,
    int Cedula,
    string Foto,
    string Nombre,
    string Apellido,
    string FullName,
    string Nacionalidad,
    string DescripcionNacionalidad,
    string Sexo,
    string Status,
    string DescripcionStatus,
    string DescripcionSexo,
    int CodigoRelacionCargo,
    int CodigoCargo,
    string CargoCodigo,
    int CodigoIcp,
    int CodigoIcpUbicacion,
    decimal Sueldo,
    string DescripcionCargo,
    int CodigoTipoNomina,
    string TipoNomina,
    int FrecuenciaPagoId,
    string DescripcionFrecuenciaPago,
    string CodigoSector,
    string CodigoPrograma,
    int TipoCuentaId,
    string DescripcionTipoCuenta,
    int BancoId,
    string DescripcionBanco,
    string NoCuenta,
    string Extra1,
    string Extra2,
    string Extra3,
    int CodigoPeriodo,
    DateTime? FechaNomina,
    DateTime? FechaIngreso,
    DateTime? FechaNominaMov,
    string Complemento,
    string Tipo,
    decimal Monto,
    string StatusMov,
    string Codigo,
    string Denominacion,
    int CodigoConcepto,
    string Avatar,
    string UnidadEjecutora,
    string EstadoCivil,
    string SearchText
);

public class RhHistoricoMovimientoGetHistoricoFechaHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<RhHistoricoMovimientoResponse>>> HandleAsync(
        RhHistoricoMovimientoGetHistoricoFechaQuery query)
    {
        if (query.Desde == default)
        {
            return BuildInvalidResult("Fecha Desde Invalida", query.Page);
        }

        if (query.Hasta == default)
        {
            return BuildInvalidResult("Fecha Hasta Invalida", query.Page);
        }

        if (query.Hasta.Date < query.Desde.Date)
        {
            return BuildInvalidResult("Fecha Hasta no puede ser menor que Fecha Desde", query.Page);
        }

        var tipoQuery = string.IsNullOrWhiteSpace(query.TipoQuery) ? "MASIVO" : query.TipoQuery.Trim();
        var isMasivo = string.Equals(tipoQuery, "MASIVO", StringComparison.OrdinalIgnoreCase);
        var isMasivoExcel = string.Equals(tipoQuery, "MASIVO_EXCEL", StringComparison.OrdinalIgnoreCase);
        var isIndividual = string.Equals(tipoQuery, "INDIVIDUAL", StringComparison.OrdinalIgnoreCase);

        if (!isMasivo && !isMasivoExcel && !isIndividual)
        {
            return BuildInvalidResult("Tipo de consulta no soportado.", query.Page);
        }

        if (isMasivo && query.Hasta.Date > query.Desde.Date.AddYears(1))
        {
            return BuildInvalidResult("El rango de fechas no puede ser mayor a un año.", query.Page);
        }

        if (isIndividual && query.CodigoPersona <= 0)
        {
            return BuildInvalidResult("Debe seleccionar una persona.", query.Page);
        }

        using var cn = _connectionDB.GetRhConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<List<RhHistoricoMovimientoResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión RH: {ex.Message}",
                Page = query.Page
            };
        }

        using var cmd = new OracleCommand("RH.SP_RH_HISTORICO_MOV_MASIVO_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_desde", OracleDbType.Date).Value = query.Desde.Date;
        cmd.Parameters.Add("p_hasta", OracleDbType.Date).Value = query.Hasta.Date;
        cmd.Parameters.Add("p_codigo_persona", OracleDbType.Int32).Value = isIndividual ? query.CodigoPersona : 0;
        cmd.Parameters.Add("p_codigos_tipo_nomina", OracleDbType.Varchar2).Value = DbValue(ToCsv(query.CodigoTipoNomina));
        cmd.Parameters.Add("p_codigos_concepto", OracleDbType.Varchar2).Value = DbValue(ToCsv(query.CodigoConcepto));
        cmd.Parameters.Add("p_page", OracleDbType.Int32).Value = Math.Max(query.Page, 0);
        cmd.Parameters.Add("p_page_size", OracleDbType.Int32).Value = query.PageSize <= 0 ? 0 : Math.Clamp(query.PageSize, 1, 500);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<RhHistoricoMovimientoResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(MapHistorico(reader));
                }
            }

            var dbMessage = GetMessage(pMessage);
            var isSuccess = IsSuccessMessage(dbMessage);
            var totalRecords = GetIntOutput(pTotalRecords);

            return new ResultDto<List<RhHistoricoMovimientoResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = totalRecords == 0 ? list.Count : totalRecords,
                IsValid = isSuccess,
                LinkData = string.Empty,
                Message = isSuccess ? string.Empty : dbMessage,
                Page = query.Page
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<RhHistoricoMovimientoResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}",
                Page = query.Page
            };
        }
    }

    private static ResultDto<List<RhHistoricoMovimientoResponse>> BuildInvalidResult(string message, int page)
    {
        return new ResultDto<List<RhHistoricoMovimientoResponse>>(new List<RhHistoricoMovimientoResponse>())
        {
            Data = new List<RhHistoricoMovimientoResponse>(),
            IsValid = false,
            Message = message,
            LinkData = string.Empty,
            Page = page,
            CantidadRegistros = 0
        };
    }

    private static string ToCsv(List<HistoricoTipoNominaFilter>? values)
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

    private static string ToCsv(List<HistoricoConceptoFilter>? values)
    {
        if (values is null)
        {
            return string.Empty;
        }

        return string.Join(",", values
            .Select(value => value.CodigoConcepto)
            .Where(value => value > 0)
            .Distinct());
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
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

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static RhHistoricoMovimientoResponse MapHistorico(IDataReader reader)
    {
        return new RhHistoricoMovimientoResponse(
            reader.SafeGetInt32("CODIGO_HISTORICO_NOMINA"),
            reader.SafeGetInt32("CODIGO_PERSONA"),
            reader.SafeGetInt32("CEDULA"),
            reader.SafeGetString("FOTO"),
            reader.SafeGetString("NOMBRE"),
            reader.SafeGetString("APELLIDO"),
            reader.SafeGetString("FULL_NAME"),
            reader.SafeGetString("NACIONALIDAD"),
            reader.SafeGetString("DESCRIPCION_NACIONALIDAD"),
            reader.SafeGetString("SEXO"),
            reader.SafeGetString("STATUS"),
            reader.SafeGetString("DESCRIPCION_STATUS"),
            reader.SafeGetString("DESCRIPCION_SEXO"),
            reader.SafeGetInt32("CODIGO_RELACION_CARGO"),
            reader.SafeGetInt32("CODIGO_CARGO"),
            reader.SafeGetString("CARGO_CODIGO"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetInt32("CODIGO_ICP_UBICACION"),
            reader.SafeGetDecimal("SUELDO"),
            reader.SafeGetString("DESCRIPCION_CARGO"),
            reader.SafeGetInt32("CODIGO_TIPO_NOMINA"),
            reader.SafeGetString("TIPO_NOMINA"),
            reader.SafeGetInt32("FRECUENCIA_PAGO_ID"),
            reader.SafeGetString("DESCRIPCION_FRECUENCIA_PAGO"),
            reader.SafeGetString("CODIGO_SECTOR"),
            reader.SafeGetString("CODIGO_PROGRAMA"),
            reader.SafeGetInt32("TIPO_CUENTA_ID"),
            reader.SafeGetString("DESCRIPCION_TIPO_CUENTA"),
            reader.SafeGetInt32("BANCO_ID"),
            reader.SafeGetString("DESCRIPCION_BANCO"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            reader.SafeGetInt32("CODIGO_PERIODO"),
            SafeGetNullableDateTime(reader, "FECHA_NOMINA"),
            SafeGetNullableDateTime(reader, "FECHA_INGRESO"),
            SafeGetNullableDateTime(reader, "FECHA_NOMINA_MOV"),
            reader.SafeGetString("COMPLEMENTO"),
            reader.SafeGetString("TIPO"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("STATUS_MOV"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetString("DENOMINACION"),
            reader.SafeGetInt32("CODIGO_CONCEPTO"),
            reader.SafeGetString("AVATAR"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetString("ESTADO_CIVIL"),
            reader.SafeGetString("SEARCH_TEXT")
        );
    }
}

[ApiController]
[Route("api/RhHistoricoMovimiento")]
public class RhHistoricoMovimientoGetHistoricoFechaController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetHistoricoFecha")]
    public async Task<IActionResult> GetHistoricoFecha(RhHistoricoMovimientoGetHistoricoFechaQuery value)
    {
        var handler = new RhHistoricoMovimientoGetHistoricoFechaHandler(_connectionDB);
        var historico = await handler.HandleAsync(value);

        return Ok(historico);
    }
}
