using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Comprueba los permisos de <c>MFO_PERMISO_USR</c> para el usuario en curso.
///
/// **El modelo es por usuario, no por rol.** La decision 4 de la Fase 0 lo puso
/// por rol contra <c>SIS.OSS_USUARIO_ROL</c>; se cambio despues a asignacion
/// directa. La razon es de diagnostico: con dos ejes, explicar por que alguien
/// entra -o no entra- obliga a mirar en dos sitios, y esa pregunta se hace justo
/// cuando hay un problema. <c>MFO_PERMISO</c> se conserva para no perder lo
/// configurado, pero ningun slice la consulta.
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

            var tiene = await TienePermisoAsync(conexiones, formularioId, usuario, accion);

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

    /// <summary>
    /// Resultado de comprobar si el usuario es superusuario del ERP
    /// (<c>SIS.SIS_USUARIOS.IS_SUPERUSER</c>).
    ///
    /// Se devuelve el motivo aparte del veredicto porque los dos "no" posibles
    /// piden acciones distintas: "usted no es superusuario" lo resuelve un
    /// administrador, y "la columna IS_SUPERUSER no existe en esta instalacion"
    /// lo resuelve un DBA. Colapsarlos en un unico mensaje de permiso denegado
    /// dejaria a alguien buscando durante horas un permiso que no es el problema.
    /// </summary>
    public sealed record Superusuario(bool Es, string Motivo);

    /// <summary>
    /// Lee la marca de superusuario del ERP.
    ///
    /// Vive en SIS, igual que los roles, y por la misma razon se consulta con su
    /// propia conexion: en este repositorio una conexion no cruza schemas.
    ///
    /// **Cualquier fallo deniega.** Un error al resolver la identidad no puede
    /// convertirse en un permiso concedido.
    /// </summary>
    public static async Task<Superusuario> EsSuperusuarioAsync(ConnectionDB conexiones, string? usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario))
        {
            return new Superusuario(false, "Se requiere un usuario identificado.");
        }

        try
        {
            using var cn = conexiones.GetSisConnection();
            await cn.OpenAsync();

            // SQL constante con parametros bindeados. Se admite LOGIN o USUARIO
            // porque la cabecera X-Usuario puede traer cualquiera de los dos,
            // segun por donde se haya autenticado el cliente.
            using var cmd = new OracleCommand(
                @"SELECT NVL(MAX(NVL(u.IS_SUPERUSER, 0)), 0) AS ES_SUPER
                    FROM SIS.SIS_USUARIOS u
                   WHERE UPPER(u.LOGIN) = UPPER(:p_Usuario)
                      OR UPPER(u.USUARIO) = UPPER(:p_Usuario)", cn)
            {
                BindByName = true
            };

            cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = usuario;

            var valor = await cmd.ExecuteScalarAsync();
            var esSuper = valor is not null && valor != DBNull.Value && Convert.ToInt32(valor) == 1;

            return esSuper
                ? new Superusuario(true, string.Empty)
                : new Superusuario(false, "Esta operacion esta reservada a los superusuarios del sistema.");
        }
        catch (OracleException ex) when (ex.Number == 904)
        {
            // ORA-00904: la instalacion no tiene la columna. SisUsuarios ya
            // contempla ese caso, asi que no se puede dar por supuesta.
            return new Superusuario(false,
                "Esta instalacion no tiene la columna SIS_USUARIOS.IS_SUPERUSER, " +
                "asi que no se puede identificar a los superusuarios.");
        }
        catch (Exception ex)
        {
            return new Superusuario(false, $"No se pudo verificar el superusuario: {ex.Message}");
        }
    }

    private static async Task<int> ContarPermisosAsync(ConnectionDB conexiones, int formularioId)
    {
        using var cn = conexiones.GetMfoConnection();
        await cn.OpenAsync();

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERM_USR_GET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = formularioId;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = DBNull.Value;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Reportes", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotal = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) { }
        }

        return MfoDb.GetIntOutput(pTotal);
    }

    private static async Task<bool> TienePermisoAsync(
        ConnectionDB conexiones, int formularioId, string usuario, string accion)
    {
        using var cn = conexiones.GetMfoConnection();
        await cn.OpenAsync();

        using var cmd = MfoDb.StoredProcedure("SP_MFO_PERM_USR_GET", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = formularioId;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = usuario;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Reportes", OracleDbType.RefCursor, ParameterDirection.Output);
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
    /// Si el usuario puede ejecutar un reporte concreto.
    ///
    /// **Hereda salvo que se acote.** Sin ninguna fila en <c>MFO_PERMISO_REP</c>
    /// para los reportes de ese formulario, puede ejecutarlos todos; en cuanto
    /// se le asigna uno, solo los asignados. Es la misma politica que rige el
    /// formulario -sin configurar, abierto- y evita que asignar un formulario
    /// nuevo sean siempre dos pasos.
    ///
    /// Esto NO sustituye al permiso sobre el formulario: primero hay que poder
    /// EXPORTAR, y solo despues se mira que reportes.
    /// </summary>
    public static async Task<Resultado> PuedeEjecutarReporteAsync(
        ConnectionDB conexiones, int formularioId, int reporteId, string? usuario)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(usuario))
            {
                return Ok;
            }

            using var cn = conexiones.GetMfoConnection();
            await cn.OpenAsync();

            using var cmd = MfoDb.StoredProcedure("SP_MFO_PERM_USR_GET", cn);
            cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = formularioId;
            cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = usuario;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            cmd.Parameters.Add("p_Reportes", OracleDbType.RefCursor, ParameterDirection.Output);
            cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

            var acotados = new List<int>();

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) { }

                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        acotados.Add(reader.SafeGetInt32("REPORTE_ID"));
                    }
                }
            }

            if (acotados.Count == 0)
            {
                return Ok;
            }

            return acotados.Contains(reporteId)
                ? Ok
                : new Resultado(false, "No tiene asignado este reporte.");
        }
        catch (Exception ex)
        {
            return new Resultado(false, $"No se pudo verificar el permiso del reporte: {ex.Message}");
        }
    }
}
