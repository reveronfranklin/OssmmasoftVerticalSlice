using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteBm1;

public record ReporteBm1GetAllQuery(
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    List<int>? CodigosIcp
);

public record ReporteBm1ItemResponse(
    string UnidadTrabajo,
    string CodigoGrupo,
    string CodigoNivel1,
    string CodigoNivel2,
    string NumeroLote,
    int Cantidad,
    string NumeroPlaca,
    decimal ValorActual,
    string Articulo,
    string Especificacion,
    string Servicio,
    string ResponsableBien,
    DateTime? FechaMovimiento
);

public record ReporteBm1IcpResponse(
    int CodigoIcp,
    string UnidadTrabajo
);

public class ReporteBm1GetAllHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReporteBm1ItemResponse>>> HandleAsync(ReporteBm1GetAllQuery query)
    {
        if (!ReporteBm1Db.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return BuildInvalidResult(errorMessage);
        }

        if (!ReporteBm1Db.TryValidateDates(query, out DateTime fechaDesde, out DateTime fechaHasta, out string validationMessage))
        {
            return BuildInvalidResult(validationMessage);
        }

        using var cn = _connectionDB.GetBmConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return BuildInvalidResult($"Error tecnico al abrir conexion BM: {ex.Message}");
        }

        using var cmd = new OracleCommand("BM.SP_REP_BM1_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = fechaHasta;
        cmd.Parameters.Add("p_CodigosIcp", OracleDbType.Varchar2).Value = ReporteBm1Db.CsvDbValue(query.CodigosIcp);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<ReporteBm1ItemResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(ReporteBm1Db.MapItem(reader));
                }
            }

            string dbMessage = ReporteBm1Db.GetMessage(pMessage);
            bool isSuccess = ReporteBm1Db.IsSuccessMessage(dbMessage);

            return new ResultDto<List<ReporteBm1ItemResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReporteBm1Db.GetIntOutput(pTotalRecords),
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return BuildInvalidResult($"Error tecnico: {ex.Message}");
        }
    }

    private static ResultDto<List<ReporteBm1ItemResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<ReporteBm1ItemResponse>>(new List<ReporteBm1ItemResponse>())
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

public class ReporteBm1GetIcpsHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReporteBm1IcpResponse>>> HandleAsync()
    {
        if (!ReporteBm1Db.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return BuildInvalidResult(errorMessage);
        }

        using var cn = _connectionDB.GetBmConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return BuildInvalidResult($"Error tecnico al abrir conexion BM: {ex.Message}");
        }

        const string sql = """
            SELECT
                   C.CODIGO_ICP,
                   D.UNIDAD_EJECUTORA UNIDAD_TRABAJO
              FROM BM.BM_BIENES A
              JOIN BM.BM_MOV_BIENES B ON A.CODIGO_BIEN = B.CODIGO_BIEN
              JOIN BM.BM_DIR_BIEN C ON B.CODIGO_DIR_BIEN = C.CODIGO_DIR_BIEN
              JOIN PRE.PRE_INDICE_CAT_PRG D ON C.CODIGO_ICP = D.CODIGO_ICP
             WHERE A.CODIGO_EMPRESA = :p_CodigoEmpresa
             GROUP BY C.CODIGO_ICP, D.UNIDAD_EJECUTORA
             ORDER BY D.UNIDAD_EJECUTORA
            """;

        using var cmd = new OracleCommand(sql, cn);
        cmd.BindByName = true;
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;

        var list = new List<ReporteBm1IcpResponse>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ReporteBm1IcpResponse(
                    reader.SafeGetInt32("CODIGO_ICP"),
                    reader.SafeGetString("UNIDAD_TRABAJO")
                ));
            }

            return new ResultDto<List<ReporteBm1IcpResponse>>(list)
            {
                Data = list,
                CantidadRegistros = list.Count,
                IsValid = true,
                Message = "Success"
            };
        }
        catch (Exception ex)
        {
            return BuildInvalidResult($"Error tecnico: {ex.Message}");
        }
    }

    private static ResultDto<List<ReporteBm1IcpResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<ReporteBm1IcpResponse>>(new List<ReporteBm1IcpResponse>())
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReporteBm1Db
{
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        empresa = 0;
        errorMessage = string.Empty;
        var empresaString = config["settings:EmpresaConfig"];

        if (string.IsNullOrEmpty(empresaString))
        {
            errorMessage = "Configuracion 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(empresaString, NumberStyles.Integer, CultureInfo.InvariantCulture, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un numero valido.";
            return false;
        }

        return true;
    }

    public static bool TryValidateDates(
        ReporteBm1GetAllQuery query,
        out DateTime fechaDesde,
        out DateTime fechaHasta,
        out string message)
    {
        fechaDesde = query.FechaDesde?.Date ?? DateTime.MinValue;
        fechaHasta = query.FechaHasta?.Date ?? DateTime.MinValue;
        message = string.Empty;

        if (!query.FechaDesde.HasValue)
        {
            message = "Debe indicar la fecha desde.";
            return false;
        }

        if (!query.FechaHasta.HasValue)
        {
            message = "Debe indicar la fecha hasta.";
            return false;
        }

        if (fechaDesde > fechaHasta)
        {
            message = "La fecha desde no puede ser mayor que la fecha hasta.";
            return false;
        }

        return true;
    }

    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static object CsvDbValue(List<int>? values)
    {
        var csv = ToCsv(values);
        return string.IsNullOrWhiteSpace(csv) ? DBNull.Value : csv;
    }

    public static string ToCsv(IEnumerable<int>? values)
    {
        if (values is null)
        {
            return string.Empty;
        }

        return string.Join(",", values.Where(value => value > 0).Distinct().OrderBy(value => value));
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : Convert.ToInt32(parameter.Value.ToString(), CultureInfo.InvariantCulture);
    }

    public static ReporteBm1ItemResponse MapItem(IDataReader reader)
    {
        return new ReporteBm1ItemResponse(
            reader.SafeGetString("UNIDAD_TRABAJO"),
            reader.SafeGetString("CODIGO_GRUPO"),
            reader.SafeGetString("CODIGO_NIVEL1"),
            reader.SafeGetString("CODIGO_NIVEL2"),
            reader.SafeGetString("NUMERO_LOTE"),
            reader.SafeGetInt32("CANTIDAD"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetDecimal("VALOR_ACTUAL"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("ESPECIFICACION"),
            reader.SafeGetString("SERVICIO"),
            reader.SafeGetString("RESPONSABLE_BIEN"),
            SafeGetNullableDateTime(reader, "FECHA_MOVIMIENTO")
        );
    }

    public static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string BuildInlineFileName(string prefix, string extension)
    {
        return $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}.{extension}";
    }

    public static FileContentResult BuildPdfFile(ControllerBase controller, byte[] bytes)
    {
        var fileName = BuildInlineFileName("reporte-bm1", "pdf");
        controller.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return controller.File(bytes, "application/pdf", enableRangeProcessing: true);
    }

    public static FileContentResult BuildExcelFile(ControllerBase controller, byte[] bytes)
    {
        var fileName = BuildInlineFileName("reporte-bm1", "xlsx");
        return controller.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}

[ApiController]
[Route("api/ReporteBm1")]
public class ReporteBm1Controller(
    ConnectionDB _connectionDB,
    IConfiguration _config,
    IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteBm1GetAllQuery value)
    {
        var handler = new ReporteBm1GetAllHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }

    [HttpGet]
    [Route("GetIcps")]
    public async Task<IActionResult> GetIcps()
    {
        var handler = new ReporteBm1GetIcpsHandler(_connectionDB, _config);
        var result = await handler.HandleAsync();
        return Ok(result);
    }

    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteBm1GetAllQuery value)
    {
        var handler = new ReporteBm1GetAllHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        if (!result.IsValid || result.Data is null)
        {
            return Ok(result);
        }

        if (result.Data.Count == 0)
        {
            return Ok(new ResultDto<string>(string.Empty)
            {
                IsValid = false,
                Message = "No hay registros para generar el PDF con los filtros seleccionados."
            });
        }

        Response.Headers.Append("X-Reporte-Bm1-Count", result.Data.Count.ToString(CultureInfo.InvariantCulture));
        var bytes = ReporteBm1PdfGenerator.Generate(result.Data, value, _environment);
        return ReporteBm1Db.BuildPdfFile(this, bytes);
    }

    [HttpPost]
    [Route("excel")]
    public async Task<IActionResult> Excel(ReporteBm1GetAllQuery value)
    {
        var handler = new ReporteBm1GetAllHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        if (!result.IsValid || result.Data is null)
        {
            return Ok(result);
        }

        var bytes = ReporteBm1ExcelGenerator.Generate(result.Data);
        return ReporteBm1Db.BuildExcelFile(this, bytes);
    }
}
