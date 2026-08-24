using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Identidad del formulario: alta, consulta y cambio de estado.
///
/// El ALIAS no se puede modificar despues del alta y por eso no aparece en
/// MfoFormularioUpdateRequest: es la identidad estable que usan las rutas del
/// frontend, el enlace de reportes y el nombre de la vista de proyeccion.
/// Renombrar es cambiar NOMBRE.
///
/// El usuario conectado se toma de la cabecera que ya usa el resto del vertical
/// slice; si no viene, se guarda nulo en vez de fallar, porque el motor puede
/// usarse en flujos sin usuario identificado.
/// </summary>
[ApiController]
[Route("api/MfoFormulario")]
public class MfoFormularioController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    private string? Usuario => Request.Headers["X-Usuario"].FirstOrDefault();

    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(MfoFormularioFilterRequest request)
    {
        if (!MfoDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(MfoDb.InvalidList<MfoFormularioResponse>(error));
        }

        // El catalogo tambien es una frontera de autorizacion. Filtrarlo solo
        // en la UI expondria nombres y alias de formularios no asignados a
        // cualquier cliente que invoque el endpoint directamente.
        var superusuario = await MfoAutorizacion.EsSuperusuarioAsync(connectionDB, Usuario);

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            return Ok(MfoDb.InvalidList<MfoFormularioResponse>(openError));
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_FORM_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_Estado", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Estado);
        cmd.Parameters.Add("p_ModoUso", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ModoUso);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_EsSuperuser", OracleDbType.Int32).Value = superusuario.Es ? 1 : 0;
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteListAsync(cmd, r => MfoDb.MapFormulario(r, true),
                                               request.Page, request.PageSize));
    }

    /// <summary>
    /// Admite id o alias. El frontend rutea por alias y el diseñador por id;
    /// obligar a traducir antes solo agregaria un viaje a la base.
    /// </summary>
    [HttpGet("getById")]
    public async Task<IActionResult> GetById([FromQuery] int? id, [FromQuery] string? alias)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            return Ok(MfoDb.Invalid<MfoFormularioDetalle>(openError));
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_FORM_GET_BY_ID", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = MfoDb.DbValue(id);
        cmd.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = MfoDb.DbValue(alias);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Versiones", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        MfoFormularioResponse? formulario = null;
        var versiones = new List<MfoVersionResponse>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                formulario = MfoDb.MapFormulario(reader, false);
            }

            // Los dos cursores llegan en el mismo lector, en el orden en que el
            // procedimiento los declara.
            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    versiones.Add(MfoDb.MapVersion(reader));
                }
            }
        }
        catch (OracleException ex)
        {
            return Ok(MfoDb.Invalid<MfoFormularioDetalle>($"Error de base de datos ({ex.Number}): {ex.Message}"));
        }

        var message = MfoDb.GetMessage(pMessage);
        var isSuccess = MfoDb.IsSuccessMessage(message) && formulario is not null;

        return Ok(new ResultDto<MfoFormularioDetalle>(new MfoFormularioDetalle(formulario!, versiones))
        {
            Data = isSuccess ? new MfoFormularioDetalle(formulario!, versiones) : null,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message
        });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(MfoFormularioCreateRequest request)
    {
        if (!MfoDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(MfoDb.Invalid<int>(error));
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            return Ok(MfoDb.Invalid<int>(openError));
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_FORM_CREATE", cn);
        cmd.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Alias);
        cmd.Parameters.Add("p_Nombre", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Nombre);
        cmd.Parameters.Add("p_Descripcion", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Descripcion);
        cmd.Parameters.Add("p_Categoria", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Categoria);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_EntidadDestino", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.EntidadDestino);
        cmd.Parameters.Add("p_MaxRespUsuario", OracleDbType.Int32).Value = MfoDb.DbValue(request.MaxRespUsuario);
        cmd.Parameters.Add("p_PermiteBorrador", OracleDbType.Char).Value = MfoDb.DbFlag(request.PermiteBorrador);
        cmd.Parameters.Add("p_ModoUso", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ModoUso);
        cmd.Parameters.Add("p_RegistraEjec", OracleDbType.Char).Value = MfoDb.DbFlag(request.RegistraEjec);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd, "p_FormularioId"));
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update(MfoFormularioUpdateRequest request)
    {
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Formulario, request.FormularioId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            return Ok(MfoDb.Invalid<int>(openError));
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_FORM_UPDATE", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = request.FormularioId;
        cmd.Parameters.Add("p_Nombre", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Nombre);
        cmd.Parameters.Add("p_Descripcion", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Descripcion);
        cmd.Parameters.Add("p_Categoria", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Categoria);
        cmd.Parameters.Add("p_EntidadDestino", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.EntidadDestino);
        cmd.Parameters.Add("p_MaxRespUsuario", OracleDbType.Int32).Value = MfoDb.DbValue(request.MaxRespUsuario);
        cmd.Parameters.Add("p_PermiteBorrador", OracleDbType.Char).Value = MfoDb.DbFlag(request.PermiteBorrador);
        cmd.Parameters.Add("p_ModoUso", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.ModoUso);
        cmd.Parameters.Add("p_RegistraEjec", OracleDbType.Char).Value = MfoDb.DbFlag(request.RegistraEjec);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd));
    }

    /// <summary>
    /// Activar o inactivar. Es el equivalente a "borrar" desde la UI: un
    /// formulario con respuestas no se elimina nunca.
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(MfoFormularioEstadoRequest request)
    {
        var permiso = await MfoAmbitoDefinicion.PuedeDisenarAsync(
            connectionDB, MfoAmbitoDefinicion.Pieza.Formulario, request.FormularioId, Usuario);
        if (!permiso.Permitido) return Ok(MfoDb.Invalid<int>(permiso.Mensaje));

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            return Ok(MfoDb.Invalid<int>(openError));
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_FORM_ESTADO", cn);
        cmd.Parameters.Add("p_FormularioId", OracleDbType.Int32).Value = request.FormularioId;
        cmd.Parameters.Add("p_Estado", OracleDbType.Varchar2).Value = MfoDb.DbValue(request.Estado);
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        return Ok(await MfoDb.ExecuteScalarAsync(cmd));
    }
}

public record MfoFormularioDetalle(MfoFormularioResponse Formulario, List<MfoVersionResponse> Versiones);
