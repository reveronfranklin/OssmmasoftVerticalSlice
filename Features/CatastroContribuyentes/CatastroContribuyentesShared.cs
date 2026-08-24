using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.CatastroContribuyentes;

public record CatastroContribuyenteResponse(long CodigoContribuyente, long IdentificacionId, string NumeroIdentificacion, string NombreRazonSocial, string? ApellidoAcronimo, long EstatusId, DateTime? FechaIngreso);
public record CatastroContribuyenteDireccionResponse(long CodigoDireccion, long? PaisId, long? EstadoId, long? MunicipioId, long? CiudadId, long? ParroquiaId, long? SectorId, string? Vialidad, string? Vivienda, string? NumeroUnidad, string? ComplementoDireccion, bool EsPrincipal);
public record CatastroContribuyenteComunicacionResponse(long CodigoComunicacion, long TipoComunicacionId, string? CodigoArea, string LineaComunicacion, string? Extension, bool EsPrincipal);
public record CatastroContribuyenteDetalleResponse(CatastroContribuyenteResponse Contribuyente, List<CatastroContribuyenteDireccionResponse> Direcciones, List<CatastroContribuyenteComunicacionResponse> Comunicaciones);

public static class CatastroContribuyentesDb
{
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        if (int.TryParse(config["settings:EmpresaConfig"], out empresa))
        {
            errorMessage = string.Empty;
            return true;
        }
        errorMessage = "Configuración 'EmpresaConfig' no encontrada o inválida.";
        return false;
    }

    public static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    public static string? String(OracleDataReader reader, string name) => reader[name] is DBNull ? null : reader[name].ToString();
    public static long? Long(OracleDataReader reader, string name) => reader[name] is DBNull ? null : Convert.ToInt64(reader[name]);
    public static DateTime? Date(OracleDataReader reader, string name) => reader[name] is DBNull ? null : Convert.ToDateTime(reader[name]);
    public static int OutputInt(OracleParameter value) => value.Value is null or DBNull ? 0 : Convert.ToInt32(value.Value.ToString());
    public static string Message(OracleParameter value) => value.Value is null or DBNull ? string.Empty : value.Value.ToString() ?? string.Empty;
    public static bool Success(string message) => message.Contains("success", StringComparison.OrdinalIgnoreCase) || message.Contains("suscces", StringComparison.OrdinalIgnoreCase);

    public static CatastroContribuyenteResponse MapContribuyente(OracleDataReader reader) => new(
        Convert.ToInt64(reader["CODIGO_CONTRIBUYENTE"]),
        Convert.ToInt64(reader["IDENTIFICACION_ID"]),
        String(reader, "NUMERO_IDENTIFICACION") ?? string.Empty,
        String(reader, "NOMBRE_RAZON_SOCIAL") ?? string.Empty,
        String(reader, "APELLIDO_ACRONIMO"),
        Convert.ToInt64(reader["ESTATUS_ID"]),
        Date(reader, "FECHA_INGRESO"));
}
