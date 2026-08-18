using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Comprueba los permisos de <c>MFO_PERMISO</c> para el usuario en curso.
///
/// Decision 4 de la Fase 0: los roles salen de <c>SIS.OSS_USUARIO_ROL</c>. Son
/// dos consultas a dos schemas distintos -SIS para los roles del usuario, MFO
/// para los permisos del formulario- porque en este repositorio una conexion no
/// cruza schemas. No hace falta transaccion: las dos son lecturas.
///
/// Politica cuando el formulario **no tiene ningun permiso definido**: se
/// permite. Es deliberado y hay que entenderlo: el motor se instala con
/// formularios sin permisos configurados, y denegar por defecto los dejaria
/// inaccesibles sin ninguna pista de por que. En cuanto se define el primer
/// permiso de un formulario, ese formulario pasa a estar cerrado a quien no lo
/// tenga.
///
/// Sin usuario identificado no se puede comprobar nada, asi que un formulario
/// **con** permisos definidos se deniega.
/// </summary>
public static class MfoAutorizacion
{
    public const string Disenar = "DISENAR";
    public const string Llenar = "LLENAR";
    public const string Ver = "VER";
    public const string Exportar = "EXPORTAR";
    public const string Anular = "ANULAR";

    public sealed record Resultado(bool Permitido, string Mensaje);

    private static readonly Resultado Ok = new(true, string.Empty);

    public static async Task<Resultado> PuedeAsync(
        ConnectionDB conexiones, int formularioId, string accion, string? usuario)
    {
        try
        {
            var definidos = await ContarPermisosAsync(conexiones, formularioId);

            // Formulario sin permisos configurados: abierto.
            if (definidos == 0)
            {
                return Ok;
            }

            if (string.IsNullOrWhiteSpace(usuario))
            {
                return new Resultado(false,
                    "Este formulario requiere un usuario identificado.");
            }

            var roles = await RolesDelUsuarioAsync(conexiones, usuario);
            if (roles.Count == 0)
            {
                return new Resultado(false, $"No tiene permiso para {accion.ToLowerInvariant()} este formulario.");
            }

            var tiene = await TienePermisoAsync(conexiones, formularioId, roles, accion);

            return tiene
                ? Ok
                : new Resultado(false, $"No tiene permiso para {accion.ToLowerInvariant()} este formulario.");
        }
        catch (Exception ex)
        {
            // Un fallo al comprobar permisos deniega. Lo contrario convertiria
            // cualquier caida de SIS en una puerta abierta.
            return new Resultado(false, $"No se pudo verificar el permiso: {ex.Message}");
        }
    }

    private static async Task<int> ContarPermisosAsync(ConnectionDB conexiones, int formularioId)
    {
        using var cn = conexiones.GetMfoConnection();
        await cn.OpenAsync();

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERMISO_GET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = formularioId;
        cmd.Parameters.Add("p_RolesCsv", OracleDbType.Varchar2).Value = DBNull.Value;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotal = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) { }
        }

        return MfoDb.GetIntOutput(pTotal);
    }

    private static async Task<bool> TienePermisoAsync(
        ConnectionDB conexiones, int formularioId, List<string> roles, string accion)
    {
        using var cn = conexiones.GetMfoConnection();
        await cn.OpenAsync();

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERMISO_GET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = formularioId;
        cmd.Parameters.Add("p_RolesCsv", OracleDbType.Varchar2).Value = string.Join(',', roles);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.SafeGetString("ACCION"), accion, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Roles del usuario en el ERP. Se lee de SIS, que es donde vive la
    /// identidad; el motor solo guarda el codigo para compararlo.
    /// </summary>
    private static async Task<List<string>> RolesDelUsuarioAsync(ConnectionDB conexiones, string usuario)
    {
        using var cn = conexiones.GetSisConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("SIS.SP_OSS_USR_ROL_GET_USR", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = usuario;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        var roles = new List<string>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // El codigo de rol es CODIGO_USUARIO_ROL; la descripcion se acepta
            // tambien para que MFO_PERMISO se pueda configurar con cualquiera de
            // las dos, que es como se administran los roles en el resto del ERP.
            var codigo = reader.SafeGetInt32("CODIGO_USUARIO_ROL").ToString();
            if (codigo != "0") roles.Add(codigo);

            var descripcion = reader.SafeGetString("DESCRIPCION");
            if (!string.IsNullOrWhiteSpace(descripcion)) roles.Add(descripcion.Trim().ToUpperInvariant());
        }

        return roles;
    }
}
