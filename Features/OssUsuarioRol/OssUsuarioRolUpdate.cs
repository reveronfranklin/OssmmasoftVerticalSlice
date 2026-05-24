using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.OssUsuarioRol;

public record UpdateOssUsuarioRolCommand(
    int CodigoUsuarioRol,
    string Usuario,
    int CodigoUsuario,
    string? Descripcion,
    JsonElement JsonMenu
);

public class UpdateOssUsuarioRolHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<string>> HandleAsync(UpdateOssUsuarioRolCommand command)
    {
        if (!OssUsuarioRolDb.TrySerializeJsonMenu(command.JsonMenu, out var jsonMenu, out var validationMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validationMessage };
        }

        using var cn = _connectionDB.GetSisConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty)
            {
                Data = null,
                IsValid = false,
                Message = $"Error técnico al abrir conexión SIS: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("SIS.SP_OSS_USR_ROL_UPD", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_USUARIO_ROL", OracleDbType.Int32).Value = command.CodigoUsuarioRol;
        cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = OssUsuarioRolDb.DbValue(command.Usuario);
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = command.CodigoUsuario;
        cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = OssUsuarioRolDb.DbValue(command.Descripcion);
        cmd.Parameters.Add("p_JSON_MENU", OracleDbType.Clob).Value = jsonMenu;

        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            string dbMessage = OssUsuarioRolDb.GetMessage(pMessage);
            bool isSuccess = OssUsuarioRolDb.IsSuccessMessage(dbMessage);

            return new ResultDto<string>(isSuccess ? "Registro actualizado correctamente" : string.Empty)
            {
                Data = isSuccess ? "Registro actualizado correctamente" : null,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty)
            {
                Data = null,
                IsValid = false,
                Message = $"Error técnico en Update: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/OssUsuarioRol")]
public class OssUsuarioRolUpdateController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("update")]
    public async Task<IActionResult> Update(UpdateOssUsuarioRolCommand value)
    {
        var handler = new UpdateOssUsuarioRolHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
