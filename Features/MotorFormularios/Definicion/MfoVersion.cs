using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Ciclo de vida de una version: crear, clonar, validar, publicar, archivar y
/// cargar la definicion completa.
/// </summary>
[ApiController]
[Route("api/MfoVersion")]
public class MfoVersionController(
    ConnectionDB connectionDB,
    MfoDefinicionCache cache) : ControllerBase
{
    private string? Usuario => Request.Headers["X-Usuario"].FirstOrDefault();

    [HttpPost("create")]
    public async Task<IActionResult> Create(MfoVersionCreateRequest request)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_VER_CREATE", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = request.FormularioId;
        cmd.Parameters.Add("p_Notas", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Notas);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_VersionId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_VersionId"));
    }

    /// <summary>
    /// Clona una version a un BORRADOR nuevo. Devuelve tambien cuantas
    /// condiciones no se pudieron remapear: son ramas de logica que se pierden,
    /// y el diseñador tiene que poder avisarlo en pantalla en vez de que el
    /// usuario lo descubra cuando el formulario no se comporta como esperaba.
    /// </summary>
    [HttpPost("clone")]
    public async Task<IActionResult> Clone(MfoVersionCloneRequest request)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<MfoCloneResultado>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_VER_CLONE", cn);
        cmd.Parameters.Add("p_VersionOrigenId", OracleDbType.Int32).Value = request.VersionOrigenId;
        cmd.Parameters.Add("p_Notas", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Notas);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        var pVersion = cmd.Parameters.Add("p_VersionId", OracleDbType.Int32, ParameterDirection.Output);
        var pOmitidas = cmd.Parameters.Add("p_CondicionesOmitidas", OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        await cmd.ExecuteNonQueryAsync();

        var message = MfoDb.GetMessage(pMessage);
        var isSuccess = MfoDb.IsSuccessMessage(message);
        var data = new MfoCloneResultado(MfoDb.GetIntOutput(pVersion), MfoDb.GetIntOutput(pOmitidas));

        return Ok(new ResultDto<MfoCloneResultado>(data)
        {
            Data = isSuccess ? data : null,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message
        });
    }

    /// <summary>
    /// Valida sin publicar. Se expone aparte de la publicacion para que el
    /// diseñador pueda mostrar los hallazgos ANTES de que el usuario pulse
    /// publicar, que es cuando sirven de algo.
    /// </summary>
    [HttpPost("validar")]
    public async Task<IActionResult> Validar(MfoVersionIdRequest request)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoHallazgoResponse>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_VER_VALIDAR", cn);
        cmd.Parameters.Add("p_VersionId", OracleDbType.Int32).Value = request.VersionId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        var pErrores = cmd.Parameters.Add("p_Errores", OracleDbType.Int32, ParameterDirection.Output);

        var result = await MfoDb.ExecuteListAsync(cmd, MfoDb.MapHallazgo);

        // Total1 lleva la cuenta de hallazgos bloqueantes. Una version con solo
        // avisos es publicable; el frontend necesita distinguirlo sin recorrer
        // la lista.
        result.Total1 = MfoDb.GetIntOutput(pErrores);

        return Ok(result);
    }

    [HttpPost("publicar")]
    public async Task<IActionResult> Publicar(MfoVersionIdRequest request)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_VER_PUBLICAR", cn);
        cmd.Parameters.Add("p_VersionId", OracleDbType.Int32).Value = request.VersionId;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        var result = await MfoDb.ExecuteScalarAsync(cmd);

        // Al publicar cambia cual es la version vigente de ese alias. La cache
        // por VERSION_ID no se invalida -una version publicada es inmutable-
        // pero la resolucion alias -> version si.
        if (result.IsValid)
        {
            cache.InvalidarAlias();
        }

        return Ok(result);
    }

    [HttpPost("archivar")]
    public async Task<IActionResult> Archivar(MfoVersionIdRequest request)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_VER_ARCHIVAR", cn);
        cmd.Parameters.Add("p_VersionId", OracleDbType.Int32).Value = request.VersionId;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        var result = await MfoDb.ExecuteScalarAsync(cmd);

        if (result.IsValid)
        {
            cache.InvalidarAlias();
        }

        return Ok(result);
    }

    /// <summary>
    /// Definicion completa: secciones -> campos -> opciones + reglas, mas las
    /// condiciones de la version. Una sola llamada por formulario.
    ///
    /// Por alias devuelve la version PUBLICADA vigente, que es lo que necesita
    /// el renderizador; por id devuelve la que se pida, que es lo que necesita
    /// el diseñador para trabajar sobre un borrador.
    /// </summary>
    [HttpGet("getFull")]
    public async Task<IActionResult> GetFull([FromQuery] int? versionId, [FromQuery] string? alias)
    {
        var result = await cache.ObtenerAsync(connectionDB, versionId, alias);
        return Ok(result);
    }
}

public record MfoCloneResultado(int VersionId, int CondicionesOmitidas);
