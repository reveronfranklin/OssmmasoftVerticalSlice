using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OssmmasoftVerticalSlice.Features.Email;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public record ReciboEmailAnalizar(int CodigoTipoNomina, int CodigoPeriodo, int CodigoIcp, List<int> Personas);
public record ReciboEmailConfirmar(string AnalisisId, string Revision, string Solicitud);
public record ReciboEmailConsultar(string LoteId, int Page = 1, int PageSize = 20);
public record ReciboEmailHistorial(int Page = 1, int PageSize = 20);
public record ReciboEmailIdentificar(int ItemId);
public record ReciboEmailReintentar(int ItemId, string Solicitud);
public record ReciboEmailReenviar(int ItemId, string Solicitud, string Motivo, string Revision);
public record ReciboEmailReenvioPreview(int ItemId, string Nombre, string Correo, string Revision);
public record ReciboEmailContacto(int Codigo, string Correo, bool Principal);
public record ReciboEmailDestino(string Correo, string Error);
public record ReciboEmailItem(int ItemId, int CodigoPersona, string Nombre, string Correo, string Estado, string Error, int Intentos);
public record ReciboEmailLote(string LoteId, string Revision, string Estado, string Motivo, int CodigoTipoNomina,
    int CodigoPeriodo, int CodigoIcp, DateTime Creado, DateTime Vence, bool Habilitado, int Total,
    Dictionary<string,int> Conteos, List<ReciboEmailItem> Items, int Page, int PageSize);
public record ReciboEmailLotes(List<ReciboEmailLote> Items, int Total, int Page, int PageSize);
public record ReciboEmailFuente(RhReciboDatos Datos, int Empresa, int Persona, int Nomina, int Periodo, string LogoHash);
internal record RrmItem(int Id, int Persona, string Nombre, string Correo, string Estado, string Error,
    string Fuente, string Huella, string LogoHash, string SolSmtp, int Intentos, byte[]? Pdf = null, string PdfHash = "", string Token = "");
internal record RrmLot(string Id, int Actor, string Revision, string Estado, string Motivo, int Nomina, int Periodo,
    int Icp, DateTime Creado, DateTime Vence, List<RrmItem> Items);

public static class RhReciboEmailRules
{
    public static bool ValidKey(string? value) => EmailManualRules.ValidKey(value) && value == value!.ToLowerInvariant();
    public const string Permission = "nomina.recibos.enviar";
    public const string ResendPermission = "nomina.recibos.reenviar";
    public const int MaxItems = 100;
    public const int MaxPdfBytes = 5 * 1024 * 1024;
    public static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    public static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    public static ReciboEmailDestino ResolveContact(IEnumerable<ReciboEmailContacto> rows)
    {
        var contacts = rows.ToList();
        var primary = contacts.Where(x => x.Principal).ToList();
        var candidates = (primary.Count > 0 ? primary : contacts.Where(x => !string.IsNullOrWhiteSpace(x.Correo))).Select(x => x.Correo.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (candidates.Count == 0) return new("", "Sin correo en Comunicaciones (tipo 1115).");
        if (candidates.Count != 1) return new("", "Varios correos sin principal unico. Revise Comunicaciones.");
        return EmailManualRules.IsAddress(candidates[0]) ? new(candidates[0], "") : new("", "Correo invalido en Comunicaciones.");
    }
    public static bool ValidSource(ReciboEmailFuente value, int empresa, int persona, int nomina, int periodo, int icp = 0) =>
        !value.Datos.EsTemporal && value.Empresa == empresa && value.Persona == persona && value.Nomina == nomina
        && value.Periodo == periodo && value.Datos.Lineas.Count > 0 && value.Datos.Lineas.All(x =>
            x.CodigoEmpresa == empresa && x.CodigoPersona == persona && x.CodigoTipoNomina == nomina
            && x.CodigoPeriodo == periodo && (icp == 0 || x.CodigoIcp == icp));
    public static bool ValidPdf(byte[] value) => value.Length is > 5 and <= MaxPdfBytes && value.AsSpan(0,5).SequenceEqual("%PDF-"u8);
    public static string Serialize(ReciboEmailFuente source)
    {
        var ordered=source.Datos.Lineas.OrderBy(x=>x.CodigoIcpConcat,StringComparer.Ordinal).ThenBy(x=>x.CodigoPersona)
            .ThenBy(x=>x.NumeroConcepto,StringComparer.Ordinal).ThenBy(x=>x.ComplementoConcepto,StringComparer.Ordinal)
            .ThenBy(x=>x.Monto).ThenBy(x=>JsonSerializer.Serialize(x),StringComparer.Ordinal).ToList();
        return JsonSerializer.Serialize(source with { Datos=source.Datos with { Lineas=ordered } });
    }
    public static string Subject(ReciboEmailFuente source)
    {
        var value=$"Recibo de pago - {source.Datos.TipoNomina} - {source.Datos.Lineas[0].FechaNomina:dd/MM/yyyy}";
        value=new string(value.Select(c=>char.IsControl(c)?' ':c).ToArray());
        while(Encoding.UTF8.GetByteCount(value)>EmailManualRules.MaxSubjectBytes) value=value[..^1];
        return value;
    }
    public static string FileName(int persona, int nomina, int periodo) => $"recibo_{persona}_{nomina}_{periodo}.pdf";
    public static string LotState(string state, IEnumerable<string> items)
    {
        if (state is "ANALISIS" or "PAUSADO") return state;
        var list = items.ToList();
        if (list.Any(x => x is "PENDIENTE" or "GENERANDO" or "LISTO" or "EN_COLA" or "ENVIANDO")) return "PROCESANDO";
        return list.Any(x => x is "ERROR" or "INCIERTO" or "OMITIDO") ? "FINALIZADO_INCIDENCIAS" : "FINALIZADO";
    }
}
