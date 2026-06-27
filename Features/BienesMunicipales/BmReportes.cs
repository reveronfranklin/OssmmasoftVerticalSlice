using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Globalization;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmReportes")]
public class BmReportesController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("Placa")]
    public async Task<IActionResult> Placa(BmReportePlacaRequest request)
    {
        return Ok(await GetPlacaAsync(request));
    }

    [HttpPost("PlacaPdf")]
    public async Task<IActionResult> PlacaPdf(BmReportePlacaRequest request)
    {
        var result = await GetPlacaAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GeneratePdf(
            "REPORTE DE BIEN POR PLACA",
            $"Placa: {request.NumeroPlaca}",
            result.Data,
            BmReportesExport.PlacaColumns());

        return BmReportesExport.BuildPdfFile(this, "bm-reporte-placa", bytes);
    }

    [HttpPost("PlacaExcel")]
    public async Task<IActionResult> PlacaExcel(BmReportePlacaRequest request)
    {
        var result = await GetPlacaAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GenerateExcel("Placa", result.Data, BmReportesExport.PlacaColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-placa", bytes);
    }

    [HttpPost("Lote")]
    public async Task<IActionResult> Lote(BmReporteLoteRequest request) => Ok(await GetLoteAsync(request));

    [HttpPost("LotePdf")]
    public async Task<IActionResult> LotePdf(BmReporteLoteRequest request)
    {
        var result = await GetLoteAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GeneratePdf("REPORTE DE BIENES INCORPORADOS POR LOTE", $"Lote: {request.NumeroLote}", result.Data, BmReportesExport.LoteColumns());
        return BmReportesExport.BuildPdfFile(this, "bm-reporte-lote", bytes);
    }

    [HttpPost("LoteExcel")]
    public async Task<IActionResult> LoteExcel(BmReporteLoteRequest request)
    {
        var result = await GetLoteAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GenerateExcel("Lote", result.Data, BmReportesExport.LoteColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-lote", bytes);
    }

    [HttpPost("Ficha")]
    public async Task<IActionResult> Ficha(BmReportePlacaRequest request) => Ok(await GetFichaAsync(request));

    [HttpPost("FichaPdf")]
    public async Task<IActionResult> FichaPdf(BmReportePlacaRequest request)
    {
        var result = await GetFichaAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GeneratePdf("REPORTE DE FICHA DEL BIEN", $"Placa: {request.NumeroPlaca}", result.Data, BmReportesExport.FichaColumns());
        return BmReportesExport.BuildPdfFile(this, "bm-reporte-ficha", bytes);
    }

    [HttpPost("FichaExcel")]
    public async Task<IActionResult> FichaExcel(BmReportePlacaRequest request)
    {
        var result = await GetFichaAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GenerateExcel("Ficha", result.Data, BmReportesExport.FichaColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-ficha", bytes);
    }

    [HttpPost("Ubicacion")]
    public async Task<IActionResult> Ubicacion(BmReporteUbicacionRequest request)
    {
        return Ok(await GetUbicacionAsync(request));
    }

    [HttpPost("UbicacionPdf")]
    public async Task<IActionResult> UbicacionPdf(BmReporteUbicacionRequest request)
    {
        var result = await GetUbicacionAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GeneratePdf(
            "REPORTE DE BIENES POR UBICACION",
            $"Codigo ICP: {request.CodigoIcp}",
            result.Data,
            BmReportesExport.UbicacionColumns());

        return BmReportesExport.BuildPdfFile(this, "bm-reporte-ubicacion", bytes);
    }

    [HttpPost("UbicacionExcel")]
    public async Task<IActionResult> UbicacionExcel(BmReporteUbicacionRequest request)
    {
        var result = await GetUbicacionAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GenerateExcel("Ubicacion", result.Data, BmReportesExport.UbicacionColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-ubicacion", bytes);
    }

    [HttpPost("Movimientos")]
    public async Task<IActionResult> Movimientos(BmReporteMovimientoRequest request)
    {
        return Ok(await GetMovimientosAsync(request));
    }

    [HttpPost("MovimientosPdf")]
    public async Task<IActionResult> MovimientosPdf(BmReporteMovimientoRequest request)
    {
        var result = await GetMovimientosAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GeneratePdf(
            "REPORTE DE MOVIMIENTOS DEL BIEN",
            $"Codigo bien: {request.CodigoBien}",
            result.Data,
            BmReportesExport.MovimientoColumns());

        return BmReportesExport.BuildPdfFile(this, "bm-reporte-movimientos", bytes);
    }

    [HttpPost("MovimientosExcel")]
    public async Task<IActionResult> MovimientosExcel(BmReporteMovimientoRequest request)
    {
        var result = await GetMovimientosAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GenerateExcel("Movimientos", result.Data, BmReportesExport.MovimientoColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-movimientos", bytes);
    }

    [HttpPost("MovimientosFiltro")]
    public async Task<IActionResult> MovimientosFiltro(BmReporteMovimientoFiltroRequest request) => Ok(await GetMovimientosFiltroAsync(request));

    [HttpPost("MovimientosFiltroPdf")]
    public async Task<IActionResult> MovimientosFiltroPdf(BmReporteMovimientoFiltroRequest request)
    {
        var result = await GetMovimientosFiltroAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GeneratePdf("REPORTE DE MOVIMIENTOS POR FILTRO", BuildMovimientoFilter(request), result.Data, BmReportesExport.MovimientoColumns());
        return BmReportesExport.BuildPdfFile(this, "bm-reporte-movimientos-filtro", bytes);
    }

    [HttpPost("MovimientosFiltroExcel")]
    public async Task<IActionResult> MovimientosFiltroExcel(BmReporteMovimientoFiltroRequest request)
    {
        var result = await GetMovimientosFiltroAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GenerateExcel("Movimientos", result.Data, BmReportesExport.MovimientoColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-movimientos-filtro", bytes);
    }

    [HttpPost("Solicitudes")]
    public async Task<IActionResult> Solicitudes(BmReporteSolicitudRequest request) => Ok(await GetSolicitudesAsync(request));

    [HttpPost("SolicitudesPdf")]
    public async Task<IActionResult> SolicitudesPdf(BmReporteSolicitudRequest request)
    {
        var result = await GetSolicitudesAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GeneratePdf("REPORTE DE SOLICITUDES DE MOVIMIENTO", BuildSolicitudFilter(request), result.Data, BmReportesExport.SolicitudColumns());
        return BmReportesExport.BuildPdfFile(this, "bm-reporte-solicitudes", bytes);
    }

    [HttpPost("SolicitudesExcel")]
    public async Task<IActionResult> SolicitudesExcel(BmReporteSolicitudRequest request)
    {
        var result = await GetSolicitudesAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GenerateExcel("Solicitudes", result.Data, BmReportesExport.SolicitudColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-solicitudes", bytes);
    }

    [HttpPost("ProcesosMasivos")]
    public async Task<IActionResult> ProcesosMasivos(BmReporteProcesoMasivoRequest request) => Ok(await GetProcesosMasivosAsync(request));

    [HttpPost("ProcesosMasivosPdf")]
    public async Task<IActionResult> ProcesosMasivosPdf(BmReporteProcesoMasivoRequest request)
    {
        var result = await GetProcesosMasivosAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GeneratePdf("REPORTE DE PROCESOS MASIVOS", BuildProcesoFilter(request), result.Data, BmReportesExport.ProcesoMasivoColumns());
        return BmReportesExport.BuildPdfFile(this, "bm-reporte-procesos-masivos", bytes);
    }

    [HttpPost("ProcesosMasivosExcel")]
    public async Task<IActionResult> ProcesosMasivosExcel(BmReporteProcesoMasivoRequest request)
    {
        var result = await GetProcesosMasivosAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);
        var bytes = BmReportesExport.GenerateExcel("Procesos", result.Data, BmReportesExport.ProcesoMasivoColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-procesos-masivos", bytes);
    }

    [HttpPost("ConteoDiferencias")]
    public async Task<IActionResult> ConteoDiferencias(BmReporteConteoRequest request)
    {
        return Ok(await GetConteoDiferenciasAsync(request));
    }

    [HttpPost("ConteoDiferenciasPdf")]
    public async Task<IActionResult> ConteoDiferenciasPdf(BmReporteConteoRequest request)
    {
        var result = await GetConteoDiferenciasAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GeneratePdf(
            "REPORTE DE DIFERENCIAS DE CONTEO",
            $"Codigo conteo: {request.CodigoBmConteo}",
            result.Data,
            BmReportesExport.ConteoDifColumns());

        return BmReportesExport.BuildPdfFile(this, "bm-reporte-conteo-diferencias", bytes);
    }

    [HttpPost("ConteoDiferenciasExcel")]
    public async Task<IActionResult> ConteoDiferenciasExcel(BmReporteConteoRequest request)
    {
        var result = await GetConteoDiferenciasAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GenerateExcel("Diferencias", result.Data, BmReportesExport.ConteoDifColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-conteo-diferencias", bytes);
    }

    [HttpPost("ConteoHistorico")]
    public async Task<IActionResult> ConteoHistorico(BmReporteConteoHistRequest request)
    {
        return Ok(await GetConteoHistoricoAsync(request));
    }

    [HttpPost("ConteoHistoricoPdf")]
    public async Task<IActionResult> ConteoHistoricoPdf(BmReporteConteoHistRequest request)
    {
        var result = await GetConteoHistoricoAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GeneratePdf(
            "REPORTE HISTORICO DE CONTEOS",
            BuildConteoHistoricoFilter(request),
            result.Data,
            BmReportesExport.ConteoHistColumns());

        return BmReportesExport.BuildPdfFile(this, "bm-reporte-conteo-historico", bytes);
    }

    [HttpPost("ConteoHistoricoExcel")]
    public async Task<IActionResult> ConteoHistoricoExcel(BmReporteConteoHistRequest request)
    {
        var result = await GetConteoHistoricoAsync(request);
        if (!result.IsValid || result.Data is null) return Ok(result);

        var bytes = BmReportesExport.GenerateExcel("Historico", result.Data, BmReportesExport.ConteoHistColumns());
        return BmReportesExport.BuildExcelFile(this, "bm-reporte-conteo-historico", bytes);
    }

    private async Task<ResultDto<List<BmReportePlacaResponse>>> GetPlacaAsync(BmReportePlacaRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmReportePlacaResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmReportePlacaResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_PLACA", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroPlaca);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapPlaca);
    }

    private async Task<ResultDto<List<BmReporteUbicacionResponse>>> GetUbicacionAsync(BmReporteUbicacionRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmReporteUbicacionResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmReporteUbicacionResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_UBI_ICP", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoIcp", OracleDbType.Int32).Value = request.CodigoIcp;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, reader => new BmReporteUbicacionResponse(
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            reader.SafeGetString("DIRECCION"),
            reader.SafeGetInt32("TOTAL_BIENES"),
            reader.SafeGetDecimal("VALOR_TOTAL")
        ));
    }

    private async Task<ResultDto<List<BmReporteLoteResponse>>> GetLoteAsync(BmReporteLoteRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error)) return BmDb.InvalidList<BmReporteLoteResponse>(error);
        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmReporteLoteResponse>(openError);
        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_LOTE", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_NumeroLote", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroLote);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        return await BmDb.ExecuteListAsync(cmd, reader => new BmReporteLoteResponse(
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("NUMERO_LOTE"),
            reader.SafeGetString("ARTICULO"),
            BmDb.GetDate(reader, "FECHA_INS"),
            BmDb.GetDate(reader, "FECHA_COMPRA"),
            reader.SafeGetDecimal("VALOR_INICIAL"),
            reader.SafeGetDecimal("VALOR_ACTUAL"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetString("RESPONSABLE_BIEN")));
    }

    private async Task<ResultDto<List<BmReporteFichaResponse>>> GetFichaAsync(BmReportePlacaRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error)) return BmDb.InvalidList<BmReporteFichaResponse>(error);
        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmReporteFichaResponse>(openError);
        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_FICHA", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroPlaca);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        return await BmDb.ExecuteListAsync(cmd, reader => new BmReporteFichaResponse(
            reader.SafeGetString("SECCION"),
            reader.SafeGetString("REFERENCIA"),
            reader.SafeGetString("DESCRIPCION"),
            BmDb.GetDate(reader, "FECHA"),
            reader.SafeGetString("UNIDAD"),
            reader.SafeGetString("OBSERVACION")));
    }

    private async Task<ResultDto<List<BmMovimientoResponse>>> GetMovimientosAsync(BmReporteMovimientoRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmMovimientoResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmMovimientoResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_MOV_BIEN", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, BmMovBienesController.MapMovimiento);
    }

    private async Task<ResultDto<List<BmMovimientoResponse>>> GetMovimientosFiltroAsync(BmReporteMovimientoFiltroRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmMovimientoResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmMovimientoResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_MOV_FILT", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_TipoMovimiento", OracleDbType.Varchar2).Value = BmDb.DbValue(request.TipoMovimiento);
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = BmDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = BmDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_CodigoIcp", OracleDbType.Int32).Value = request.CodigoIcp;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, BmMovBienesController.MapMovimiento);
    }

    private async Task<ResultDto<List<BmReporteSolicitudResponse>>> GetSolicitudesAsync(BmReporteSolicitudRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmReporteSolicitudResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmReporteSolicitudResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_SOL_MOV", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Aprobado", OracleDbType.Int32).Value = request.Aprobado;
        cmd.Parameters.Add("p_TipoMovimiento", OracleDbType.Varchar2).Value = BmDb.DbValue(request.TipoMovimiento);
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = BmDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = BmDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, reader => new BmReporteSolicitudResponse(
            reader.SafeGetInt32("CODIGO_SOL_MOV_BIEN"),
            reader.SafeGetString("NUMERO_SOLICITUD"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("TIPO_MOVIMIENTO"),
            reader.SafeGetString("TIPO_MOVIMIENTO_DESC"),
            BmDb.GetDate(reader, "FECHA_MOVIMIENTO"),
            reader.SafeGetBoolean("APROBADO"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetString("CONCEPTO_MOVIMIENTO"),
            reader.SafeGetString("NOTA_INCIDENCIA")
        ));
    }

    private async Task<ResultDto<List<BmProcesoMasivoResponse>>> GetProcesosMasivosAsync(BmReporteProcesoMasivoRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmProcesoMasivoResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmProcesoMasivoResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_REP_PROC_MAS", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoProcMasivo", OracleDbType.Int32).Value = request.CodigoProcesoMasivo;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = BmDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = BmDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapProcesoMasivo);
    }

    private async Task<ResultDto<List<BmReporteConteoDifResponse>>> GetConteoDiferenciasAsync(BmReporteConteoRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmReporteConteoDifResponse>(error);
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return BmDb.InvalidList<BmReporteConteoDifResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_REP_CONT_DIF", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, reader => new BmReporteConteoDifResponse(
            reader.SafeGetInt32("CODIGO_BM_CONTEO"),
            reader.SafeGetInt32("CODIGO_BM_CONTEO_DETALLE"),
            reader.SafeGetInt32("CONTEO"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_TRABAJO"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetInt32("CANTIDAD"),
            reader.SafeGetInt32("CANTIDAD_CONTADA"),
            reader.SafeGetInt32("DIFERENCIA"),
            reader.SafeGetString("COMENTARIO")
        ));
    }

    private async Task<ResultDto<List<BmReporteConteoHistResponse>>> GetConteoHistoricoAsync(BmReporteConteoHistRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmReporteConteoHistResponse>(error);
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return BmDb.InvalidList<BmReporteConteoHistResponse>(openError);

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_REP_CONT_HIST", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = BmDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = BmDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, reader => new BmReporteConteoHistResponse(
            reader.SafeGetInt32("CODIGO_BM_CONTEO"),
            reader.SafeGetString("TITULO"),
            BmDb.GetDate(reader, "FECHA"),
            BmDb.GetDate(reader, "FECHA_CIERRE"),
            reader.SafeGetInt32("TOTAL_CANTIDAD"),
            reader.SafeGetInt32("TOTAL_CANTIDAD_CONTADA"),
            reader.SafeGetInt32("TOTAL_DIFERENCIA"),
            reader.SafeGetString("COMENTARIO")
        ));
    }

    private static BmReportePlacaResponse MapPlaca(IDataReader reader)
    {
        return new BmReportePlacaResponse(
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("ESPECIFICACION"),
            reader.SafeGetDecimal("VALOR_INICIAL"),
            reader.SafeGetDecimal("VALOR_ACTUAL"),
            reader.SafeGetInt32("CODIGO_MOV_BIEN"),
            reader.SafeGetString("TIPO_MOVIMIENTO"),
            BmDb.GetDate(reader, "FECHA_MOVIMIENTO"),
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetString("RESPONSABLE_BIEN"),
            reader.SafeGetString("ESTADO_OPERATIVO")
        );
    }

    private static BmProcesoMasivoResponse MapProcesoMasivo(IDataReader reader)
    {
        return new BmProcesoMasivoResponse(
            reader.SafeGetInt32("CODIGO_PROC_MASIVO"),
            reader.SafeGetInt32("CODIGO_PROC_MAS_DET"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetInt32("CODIGO_DIR_ORIGEN"),
            reader.SafeGetInt32("CODIGO_ICP_ORIGEN"),
            reader.SafeGetString("UNIDAD_ORIGEN"),
            reader.SafeGetInt32("CODIGO_DIR_DESTINO"),
            reader.SafeGetString("UNIDAD_DESTINO"),
            reader.SafeGetString("ESTADO"),
            reader.SafeGetString("MENSAJE"),
            reader.SafeGetInt32("CODIGO_MOV_BIEN"),
            reader.SafeGetInt32("TOTAL_PROCESADOS"),
            reader.SafeGetInt32("TOTAL_EXITOSOS"),
            reader.SafeGetInt32("TOTAL_RECHAZADOS")
        );
    }

    private static string BuildConteoHistoricoFilter(BmReporteConteoHistRequest request)
    {
        var desde = request.FechaDesde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha desde";
        var hasta = request.FechaHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha hasta";
        return $"Fecha desde: {desde} | Fecha hasta: {hasta}";
    }

    private static string BuildMovimientoFilter(BmReporteMovimientoFiltroRequest request)
    {
        var tipo = string.IsNullOrWhiteSpace(request.TipoMovimiento) ? "Todos" : request.TipoMovimiento;
        var desde = request.FechaDesde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha desde";
        var hasta = request.FechaHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha hasta";
        var icp = request.CodigoIcp > 0 ? request.CodigoIcp.ToString(CultureInfo.InvariantCulture) : "Todos";
        return $"Tipo: {tipo} | Fecha desde: {desde} | Fecha hasta: {hasta} | ICP: {icp}";
    }

    private static string BuildSolicitudFilter(BmReporteSolicitudRequest request)
    {
        var aprobado = request.Aprobado < 0 ? "Todas" : request.Aprobado == 1 ? "Aprobadas" : "Pendientes";
        var tipo = string.IsNullOrWhiteSpace(request.TipoMovimiento) ? "Todos" : request.TipoMovimiento;
        var desde = request.FechaDesde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha desde";
        var hasta = request.FechaHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha hasta";
        return $"Estado: {aprobado} | Tipo: {tipo} | Fecha desde: {desde} | Fecha hasta: {hasta}";
    }

    private static string BuildProcesoFilter(BmReporteProcesoMasivoRequest request)
    {
        var proceso = request.CodigoProcesoMasivo > 0
            ? request.CodigoProcesoMasivo.ToString(CultureInfo.InvariantCulture)
            : "Todos";
        var desde = request.FechaDesde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha desde";
        var hasta = request.FechaHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha hasta";
        return $"Proceso: {proceso} | Fecha desde: {desde} | Fecha hasta: {hasta}";
    }
}
