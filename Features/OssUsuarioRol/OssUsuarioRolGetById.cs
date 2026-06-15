using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.OssUsuarioRol;

public record GetOssUsuarioRolByIdQuery(int CodigoUsuarioRol);

public class GetOssUsuarioRolByIdHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<OssUsuarioRolResponse>> HandleAsync(GetOssUsuarioRolByIdQuery query)
    {
        using var cn = _connectionDB.GetSisConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<OssUsuarioRolResponse>(null!)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión SIS: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("SIS.SP_OSS_USR_ROL_GET_ID", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_USUARIO_ROL", OracleDbType.Int32).Value = query.CodigoUsuarioRol;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        OssUsuarioRolResponse? resultData = null;

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    resultData = OssUsuarioRolDb.MapUsuarioRol(reader);
                }
            }

            string dbMessage = OssUsuarioRolDb.GetMessage(pMessage);
            bool isSuccess = OssUsuarioRolDb.IsSuccessMessage(dbMessage) && resultData is not null;

            if (!isSuccess && dbMessage.Contains("TTC", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteInlineAsync(cn, query.CodigoUsuarioRol);
            }

            return new ResultDto<OssUsuarioRolResponse>(resultData!)
            {
                IsValid = isSuccess,
                Message = resultData is null ? "Registro no encontrado" : dbMessage
            };
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("TTC", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteInlineAsync(cn, query.CodigoUsuarioRol);
            }

            return new ResultDto<OssUsuarioRolResponse>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }

    private static async Task<ResultDto<OssUsuarioRolResponse>> ExecuteInlineAsync(OracleConnection cn, int codigoUsuarioRol)
    {
        using var cmd = new OracleCommand($@"
            SELECT CODIGO_USUARIO_ROL,
                   USUARIO,
                   CODIGO_USUARIO,
                   DESCRIPCION,
                   {OssUsuarioRolDb.JsonMenuSelectList()}
              FROM SIS.OSS_USUARIO_ROL
             WHERE CODIGO_USUARIO_ROL = :p_CODIGO_USUARIO_ROL", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO_ROL", OracleDbType.Int32).Value = codigoUsuarioRol;

        OssUsuarioRolResponse? resultData = null;
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                resultData = OssUsuarioRolDb.MapUsuarioRol(reader);
            }
        }

        return new ResultDto<OssUsuarioRolResponse>(resultData!)
        {
            Data = resultData,
            IsValid = resultData is not null,
            Message = resultData is null ? "Registro no encontrado" : "success"
        };
    }
}

[ApiController]
[Route("api/OssUsuarioRol")]
public class OssUsuarioRolGetByIdController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("getById")]
    public async Task<IActionResult> GetById(GetOssUsuarioRolByIdQuery value)
    {
        var handler = new GetOssUsuarioRolByIdHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
