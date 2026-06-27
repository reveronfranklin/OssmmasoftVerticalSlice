using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmBienes")]
public class BmBienesController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(BmBienFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmBienResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmBienResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_BIEN_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page <= 0 ? 1 : request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize <= 0 ? 25 : request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapBien, request.Page));
    }

    [HttpPost("GetById")]
    public async Task<IActionResult> GetById(BmBienByIdRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmBienResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmBienResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_BIEN_GET_ID", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapBien));
    }

    [HttpPost("GetByNumeroPlaca")]
    public async Task<IActionResult> GetByNumeroPlaca(BmBienByPlacaRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmBienResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmBienResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_BIEN_GET_PLACA", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroPlaca);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapBien));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmBienUpsertRequest request)
    {
        return Ok(await MutateBienAsync("BM.SP_BM_BIEN_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmBienUpsertRequest request)
    {
        return Ok(await MutateBienAsync("BM.SP_BM_BIEN_UPD", request));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmBienResponse>>> MutateBienAsync(string procedureName, BmBienUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmBienResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmBienResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = request.CodigoArticulo;
        cmd.Parameters.Add("p_CodigoProveedor", OracleDbType.Int32).Value = request.CodigoProveedor == 0 ? DBNull.Value : request.CodigoProveedor;
        cmd.Parameters.Add("p_CodigoOrdenCompra", OracleDbType.Int32).Value = request.CodigoOrdenCompra == 0 ? DBNull.Value : request.CodigoOrdenCompra;
        cmd.Parameters.Add("p_OrigenId", OracleDbType.Int32).Value = request.OrigenId == 0 ? DBNull.Value : request.OrigenId;
        cmd.Parameters.Add("p_FechaFabricacion", OracleDbType.Date).Value = BmDb.DbValue(request.FechaFabricacion);
        cmd.Parameters.Add("p_NumeroOrdenCompra", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroOrdenCompra);
        cmd.Parameters.Add("p_FechaCompra", OracleDbType.Date).Value = BmDb.DbValue(request.FechaCompra);
        cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroPlaca);
        cmd.Parameters.Add("p_NumeroLote", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroLote);
        cmd.Parameters.Add("p_ValorInicial", OracleDbType.Decimal).Value = request.ValorInicial;
        cmd.Parameters.Add("p_ValorActual", OracleDbType.Decimal).Value = request.ValorActual;
        cmd.Parameters.Add("p_NumeroFactura", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroFactura);
        cmd.Parameters.Add("p_FechaFactura", OracleDbType.Date).Value = BmDb.DbValue(request.FechaFactura);
        cmd.Parameters.Add("p_TipoImpuestoId", OracleDbType.Int32).Value = request.TipoImpuestoId == 0 ? DBNull.Value : request.TipoImpuestoId;
        if (procedureName.EndsWith("_INS", StringComparison.OrdinalIgnoreCase))
        {
            cmd.Parameters.Add("p_Cantidad", OracleDbType.Int32).Value = request.Cantidad <= 0 ? 1 : request.Cantidad;
            cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien == 0 ? DBNull.Value : request.CodigoDirBien;
        }
        else if (procedureName.EndsWith("_UPD", StringComparison.OrdinalIgnoreCase))
        {
            cmd.Parameters.Add("p_UsuarioUpd", OracleDbType.Int32).Value = request.UsuarioId == 0 ? DBNull.Value : request.UsuarioId;
        }

        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapBien);
    }

    internal static BmBienResponse MapBien(IDataReader reader)
    {
        var fechaCompra = BmDb.GetDate(reader, "FECHA_COMPRA");
        var fechaFactura = BmDb.GetDate(reader, "FECHA_FACTURA");

        return new BmBienResponse(
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetInt32("CODIGO_ARTICULO"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("NUMERO_LOTE"),
            reader.SafeGetDecimal("VALOR_INICIAL"),
            reader.SafeGetDecimal("VALOR_ACTUAL"),
            fechaCompra,
            BmDb.ToDateString(fechaCompra),
            fechaFactura,
            BmDb.ToDateString(fechaFactura),
            reader.SafeGetString("NUMERO_FACTURA"),
            reader.SafeGetString("NUMERO_ORDEN_COMPRA"),
            reader.SafeGetInt32("CODIGO_PROVEEDOR"),
            reader.SafeGetString("PROVEEDOR"),
            reader.SafeGetInt32("ORIGEN_ID"),
            reader.SafeGetString("ORIGEN"),
            reader.SafeGetInt32("TIPO_IMPUESTO_ID"),
            reader.SafeGetString("TIPO_IMPUESTO"),
            reader.SafeGetString("ESPECIFICACION"),
            reader.SafeGetString("SERVICIO"),
            reader.SafeGetString("RESPONSABLE_BIEN"),
            reader.SafeGetString("UNIDAD_TRABAJO"),
            reader.SafeGetString("SEARCH_TEXT")
        );
    }
}
