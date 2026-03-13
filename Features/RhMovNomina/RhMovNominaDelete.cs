using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;



namespace OssmmasoftVerticalSlice.Features.RhMovNomina;


// Request para Eliminar
public record DeleteRhMovNominaCommand(int CodigoMovNomina);

//response


//handler
//handler asíncrono
public class DeleteRhMovNominaHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<string>> HandleAsync(DeleteRhMovNominaCommand command)
    {
        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_MOV_NOMINA_DELETE", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        // Parámetro de entrada: Solo el ID
        cmd.Parameters.Add("p_CODIGO_MOV_NOMINA", OracleDbType.Int32).Value = command.CodigoMovNomina;

         // Parámetro de Salida
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

            return new ResultDto<string>(isSuccess ? "Registro Eliminado correctamente" : null)
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
                Message = $"Error técnico al eliminar: {ex.Message}"
            };
        }
    }
}
//Endpoint
[ApiController]
[Route("api/RhMovNomina")]
public class RhMovNominaDeleteController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("delete")]
    public async Task<IActionResult> GetAll(DeleteRhMovNominaCommand value)
    {
        try
        {
            var handler = new DeleteRhMovNominaHandler(_connectionDB);
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