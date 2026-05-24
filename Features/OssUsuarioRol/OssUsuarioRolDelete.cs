using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.OssUsuarioRol;

public record DeleteOssUsuarioRolCommand(int CodigoUsuarioRol);

public class DeleteOssUsuarioRolHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<string>> HandleAsync(DeleteOssUsuarioRolCommand command)
    {
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

        using var cmd = new OracleCommand("SIS.SP_OSS_USR_ROL_DEL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_USUARIO_ROL", OracleDbType.Int32).Value = command.CodigoUsuarioRol;
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            string dbMessage = OssUsuarioRolDb.GetMessage(pMessage);
            bool isSuccess = OssUsuarioRolDb.IsSuccessMessage(dbMessage);

            return new ResultDto<string>(isSuccess ? "Registro eliminado correctamente" : string.Empty)
            {
                Data = isSuccess ? "Registro eliminado correctamente" : null,
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
                Message = $"Error técnico al eliminar: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/OssUsuarioRol")]
public class OssUsuarioRolDeleteController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("delete")]
    public async Task<IActionResult> Delete(DeleteOssUsuarioRolCommand value)
    {
        var handler = new DeleteOssUsuarioRolHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
