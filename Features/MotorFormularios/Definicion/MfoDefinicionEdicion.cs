using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Edicion del contenido de una version en BORRADOR: secciones, campos,
/// opciones, reglas y condiciones.
///
/// Ninguna de estas operaciones comprueba que la version este en BORRADOR: eso
/// lo imponen los triggers de inmutabilidad, que cubren INSERT, UPDATE y DELETE.
/// Repetir la comprobacion aqui seria una segunda definicion de la misma regla,
/// con el riesgo de que las dos se separen. Los procedimientos traducen el error
/// del trigger a un mensaje de negocio.
///
/// Lo que si se comprueba aqui es el permiso <c>DISENAR</c>, y se comprueba en
/// todas: editar la definicion de un formulario ajeno es mas grave que leer sus
/// respuestas, porque cambia lo que van a capturar todos los demas. El ambito se
/// resuelve con <see cref="MfoAmbitoDefinicion"/> a partir del id de la pieza
/// que se toca, nunca de un formularioId enviado por el cliente.
/// </summary>
[ApiController]
[Route("api/MfoDefinicion")]
public class MfoDefinicionController(ConnectionDB connectionDB) : ControllerBase
{
    private string? Usuario => Request.Headers["X-Usuario"].FirstOrDefault();

    // ------------------------------------------------------------------------
    // Secciones
    // ------------------------------------------------------------------------

    [HttpPost("seccion/upsert")]
    public async Task<IActionResult> SeccionUpsert(MfoSeccionUpsertRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Version, request.VersionId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_SEC_UPSERT", cn);
        cmd.Parameters.Add("p_SeccionId", OracleDbType.Int32).Value = MfoDb.DbValue(request.SeccionId);
        cmd.Parameters.Add("p_VersionId", OracleDbType.Int32).Value = request.VersionId;
        cmd.Parameters.Add("p_Clave", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Clave);
        cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Titulo);
        cmd.Parameters.Add("p_Descripcion", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Descripcion);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_Columnas", OracleDbType.Int32).Value = request.Columnas;
        cmd.Parameters.Add("p_EsPaso", OracleDbType.Char).Value = MfoDb.DbFlag(request.EsPaso);
        cmd.Parameters.Add("p_Repetible", OracleDbType.Char).Value = MfoDb.DbFlag(request.Repetible);
        cmd.Parameters.Add("p_MinFilas", OracleDbType.Int32).Value = MfoDb.DbValue(request.MinFilas);
        cmd.Parameters.Add("p_MaxFilas", OracleDbType.Int32).Value = MfoDb.DbValue(request.MaxFilas);
        cmd.Parameters.Add("p_Colapsable", OracleDbType.Char).Value = MfoDb.DbFlag(request.Colapsable);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    /// <summary>
    /// Borra la seccion con sus campos. Devuelve cuantas condiciones se
    /// perdieron con ella: es configuracion que desaparece y el diseñador tiene
    /// que poder avisarlo.
    /// </summary>
    [HttpPost("seccion/delete")]
    public async Task<IActionResult> SeccionDelete(MfoIdRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Seccion, request.Id, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_SEC_DELETE", cn);
        cmd.Parameters.Add("p_SeccionId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_CondicionesBorradas", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_CondicionesBorradas"));
    }

    // ------------------------------------------------------------------------
    // Campos
    // ------------------------------------------------------------------------

    [HttpPost("campo/upsert")]
    public async Task<IActionResult> CampoUpsert(MfoCampoUpsertRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Seccion, request.SeccionId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_CAMPO_UPSERT", cn);
        cmd.Parameters.Add("p_CampoId", OracleDbType.Int32).Value = MfoDb.DbValue(request.CampoId);
        cmd.Parameters.Add("p_SeccionId", OracleDbType.Int32).Value = request.SeccionId;
        cmd.Parameters.Add("p_TipoCodigo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.TipoCodigo);
        cmd.Parameters.Add("p_Clave", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Clave);
        cmd.Parameters.Add("p_Etiqueta", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Etiqueta);
        cmd.Parameters.Add("p_Ayuda", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Ayuda);
        cmd.Parameters.Add("p_Placeholder", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Placeholder);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_Ancho", OracleDbType.Int32).Value = request.Ancho;
        cmd.Parameters.Add("p_Requerido", OracleDbType.Char).Value = MfoDb.DbFlag(request.Requerido);
        cmd.Parameters.Add("p_SoloLectura", OracleDbType.Char).Value = MfoDb.DbFlag(request.SoloLectura);
        cmd.Parameters.Add("p_ValorDefecto", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ValorDefecto);
        cmd.Parameters.Add("p_OrigenOpciones", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.OrigenOpciones);
        cmd.Parameters.Add("p_CatalogoClave", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.CatalogoClave);
        cmd.Parameters.Add("p_Mascara", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Mascara);
        cmd.Parameters.Add("p_Unidad", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Unidad);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    [HttpPost("campo/delete")]
    public async Task<IActionResult> CampoDelete(MfoIdRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Campo, request.Id, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_CAMPO_DELETE", cn);
        cmd.Parameters.Add("p_CampoId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_CondicionesBorradas", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_CondicionesBorradas"));
    }

    /// <summary>
    /// Reordena los campos de una seccion. La lista de ids se serializa aqui a
    /// CSV porque son numeros y no pueden contener el separador; el
    /// procedimiento la parsea con INSTR/SUBSTR y convierte cada elemento con
    /// TO_NUMBER, sin construir SQL dinamico.
    /// </summary>
    [HttpPost("campo/reorder")]
    public async Task<IActionResult> CampoReorder(MfoCampoReorderRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Seccion, request.SeccionId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        var csv = string.Join(',', request.CamposId.Select(id => id.ToString(CultureInfo.InvariantCulture)));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_CAMPO_REORDER", cn);
        cmd.Parameters.Add("p_SeccionId", OracleDbType.Int32).Value = request.SeccionId;
        cmd.Parameters.Add("p_CamposCsv", OracleDbType.Varchar2).Value = MfoDb.DbValue(csv);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Reordenados", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_Reordenados"));
    }

    // ------------------------------------------------------------------------
    // Opciones
    // ------------------------------------------------------------------------

    [HttpPost("opcion/upsert")]
    public async Task<IActionResult> OpcionUpsert(MfoOpcionUpsertRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Campo, request.CampoId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_OPCION_UPSERT", cn);
        cmd.Parameters.Add("p_OpcionId", OracleDbType.Int32).Value = MfoDb.DbValue(request.OpcionId);
        cmd.Parameters.Add("p_CampoId", OracleDbType.Int32).Value = request.CampoId;
        cmd.Parameters.Add("p_Valor", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Valor);
        cmd.Parameters.Add("p_Etiqueta", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Etiqueta);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_Grupo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Grupo);
        cmd.Parameters.Add("p_EsDefecto", OracleDbType.Char).Value = MfoDb.DbFlag(request.EsDefecto);
        cmd.Parameters.Add("p_Activo", OracleDbType.Char).Value = MfoDb.DbFlag(request.Activo);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    [HttpPost("opcion/delete")]
    public async Task<IActionResult> OpcionDelete(MfoIdRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Opcion, request.Id, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_OPCION_DELETE", cn);
        cmd.Parameters.Add("p_OpcionId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd));
    }

    // ------------------------------------------------------------------------
    // Reglas
    // ------------------------------------------------------------------------

    [HttpPost("regla/upsert")]
    public async Task<IActionResult> ReglaUpsert(MfoReglaUpsertRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Campo, request.CampoId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REGLA_UPSERT", cn);
        cmd.Parameters.Add("p_ReglaId", OracleDbType.Int32).Value = MfoDb.DbValue(request.ReglaId);
        cmd.Parameters.Add("p_CampoId", OracleDbType.Int32).Value = request.CampoId;
        cmd.Parameters.Add("p_TipoRegla", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.TipoRegla);
        cmd.Parameters.Add("p_Param1", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Param1);
        cmd.Parameters.Add("p_Param2", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Param2);
        cmd.Parameters.Add("p_Mensaje", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Mensaje);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_Activo", OracleDbType.Char).Value = MfoDb.DbFlag(request.Activo);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    [HttpPost("regla/delete")]
    public async Task<IActionResult> ReglaDelete(MfoIdRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Regla, request.Id, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_REGLA_DELETE", cn);
        cmd.Parameters.Add("p_ReglaId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd));
    }

    // ------------------------------------------------------------------------
    // Condiciones
    // ------------------------------------------------------------------------

    /// <summary>
    /// El destino y el origen viajan por CLAVE, no por id: es lo que maneja el
    /// diseñador, y resolverlo en el procedimiento evita que el frontend tenga
    /// que conocer los ids internos de la version.
    /// </summary>
    [HttpPost("condicion/upsert")]
    public async Task<IActionResult> CondicionUpsert(MfoCondicionUpsertRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Version, request.VersionId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_COND_UPSERT", cn);
        cmd.Parameters.Add("p_CondicionId", OracleDbType.Int32).Value = MfoDb.DbValue(request.CondicionId);
        cmd.Parameters.Add("p_VersionId", OracleDbType.Int32).Value = request.VersionId;
        cmd.Parameters.Add("p_Accion", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Accion);
        cmd.Parameters.Add("p_DestinoTipo", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.DestinoTipo);
        cmd.Parameters.Add("p_ClaveDestino", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveDestino);
        cmd.Parameters.Add("p_ClaveOrigen", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ClaveOrigen);
        cmd.Parameters.Add("p_Operador", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Operador);
        cmd.Parameters.Add("p_ValorCompara", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ValorCompara);
        cmd.Parameters.Add("p_Grupo", OracleDbType.Int32).Value = request.Grupo;
        cmd.Parameters.Add("p_Conector", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Conector);
        cmd.Parameters.Add("p_Orden", OracleDbType.Int32).Value = request.Orden;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_OutId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_OutId"));
    }

    [HttpPost("condicion/delete")]
    public async Task<IActionResult> CondicionDelete(MfoIdRequest request)
    {
        // DISENAR sobre el formulario dueño. El ambito se resuelve en la base a
        // partir del id que se esta tocando: aceptar un formularioId del cliente
        // permitiria editar la definicion de otro formulario mandando el propio.
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Condicion, request.Id, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<int>(openError));

        using var cmd = MfoDb.StoredProcedure("SP_MFO_COND_DELETE", cn);
        cmd.Parameters.Add("p_CondicionId", OracleDbType.Int32).Value = request.Id;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd));
    }
}
