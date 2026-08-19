using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReporteBm1;

// =============================================================================
// Inventario de Bienes Muebles BM-1 (Especial). Requerimiento 27.
//
// Comparte el query de negocio con ReporteBm1 -es el mismo SP- y se diferencia
// en dos cosas: filtros opcionales adicionales y un layout de formulario oficial
// con encabezado de entidad, agrupamiento por unidad con salto de pagina,
// subtotales y bloque de firmas.
//
// Vive en la misma feature a proposito (opcion (a) del requerimiento 27): el
// query de negocio es identico, y mantener dos copias garantiza que un dia se
// corrija una sola.
// =============================================================================

/// <summary>
/// Filtros del BM-1 Especial. **Todos opcionales**, como en el reporte legado:
/// sin ninguno, imprime el inventario completo de la empresa.
/// </summary>
public record ReporteBm1EspQuery(
    int? CodigoDirBien = null,
    string? PlacaDesde = null,
    string? PlacaHasta = null,
    int? CodigoArticulo = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,

    /// <summary>
    /// No es un filtro: es el nombre que se imprime en el bloque de firmas,
    /// igual que el P_USUARIO_RESPONSABLE del reporte original.
    /// </summary>
    string? Responsable = null
);

/// <summary>
/// Datos de la entidad para el encabezado. Se resuelven una sola vez y no
/// dependen de ningun filtro.
/// </summary>
public record ReporteBm1EspEntidad(
    string EntidadPropietaria,
    string Estado,
    string Municipio,
    string Direccion
);

/// <summary>
/// Una unidad de trabajo con sus bienes y sus subtotales. El agrupamiento se
/// hace en C# sobre el resultado plano, como el resto de reportes de un solo
/// nivel del repositorio.
/// </summary>
public record ReporteBm1EspUnidad(
    string UnidadTrabajo,
    List<ReporteBm1ItemResponse> Items,
    int Cantidad,
    decimal Total
);

public class ReporteBm1EspHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReporteBm1EspUnidad>>> HandleAsync(ReporteBm1EspQuery query)
    {
        if (!ReporteBm1Db.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return Invalid(errorMessage);
        }

        // Las fechas son opcionales aqui, al reves que en el listado tabular.
        // Si vienen las dos, se exige coherencia: un rango invertido devuelve
        // vacio en silencio, que es peor que rechazarlo.
        if (query.FechaDesde.HasValue && query.FechaHasta.HasValue
            && query.FechaDesde.Value.Date > query.FechaHasta.Value.Date)
        {
            return Invalid("La fecha desde no puede ser posterior a la fecha hasta.");
        }

        using var cn = _connectionDB.GetBmConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Invalid($"Error tecnico al abrir conexion BM: {ex.Message}");
        }

        using var cmd = new OracleCommand("BM.SP_REP_BM1_GET", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = DbValue(query.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = DbValue(query.FechaHasta);
        cmd.Parameters.Add("p_CodigosIcp", OracleDbType.Varchar2).Value = DBNull.Value;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = DbValue(query.CodigoDirBien);
        cmd.Parameters.Add("p_PlacaDesde", OracleDbType.Varchar2).Value = DbValue(query.PlacaDesde);
        cmd.Parameters.Add("p_PlacaHasta", OracleDbType.Varchar2).Value = DbValue(query.PlacaHasta);
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = DbValue(query.CodigoArticulo);

        // Sin esto el quiebre por unidad partiria una misma unidad en varios
        // bloques: el orden por defecto del SP es por fecha de movimiento.
        cmd.Parameters.Add("p_OrdenUnidad", OracleDbType.Char).Value = "S";

        var planos = new List<ReporteBm1ItemResponse>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                planos.Add(ReporteBm1Db.MapItem(reader));
            }
        }
        catch (OracleException ex)
        {
            return Invalid($"Error de base de datos ({ex.Number}): {ex.Message}");
        }

        var message = pMessage.Value == DBNull.Value ? string.Empty : pMessage.Value?.ToString() ?? string.Empty;

        if (!string.Equals(message, "Success", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(message, "success", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid(message);
        }

        // GroupBy conserva el orden de aparicion, que ya viene por unidad desde
        // el SP: reordenar aqui seria trabajo repetido.
        var unidades = planos
            .GroupBy(i => i.UnidadTrabajo ?? string.Empty)
            .Select(g => new ReporteBm1EspUnidad(
                g.Key,
                g.ToList(),
                g.Sum(i => i.Cantidad),
                g.Sum(i => i.Cantidad * i.ValorActual)))
            .ToList();

        return new ResultDto<List<ReporteBm1EspUnidad>>(unidades)
        {
            Data = unidades,
            IsValid = true,
            Message = string.Empty,
            CantidadRegistros = planos.Count
        };
    }

    /// <summary>
    /// Encabezado de entidad. Se resuelve con una consulta directa y no con un
    /// procedimiento propio: es un solo registro y no tiene logica de negocio,
    /// asi que un SP dedicado seria un objeto mas que mantener sin ganar nada.
    ///
    /// La identificacion vigente es la del presupuesto mas reciente, tal como
    /// lo resolvia el reporte original.
    /// </summary>
    public async Task<ReporteBm1EspEntidad> ObtenerEntidadAsync()
    {
        var vacio = new ReporteBm1EspEntidad(string.Empty, string.Empty, string.Empty, string.Empty);

        if (!ReporteBm1Db.TryGetEmpresa(_config, out int empresa, out _))
        {
            return vacio;
        }

        try
        {
            // Se usa la conexion BM y se califica el schema, igual que hace el
            // propio SP con PRE.PRE_INDICE_CAT_PRG: el usuario BM ya tiene los
            // permisos de lectura sobre PRE y abrir una segunda conexion solo
            // para leer una fila seria pagar de mas.
            using var cn = _connectionDB.GetBmConnection();
            await cn.OpenAsync();

            using var cmd = new OracleCommand(
                @"SELECT A.DENOMINACION_ONP, A.ESTADO, A.MUNICIPIO, A.DOMICILIO_LEGAL
                    FROM PRE.PRE_IDENTIFICACIONES A
                   WHERE A.CODIGO_EMPRESA = :p_CodigoEmpresa
                     AND A.CODIGO_PRESUPUESTO = (
                          SELECT MAX(PP.CODIGO_PRESUPUESTO) FROM PRE.PRE_PRESUPUESTOS PP
                         )", cn)
            {
                BindByName = true
            };

            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;

            using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync()
                ? new ReporteBm1EspEntidad(
                    reader.SafeGetString("DENOMINACION_ONP"),
                    reader.SafeGetString("ESTADO"),
                    reader.SafeGetString("MUNICIPIO"),
                    reader.SafeGetString("DOMICILIO_LEGAL"))
                : vacio;
        }
        catch
        {
            // El encabezado es presentacion: si no se puede resolver, el reporte
            // sale con los campos en blanco antes que no salir.
            return vacio;
        }
    }

    private static object DbValue(DateTime? value) => value.HasValue ? value.Value : DBNull.Value;

    private static object DbValue(int? value) => value.HasValue ? value.Value : DBNull.Value;

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static ResultDto<List<ReporteBm1EspUnidad>> Invalid(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}
