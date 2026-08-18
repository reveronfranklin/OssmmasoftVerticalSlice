using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Catalogo de tipos de campo. Alimenta la paleta del diseñador y, sobre todo,
/// el mapa COMPONENTE -> componente React del renderizador: cada fila de aqui
/// tiene que tener su contraparte registrada en registroTipos.ts.
/// </summary>
[ApiController]
[Route("api/MfoCatalogo")]
public class MfoCatalogoController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    /// <summary>
    /// Resuelve las opciones de un campo con <c>ORIGEN_OPCIONES = 'CATALOGO'</c>.
    ///
    /// La clave se busca en la lista blanca de <see cref="MfoCatalogoRegistro"/>;
    /// **nunca se concatena en SQL**. Si no esta registrada, la peticion falla y
    /// no se ejecuta nada, aunque la clave venga de una fila de MFO_CAMPO.
    /// </summary>
    [HttpGet("opciones")]
    public async Task<IActionResult> Opciones([FromQuery] string clave)
    {
        if (!MfoDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(MfoDb.InvalidList<MfoCatalogoOpcionResponse>(error));
        }

        return Ok(await MfoCatalogoRegistro.ResolverAsync(connectionDB, clave, empresa));
    }

    /// <summary>
    /// Claves de catalogo registradas. El diseñador las ofrece en una lista en
    /// vez de un campo de texto libre: una clave escrita a mano no resolveria
    /// nunca.
    /// </summary>
    [HttpGet("catalogos")]
    public IActionResult Catalogos()
    {
        var claves = MfoCatalogoRegistro.ClavesRegistradas().ToList();

        return Ok(new ResultDto<List<string>>(claves)
        {
            Data = claves, IsValid = true, Message = string.Empty, CantidadRegistros = claves.Count
        });
    }

    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivos = true)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            return Ok(MfoDb.InvalidList<MfoTipoCampoResponse>(openError));
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_TIPO_GET_ALL", cn);
        cmd.Parameters.Add("p_SoloActivos", OracleDbType.Char).Value = MfoDb.DbFlag(soloActivos);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteListAsync(cmd, MfoDb.MapTipoCampo));
    }
}
