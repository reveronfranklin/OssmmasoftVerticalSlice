using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmReplicaConteo")]
public class BmReplicaConteoController(ConnectionDB connectionDB) : ControllerBase
{
    [HttpPost("Replicar")]
    public async Task<IActionResult> Replicar()
    {
        try
        {
            using var bm = connectionDB.GetBmConnection();
            var bmError = await BmDb.TryOpenAsync(bm, "BM origen");
            if (bmError is not null) return Ok(Invalid(bmError));

            using var rh = connectionDB.GetRhConnection();
            var rhError = await BmDb.TryOpenAsync(rh, "RH origen");
            if (rhError is not null) return Ok(Invalid(rhError));

            var articulos = await ReadTableAsync(bm, "BM.BM_ARTICULOS");
            var bienes = await ReadTableAsync(bm, "BM.BM_BIENES");
            var movimientos = await ReadTableAsync(bm, "BM.BM_MOV_BIENES");
            var direcciones = await ReadTableAsync(bm, "BM.BM_DIR_BIEN");
            var clasificaciones = await ReadTableAsync(bm, "BM.BM_CLASIFICACION_BIENES");
            var personas = await ReadTableAsync(rh, "RH.RH_PERSONAS");

            using var bmc = connectionDB.GetBmcConnection();
            var bmcError = await BmDb.TryOpenAsync(bmc, "BMC destino");
            if (bmcError is not null) return Ok(Invalid(bmcError));

            using var tx = bmc.BeginTransaction();
            try
            {
                await ReplaceTableAsync(bmc, tx, "BMC.BM_ARTICULOS", articulos);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_BIENES", bienes);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_MOV_BIENES", movimientos);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_DIR_BIEN", direcciones);
                await ReplaceTableAsync(bmc, tx, "BMC.BM_CLASIFICACION_BIENES", clasificaciones);
                await ReplaceTableAsync(bmc, tx, "BMC.RH_PERSONAS", personas);
                tx.Commit();
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

            return Ok(new ResultDto<List<BmReplicaConteoResponse>>(new List<BmReplicaConteoResponse> { response })
            {
                IsValid = true,
                Message = "Success",
                CantidadRegistros = articulos.Rows.Count + bienes.Rows.Count + movimientos.Rows.Count
                    + direcciones.Rows.Count + clasificaciones.Rows.Count + personas.Rows.Count
            });
        }
        catch (Exception ex)
        {
            return Ok(Invalid($"Error tecnico al replicar datos: {ex.Message}"));
        }
    }

    private static ResultDto<List<BmReplicaConteoResponse>> Invalid(string message) =>
        BmDb.InvalidList<BmReplicaConteoResponse>(message);

    private static async Task<DataTable> ReadTableAsync(OracleConnection cn, string tableName)
    {
        using var cmd = new OracleCommand($"SELECT * FROM {tableName}", cn) { BindByName = true };
        using var reader = await cmd.ExecuteReaderAsync();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private static async Task ReplaceTableAsync(
        OracleConnection cn,
        OracleTransaction tx,
        string tableName,
        DataTable source)
    {
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

        foreach (DataRow row in source.Rows)
        {
            for (var index = 0; index < columns.Count; index++)
            {
                insert.Parameters[index].Value = row.IsNull(index) ? DBNull.Value : row[index];
            }
            await insert.ExecuteNonQueryAsync();
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
