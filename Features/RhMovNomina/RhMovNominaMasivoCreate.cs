using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;



namespace OssmmasoftVerticalSlice.Features.RhMovNomina;

// Request para Insertar
public record CreateRhMovNominaMasivoCommand(
    int CodigoTipoNomina,
    int[] CodigoPersona,
    int CodigoConcepto,
    string ComplementoConcepto,
    string Tipo, // E, F, V
    int FrecuenciaId,
    decimal Monto,
    string Status, // A o null
    int UsuarioIns,
    string? Extra1 = null,
    string? Extra2 = null,
    string? Extra3 = null
);

//response


//handler
//handler asíncrono
public class CreateRhMovNominaMasivoHandler(ConnectionDB _connectionDB,IConfiguration _config)
{


public async Task<ResultDto<string>> HandleAsync(CreateRhMovNominaMasivoCommand command)
{


try
        {
            if (command.CodigoPersona is null || command.CodigoPersona.Length == 0)
            {
                return new ResultDto<string>(null)
                {
                    IsValid = false,
                    Message = "Debe indicar al menos una persona para procesar."
                };
            }

            var handler = new CreateRhMovNominaHandler(_connectionDB,_config);
            var errores = new List<string>();
            var procesados = 0;

            foreach (var codigoPersona in command.CodigoPersona)
            {
                CreateRhMovNominaCommand value = new CreateRhMovNominaCommand(
                     command.CodigoTipoNomina,
                     codigoPersona,
                     command.CodigoConcepto,
                     command.ComplementoConcepto,
                     command.Tipo, // E, F, V
                     command.FrecuenciaId,
                     command.Monto,
                     command.Status, // A o null
                     command.UsuarioIns,
                     command.Extra1,
                     command.Extra2,
                     command.Extra3
                     );

                var movNomina = await handler.HandleAsync(value);
                if (movNomina.IsValid)
                {
                    procesados++;
                    continue;
                }

                var detalle = string.IsNullOrWhiteSpace(movNomina.Message) ? "Error no especificado." : movNomina.Message;
                errores.Add($"Persona {codigoPersona}: {detalle}");
            }

            if (errores.Count == 0)
            {
                return new ResultDto<string>($"{procesados} de {command.CodigoPersona.Length} registros procesados")
                {
                    IsValid = true,
                    Message = "Success"
                };
            }

            var resumenErrores = string.Join(" | ", errores);
            return new ResultDto<string>($"{procesados} de {command.CodigoPersona.Length} registros procesados")
            {
                IsValid = false,
                Message = resumenErrores
            };
        }
        catch (Exception ex)
        {
          
              return new ResultDto<string>(null)
              {
                  IsValid = false,
                  Message = $"Error técnico: {ex.Message}"
              };
        }


       
       
        
      


    }
  
}





//Endpoint
[ApiController]
[Route("api/RhMovNominaMasivo")]
public class RhMovNomiaMasivoController(ConnectionDB _connectionDB,IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> GetAll(CreateRhMovNominaMasivoCommand value)
    {
        try
        {
            var handler = new CreateRhMovNominaMasivoHandler(_connectionDB,_config);
            // Llamada asíncrona al handler
            var masivos = await handler.HandleAsync(value);

            return Ok(masivos);
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
