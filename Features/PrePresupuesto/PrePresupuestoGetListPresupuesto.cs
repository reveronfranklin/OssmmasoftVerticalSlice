using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.PrePresupuesto;

public class PrePresupuestoGetListPresupuestoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<PrePresupuestoListResponse>>> HandleAsync()
    {
        using var cn = _connectionDB.GetPresupuestoConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<List<PrePresupuestoListResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión PRE: {ex.Message}"
            };
        }

        try
        {
            // Los financiados de todos los presupuestos se traen de una sola vez
            // y se indexan por presupuesto. Asi la lista se arma con dos consultas
            // fijas en lugar de una por fila.
            var financiadosPorPresupuesto = await GetFinanciadosAsync(cn);

            return await GetPresupuestosAsync(cn, financiadosPorPresupuesto);
        }
        catch (Exception ex)
        {
            return new ResultDto<List<PrePresupuestoListResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error tecnico: {ex.Message}"
            };
        }
    }

    private static async Task<Dictionary<int, List<PrePresupuestoFinanciadoResponse>>> GetFinanciadosAsync(OracleConnection cn)
    {
        var financiados = new Dictionary<int, List<PrePresupuestoFinanciadoResponse>>();

        using var cmd = new OracleCommand("PRE.SP_PRE_PRESUP_FIN_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                int codigoPresupuesto = PrePresupuestoDb.GetInt(reader, "CODIGO_PRESUPUESTO");

                if (!financiados.TryGetValue(codigoPresupuesto, out var lista))
                {
                    lista = [];
                    financiados[codigoPresupuesto] = lista;
                }

                lista.Add(new PrePresupuestoFinanciadoResponse(
                    PrePresupuestoDb.GetInt(reader, "FINANCIADO_ID"),
                    PrePresupuestoDb.GetString(reader, "DESCRIPCION_FINANCIADO")
                ));
            }
        }

        string dbMessage = PrePresupuestoDb.GetMessage(pMessage);
        if (!PrePresupuestoDb.IsSuccessMessage(dbMessage))
        {
            throw new InvalidOperationException($"SP_PRE_PRESUP_FIN_GET: {dbMessage}");
        }

        return financiados;
    }

    private static async Task<ResultDto<List<PrePresupuestoListResponse>>> GetPresupuestosAsync(
        OracleConnection cn,
        Dictionary<int, List<PrePresupuestoFinanciadoResponse>> financiadosPorPresupuesto)
    {
        using var cmd = new OracleCommand("PRE.SP_PRE_PRESUP_LIST_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        var list = new List<PrePresupuestoListResponse>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                int codigoPresupuesto = PrePresupuestoDb.GetInt(reader, "CODIGO_PRESUPUESTO");

                list.Add(new PrePresupuestoListResponse(
                    codigoPresupuesto,
                    PrePresupuestoDb.GetString(reader, "DENOMINACION"),
                    PrePresupuestoDb.GetInt(reader, "ANO"),
                    PrePresupuestoDb.GetInt(reader, "PRESUPUESTO_EN_EJECUCION") == 1,
                    financiadosPorPresupuesto.TryGetValue(codigoPresupuesto, out var financiados)
                        ? financiados
                        : []
                ));
            }
        }

        string dbMessage = PrePresupuestoDb.GetMessage(pMessage);
        if (!PrePresupuestoDb.IsSuccessMessage(dbMessage))
        {
            return new ResultDto<List<PrePresupuestoListResponse>>(null!)
            {
                IsValid = false,
                Message = dbMessage
            };
        }

        // Cero presupuestos es una respuesta valida, no un error.
        return new ResultDto<List<PrePresupuestoListResponse>>(list)
        {
            IsValid = true,
            Message = string.Empty
        };
    }
}

[ApiController]
[Route("api/PrePresupuesto")]
public class PrePresupuestoGetListPresupuestoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpGet]
    [Route("GetListPresupuesto")]
    public async Task<IActionResult> GetListPresupuesto()
    {
        var handler = new PrePresupuestoGetListPresupuestoHandler(_connectionDB);
        var result = await handler.HandleAsync();

        return Ok(result);
    }
}
