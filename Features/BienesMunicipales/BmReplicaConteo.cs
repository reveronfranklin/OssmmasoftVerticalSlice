using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmReplicaConteo")]
public class BmReplicaConteoController(BmReplicaConteoService service, BmReplicaEstado estado, IConfiguration config) : ControllerBase
{
    [HttpGet("Estado")]
    public IActionResult Estado() => Ok(estado.Resultado());

    [HttpPost("Iniciar")]
    public IActionResult Iniciar()
    {
        if (config["settings:ReplicarConteo"] != "1")
            return Ok(estado.Resultado(false, "La replica esta deshabilitada en la configuracion."));
        if (!estado.Iniciar())
            return Ok(estado.Resultado(false, "Ya hay una replica en curso."));
        if (!estado.Solicitudes.Writer.TryWrite(true))
        {
            estado.Terminar(false, "No fue posible iniciar la replica.");
            return Ok(estado.Resultado(false));
        }
        return Ok(estado.Resultado());
    }

    [HttpPost("Replicar")]
    public async Task<IActionResult> Replicar() => Ok(await service.ReplicarAsync());
}

public class BmReplicaConteoService(ConnectionDB connectionDB, IConfiguration config, BmReplicaEstado estado)
{

    public async Task<ResultDto<List<BmReplicaConteoResponse>>> ReplicarAsync(bool reservada = false)
    {
        if (!reservada && !estado.Iniciar()) return BmDb.InvalidList<BmReplicaConteoResponse>("Ya hay una replica en curso.");
        if (!string.Equals(config["settings:ReplicarConteo"], "1", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid(
                "La replica de conteo esta deshabilitada en la configuracion (settings:ReplicarConteo)."
            );
        }

        try
        {
            if (!string.Equals(config["settings:ReplicarConteo"], "1", StringComparison.OrdinalIgnoreCase))
            {
                return Invalid(
                    "La replica de conteo fue deshabilitada antes de iniciar su ejecucion."
                );
            }

            using var bm = connectionDB.GetBmConnection();
            var bmError = await BmDb.TryOpenAsync(bm, "BM origen");
            if (bmError is not null) return Invalid(bmError);

            using var rh = connectionDB.GetRhConnection();
            var rhError = await BmDb.TryOpenAsync(rh, "RH origen");
            if (rhError is not null) return Invalid(rhError);

            var articulos = await ReadTableAsync(bm, "BM.BM_ARTICULOS");
            var bienes = await ReadTableAsync(bm, "BM.BM_BIENES");
            var movimientos = await ReadTableAsync(bm, "BM.BM_MOV_BIENES");
            var direcciones = await ReadTableAsync(bm, "BM.BM_DIR_BIEN");
            var clasificaciones = await ReadTableAsync(bm, "BM.BM_CLASIFICACION_BIENES");
            var descriptivas = await ReadTableAsync(bm, "BM.BM_DESCRIPTIVAS");
            var personas = await ReadTableAsync(rh, "RH.RH_PERSONAS");

            // Legacy usa una conexion distinta para la replica de personas: RH es
            // el origen y RHC es el esquema replica en el servidor de conteo.
            using var rhc = connectionDB.GetRhcConnection();
            var rhcError = await BmDb.TryOpenAsync(rhc, "RHC destino");
            if (rhcError is not null) return Invalid(rhcError);

            using (var rhcTx = rhc.BeginTransaction())
            {
                try
                {
                    await ReplaceTableAsync(rhc, rhcTx, "RHC.RH_PERSONAS", personas);
                    await VerifyRowCountAsync(rhc, rhcTx, "RHC.RH_PERSONAS", personas.Rows.Count);
                    rhcTx.Commit();
                    estado.Tabla("RHC.RH_PERSONAS", "Confirmada");
                }
                catch
                {
                    rhcTx.Rollback();
                    throw;
                }
            }

            using var bmc = connectionDB.GetBmcConnection();
            var bmcError = await BmDb.TryOpenAsync(bmc, "BMC destino");
            if (bmcError is not null) return Invalid(bmcError);

            using var tx = bmc.BeginTransaction();
            try
            {
                await ReplaceTableAsync(bmc, tx, "BMC.BM_ARTICULOS", articulos);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_BIENES", bienes);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_MOV_BIENES", movimientos);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_DIR_BIEN", direcciones);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_CLASIFICACION_BIENES", clasificaciones);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_DESCRIPTIVAS", descriptivas);
                await VerifyRowCountAsync(bmc, tx, "BMC.BM_ARTICULOS", articulos.Rows.Count);
                await VerifyRowCountAsync(bmc, tx, "BMC.BM_BIENES", bienes.Rows.Count);
                await VerifyRowCountAsync(bmc, tx, "BMC.BM_MOV_BIENES", movimientos.Rows.Count);
                await VerifyRowCountAsync(bmc, tx, "BMC.BM_DIR_BIEN", direcciones.Rows.Count);
                await VerifyRowCountAsync(
                    bmc,
                    tx,
                    "BMC.BM_CLASIFICACION_BIENES",
                    clasificaciones.Rows.Count);
                await VerifyRowCountAsync(bmc, tx, "BMC.BM_DESCRIPTIVAS", descriptivas.Rows.Count);
                tx.Commit();
                foreach (var tabla in estado.Leer().Tablas.Where(t => t.Tabla.StartsWith("BMC.")))
                    estado.Tabla(tabla.Tabla, "Confirmada");
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            var response = new BmReplicaConteoResponse(
                articulos.Rows.Count,
                bienes.Rows.Count,
                movimientos.Rows.Count,
                direcciones.Rows.Count,
                clasificaciones.Rows.Count,
                personas.Rows.Count);

            estado.Terminar(true, "Replica completada correctamente.");
            return new ResultDto<List<BmReplicaConteoResponse>>(new List<BmReplicaConteoResponse> { response })
            {
                IsValid = true,
                Message = "Success",
                CantidadRegistros = articulos.Rows.Count + bienes.Rows.Count + movimientos.Rows.Count
                    + direcciones.Rows.Count + clasificaciones.Rows.Count + descriptivas.Rows.Count
                    + personas.Rows.Count
            };
        }
        catch (Exception ex)
        {
            return Invalid($"Error tecnico al replicar datos: {ex.Message}");
        }
    }

    private ResultDto<List<BmReplicaConteoResponse>> Invalid(string message)
    {
        estado.Terminar(false, message);
        return BmDb.InvalidList<BmReplicaConteoResponse>(message);
    }

    private async Task<DataTable> ReadTableAsync(OracleConnection cn, string tableName)
    {
        var destino = tableName.Replace("BM.", "BMC.").Replace("RH.", "RHC.");
        estado.Tabla(destino, "Leyendo");
        using var cmd = new OracleCommand($"SELECT * FROM {tableName}", cn) { BindByName = true };
        using var reader = await cmd.ExecuteReaderAsync();
        var table = new DataTable();
        table.Load(reader);
        estado.Tabla(destino, "Leida", table.Rows.Count);
        return table;
    }

    private async Task ReplaceTableAsync(
        OracleConnection cn,
        OracleTransaction tx,
        string tableName,
        DataTable source)
    {
        estado.Tabla(tableName, "Copiando", source.Rows.Count, 0);
        using (var delete = new OracleCommand($"DELETE FROM {tableName}", cn) { Transaction = tx })
        {
            await delete.ExecuteNonQueryAsync();
        }

        if (source.Rows.Count == 0) return;

        var columns = source.Columns.Cast<DataColumn>().ToList();
        var columnSql = string.Join(", ", columns.Select(column => $"\"{column.ColumnName}\""));
        var valueSql = string.Join(", ", columns.Select((_, index) => $":p{index}"));

        using var insert = new OracleCommand($"INSERT INTO {tableName} ({columnSql}) VALUES ({valueSql})", cn)
        {
            Transaction = tx,
            BindByName = true
        };

        for (var index = 0; index < columns.Count; index++)
        {
            insert.Parameters.Add($"p{index}", MapOracleType(columns[index].DataType));
        }

        var copiados = 0;
        foreach (DataRow row in source.Rows)
        {
            for (var index = 0; index < columns.Count; index++)
            {
                insert.Parameters[index].Value = row.IsNull(index) ? DBNull.Value : row[index];
            }
            await insert.ExecuteNonQueryAsync();
            copiados++;
            if (copiados % 100 == 0 || copiados == source.Rows.Count)
                estado.Tabla(tableName, "Copiando", source.Rows.Count, copiados);
        }
    }

    private async Task VerifyRowCountAsync(
        OracleConnection cn,
        OracleTransaction tx,
        string tableName,
        int expected)
    {
        estado.Tabla(tableName, "Verificando");
        using var cmd = new OracleCommand($"SELECT COUNT(*) FROM {tableName}", cn)
        {
            Transaction = tx,
            BindByName = true
        };
        var value = await cmd.ExecuteScalarAsync();
        var actual = Convert.ToInt32(value);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"La verificacion de {tableName} fallo. Origen: {expected}; destino: {actual}.");
        }
    }

    private static OracleDbType MapOracleType(Type type)
    {
        if (type == typeof(string)) return OracleDbType.Varchar2;
        if (type == typeof(DateTime)) return OracleDbType.Date;
        if (type == typeof(byte[])) return OracleDbType.Raw;
        if (type == typeof(float) || type == typeof(double)) return OracleDbType.Double;
        return OracleDbType.Decimal;
    }
}

public class BmReplicaConteoWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<BmReplicaConteoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutos = Math.Max(1, config.GetValue<int?>("settings:ReplicarConteoIntervaloMinutos") ?? 60);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutos));
        logger.LogInformation("Worker de replica BM/BMC iniciado. Intervalo: {Minutos} minutos.", minutos);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var enabled = config["settings:ReplicarConteo"];
            if (!string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Replica BM/BMC omitida porque settings:ReplicarConteo esta deshabilitado.");
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<BmReplicaConteoService>();
                var result = await service.ReplicarAsync();
                if (result.IsValid)
                {
                    logger.LogInformation("Replica BM/BMC completada. Registros: {Total}.", result.CantidadRegistros);
                }
                else
                {
                    logger.LogError("Replica BM/BMC fallo: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error no controlado en el worker de replica BM/BMC.");
            }
        }
    }
}
