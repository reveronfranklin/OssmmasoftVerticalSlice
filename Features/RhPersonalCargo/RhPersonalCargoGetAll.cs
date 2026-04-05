using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;

namespace OssmmasoftVerticalSlice.Features.RhPersonalCargo;

// Request
public record RhPersonalCargoGetAllQuery(string p_where);

// Response (Ajustado para ser consistente con los tipos de Oracle)
public record GetRhPersonalCargoGetAllResponse(
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

public class GetRhPersonalCargoGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<GetRhPersonalCargoGetAllResponse>>> HandleAsync(RhPersonalCargoGetAllQuery value)
    {
        // 1. Limpieza inicial
        string cleanWhere = value.p_where?.Trim() ?? "";
        
        // Quitar comillas externas accidentales si existen (ej: "'STATUS = 'A''")
        if (cleanWhere.StartsWith("'") && cleanWhere.EndsWith("'") && cleanWhere.Length > 2)
        {
            cleanWhere = cleanWhere.Substring(1, cleanWhere.Length - 2);
        }

        // 2. Validación de Balanceo de Comillas (Prevenir ORA-01756)
        int quoteCount = cleanWhere.Count(f => f == '\'');
        if (quoteCount % 2 != 0)
        {
            return new ResultDto<List<GetRhPersonalCargoGetAllResponse>>(null)
            {
                IsValid = false,
                Message = $"Error de sintaxis: Hay una comilla simple sin cerrar en el filtro: [{cleanWhere}]"
            };
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_PERS_CARGO_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        // Parámetro de entrada
        cmd.Parameters.Add("p_where", OracleDbType.Varchar2).Value = 
            string.IsNullOrWhiteSpace(cleanWhere) ? (object)DBNull.Value : cleanWhere;

        // Parámetros de salida
        var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<GetRhPersonalCargoGetAllResponse>();

        try 
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                        list.Add(new GetRhPersonalCargoGetAllResponse(
                            reader.SafeGetInt32("CODIGO_PERSONA"),
                            reader.SafeGetString("CEDULA"),
                            reader.SafeGetString("FOTO"),
                            reader.SafeGetString("NOMBRE"),
                            reader.SafeGetString("APELLIDO"),
                            reader.SafeGetString("NACIONALIDAD"),
                            reader.SafeGetString("DESCRIPCION_NACIONALIDAD"),
                            reader.SafeGetString("SEXO"),
                            reader.SafeGetInt32("ESTADO_CIVIL_ID"),
                            reader.SafeGetString("ESTADO_CIVIL"),
                            reader.SafeGetString("STATUS"),
                            reader.SafeGetString("DESCRIPCION_STATUS"),
                            reader.SafeGetInt32("CODIGO_EMPRESA"),
                            reader.SafeGetString("DESCRIPCION_SEXO"),
                            reader.SafeGetInt32("CODIGO_RELACION_CARGO"),
                            reader.SafeGetInt32("CODIGO_CARGO"),
                            reader.SafeGetString("CARGO_CODIGO"),
                            reader.SafeGetInt32("CODIGO_ICP"),
                            reader.SafeGetInt32("CODIGO_ICP_UBICACION"),
                            reader.SafeGetDecimal("SUELDO"),
                            reader.SafeGetString("DESCRIPCION_CARGO"),
                            reader.SafeGetInt32("CODIGO_TIPO_NOMINA"),
                            reader.SafeGetString("TIPO_NOMINA"),
                            reader.SafeGetInt32("FRECUENCIA_PAGO_ID"),
                            reader.SafeGetString("CODIGO_SECTOR"),
                            reader.SafeGetString("CODIGO_PROGRAMA"),
                            reader.SafeGetString("CODIGO_SUBPROGRAMA"),
                            reader.SafeGetString("CODIGO_PROYECTO"),
                            reader.SafeGetString("CODIGO_ACTIVIDAD"),
                            reader.SafeGetString("CODIGO_OFICINA"),
                            reader.SafeGetString("DENOMINACION_ICP"),
                            reader.GetDateTime(reader.GetOrdinal("FECHA_INGRESO")),
                            reader.SafeGetInt32("TIPO_CUENTA_ID"),
                            reader.SafeGetString("DESCRIPCION_TIPO_CUENTA"),
                            reader.SafeGetInt32("BANCO_ID"),
                            reader.SafeGetString("DESCRIPCION_BANCO"),
                            reader.SafeGetString("NO_CUENTA"),
                            reader.SafeGetString("SIGLAS_TIPO_NOMINA"),
                            reader.SafeGetString("RIF")
                        ));
                }
            }

            string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() : "Success";
            int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString()) : 0;
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            return new ResultDto<List<GetRhPersonalCargoGetAllResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = dbTotalRecords,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (OracleException ex)
        {
            // LOG EN CONSOLA DE DEBUG
            Debug.WriteLine(">>>> ORACLE ERROR: " + ex.Message);
            Debug.WriteLine(">>>> FILTRO ENVIADO: " + cleanWhere);

            return new ResultDto<List<GetRhPersonalCargoGetAllResponse>>(null)
            {
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}. Filtro: {cleanWhere}"
            };
        }
    }
}
//Endpoint
[ApiController]
[Route("api/RhPersonalCargoGetAll")]
public class RhPersonalCargoGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(RhPersonalCargoGetAllQuery value)
    {
        try
        {
            var handler = new GetRhPersonalCargoGetAllHandler(_connectionDB);
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