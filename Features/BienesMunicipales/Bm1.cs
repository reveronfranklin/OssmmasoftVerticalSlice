using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/Bm1")]
public class Bm1Controller(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
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
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<Bm1Response>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<Bm1Response>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM1_GET_BY_ICP", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = BmDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = BmDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_CodigosIcp", OracleDbType.Varchar2).Value = BmDb.DbValue(BmDb.ToIcpCsv(request.ListIcpSeleccionado));
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapBm1));
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
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien;
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
            reader.SafeGetString("NRO_PLACA")
        );
    }
}
