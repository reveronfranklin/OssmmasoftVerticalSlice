using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;



namespace OssmmasoftVerticalSlice.Features.RhMovNomina;


// Request para Consultar
public record GetRhMovNominaQuery(int CodigoMovNomina);

//response
public record RhMovNominaResponse(
    int CodigoMovNomina,
    int CodigoTipoNomina,
    int CodigoPersona,
    int CodigoConcepto,
    string ComplementoConcepto,
    string Tipo,
    int FrecuenciaId,
    decimal Monto,
    string Status,
    string Extra1,
    string Extra2,
    string Extra3,
    int UsuarioIns,
    DateTime FechaIns,
    int? UsuarioUpd,
    DateTime? FechaUpd,
    int CodigoEmpresa
);

//handler
//handler asíncrono
public class GetRhMovNominaByIdHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<RhMovNominaResponse>> HandleAsync(GetRhMovNominaQuery value)
    {
        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_MOV_NOM_GET_BY_ID", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_MOV_NOMINA", OracleDbType.Int32).Value = value.CodigoMovNomina;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000).Direction = ParameterDirection.Output;

        RhMovNominaResponse resultData = null;

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                resultData = new RhMovNominaResponse(
                    reader.GetInt32(reader.GetOrdinal("CODIGO_MOV_NOMINA")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_TIPO_NOMINA")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_PERSONA")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_CONCEPTO")),
                    reader.IsDBNull(reader.GetOrdinal("COMPLEMENTO_CONCEPTO")) ? "" : reader.GetString(reader.GetOrdinal("COMPLEMENTO_CONCEPTO")),
                    reader.GetString(reader.GetOrdinal("TIPO")),
                    reader.GetInt32(reader.GetOrdinal("FRECUENCIA_ID")),
                    reader.GetDecimal(reader.GetOrdinal("MONTO")),
                    reader.IsDBNull(reader.GetOrdinal("STATUS")) ? "" : reader.GetString(reader.GetOrdinal("STATUS")),
                    reader.IsDBNull(reader.GetOrdinal("EXTRA1")) ? "" : reader.GetString(reader.GetOrdinal("EXTRA1")),
                    reader.IsDBNull(reader.GetOrdinal("EXTRA2")) ? "" : reader.GetString(reader.GetOrdinal("EXTRA2")),
                    reader.IsDBNull(reader.GetOrdinal("EXTRA3")) ? "" : reader.GetString(reader.GetOrdinal("EXTRA3")),
                    reader.GetInt32(reader.GetOrdinal("USUARIO_INS")),
                    reader.GetDateTime(reader.GetOrdinal("FECHA_INS")),
                    reader.IsDBNull(reader.GetOrdinal("USUARIO_UPD")) ? null : reader.GetInt32(reader.GetOrdinal("USUARIO_UPD")),
                    reader.IsDBNull(reader.GetOrdinal("FECHA_UPD")) ? null : reader.GetDateTime(reader.GetOrdinal("FECHA_UPD")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_EMPRESA"))
                );
            }
        }

        string dbMessage = pMessage.ToString() ?? "Error";
        bool exists = resultData != null;

        return new ResultDto<RhMovNominaResponse>(resultData)
        {
            IsValid = exists,
            Message = exists ? "Success" : "Registro no encontrado"
        };
    }
}


//Endpoint
[ApiController]
[Route("api/RhMovNomina")]
public class GetByIdController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("getById")]
    public async Task<IActionResult> GetById(GetRhMovNominaQuery value)
    {
        try
        {
            var handler = new GetRhMovNominaByIdHandler(_connectionDB);
            // Llamada asíncrona al handler
            var categories = await handler.HandleAsync(value);

            return Ok(categories);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new 
            { 
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            });
        }
    }
}