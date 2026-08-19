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
/// dato, y administrarlo exige **una sola cosa**: ser superusuario del ERP
/// (<c>SIS_USUARIOS.IS_SUPERUSER</c>). No se pide ademas <c>DISENAR</c> sobre el
/// formulario, y esa omision es deliberada: quien administra la seguridad tiene
/// que poder recuperarla siempre, incluso de una asignacion que lo dejo a el
/// mismo fuera. Sin esta salida, cerrar mal un formulario solo se arreglaba por
/// SQL.
///
/// La marca de superusuario se lee del servidor, no de lo que diga el cliente:
/// el frontend oculta el boton usando el `userData` de localStorage, pero eso es
/// solo presentacion -localStorage lo edita cualquiera- y esta comprobacion es
/// la que de verdad manda.
///
/// **Alcance:** el superusuario lo es para administrar permisos, no para el
/// resto del motor. Sigue necesitando <c>LLENAR</c>, <c>VER</c> o lo que
/// corresponda para operar sobre un formulario cerrado. Lo que siempre puede es
/// concederselo.
///
/// **Cuidado con dejar la instalacion sin superusuarios.** Si ninguna fila de
/// <c>SIS_USUARIOS</c> tiene <c>IS_SUPERUSER = 1</c> -o la columna no existe-,
/// nadie puede administrar permisos desde la aplicacion. Es fail-closed a
/// proposito, pero conviene comprobarlo antes de desplegar este cambio.
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
        // Ser superusuario es condicion unica y suficiente. No se exige ademas
        // DISENAR sobre el formulario a proposito: si se exigiera, un
        // superusuario podria dejarse fuera de un formulario con una asignacion
        // mal hecha y ya no habria forma de arreglarlo sin SQL. Quien administra
        // la seguridad tiene que poder recuperarla siempre.
        var superusuario = await MfoAutorizacion.EsSuperusuarioAsync(connectionDB, Usuario);
        if (!superusuario.Es) return Ok(MfoDb.Invalid<int>(superusuario.Motivo));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        var csv = string.Join(',', request.Acciones ?? []);

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERM_USR_SET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = request.FormularioId;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Usuario);
        cmd.Parameters.Add("p_AccionesCsv", OracleDbType.Varchar2).Value = MfoDb.DbValue(csv);
        cmd.Parameters.Add("p_UsuarioIns", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Asignados", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_Asignados"));
    }

    /// <summary>
    /// Reportes concretos que una persona puede ejecutar.
    ///
    /// **Una lista vacia no revoca**: devuelve al usuario a heredar todos los
    /// reportes del formulario. Para quitarle el acceso se revoca EXPORTAR, no
    /// se vacia esta lista. La pantalla tiene que decirlo, porque el boton
    /// "guardar sin nada marcado" sugiere lo contrario.
    /// </summary>
    [HttpPost("reportes")]
    public async Task<IActionResult> Reportes(MfoPermisoReporteSetRequest request)
    {
        var superusuario = await MfoAutorizacion.EsSuperusuarioAsync(connectionDB, Usuario);
        if (!superusuario.Es) return Ok(MfoDb.Invalid<int>(superusuario.Motivo));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        var csv = string.Join(',', request.ReportesId ?? []);

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERM_REP_SET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = request.FormularioId;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Usuario);
        cmd.Parameters.Add("p_ReportesCsv", OracleDbType.Varchar2).Value = MfoDb.DbValue(csv);
        cmd.Parameters.Add("p_UsuarioIns", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Asignados", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_Asignados"));
    }

    /// <summary>
    /// Sin usuario: todo lo asignado en el formulario, para la pantalla de
    /// administracion. Con usuario: solo lo suyo.
    /// </summary>
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll([FromQuery] int formularioId, [FromQuery] string? usuario)
    {
        var superusuario = await MfoAutorizacion.EsSuperusuarioAsync(connectionDB, Usuario);
        if (!superusuario.Es) return Ok(MfoDb.Invalid<MfoPermisoDetalle>(superusuario.Motivo));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<MfoPermisoDetalle>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERM_USR_GET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = formularioId;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(usuario);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Reportes", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var acciones = new List<MfoPermisoUsuario>();
        var reportes = new List<MfoPermisoReporte>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                acciones.Add(new MfoPermisoUsuario(
                    reader.SafeGetInt32("PERM_USR_ID"), reader.SafeGetInt32("FORMULARIO_ID"),
                    reader.SafeGetString("USUARIO"), reader.SafeGetString("ACCION")));
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    reportes.Add(new MfoPermisoReporte(
                        reader.SafeGetInt32("PERM_REP_ID"), reader.SafeGetInt32("REPORTE_ID"),
                        reader.SafeGetString("USUARIO"), reader.SafeGetString("CLAVE"),
                        reader.SafeGetString("NOMBRE")));
                }
            }
        }
        catch (OracleException ex)
        {
            return Ok(MfoDb.Invalid<MfoPermisoDetalle>($"Error de base de datos ({ex.Number}): {ex.Message}"));
        }

        var message = MfoDb.GetMessage(pMessage);
        var isSuccess = MfoDb.IsSuccessMessage(message);
        var data = new MfoPermisoDetalle(acciones, reportes);

        return Ok(new ResultDto<MfoPermisoDetalle>(data)
        {
            Data = isSuccess ? data : null,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message,
            CantidadRegistros = acciones.Count
        });
    }
}

public record MfoPermisoUsuario(int PermUsrId, int FormularioId, string Usuario, string Accion);

public record MfoPermisoReporte(int PermRepId, int ReporteId, string Usuario, string Clave, string Nombre);

/// <summary>
/// Los dos ejes juntos: que puede hacer cada persona y que reportes tiene
/// acotados. La pantalla necesita los dos a la vez para pintar una sola tabla.
/// </summary>
public record MfoPermisoDetalle(List<MfoPermisoUsuario> Acciones, List<MfoPermisoReporte> Reportes);

public record MfoPermisoReporteSetRequest(int FormularioId, string Usuario, List<int> ReportesId);
