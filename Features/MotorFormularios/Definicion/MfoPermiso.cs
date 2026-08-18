using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Permisos del formulario por rol.
///
/// ROL_CODIGO es el codigo de rol de SIS.OSS_USUARIO_ROL (decision 4 de la
/// Fase 0). El motor guarda el codigo y no consulta esa tabla -esta en otro
/// schema y una transaccion no cruza schemas en este repositorio-: la
/// comparacion con la identidad del usuario la hace el backend.
///
/// La aplicacion efectiva del permiso vive en cada slice; aqui se administra el
/// dato, y administrarlo exige <c>DISENAR</c> sobre el formulario.
///
/// **Trampa conocida al configurar el primer permiso.** Un formulario sin
/// permisos esta abierto, asi que la primera asignacion la puede hacer
/// cualquiera. Si esa primera asignacion no incluye <c>DISENAR</c> para un rol
/// que el administrador tenga, el formulario queda cerrado a todos y solo se
/// puede recuperar por SQL. Es la consecuencia inevitable de no tener un
/// superusuario del motor, y se prefiere dejarla documentada antes que inventar
/// una excepcion implicita que despues nadie recuerde que existe.
/// </summary>
[ApiController]
[Route("api/MfoPermiso")]
public class MfoPermisoController(ConnectionDB connectionDB) : ControllerBase
{
    private string? Usuario => Request.Headers["X-Usuario"].FirstOrDefault();

    /// <summary>
    /// Reemplaza el conjunto completo de acciones de ese rol sobre ese
    /// formulario. Es un reemplazo y no un alta incremental porque la pantalla
    /// de permisos muestra casillas: lo que el usuario ve al guardar es el
    /// estado final, no un delta.
    /// </summary>
    [HttpPost("set")]
    public async Task<IActionResult> Set(MfoPermisoSetRequest request)
    {
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Formulario, request.FormularioId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        var csv = string.Join(',', request.Acciones ?? []);

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERMISO_SET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = request.FormularioId;
        cmd.Parameters.Add("p_RolCodigo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.RolCodigo);
        cmd.Parameters.Add("p_AccionesCsv", OracleDbType.Varchar2).Value = MfoDb.DbValue(csv);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Asignados", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_Asignados"));
    }

    /// <summary>
    /// Sin roles: todos los permisos del formulario, para la pantalla de
    /// administracion. Con roles: solo las acciones permitidas a esa lista, que
    /// es la comprobacion que hara cada slice.
    /// </summary>
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll([FromQuery] int formularioId, [FromQuery] string? roles)
    {
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Formulario, formularioId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.InvalidList<MfoPermisoResponse>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.InvalidList<MfoPermisoResponse>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERMISO_GET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = formularioId;
        cmd.Parameters.Add("p_RolesCsv", OracleDbType.Varchar2).Value = MfoDb.DbValue(roles);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteListAsync(cmd, MfoDb.MapPermiso));
    }
}
