using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmDescriptivas")]
public class BmDescriptivasController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByTitulo")]
    public async Task<IActionResult> GetByTitulo(BmDescriptivaByTituloRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmDescriptivaResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmDescriptivaResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_DESC_GET_TIT", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_TituloId", OracleDbType.Int32).Value = request.TituloId;
        cmd.Parameters.Add("p_DescripcionId", OracleDbType.Int32).Value = request.DescripcionId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmDescriptivaResponse(
            reader.SafeGetInt32("ID"),
            reader.SafeGetInt32("DESCRIPCION_ID"),
            request.TituloId,
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3")
        )));
    }

    [HttpPost("GetByFk")]
    public async Task<IActionResult> GetByFk(BmDescriptivaByFkRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmDescriptivaResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmDescriptivaResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_DESC_GET_FK", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_DescripcionFkId", OracleDbType.Int32).Value = request.DescripcionFkId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmDescriptivaResponse(
            reader.SafeGetInt32("ID"),
            reader.SafeGetInt32("DESCRIPCION_ID"),
            0,
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3")
        )));
    }
}
