using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.OssUsuarioRol;

public record GetOssUsuarioRolByUsuarioQuery(string Usuario);

public class GetOssUsuarioRolByUsuarioHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<OssUsuarioRolResponse>>> HandleAsync(GetOssUsuarioRolByUsuarioQuery query)
    {
        using var cn = _connectionDB.GetSisConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<List<OssUsuarioRolResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión SIS: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("SIS.SP_OSS_USR_ROL_GET_USR", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = OssUsuarioRolDb.DbValue(query.Usuario);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        var list = new List<OssUsuarioRolResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(OssUsuarioRolDb.MapUsuarioRol(reader));
                }
            }

            string dbMessage = OssUsuarioRolDb.GetMessage(pMessage);
            bool isSuccess = OssUsuarioRolDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<OssUsuarioRolResponse>>(list)
            {
                IsValid = isSuccess,
                Message = isSuccess && list.Count == 0 ? "Registro no encontrado" : dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<OssUsuarioRolResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/OssUsuarioRol")]
public class OssUsuarioRolGetByUsuarioController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("getByUsuario")]
    public async Task<IActionResult> GetByUsuario(GetOssUsuarioRolByUsuarioQuery value)
    {
        var handler = new GetOssUsuarioRolByUsuarioHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
