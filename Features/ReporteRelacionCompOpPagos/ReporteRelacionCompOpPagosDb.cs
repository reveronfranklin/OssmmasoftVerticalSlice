using System.Data;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.ReporteRelacionCompOpPagos;

public record ReporteRelacionCompOpPagosQuery(
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    List<int>? CodigosProveedores = null,
    string? Usuario = null);

public record CopClave(int CodigoPresupuesto, int CodigoCompromiso, int CodigoIcp, int CodigoPuc);
public record CopOrdenClave(CopClave Compromiso, int CodigoCompromisoOp, int CodigoOrdenPago);
public record CopPago(CopOrdenClave Orden, int CodigoCheque, string NumeroCheque,
    DateTime? FechaCheque, decimal MontoCheque);
public record CopOrden(CopOrdenClave Clave, string NumeroOrdenPago, DateTime? FechaOrdenPago,
    decimal MontoOrdenPago, List<CopPago> Pagos);
public record CopCompromiso(CopClave Clave, string NumeroCompromiso, DateTime? FechaCompromiso,
    int CodigoProveedor, string NombreProveedor, string CedRif, string Motivo, string Partida,
    decimal MontoCompromiso, string Entidad, List<CopOrden> Ordenes);
public record CopReporte(List<CopCompromiso> Items)
{
    public int CantidadCompromisos => Items.Select(i => (i.Clave.CodigoPresupuesto, i.Clave.CodigoCompromiso)).Distinct().Count();
    public int CantidadFilas => Items.Count + Items.Sum(i => i.Ordenes.Count + i.Ordenes.Sum(o => o.Pagos.Count));
    public decimal TotalComprometido => Items.Sum(i => i.MontoCompromiso);
    public decimal TotalOrdenado => Items.Sum(i => i.Ordenes.Sum(o => o.MontoOrdenPago));
    public decimal TotalPagado => Items.Sum(i => i.Ordenes.Sum(o => o.Pagos.Sum(p => p.MontoCheque)));
}

// Publico para verificar las claves y las sumas sin depender de una base Oracle.
public static class CopEnsamblador
{
    public static CopReporte Crear(IEnumerable<CopCompromiso> compromisos,
        IEnumerable<CopOrden> ordenes, IEnumerable<CopPago> pagos)
    {
        var items = compromisos.Select(i => i with { Ordenes = [] }).ToList();
        var porCompromiso = items.ToDictionary(i => i.Clave);
        var porOrden = new Dictionary<CopOrdenClave, CopOrden>();
        foreach (var orden in ordenes)
        {
            if (!porCompromiso.TryGetValue(orden.Clave.Compromiso, out var padre))
                throw new InvalidOperationException("Se encontro una orden sin su compromiso/partida.");
            var nueva = orden with { Pagos = [] };
            porOrden.Add(nueva.Clave, nueva);
            padre.Ordenes.Add(nueva);
        }
        var clavesPago = new HashSet<(CopOrdenClave, int)>();
        foreach (var pago in pagos)
        {
            if (!porOrden.TryGetValue(pago.Orden, out var orden))
                throw new InvalidOperationException("Se encontro un cheque sin su orden/partida.");
            if (!clavesPago.Add((pago.Orden, pago.CodigoCheque)))
                throw new InvalidOperationException("Se encontro un cheque duplicado para la misma orden/partida.");
            orden.Pagos.Add(pago);
        }
        return new CopReporte(items);
    }
}

internal static class ReporteRelacionCompOpPagosDb
{
    public static string? Validar(ReporteRelacionCompOpPagosQuery query)
    {
        if (!query.FechaDesde.HasValue || !query.FechaHasta.HasValue)
            return "Indique la fecha desde y la fecha hasta.";
        if (query.FechaDesde.Value.Date > query.FechaHasta.Value.Date)
            return "La fecha desde no puede ser posterior a la fecha hasta.";
        if (query.FechaHasta.Value.Date == DateTime.MaxValue.Date)
            return "La fecha hasta excede el rango admitido.";
        if (query.CodigosProveedores?.Any(p => p <= 0) == true)
            return "Los codigos de proveedores deben ser mayores que cero.";
        if (Proveedores(query.CodigosProveedores)?.Length > 32767)
            return "La seleccion de proveedores excede el limite admitido.";
        return null;
    }

    public static string? Proveedores(IEnumerable<int>? codigos)
    {
        var valores = codigos?.Distinct().Order().Select(c => c.ToString(CultureInfo.InvariantCulture)).ToList();
        return valores is { Count: > 0 } ? string.Join(',', valores) : null;
    }

    public static bool Empresa(IConfiguration config, out int empresa) =>
        int.TryParse(config["settings:EmpresaConfig"], NumberStyles.Integer, CultureInfo.InvariantCulture, out empresa) && empresa > 0;
    public static object DbValue(string? value) => string.IsNullOrEmpty(value) ? DBNull.Value : value;
    public static bool Exito(OracleParameter message) =>
        message.Value?.ToString()?.Trim().ToLowerInvariant() is "success" or "suscces";
    public static int Int(IDataRecord r, string n) => r.IsDBNull(r.GetOrdinal(n)) ? 0 : Convert.ToInt32(r[n]);
    public static string Str(IDataRecord r, string n) => r.IsDBNull(r.GetOrdinal(n)) ? string.Empty : Convert.ToString(r[n], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    public static decimal Monto(IDataRecord r, string n) => r.IsDBNull(r.GetOrdinal(n)) ? 0m : Convert.ToDecimal(r[n]);
    public static DateTime? Fecha(IDataRecord r, string n) => r.IsDBNull(r.GetOrdinal(n)) ? null : Convert.ToDateTime(r[n]);
    public static CopClave Clave(IDataRecord r) => new(Int(r, "CODIGO_PRESUPUESTO"), Int(r, "CODIGO_COMPROMISO"), Int(r, "CODIGO_ICP"), Int(r, "CODIGO_PUC"));
    public static CopOrdenClave OrdenClave(IDataRecord r) => new(Clave(r), Int(r, "CODIGO_COMPROMISO_OP"), Int(r, "CODIGO_ORDEN_PAGO"));
    public static CopCompromiso Compromiso(IDataRecord r) => new(Clave(r), Str(r, "NUMERO_COMPROMISO"), Fecha(r, "FECHA_COMPROMISO"),
        Int(r, "CODIGO_PROVEEDOR"), Str(r, "NOMBRE_PROVEEDOR"), Str(r, "CED_RIF"), Str(r, "MOTIVO"), Str(r, "PARTIDA"),
        Monto(r, "MONTO_COMPROMISO"), Str(r, "ENTIDAD"), []);
    public static CopOrden Orden(IDataRecord r) => new(OrdenClave(r), Str(r, "NUMERO_ORDEN_PAGO"), Fecha(r, "FECHA_ORDEN_PAGO"), Monto(r, "MONTO_ORDEN_PAGO"), []);
    public static CopPago Pago(IDataRecord r) => new(OrdenClave(r), Int(r, "CODIGO_CHEQUE"), Str(r, "NUMERO_CHEQUE"), Fecha(r, "FECHA_CHEQUE"), Monto(r, "MONTO_CHEQUE"));
}
