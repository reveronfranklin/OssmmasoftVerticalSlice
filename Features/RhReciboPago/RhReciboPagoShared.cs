using System.Data;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public record RhReciboPagoFilter(int CodigoTipoNomina, int CodigoPeriodo,
    int? CodigoIcp = null, int? CodigoPersona = null, bool SendEmail = false);
public record RhReciboFecha(string Year, string Month, string Day);
public record RhReciboResumen(int Id, int CodigoTipoNomina, int CodigoPeriodo, int Periodo,
    DateTime FechaNomina, string FechaNominaString, RhReciboFecha FechaNominaObj,
    string CodigoIcpConcat, int CodigoIcp, string Denominacion, int CodigoPersona,
    string Nombre, string DescripcionPeriodo);
public record RhReciboOficina(string CodigoIcpConcat, int CodigoIcp, string Denominacion);
public record RhReciboDatos(IReadOnlyList<RhReciboLinea> Lineas, bool EsTemporal, string TipoNomina);

public record RhReciboLinea
{
    public DateTime FechaNomina { get; init; }
    public int CodigoPeriodo { get; init; }
    public int CodigoTipoNomina { get; init; }
    public string CodigoIcpConcat { get; init; } = "";
    public int CodigoIcp { get; init; }
    public string Denominacion { get; init; } = "";
    public string DenominacionCargo { get; init; } = "";
    public int Cedula { get; init; }
    public string Nombre { get; init; } = "";
    public string NoCuenta { get; init; } = "";
    public string NumeroConcepto { get; init; } = "";
    public string TipoMovConcepto { get; init; } = "";
    public string DenominacionConcepto { get; init; } = "";
    public string ComplementoConcepto { get; init; } = "";
    public string Porcentaje { get; init; } = "";
    public string TipoConcepto { get; init; } = "";
    public decimal Monto { get; init; }
    public decimal Asignacion { get; init; }
    public decimal Deduccion { get; init; }
    public string Status { get; init; } = "";
    public string DescripcionStatus { get; init; } = "";
    public int CodigoPersona { get; init; }
    public DateTime FechaIngreso { get; init; }
    public string CargoCodigo { get; init; } = "";
    public string Banco { get; init; } = "";
    public string CodigoConcepto { get; init; } = "";
    public string Modulo { get; init; } = "";
    public string CodigoIdentificador { get; init; } = "";
    public int CodigoEmpresa { get; init; }
    public string Descripcion { get; init; } = "";
    public int Periodo { get; init; }
    public decimal Sueldo { get; init; }
}

internal static class RhReciboPagoDb
{
    internal static string? Validate(RhReciboPagoFilter filter) =>
        filter.CodigoTipoNomina <= 0 || filter.CodigoPeriodo <= 0 || filter.CodigoIcp < 0 || filter.CodigoPersona < 0
            ? "Nomina, periodo o filtros de recibo invalidos."
            : filter.SendEmail ? "El envio por correo no esta habilitado para este reporte." : null;
    internal static object DbValue(object? value) => value ?? DBNull.Value;
    internal static bool Success(string message) => message.Equals("success", StringComparison.OrdinalIgnoreCase)
        || message.Equals("suscces", StringComparison.OrdinalIgnoreCase);
    internal static ResultDto<T> Fail<T>(string message) => new(default!) { IsValid = false, Message = message };

    internal static async Task<ResultDto<RhReciboDatos>> ReadAsync(ConnectionDB db, IConfiguration config,
        RhReciboPagoFilter filter, bool historicoOnly, CancellationToken ct)
    {
        if (Validate(filter) is { } error) return Fail<RhReciboDatos>(error);
        if (!int.TryParse(config["settings:EmpresaConfig"], out var empresa) || empresa <= 0)
            return Fail<RhReciboDatos>("EmpresaConfig debe ser un numero valido.");
        using var cn = db.GetRhConnection();
        try { await cn.OpenAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return Fail<RhReciboDatos>($"Error tecnico al abrir conexion RH: {ex.Message}"); }
        using var cmd = new OracleCommand("RH.SP_RH_RECIBO_PAGO_GET", cn) {
            CommandType = CommandType.StoredProcedure, BindByName = true
        };
        cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_NOMINA", OracleDbType.Int32).Value = filter.CodigoTipoNomina;
        cmd.Parameters.Add("p_PERIODO", OracleDbType.Int32).Value = filter.CodigoPeriodo;
        cmd.Parameters.Add("p_ICP", OracleDbType.Int32).Value = DbValue(filter.CodigoIcp);
        cmd.Parameters.Add("p_PERSONA", OracleDbType.Int32).Value = DbValue(filter.CodigoPersona);
        cmd.Parameters.Add("p_SOLO_HIST", OracleDbType.Int32).Value = historicoOnly ? 1 : 0;
        var temporal = cmd.Parameters.Add("p_TEMPORAL", OracleDbType.Int32, ParameterDirection.Output);
        var nomina = cmd.Parameters.Add("p_DESC_NOMINA", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var message = cmd.Parameters.Add("p_MESSAGE", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
        try
        {
            var rows = new List<RhReciboLinea>();
            using (var reader = await cmd.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) rows.Add(Map(reader));
            var text = message.Value?.ToString() ?? "Sin respuesta de la base de datos.";
            if (!Success(text)) return Fail<RhReciboDatos>(text);
            var isTemporal = temporal.Value != DBNull.Value && temporal.Value?.ToString() == "1";
            // El mapper legacy temporal omite Sueldo; preservar esa presentacion.
            rows = NormalizeRows(rows, isTemporal);
            return new(new(rows, isTemporal, nomina.Value?.ToString() ?? "")) { IsValid = true, Message = "success" };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return Fail<RhReciboDatos>($"Error tecnico: {ex.Message}"); }
    }

    internal static List<RhReciboLinea> NormalizeRows(List<RhReciboLinea> rows, bool isTemporal) =>
        isTemporal ? rows.Select(row => row with { Sueldo = 0m }).ToList() : rows;

    internal static RhReciboLinea Map(IDataReader reader) => new()
    {
        FechaNomina = reader.IsDBNull(reader.GetOrdinal("FECHA_NOMINA")) ? default : Convert.ToDateTime(reader["FECHA_NOMINA"], CultureInfo.InvariantCulture),
        CodigoPeriodo = reader.SafeGetInt32("CODIGO_PERIODO"),
        CodigoTipoNomina = reader.SafeGetInt32("CODIGO_TIPO_NOMINA"),
        CodigoIcpConcat = reader.SafeGetString("CODIGO_ICP_CONCAT"),
        CodigoIcp = reader.SafeGetInt32("CODIGO_ICP"),
        Denominacion = reader.SafeGetString("DENOMINACION"),
        DenominacionCargo = reader.SafeGetString("DENOMINACION_CARGO"),
        Cedula = reader.SafeGetInt32("CEDULA"),
        Nombre = reader.SafeGetString("NOMBRE"),
        NoCuenta = reader.SafeGetString("NO_CUENTA"),
        NumeroConcepto = reader.SafeGetString("NUMERO_CONCEPTO"),
        TipoMovConcepto = reader.SafeGetString("TIPO_MOV_CONCEPTO"),
        DenominacionConcepto = reader.SafeGetString("DENOMINACION_CONCEPTO"),
        ComplementoConcepto = reader.SafeGetString("COMPLEMENTO_CONCEPTO"),
        Porcentaje = reader.SafeGetString("PORCENTAJE"),
        TipoConcepto = reader.SafeGetString("TIPO_CONCEPTO"),
        Monto = reader.SafeGetDecimal("MONTO"),
        Asignacion = reader.SafeGetDecimal("ASIGNACION"),
        Deduccion = reader.SafeGetDecimal("DEDUCCION"),
        Status = reader.SafeGetString("STATUS"),
        DescripcionStatus = reader.SafeGetString("DESCRIPCION_STATUS"),
        CodigoPersona = reader.SafeGetInt32("CODIGO_PERSONA"),
        FechaIngreso = reader.IsDBNull(reader.GetOrdinal("FECHA_INGRESO")) ? default : Convert.ToDateTime(reader["FECHA_INGRESO"], CultureInfo.InvariantCulture),
        CargoCodigo = reader.SafeGetString("CARGO_CODIGO"),
        Banco = reader.SafeGetString("BANCO"),
        CodigoConcepto = reader.SafeGetString("CODIGO_CONCEPTO"),
        Modulo = reader.SafeGetString("MODULO"),
        CodigoIdentificador = reader.SafeGetString("CODIGO_IDENTIFICADOR"),
        CodigoEmpresa = reader.SafeGetInt32("CODIGO_EMPRESA"),
        Descripcion = reader.SafeGetString("DESCRIPCION"),
        Periodo = reader.SafeGetInt32("PERIODO"),
        Sueldo = reader.SafeGetDecimal("SUELDO"),
    };

    internal static List<RhReciboResumen> Summary(IReadOnlyList<RhReciboLinea> rows) => rows
        .GroupBy(row => new { row.CodigoTipoNomina, row.CodigoPeriodo, row.Periodo, row.FechaNomina,
            row.CodigoIcpConcat, row.CodigoIcp, row.Denominacion, row.CodigoPersona, row.Nombre })
        .OrderBy(group => group.Key.Denominacion).ThenBy(group => group.Key.Nombre)
        .Select((group, index) => {
            var r = group.Key;
            return new RhReciboResumen(index + 1, r.CodigoTipoNomina, r.CodigoPeriodo, r.Periodo,
                r.FechaNomina, r.FechaNomina.ToString("dd/MM/yyyy"),
                new(r.FechaNomina.ToString("yyyy"), r.FechaNomina.ToString("MM"), r.FechaNomina.ToString("dd")),
                r.CodigoIcpConcat, r.CodigoIcp, r.Denominacion, r.CodigoPersona, r.Nombre,
                r.Periodo == 1 ? "1ra. Quincena" : r.Periodo == 2 ? "2da. Quincena" : $"Periodo {r.Periodo}");
        }).ToList();
}
