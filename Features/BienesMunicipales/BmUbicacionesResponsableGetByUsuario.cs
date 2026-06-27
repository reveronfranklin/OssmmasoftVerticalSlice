using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmUbicacionesResponsable")]
public class BmUbicacionesResponsableController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByUsuarioResponsable")]
    public async Task<IActionResult> GetByUsuarioResponsable(BmUbicacionResponsableRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmUbicacionResponsableResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmUbicacionResponsableResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_UBI_RESP_GET", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoUsuario", OracleDbType.Int32).Value = request.CodigoUsuario;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmUbicacionResponsableResponse(
            reader.SafeGetInt32("CODIGO_BM_CONTEO"),
            reader.SafeGetInt32("CONTEO"),
            reader.SafeGetString("TITULO"),
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetInt32("CODIGO_USUARIO"),
            reader.SafeGetInt32("CODIGO_PERSONA"),
            reader.SafeGetString("LOGIN"),
            reader.SafeGetInt32("CEDULA"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("KEY_UBICACION_RESPONSABLE")
        )));
    }
}
