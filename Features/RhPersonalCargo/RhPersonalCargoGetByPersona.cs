using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;



namespace OssmmasoftVerticalSlice.Features.RhPersonalCargo;

//request
public record RhPersonalCargoQuery(int CodigoPersona);



//response
public record GetRhPersonalCargoResponse(
    int CodigoPersona,
    string Cedula,
    string Foto,
    string Nombre,
    string Apellido,
    string Nacionalidad,
    string DescripcionNacionalidad,
    string Sexo,
    int EstadoCivilId,
    string EstadoCivil,
    string Status,
    string DescripcionStatus,
    int CodigoEmpresa,
    string DescricionSexo,
    int CodigoRelacionCargo,
    int CodigoCargo,
    string CargoCodigo,
    int CodigoIcp,
    int CodigoIcpUbicacion,
    decimal Sueldo,
    string DescripcionCargo,
    int CodigoTipoNomina,
    string TipoNomina,
    int FrecuenciaPagoId,
    string CodigoSector,
    string CodigoPrograma,
    string CodigoSubPrograma,
    string CodigoProyecto,
    string CodigoActividad,
    string CodigoOficina,
    string DenominacionIcp,
    DateTime FechaIngreso,
    int TipoCuentaId,
    string DescripcionTipoCuenta,
    int BancoId,
    string DescripcionBanco,
    string NoCuenta,
    string SiglastipoNomina,
    string Rif

   );




//handler
//handler asíncrono
public class GetRhPersonalCargoHandler(ConnectionDB _connectionDB)
{


public async Task<ResultDto<List<GetRhPersonalCargoResponse>>> HandleAsync(RhPersonalCargoQuery value)
{
    using var cn = _connectionDB.GetRhConnection();
    await cn.OpenAsync();

    using var cmd = new OracleCommand("RH.SP_PERS_CARGO_GET_BY_PE", cn); // Nombre corto < 30 char
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.BindByName = true;

    // Parámetros de entrada
    cmd.Parameters.Add("p_Codigo_Persona", OracleDbType.Int32).Value = value.CodigoPersona;

    // Parámetros de salida con referencias
    var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
    var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
    var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

    var list = new List<GetRhPersonalCargoResponse>();

    try 
    {
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(new GetRhPersonalCargoResponse(
                    reader.GetInt32(reader.GetOrdinal("CODIGO_PERSONA")),
                    reader.GetString(reader.GetOrdinal("CEDULA")),
                    reader.GetString(reader.GetOrdinal("FOTO")),
                    reader.GetString(reader.GetOrdinal("NOMBRE")),
                    reader.GetString(reader.GetOrdinal("APELLIDO")),
                    reader.GetString(reader.GetOrdinal("NACIONALIDAD")),
                    reader.GetString(reader.GetOrdinal("DESCRIPCION_NACIONALIDAD")),
                    reader.GetString(reader.GetOrdinal("SEXO")),
                    reader.GetInt32(reader.GetOrdinal("ESTADO_CIVIL_ID")),
                    reader.GetString(reader.GetOrdinal("ESTADO_CIVIL")),
                    reader.GetString(reader.GetOrdinal("STATUS")),
                    reader.GetString(reader.GetOrdinal("DESCRIPCION_STATUS")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_EMPRESA")),
                    reader.GetString(reader.GetOrdinal("DESCRIPCION_SEXO")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_RELACION_CARGO")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_CARGO")),
                    reader.GetString(reader.GetOrdinal("CARGO_CODIGO")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_ICP")),
                    reader.IsDBNull(reader.GetOrdinal("CODIGO_ICP_UBICACION")) ? 0 : reader.GetInt32(reader.GetOrdinal("CODIGO_ICP_UBICACION")),
                    reader.GetDecimal(reader.GetOrdinal("SUELDO")),
                    reader.GetString(reader.GetOrdinal("DESCRIPCION_CARGO")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_TIPO_NOMINA")),
                    reader.GetString(reader.GetOrdinal("TIPO_NOMINA")),
                    reader.GetInt32(reader.GetOrdinal("FRECUENCIA_PAGO_ID")),
                    reader.GetString(reader.GetOrdinal("CODIGO_SECTOR")),
                    reader.GetString(reader.GetOrdinal("CODIGO_PROGRAMA")),
                    reader.GetString(reader.GetOrdinal("CODIGO_SUBPROGRAMA")),
                    reader.GetString(reader.GetOrdinal("CODIGO_PROYECTO")),
                    reader.GetString(reader.GetOrdinal("CODIGO_ACTIVIDAD")),
                    reader.GetString(reader.GetOrdinal("CODIGO_OFICINA")),
                    reader.GetString(reader.GetOrdinal("DENOMINACION_ICP")),
                    reader.GetDateTime(reader.GetOrdinal("FECHA_INGRESO")),
                     reader.GetInt32(reader.GetOrdinal("TIPO_CUENTA_ID")),
                    reader.GetString(reader.GetOrdinal("DESCRIPCION_TIPO_CUENTA")),
                     reader.GetInt32(reader.GetOrdinal("BANCO_ID")),
                    reader.GetString(reader.GetOrdinal("DESCRIPCION_BANCO")),
                    reader.GetString(reader.GetOrdinal("NO_CUENTA")),
                    reader.GetString(reader.GetOrdinal("SIGLAS_TIPO_NOMINA")),
                    reader.GetString(reader.GetOrdinal("RIF"))
                   

                ));
            }
        }

        // Recuperar valores después del reader
        string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() : "Error desconocido";
        int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString()) : 0;

        // Determinamos si la operación fue exitosa según el mensaje del SP
        bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

        return new ResultDto<List<GetRhPersonalCargoResponse>>(list)
        {
            Data = isSuccess ? list : null, // Si falló, limpiamos la data
            CantidadRegistros = dbTotalRecords,
            Page = 1,
            TotalPage = 1,
            IsValid = isSuccess,
            Message = dbMessage
        };
    }
    catch (Exception ex)
    {
        // Error de conexión o de red
        return new ResultDto<List<GetRhPersonalCargoResponse>>(null)
        {
            IsValid = false,
            Message = $"Error técnico: {ex.Message}"
        };
    }
}



}

//Endpoint
[ApiController]
[Route("api/RhPersonalCargo")]
public class GetAllCategoryController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByPersona")]
    public async Task<IActionResult> GetAll(RhPersonalCargoQuery value)
    {
        try
        {
            var handler = new GetRhPersonalCargoHandler(_connectionDB);
            // Llamada asíncrona al handler
            var categories = await handler.HandleAsync(value);

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