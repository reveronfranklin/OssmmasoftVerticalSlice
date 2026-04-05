using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.PreCargos;
using OssmmasoftVerticalSlice.Features.RhTipoNomina;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;

namespace OssmmasoftVerticalSlice.Features.RhPersonalCargo;

// Request
//public record RhPersonalCargoParametrosFilterQuery(string p_where);

// Response (Ajustado para ser consistente con los tipos de Oracle)
public record GetRhPersonalCargoParametrosFilterResponse(
    int CodigoPersona, string Cedula, string Foto, string Nombre, string Apellido,
    string Nacionalidad, string DescripcionNacionalidad, string Sexo, int EstadoCivilId,
    string EstadoCivil, string Status, string DescripcionStatus, int CodigoEmpresa,
    string DescricionSexo, int CodigoRelacionCargo, int CodigoCargo, string CargoCodigo,
    int CodigoIcp, int CodigoIcpUbicacion, decimal Sueldo, string DescripcionCargo,
    int CodigoTipoNomina, string TipoNomina, int FrecuenciaPagoId, string CodigoSector,
    string CodigoPrograma, string CodigoSubPrograma, string CodigoProyecto,
    string CodigoActividad, string CodigoOficina, string DenominacionIcp,
    DateTime FechaIngreso, int TipoCuentaId, string DescripcionTipoCuenta,
    int BancoId, string DescripcionBanco, string NoCuenta, string SiglastipoNomina,
    string Rif
);

public record ItemsParameters(
    List<Parameters>  Items
);

public record Parameters(
    string Field,
    string FieldDescription,
    List<Values> Values
);

public record Values(string Code,string Description);


public class GetRhPersonalCargoParametrosFilterHandler(ConnectionDB _connectionDB)
{


    public Parameters GetSexos()
    {
        // Corrección: Pasa los argumentos directamente al constructor
        return new Parameters
        (
            "SEXO", 
            "Sexo",
            new List<Values>
            {
                new Values("M", "Masculino"),
                new Values("F", "Femenino")
            }
        );
    }
     public Parameters GetNacionalidad()
    {
        // Corrección: Pasa los argumentos directamente al constructor
        return new Parameters
        (
            "NACIONALIDAD", 
            "Nacionalidad",
            new List<Values>
            {
                new Values("V", "Venezolano"),
                new Values("E", "Extranjero")
            }
        );
    }
 public Parameters GetCedula()
    {
        // Corrección: Pasa los argumentos directamente al constructor
        return new Parameters
        (
            "CEDULA", 
            "Cedula",
            new List<Values>
            {
                new Values("", "")
              
            }
        );
    }


     public Parameters GetNombre()
    {
        // Corrección: Pasa los argumentos directamente al constructor
        return new Parameters
        (
            "NOMBRE", 
            "Nombre",
            new List<Values>
            {
                new Values("", "")
              
            }
        );
    }
     public Parameters GetApellido()
    {
        // Corrección: Pasa los argumentos directamente al constructor
        return new Parameters
        (
            "APELLIDO", 
            "Apellido",
            new List<Values>
            {
                new Values("", "")
              
            }
        );
    }

    public Parameters GetEstadosCiviles()
    {
        // Corrección: Pasa los argumentos directamente al constructor
        return new Parameters
        (
            "ESTADO_CIVIL", 
            "Estado Civil",
            new List<Values>
            {
                new Values("356", "SOLTERO (A)"),
                new Values("355", "CASADO (A)"),
                new Values("358", "DIVORCIADO (A)"),
                new Values("357", "VIUDO (A) ")
            }
        );
    }

    public async Task<Parameters> GetTipoNomina()
    {
        
          var handler = new GetRhTipoNominaGetAllHandler(_connectionDB);
            // Llamada asíncrona al handler
            var value = new RhTipoNominaGetAllQuery("");
            var tipoNomina = await handler.HandleAsync(value);
            var list = new List<Values>();
            foreach (var item in tipoNomina.Data)
            {
                list.Add(new Values(item.CodigoTipoNomina.ToString(), item.Descripcion));
            }

            return new Parameters
            (
                "CODIGO_TIPO_NOMINA", 
                "Tipo Nomina",
                list
            );
    }


 public async Task<Parameters> GetCargos()
    {
        
            var handler = new GetPreCargoGetAllHandler(_connectionDB);
            // Llamada asíncrona al handler
            var value = new PreCargoGetAllQuery("");
            var cargos = await handler.HandleAsync(value);
            var list = new List<Values>();
            foreach (var item in cargos.Data)
            {
                list.Add(new Values(item.CodigoCargo.ToString(), item.Descripcion));
            }

            return new Parameters
            (
                "DESCRIPCION_CARGO", 
                "Cargos",
                list
            );
    }




    public async Task<ResultDto<List<ItemsParameters>>> HandleAsync()
    {
      

       
       var list = new List<ItemsParameters>();

        list.Add(new ItemsParameters
        (
            new List<Parameters> // Aquí usamos llaves para los elementos
            {
                await GetTipoNomina(),
                await GetCargos(),
                GetSexos(),
                GetEstadosCiviles(),
                GetNombre(),
                GetApellido(),
                GetCedula(),
                GetNacionalidad(),
               
            }
        ));

        try 
        {

            return new ResultDto<List<ItemsParameters>>(list)
            {
                Data = list ,
                CantidadRegistros = list.Count,
                IsValid = true,
                Message = ""
            };
        }
        catch (OracleException ex)
        {
            // LOG EN CONSOLA DE DEBUG
            Debug.WriteLine(">>>> ORACLE ERROR: " + ex.Message);
           

            return new ResultDto<List<ItemsParameters>>(null)
            {
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}. Filtro: "
            };
        }
    }
}
//Endpoint
[ApiController]
[Route("api/RhPersonalCargoParametrosFilter")]
public class RhPersonalCargoParametrosFilterController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var handler = new GetRhPersonalCargoParametrosFilterHandler(_connectionDB);
            // Llamada asíncrona al handler
            var categories = await handler.HandleAsync();

            return Ok(categories);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new 
            { 
                message = "Error interno en el servidor", 
                detail = ex.Message 
            });
        }
    }
}