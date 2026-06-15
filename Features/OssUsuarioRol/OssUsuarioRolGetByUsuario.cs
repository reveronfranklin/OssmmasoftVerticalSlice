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

            if (!isSuccess && dbMessage.Contains("TTC", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteInlineAsync(cn, query.Usuario);
            }

            return new ResultDto<List<OssUsuarioRolResponse>>(list)
            {
                Data = isSuccess ? list : null,
                IsValid = isSuccess,
                Message = isSuccess && list.Count == 0 ? "Registro no encontrado" : dbMessage
            };
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("TTC", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteInlineAsync(cn, query.Usuario);
            }

            return new ResultDto<List<OssUsuarioRolResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }

    private static async Task<ResultDto<List<OssUsuarioRolResponse>>> ExecuteInlineAsync(OracleConnection cn, string usuario)
    {
        using var cmd = new OracleCommand($@"
            SELECT CODIGO_USUARIO_ROL,
                   USUARIO,
                   CODIGO_USUARIO,
                   DESCRIPCION,
                   {OssUsuarioRolDb.JsonMenuSelectList()}
              FROM SIS.OSS_USUARIO_ROL
             WHERE UPPER(TRIM(USUARIO)) = UPPER(TRIM(:p_USUARIO))
             ORDER BY CODIGO_USUARIO_ROL DESC", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = OssUsuarioRolDb.DbValue(usuario);

        var list = new List<OssUsuarioRolResponse>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(OssUsuarioRolDb.MapUsuarioRol(reader));
            }
        }

        return new ResultDto<List<OssUsuarioRolResponse>>(list)
        {
            Data = list,
            IsValid = true,
            Message = list.Count == 0 ? "Registro no encontrado" : "success"
        };
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
