using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/Bm1")]
public class Bm1Controller(ConnectionDB connectionDB, IConfiguration config, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("GetListICP")]
    public async Task<IActionResult> GetListIcp()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmIcpResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmIcpResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM1_GET_LIST_ICP", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmIcpResponse(
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_TRABAJO")
        )));
    }

    [HttpGet("GetPlacas")]
    public async Task<IActionResult> GetPlacas()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmPlacaResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmPlacaResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM1_GET_PLACAS", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmPlacaResponse(
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("SEARCH_TEXT")
        )));
    }

    [HttpPost("GetUbicacionesOrigen")]
    public async Task<IActionResult> GetUbicacionesOrigen(BmOrigenUbicacionRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmUbicacionResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmUbicacionResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM1_GET_UBI_ORI", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoIcp", OracleDbType.Int32).Value = request.CodigoIcp;
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = request.CodigoArticulo;
        cmd.Parameters.Add("p_ResponsableText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.ResponsableText);
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page <= 0 ? 1 : request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize <= 0 ? 25 : request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, BmUbicacionesController.MapUbicacion, request.Page));
    }

    [HttpGet("GetFechaPrimerMovimiento")]
    public async Task<IActionResult> GetFechaPrimerMovimiento()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(new ResultDto<DateTime?>(null) { IsValid = false, Message = error });
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(new ResultDto<DateTime?>(null) { IsValid = false, Message = openError });

        using var cmd = BmDb.StoredProcedure("BM.SP_BM1_GET_FIRST_MOV", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        var pFecha = cmd.Parameters.Add("p_Fecha", OracleDbType.Date, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        await cmd.ExecuteNonQueryAsync();
        var message = BmDb.GetMessage(pMessage);
        var fecha = pFecha.Value == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(pFecha.Value, CultureInfo.InvariantCulture);

        return Ok(new ResultDto<DateTime?>(fecha)
        {
            Data = BmDb.IsSuccessMessage(message) ? fecha : null,
            IsValid = BmDb.IsSuccessMessage(message),
            Message = message
        });
    }

    [HttpPost("GetByListIcp")]
    public async Task<IActionResult> GetByListIcp(Bm1FilterRequest request)
    {
        return Ok(await ReadByListIcpAsync(request));
    }

    [HttpPost("PlacasPdf")]
    public async Task<IActionResult> PlacasPdf(Bm1FilterRequest request)
    {
        var result = await ReadByListIcpAsync(request);

        if (!result.IsValid || result.Data is null)
        {
            return Ok(result);
        }

        if (result.Data.Count == 0)
        {
            return Ok(new ResultDto<string>(string.Empty)
            {
                IsValid = false,
                Message = "No hay bienes para generar placas con los filtros seleccionados."
            });
        }

        Response.Headers.Append("X-Bm1-Placas-Count", result.Data.Count.ToString(CultureInfo.InvariantCulture));
        var bytes = Bm1PlacasPdfGenerator.Generate(result.Data, environment);
        var fileName = $"placas-bm1-{DateTime.Now:yyyyMMddHHmmss}.pdf";
        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";

        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }

    /// <summary>
    /// Unico punto donde se ejecuta BM.SP_BM1_GET_BY_ICP. El grid y el PDF de placas comparten esta
    /// lectura para que el filtro resuelto sea siempre el mismo (requerimiento 18).
    /// </summary>
    private async Task<ResultDto<List<Bm1Response>>> ReadByListIcpAsync(Bm1FilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<Bm1Response>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<Bm1Response>(openError);

        using var cmd = BmDb.StoredProcedure("BM.SP_BM1_GET_BY_ICP", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = BmDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = BmDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_CodigosIcp", OracleDbType.Varchar2).Value = BmDb.DbValue(BmDb.ToIcpCsv(request.ListIcpSeleccionado));
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchValue);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapBm1);
    }

    [HttpPost("GetProductMobil")]
    public async Task<IActionResult> GetProductMobile(BmProductMobileRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmProductMobileResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmProductMobileResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM1_GET_PRODUCT_MOB", cn);
        var searchText = string.IsNullOrWhiteSpace(request.SearchText) ? request.SearhText : request.SearchText;
        var page = request.Page > 0 ? request.Page : request.PageNumber > 0 ? request.PageNumber : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 25;

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien;
        cmd.Parameters.Add("p_CodigoIcp", OracleDbType.Int32).Value = request.CodigoIcp;
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = request.CodigoArticulo;
        cmd.Parameters.Add("p_ResponsableText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.ResponsableText);
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(searchText);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = pageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmProductMobileResponse(
            reader.SafeGetInt32("ID"),
            reader.SafeGetString("KEY"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("RESPONSABLE"),
            reader.SafeGetString("NRO_PLACA"),
            reader.SafeGetInt32("CODIGO_DEPARTAMENTO_RESP"),
            reader.SafeGetString("DESCRIPCION_DEPARTAMENTO"),
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            new List<string>()
        )));
    }

    private static Bm1Response MapBm1(IDataReader reader)
    {
        var fecha = BmDb.GetDate(reader, "FECHA_MOVIMIENTO");
        return new Bm1Response(
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
            reader.SafeGetString("SEARCH_TEXT"),
            reader.SafeGetString("LINK_DATA"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetInt32("CODIGO_MOV_BIEN"),
            fecha,
            fecha?.Year ?? 0,
            fecha?.Month ?? 0,
            reader.SafeGetString("NRO_PLACA"),
            reader.SafeGetString("PLACA_BARRA")
        );
    }
}
