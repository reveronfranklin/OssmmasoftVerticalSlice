using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.OssUsuarioRol;

public record CreateOssUsuarioRolCommand(
    string Usuario,
    int CodigoUsuario,
    string? Descripcion,
    JsonElement JsonMenu
);

public class CreateOssUsuarioRolHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<int>> HandleAsync(CreateOssUsuarioRolCommand command)
    {
        if (!OssUsuarioRolDb.TrySerializeJsonMenu(command.JsonMenu, out var jsonMenu, out var validationMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = validationMessage };
        }

        using var cn = _connectionDB.GetSisConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión SIS: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("SIS.SP_OSS_USR_ROL_INS", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = OssUsuarioRolDb.DbValue(command.Usuario);
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = command.CodigoUsuario;
        cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = OssUsuarioRolDb.DbValue(command.Descripcion);
        cmd.Parameters.Add("p_JSON_MENU", OracleDbType.Clob).Value = jsonMenu;

        var pCodigoUsuarioRol = cmd.Parameters.Add("p_CODIGO_USUARIO_ROL_OUT", OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            string dbMessage = OssUsuarioRolDb.GetMessage(pMessage);
            bool isSuccess = OssUsuarioRolDb.IsSuccessMessage(dbMessage);
            int codigoUsuarioRol = isSuccess ? OssUsuarioRolDb.GetIntOutput(pCodigoUsuarioRol) : 0;

            return new ResultDto<int>(codigoUsuarioRol)
            {
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/OssUsuarioRol")]
public class OssUsuarioRolCreateController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> Create(CreateOssUsuarioRolCommand value)
    {
        var handler = new CreateOssUsuarioRolHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
