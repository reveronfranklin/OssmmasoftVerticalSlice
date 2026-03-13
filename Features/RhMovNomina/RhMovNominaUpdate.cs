using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;



namespace OssmmasoftVerticalSlice.Features.RhMovNomina;

// Request para Insertar
// Request para Actualizar
public record UpdateRhMovNominaCommand(
    int CodigoMovNomina,
    int CodigoTipoNomina,
    int CodigoPersona,
    int CodigoConcepto,
    string ComplementoConcepto,
    string Tipo,
    int FrecuenciaId,
    decimal Monto,
    string Status,
    int UsuarioUpd,
    string? Extra1 = null,
    string? Extra2 = null,
    string? Extra3 = null
);

//response





//handler
//handler asíncrono
public class UpdateRhMovNominaHandler(ConnectionDB _connectionDB,IConfiguration _config)
{
    public async Task<ResultDto<string>> HandleAsync(UpdateRhMovNominaCommand command)
    {

        // 1. Validar la configuración antes de operar
        var empresaString = _config["settings:EmpresaConfig"];
        if (string.IsNullOrEmpty(empresaString))
        {
            return new ResultDto<string>(null) { IsValid = false, Message = "Configuración 'EmpresaConfig' no encontrada." };
        }

        if (!int.TryParse(empresaString, out int empresa))
        {
            return new ResultDto<string>(null) { IsValid = false, Message = "EmpresaConfig debe ser un número válido." };
        }
     
        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_MOV_NOMINA_UPDATE", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        // Parámetros de Entrada (Basados en tu UpdateRhMovNominaCommand record)
        cmd.Parameters.Add("p_CODIGO_MOV_NOMINA", OracleDbType.Int32).Value = command.CodigoMovNomina;
        cmd.Parameters.Add("p_CODIGO_TIPO_NOMINA", OracleDbType.Int32).Value = command.CodigoTipoNomina;
        cmd.Parameters.Add("p_CODIGO_PERSONA", OracleDbType.Int32).Value = command.CodigoPersona;
        cmd.Parameters.Add("p_CODIGO_CONCEPTO", OracleDbType.Int32).Value = command.CodigoConcepto;
        cmd.Parameters.Add("p_COMPLEMENTO_CONCEPTO", OracleDbType.Varchar2).Value = command.ComplementoConcepto;
        cmd.Parameters.Add("p_TIPO", OracleDbType.Varchar2).Value = command.Tipo;
        cmd.Parameters.Add("p_FRECUENCIA_ID", OracleDbType.Int32).Value = command.FrecuenciaId;
        cmd.Parameters.Add("p_MONTO", OracleDbType.Decimal).Value = command.Monto;
        cmd.Parameters.Add("p_STATUS", OracleDbType.Varchar2).Value = command.Status;
        cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = command.Extra1;
        cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = command.Extra2;
        cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = command.Extra3;
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = command.UsuarioUpd;

        // Parámetro de Salida para capturar validaciones del SP

        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000);
        pMessage.Direction = ParameterDirection.Output; // 2. Asignar la dirección por separado
        try
        {
            await cmd.ExecuteNonQueryAsync();

            // 3. Acceder a .Value y manejar posibles nulos o DBNull
             string dbMessage = pMessage.Value != DBNull.Value 
                       ? pMessage.Value.ToString() 
                       : "Sin respuesta de BD";
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            return new ResultDto<string>(isSuccess ? "Registro actualizado correctamente" : null)
            {
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(null)
            {
                IsValid = false,
                Message = $"Error técnico en Update: {ex.Message}"
            };
        }
    }
}


//Endpoint
[ApiController]
[Route("api/RhMovNomina")]
public class GetRhMovNominaUpdateController(ConnectionDB _connectionDB,IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("update")]
    public async Task<IActionResult> GetAll(UpdateRhMovNominaCommand value)
    {
        try
        {
            var handler = new UpdateRhMovNominaHandler(_connectionDB,_config);
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