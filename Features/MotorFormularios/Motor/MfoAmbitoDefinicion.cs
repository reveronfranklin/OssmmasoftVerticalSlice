using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Resuelve a que formulario pertenece cualquier pieza de una definicion, para
/// poder comprobar el permiso <c>DISENAR</c> sobre el formulario dueño.
///
/// Hace falta porque los endpoints de edicion reciben el id de la pieza que se
/// esta tocando -una opcion, una regla, una condicion- y no el del formulario.
/// Sin este salto, la unica forma de autorizar seria confiar en un
/// <c>formularioId</c> que enviara el cliente, y eso es precisamente lo que no
/// se puede hacer: bastaria con mandar el id de un formulario propio para editar
/// la definicion de otro.
///
/// Las consultas son constantes elegidas por un enum, nunca texto compuesto con
/// datos del request. El unico valor que viaja es el id, bindeado.
/// </summary>
public static class MfoAmbitoDefinicion
{
    public enum Pieza
    {
        Formulario,
        Version,
        Seccion,
        Campo,
        Opcion,
        Regla,
        Condicion,
        Permiso
    }

    /// <summary>
    /// Comprueba <c>DISENAR</c> sobre el formulario dueño de la pieza indicada.
    /// Una pieza inexistente se rechaza con el mismo mensaje que un permiso
    /// denegado no seria correcto -confundiria dos problemas distintos-, asi que
    /// se distingue.
    /// </summary>
    public static async Task<MfoAutorizacion.Resultado> PuedeDisenarAsync(
        ConnectionDB conexiones, Pieza pieza, int id, string? usuario)
    {
        if (pieza == Pieza.Formulario)
        {
            return await MfoAutorizacion.PuedeAsync(conexiones, id, MfoAutorizacion.Disenar, usuario);
        }

        var formularioId = await ResolverFormularioAsync(conexiones, pieza, id);
        if (formularioId is null)
        {
            return new MfoAutorizacion.Resultado(false, $"El elemento indicado no existe ({pieza}).");
        }

        return await MfoAutorizacion.PuedeAsync(
            conexiones, formularioId.Value, MfoAutorizacion.Disenar, usuario);
    }

    public static async Task<int?> ResolverFormularioAsync(
        ConnectionDB conexiones, Pieza pieza, int id)
    {
        try
        {
            using var cn = conexiones.GetMfoConnection();
            if (await MfoDb.TryOpenAsync(cn) is not null) return null;

            using var cmd = new OracleCommand(ConsultaDe(pieza), cn) { BindByName = true };
            cmd.Parameters.Add("p_Id", OracleDbType.Int32).Value = id;

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? reader.SafeGetInt32("FORMULARIO_ID") : null;
        }
        catch
        {
            // Igual que en MfoAutorizacion: si no se puede resolver el ambito, no
            // se puede autorizar, y no autorizar es el desenlace seguro.
            return null;
        }
    }

    private static string ConsultaDe(Pieza pieza) => pieza switch
    {
        Pieza.Formulario =>
            "SELECT FORMULARIO_ID FROM MFO.MFO_FORMULARIO WHERE FORMULARIO_ID = :p_Id",

        Pieza.Version =>
            "SELECT FORMULARIO_ID FROM MFO.MFO_VERSION WHERE VERSION_ID = :p_Id",

        Pieza.Seccion =>
            @"SELECT V.FORMULARIO_ID
                FROM MFO.MFO_SECCION S
                JOIN MFO.MFO_VERSION V ON V.VERSION_ID = S.VERSION_ID
               WHERE S.SECCION_ID = :p_Id",

        Pieza.Campo =>
            @"SELECT V.FORMULARIO_ID
                FROM MFO.MFO_CAMPO C
                JOIN MFO.MFO_VERSION V ON V.VERSION_ID = C.VERSION_ID
               WHERE C.CAMPO_ID = :p_Id",

        Pieza.Opcion =>
            @"SELECT V.FORMULARIO_ID
                FROM MFO.MFO_OPCION O
                JOIN MFO.MFO_CAMPO C   ON C.CAMPO_ID   = O.CAMPO_ID
                JOIN MFO.MFO_VERSION V ON V.VERSION_ID = C.VERSION_ID
               WHERE O.OPCION_ID = :p_Id",

        Pieza.Regla =>
            @"SELECT V.FORMULARIO_ID
                FROM MFO.MFO_REGLA G
                JOIN MFO.MFO_CAMPO C   ON C.CAMPO_ID   = G.CAMPO_ID
                JOIN MFO.MFO_VERSION V ON V.VERSION_ID = C.VERSION_ID
               WHERE G.REGLA_ID = :p_Id",

        Pieza.Condicion =>
            @"SELECT V.FORMULARIO_ID
                FROM MFO.MFO_CONDICION D
                JOIN MFO.MFO_VERSION V ON V.VERSION_ID = D.VERSION_ID
               WHERE D.CONDICION_ID = :p_Id",

        Pieza.Permiso =>
            "SELECT FORMULARIO_ID FROM MFO.MFO_PERMISO WHERE PERMISO_ID = :p_Id",

        _ => throw new ArgumentOutOfRangeException(nameof(pieza), pieza, "Pieza de definicion no soportada.")
    };
}
