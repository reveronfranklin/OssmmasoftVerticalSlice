using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.ReporteRelacionCompOpPagos;

public class ReporteRelacionCompOpPagosHandler(ConnectionDB conexiones, IConfiguration config)
{
    public async Task<ResultDto<CopReporte>> HandleAsync(ReporteRelacionCompOpPagosQuery query,
        CancellationToken cancelacion = default, int timeoutSeg = 240, int? maxFilas = null)
    {
        var error = ReporteRelacionCompOpPagosDb.Validar(query);
        if (error is not null) return Invalid(error);
        if (!ReporteRelacionCompOpPagosDb.Empresa(config, out var empresa))
            return Invalid("EmpresaConfig debe ser un numero valido mayor que cero.");
        using var cn = conexiones.GetPresupuestoConnection();
        try { await cn.OpenAsync(cancelacion); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Invalid($"Error tecnico al abrir conexion PRE: {ex.Message}"); }

        try
        {
            // Un mismo snapshot para las tres lecturas: no mezclar un compromiso
            // anterior a una anulacion con los pagos posteriores a ella.
            using var tx = cn.BeginTransaction(IsolationLevel.Serializable);
            var contador = 0;
            async Task<List<T>> Leer<T>(string procedimiento, Func<IDataRecord, T> mapear)
            {
                using var cmd = new OracleCommand(procedimiento, cn)
                {
                    Transaction = tx, CommandType = CommandType.StoredProcedure,
                    BindByName = true, CommandTimeout = Math.Max(1, timeoutSeg)
                };
                cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
                cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = query.FechaDesde!.Value.Date;
                cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = query.FechaHasta!.Value.Date;
                cmd.Parameters.Add("p_Proveedores", OracleDbType.Varchar2, 32767).Value =
                    ReporteRelacionCompOpPagosDb.DbValue(ReporteRelacionCompOpPagosDb.Proveedores(query.CodigosProveedores));
                cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
                var mensaje = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                var lista = new List<T>();
                using (var reader = await cmd.ExecuteReaderAsync(cancelacion))
                {
                    while (await reader.ReadAsync(cancelacion))
                    {
                        if (maxFilas is > 0 && ++contador > maxFilas.Value)
                            throw new CopLimiteException($"El reporte supera el limite de {maxFilas} filas, incluyendo ordenes y cheques. Refine las fechas o los proveedores.");
                        lista.Add(mapear(reader));
                    }
                }
                if (!ReporteRelacionCompOpPagosDb.Exito(mensaje))
                    throw new CopLecturaException(mensaje.Value?.ToString() ?? "Sin respuesta de base de datos.");
                return lista;
            }
            var bases = await Leer("PRE.SP_REP_COP_BASE_GET", ReporteRelacionCompOpPagosDb.Compromiso);
            var ordenes = await Leer("PRE.SP_REP_COP_OP_GET", ReporteRelacionCompOpPagosDb.Orden);
            var pagos = await Leer("PRE.SP_REP_COP_PAG_GET", ReporteRelacionCompOpPagosDb.Pago);
            var reporte = CopEnsamblador.Crear(bases, ordenes, pagos);
            tx.Commit();
            return new ResultDto<CopReporte>(reporte)
            {
                IsValid = true, CantidadRegistros = bases.Count,
                Total1 = reporte.TotalComprometido, Total2 = reporte.TotalOrdenado, Total3 = reporte.TotalPagado
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (CopLimiteException ex) { return Invalid(ex.Message); }
        catch (CopLecturaException ex) { return Invalid(ex.Message); }
        catch (Exception ex) { return Invalid($"Error tecnico: {ex.Message}"); }
    }

    private sealed class CopLimiteException(string message) : Exception(message);
    private sealed class CopLecturaException(string message) : Exception(message);
    private static ResultDto<CopReporte> Invalid(string mensaje) => new(null!) { IsValid = false, Message = mensaje };
}

[ApiController]
[Route("api/ReporteRelacionCompOpPagos")]
public class ReporteRelacionCompOpPagosController(ConnectionDB conexiones, IConfiguration config, IWebHostEnvironment ambiente) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(ReporteRelacionCompOpPagosQuery value, CancellationToken cancelacion) =>
        Ok(await new ReporteRelacionCompOpPagosHandler(conexiones, config).HandleAsync(value, cancelacion));

    [HttpPost("pdf")]
    public async Task<IActionResult> Pdf(ReporteRelacionCompOpPagosQuery value, CancellationToken cancelacion)
    {
        var usuario = User.Identity?.Name ?? value.Usuario;
        if (string.IsNullOrWhiteSpace(usuario))
            return Ok(new ResultDto<object?>(null) { IsValid = false, Message = "El usuario conectado es requerido para generar el reporte." });
        var resultado = await new ReporteRelacionCompOpPagosHandler(conexiones, config).HandleAsync(value, cancelacion);
        if (!resultado.IsValid || resultado.Data is null) return Ok(resultado);
        if (resultado.Data.Items.Count == 0)
            return Ok(new ResultDto<object?>(null) { IsValid = false, Message = "No hay compromisos con los filtros seleccionados." });
        cancelacion.ThrowIfCancellationRequested();
        var bytes = ReporteRelacionCompOpPagosPdfGenerator.Generate(resultado.Data, value, ReportPrintContext.Create(usuario), ambiente);
        Response.Headers.ContentDisposition = "inline; filename=\"relacion-compromisos-op-pagos.pdf\"";
        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }
}
