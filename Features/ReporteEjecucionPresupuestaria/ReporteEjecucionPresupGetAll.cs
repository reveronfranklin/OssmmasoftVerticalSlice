using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteEjecucionPresupuestaria;

// =============================================================================
// Ejecucion Presupuestaria y Financiera del Presupuesto de Gastos.
// Requerimiento 26 - migracion de PRE_EJECUCION_POR_FECHA_FP.M4.
//
// Es el reporte con la estructura mas profunda del repositorio: para cada
// imputacion presupuestaria (ICP) despliega los cinco niveles del Plan Unico de
// Cuentas -Partida, Generica, Especifica, Subespecifica y Nivel5- con
// indentacion creciente, cierra cada ICP con un subtotal y el reporte con un
// total general.
//
// **La regla que gobierna todo: los subtotales suman solo las filas de nivel 1.**
// Un mismo importe aparece impreso en la fila de la Partida y otra vez en cada
// uno de sus descendientes, asi que sumar todas las filas multiplica el dinero
// por el numero de niveles. El reporte legado lo evita con sus columnas de
// formula CF_*, que valen 0 salvo en las filas de nivel Partida. Aqui lo hace
// <see cref="ReporteEjecucionPresupTotales.De"/>.
//
// Comprobado contra el PDF de muestra, grupo 01-02-01-00-51: sumando solo sus
// filas de Partida da 6.944.908,00, que es el subtotal impreso; sumando todas da
// 20.834.724,00.
// =============================================================================

/// <summary>
/// Filtros del reporte.
///
/// <c>CodigoPresupuesto</c> es obligatorio y no se resuelve por configuracion,
/// igual que en el requerimiento 25: no existe un <c>settings:PresupuestoConfig</c>
/// y el ERP siempre lo hace elegir. En el motor llega del catalogo
/// <c>PRE_PRESUPUESTO</c>.
/// </summary>
public record ReporteEjecucionPresupQuery(
    int CodigoPresupuesto = 0,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,

    /// <summary>
    /// Fuente de financiamiento. Vacio = todas. Ver el encabezado del stored
    /// procedure: el valor 92 se trata como "consolidado" -todas las fuentes,
    /// excluyendo la 719 de los importes- y cualquier otro valor filtra por esa
    /// fuente. **Esa semantica es la divergencia principal de esta migracion y
    /// esta pendiente de confirmar contra datos reales.**
    /// </summary>
    int? FinanciadoId = null,
    string? Usuario = null
);

/// <summary>
/// Una fila del reporte. <c>Nivel</c> va de 1 (Partida) a 5 y decide tanto la
/// indentacion como si la fila entra en los subtotales.
/// </summary>
public record ReporteEjecucionPresupItem(
    int Nivel,
    string CodigoSector,
    string CodigoPrograma,
    string CodigoSubprograma,
    string CodigoProyecto,
    string CodigoActividad,
    string DenominacionIcp,
    string CodigoPartida,
    string CodigoGenerica,
    string CodigoEspecifica,
    string CodigoSubespecifica,
    string CodigoNivel5,
    string DenominacionPuc,
    decimal Presupuestado,
    decimal Modificado,
    decimal Vigente,
    decimal Comprometido,
    decimal Bloqueado,
    decimal Causado,
    decimal Pagado,
    decimal Deuda,
    decimal Disponibilidad,
    decimal Asignacion,
    decimal DisponibilidadFinanciera
)
{
    /// <summary>
    /// Codigo de imputacion presupuestaria armado como lo hacia
    /// <c>CODIGO_ICP_DSPFORMULA</c>: <c>01-02-01-00-51</c>.
    /// </summary>
    public string CodigoIcp =>
        $"{CodigoSector}-{CodigoPrograma}-{CodigoSubprograma}-{CodigoProyecto}-{CodigoActividad}";

    /// <summary>
    /// El codigo que se imprime en la columna de su nivel. La fila de Partida
    /// muestra <c>4.01</c>; las demas muestran solo el segmento que las
    /// identifica, que es como se lee la indentacion en el reporte original.
    /// </summary>
    public string CodigoNivel => Nivel switch
    {
        1 => CodigoPartida,
        2 => CodigoGenerica,
        3 => CodigoEspecifica,
        4 => CodigoSubespecifica,
        _ => CodigoNivel5
    };
}

/// <summary>
/// Un grupo de imputacion presupuestaria con sus filas. Es el quiebre de grupo
/// del reporte legado, que reimprime su encabezado en cada pagina y cierra con
/// un subtotal.
/// </summary>
public record ReporteEjecucionPresupGrupo(
    string CodigoIcp,
    string DenominacionIcp,
    List<ReporteEjecucionPresupItem> Items,
    ReporteEjecucionPresupTotales Totales
);

/// <summary>
/// Las diez columnas totalizadas. Se usa igual para el subtotal de un grupo ICP
/// y para el total general del reporte.
/// </summary>
public record ReporteEjecucionPresupTotales(
    decimal Presupuestado,
    decimal Modificado,
    decimal Vigente,
    decimal Comprometido,
    decimal Causado,
    decimal Pagado,
    decimal Deuda,
    decimal Disponibilidad,
    decimal Asignacion,
    decimal DisponibilidadFinanciera
)
{
    public static readonly ReporteEjecucionPresupTotales Cero =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// **Suma solo las filas de nivel 1.** Es la regla que evita contar el mismo
    /// dinero una vez por nivel de la jerarquia; ver el encabezado de este
    /// archivo y el del stored procedure.
    /// </summary>
    public static ReporteEjecucionPresupTotales De(IEnumerable<ReporteEjecucionPresupItem> items)
    {
        var raiz = items.Where(i => i.Nivel == 1).ToList();

        return new ReporteEjecucionPresupTotales(
            raiz.Sum(i => i.Presupuestado),
            raiz.Sum(i => i.Modificado),
            raiz.Sum(i => i.Vigente),
            raiz.Sum(i => i.Comprometido),
            raiz.Sum(i => i.Causado),
            raiz.Sum(i => i.Pagado),
            raiz.Sum(i => i.Deuda),
            raiz.Sum(i => i.Disponibilidad),
            raiz.Sum(i => i.Asignacion),
            raiz.Sum(i => i.DisponibilidadFinanciera));
    }

    public static ReporteEjecucionPresupTotales Sumar(
        IEnumerable<ReporteEjecucionPresupTotales> partes)
    {
        var lista = partes.ToList();

        return new ReporteEjecucionPresupTotales(
            lista.Sum(t => t.Presupuestado),
            lista.Sum(t => t.Modificado),
            lista.Sum(t => t.Vigente),
            lista.Sum(t => t.Comprometido),
            lista.Sum(t => t.Causado),
            lista.Sum(t => t.Pagado),
            lista.Sum(t => t.Deuda),
            lista.Sum(t => t.Disponibilidad),
            lista.Sum(t => t.Asignacion),
            lista.Sum(t => t.DisponibilidadFinanciera));
    }
}

public class ReporteEjecucionPresupHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReporteEjecucionPresupGrupo>>> HandleAsync(
        ReporteEjecucionPresupQuery query)
    {
        if (!ReporteEjecucionPresupDb.TryGetEmpresa(_config, out int empresa, out string errorEmpresa))
        {
            return Invalid(errorEmpresa);
        }

        if (query.CodigoPresupuesto <= 0)
        {
            return Invalid("Indique el presupuesto del reporte.");
        }

        if (!query.FechaDesde.HasValue || !query.FechaHasta.HasValue)
        {
            return Invalid("Indique la fecha desde y la fecha hasta del periodo.");
        }

        var desde = query.FechaDesde.Value.Date;
        var hasta = query.FechaHasta.Value.Date;

        if (desde > hasta)
        {
            return Invalid("La fecha desde no puede ser posterior a la fecha hasta.");
        }

        // El AfterPForm del reporte legado recortaba la fecha hasta al 31/12 del
        // ano de la fecha desde. Se conserva porque el reporte es de un ejercicio
        // presupuestario y un rango que cruce el cierre mezclaria dos: el titulo
        // dice "PERIODO PRESUPUESTARIO ANO <ano de la fecha desde>" y con un rango
        // a caballo ese titulo seria falso.
        var recortada = false;

        if (hasta.Year > desde.Year)
        {
            hasta = new DateTime(desde.Year, 12, 31);
            recortada = true;
        }

        using var cn = _connectionDB.GetPresupuestoConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Invalid($"Error tecnico al abrir conexion PRE: {ex.Message}");
        }

        using var cmd = new OracleCommand("PRE.SP_REP_EJEC_PRESUP_GET", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoPresupuesto", OracleDbType.Int32).Value = query.CodigoPresupuesto;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = desde;
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = hasta;
        cmd.Parameters.Add("p_FinanciadoId", OracleDbType.Int32).Value =
            query.FinanciadoId is int f && f > 0 ? f : DBNull.Value;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var planos = new List<ReporteEjecucionPresupItem>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                planos.Add(ReporteEjecucionPresupDb.MapItem(reader));
            }
        }
        catch (OracleException ex)
        {
            return Invalid($"Error de base de datos ({ex.Number}): {ex.Message}");
        }
        catch (Exception ex)
        {
            return Invalid($"Error tecnico: {ex.Message}");
        }

        var message = pMessage.Value == DBNull.Value ? string.Empty : pMessage.Value?.ToString() ?? string.Empty;

        if (!ReporteEjecucionPresupDb.IsSuccessMessage(message))
        {
            return Invalid(message);
        }

        // GroupBy conserva el orden de aparicion, y el SP ordena por las cinco
        // columnas del ICP antes que nada: sin eso un mismo ICP saldria partido en
        // varios bloques, cada uno con su subtotal incompleto.
        var grupos = planos
            .GroupBy(i => i.CodigoIcp)
            .Select(g => new ReporteEjecucionPresupGrupo(
                g.Key,
                g.First().DenominacionIcp,
                g.ToList(),
                ReporteEjecucionPresupTotales.De(g)))
            .ToList();

        var general = ReporteEjecucionPresupTotales.Sumar(grupos.Select(g => g.Totales));

        return new ResultDto<List<ReporteEjecucionPresupGrupo>>(grupos)
        {
            Data = grupos,
            IsValid = true,
            Message = recortada
                ? $"La fecha hasta se ajusto al 31/12/{desde.Year}: el reporte es de un solo ejercicio presupuestario."
                : string.Empty,
            CantidadRegistros = planos.Count,
            Total1 = general.Presupuestado,
            Total2 = general.Vigente,
            Total3 = general.Comprometido,
            Total4 = general.Disponibilidad
        };
    }

    private static ResultDto<List<ReporteEjecucionPresupGrupo>> Invalid(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

internal static class ReporteEjecucionPresupDb
{
    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        empresa = 0;
        errorMessage = string.Empty;
        var empresaString = config["settings:EmpresaConfig"];

        if (string.IsNullOrEmpty(empresaString))
        {
            errorMessage = "Configuracion 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(empresaString, NumberStyles.Integer, CultureInfo.InvariantCulture, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un numero valido.";
            return false;
        }

        return true;
    }

    public static ReporteEjecucionPresupItem MapItem(IDataReader reader)
    {
        return new ReporteEjecucionPresupItem(
            reader.SafeGetInt32("NIVEL"),
            reader.SafeGetString("CODIGO_SECTOR").Trim(),
            reader.SafeGetString("CODIGO_PROGRAMA").Trim(),
            reader.SafeGetString("CODIGO_SUBPROGRAMA").Trim(),
            reader.SafeGetString("CODIGO_PROYECTO").Trim(),
            reader.SafeGetString("CODIGO_ACTIVIDAD").Trim(),
            reader.SafeGetString("DENOMINACION_ICP").Trim(),
            reader.SafeGetString("CODIGO_PARTIDA").Trim(),
            reader.SafeGetString("CODIGO_GENERICA").Trim(),
            reader.SafeGetString("CODIGO_ESPECIFICA").Trim(),
            reader.SafeGetString("CODIGO_SUBESPECIFICA").Trim(),
            reader.SafeGetString("CODIGO_NIVEL5").Trim(),
            reader.SafeGetString("DENOMINACION_PUC").Trim(),
            reader.SafeGetDecimal("PRESUPUESTADO"),
            reader.SafeGetDecimal("MODIFICADO"),
            reader.SafeGetDecimal("VIGENTE"),
            reader.SafeGetDecimal("COMPROMETIDO"),
            reader.SafeGetDecimal("BLOQUEADO"),
            reader.SafeGetDecimal("CAUSADO"),
            reader.SafeGetDecimal("PAGADO"),
            reader.SafeGetDecimal("DEUDA"),
            reader.SafeGetDecimal("DISPONIBILIDAD"),
            reader.SafeGetDecimal("ASIGNACION"),
            reader.SafeGetDecimal("DISPONIBILIDAD_FINAN")
        );
    }
}

[ApiController]
[Route("api/ReporteEjecucionPresup")]
public class ReporteEjecucionPresupController(
    ConnectionDB _connectionDB,
    IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteEjecucionPresupQuery value)
    {
        var handler = new ReporteEjecucionPresupHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }

    /// <summary>
    /// PDF del reporte. El usuario conectado es obligatorio: el reporte legado
    /// imprimia el login en el pie de cada pagina (<c>user$currentdate</c>) y el
    /// pie de auditoria compartido del requerimiento 17 hace lo mismo.
    /// </summary>
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteEjecucionPresupQuery value)
    {
        if (string.IsNullOrWhiteSpace(value.Usuario))
        {
            return BadRequest(new ResultDto<object?>(null)
            {
                IsValid = false,
                Message = "El usuario conectado es requerido para generar la ejecucion presupuestaria."
            });
        }

        var handler = new ReporteEjecucionPresupHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        if (!result.IsValid || result.Data is null)
        {
            return Ok(result);
        }

        if (result.Data.Count == 0)
        {
            return Ok(new ResultDto<string>(string.Empty)
            {
                IsValid = false,
                Message = "No hay ejecucion presupuestaria con los parametros seleccionados."
            });
        }

        var printContext = ReportPrintContext.Create(value.Usuario);
        var bytes = ReporteEjecucionPresupPdfGenerator.Generate(result.Data, value, printContext);

        Response.Headers.ContentDisposition = "inline; filename=\"ejecucion-presupuestaria.pdf\"";

        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }
}
