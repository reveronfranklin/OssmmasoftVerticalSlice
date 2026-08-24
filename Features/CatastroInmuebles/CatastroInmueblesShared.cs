using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.CatastroInmuebles;

public record CatastroInmuebleResponse(
    long CodigoInmueble,
    string? CodigoCatastro,
    long? CodigoContribuyente,
    string? NombreInmueble,
    string? NumeroInmueble,
    decimal? Area,
    decimal? ValorInmueble,
    decimal? ValorTerreno,
    decimal? ValorConstruccion,
    long? CodigoParcela,
    long? CodigoFicha,
    string? Observacion);

public record CatastroInmuebleDetalleResponse(
    long CodigoInmueble,
    string? CodigoCatastro,
    long? CodigoContribuyente,
    string? NombreInmueble,
    string? NumeroInmueble,
    decimal? Area,
    decimal? ValorInmueble,
    decimal? ValorTerreno,
    decimal? ValorConstruccion,
    long? CodigoParcela,
    long? CodigoFicha,
    string? Observacion,
    long? CodigoDireccion,
    long? EstadoId,
    long? MunicipioId,
    long? ParroquiaId,
    long? SectorId,
    long? ManzanaId,
    long? ParcelaId,
    string? Vialidad,
    string? Vivienda,
    string? NumeroUnidad,
    string? ComplementoDireccion,
    bool EsDireccionPrincipal);

public record CatastroCaracteristicaResponse(long? CodigoPadre, long? CodigoDescripcion, string? Marcado);
public record CatastroDocumentoLegalResponse(long? CodigoDocumento, long? Numero, long? Folio, long? Tomo, long? Protocolo, DateTime? FechaRegistro, decimal? AreaTerreno, decimal? PrecioTerreno);
public record CatastroFolioRealResponse(string? OficinaRegistro, long? Estado, long? Municipio, long? Parroquia, long? NumeroInscripcion, DateTime? Fecha, decimal? ValorAdquisicion, string? AsientoRegistral);
public record CatastroOtroDatoResponse(string? NumeroTramite, DateTime? FechaRecepcion, string? ElaboradoPor, DateTime? FechaElaboracion, long? TipoTramite);
public record CatastroRolResponse(long CodigoRol, long? CodigoContacto, long? RolId, DateTime? FechaInicio, DateTime? FechaFin, long? CodigoContribuyente, long? NacionalidadId);
public record CatastroUsoZonaResponse(long? CodigoUso, string? Zonificacion, bool EsPrincipal, DateTime? FechaRegistro);
public record CatastroMultiusoResponse(long CodigoDireccion, long Tipo, long TipoUnidad, decimal Metros);

public record CatastroInmuebleRelacionadosResponse(
    List<CatastroCaracteristicaResponse> Caracteristicas,
    List<CatastroDocumentoLegalResponse> DocumentosLegales,
    List<CatastroFolioRealResponse> FoliosReales,
    List<CatastroOtroDatoResponse> OtrosDatos,
    List<CatastroRolResponse> Roles,
    List<CatastroUsoZonaResponse> UsosZonificacion,
    List<CatastroMultiusoResponse> Multiusos);

public static class CatastroInmueblesDb
{
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        var value = config["settings:EmpresaConfig"];
        if (string.IsNullOrWhiteSpace(value))
        {
            empresa = 0;
            errorMessage = "Configuración 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(value, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un número válido.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    public static CatastroInmuebleResponse MapInmueble(OracleDataReader reader) => new(
        Convert.ToInt64(reader["CODIGO_INMUEBLE"]),
        GetString(reader, "CODIGO_CATASTRO"),
        GetInt64(reader, "CODIGO_CONTRIBUYENTE"),
        GetString(reader, "NOMBRE_INMUEBLE"),
        GetString(reader, "NUMERO_INMUEBLE"),
        GetDecimal(reader, "AREA"),
        GetDecimal(reader, "VALOR_INMUEBLE"),
        GetDecimal(reader, "VALOR_TERRENO"),
        GetDecimal(reader, "VALOR_CONSTRUCCION"),
        GetInt64(reader, "CODIGO_PARCELA"),
        GetInt64(reader, "CODIGO_FICHA"),
        GetString(reader, "OBSERVACION"));

    public static CatastroInmuebleDetalleResponse MapDetalle(OracleDataReader reader) => new(
        Convert.ToInt64(reader["CODIGO_INMUEBLE"]),
        GetString(reader, "CODIGO_CATASTRO"),
        GetInt64(reader, "CODIGO_CONTRIBUYENTE"),
        GetString(reader, "NOMBRE_INMUEBLE"),
        GetString(reader, "NUMERO_INMUEBLE"),
        GetDecimal(reader, "AREA"),
        GetDecimal(reader, "VALOR_INMUEBLE"),
        GetDecimal(reader, "VALOR_TERRENO"),
        GetDecimal(reader, "VALOR_CONSTRUCCION"),
        GetInt64(reader, "CODIGO_PARCELA"),
        GetInt64(reader, "CODIGO_FICHA"),
        GetString(reader, "OBSERVACION"),
        GetInt64(reader, "CODIGO_DIRECCION"),
        GetInt64(reader, "ESTADO_ID"),
        GetInt64(reader, "MUNICIPIO_ID"),
        GetInt64(reader, "PARROQUIA_ID"),
        GetInt64(reader, "SECTOR_ID"),
        GetInt64(reader, "MANZANA_ID"),
        GetInt64(reader, "PARCELA_ID"),
        GetString(reader, "VIALIDAD"),
        GetString(reader, "VIVIENDA"),
        GetString(reader, "NUMERO_UNIDAD"),
        GetString(reader, "COMPLEMENTO_DIR"),
        GetInt64(reader, "PRINCIPAL") == 1);

    public static int GetIntOutput(OracleParameter parameter) =>
        parameter.Value is null or DBNull ? 0 : Convert.ToInt32(parameter.Value.ToString());

    public static string GetMessage(OracleParameter parameter) =>
        parameter.Value is null or DBNull ? string.Empty : parameter.Value.ToString() ?? string.Empty;

    public static bool IsSuccessMessage(string message) =>
        message.Contains("success", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("suscces", StringComparison.OrdinalIgnoreCase);

    public static string? GetString(OracleDataReader reader, string name) =>
        reader[name] is DBNull ? null : reader[name].ToString();

    public static long? GetInt64(OracleDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt64(reader[name]);

    public static decimal? GetDecimal(OracleDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToDecimal(reader[name]);

    public static DateTime? GetDateTime(OracleDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToDateTime(reader[name]);
}
