namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntBancoFormatoGetAllQuery(
    int UsuarioId,
    int? CodigoBanco = null,
    int? CodigoCuentaBanco = null,
    string TipoFormato = "",
    bool SoloActivos = true,
    string SearchText = "");

public record CntBancoFormatoSaveCommand(
    int UsuarioId,
    int? CodigoFormato,
    int CodigoBanco,
    int? CodigoCuentaBanco,
    string NombreFormato,
    string TipoFormato,
    string? Delimitador,
    bool TieneEncabezado,
    int FilaInicio,
    string? HojaExcel,
    string? MapeoJson,
    string? ReglasJson,
    bool Activo);

public record CntBancoFormatoDeleteCommand(int UsuarioId, int CodigoFormato);

public record CntBancoFormatoResponse(
    int CodigoFormato,
    int CodigoBanco,
    string Banco,
    int? CodigoCuentaBanco,
    string Cuenta,
    string NombreFormato,
    string TipoFormato,
    string Delimitador,
    bool TieneEncabezado,
    int FilaInicio,
    string HojaExcel,
    string MapeoJson,
    string ReglasJson,
    bool Activo,
    int CodigoEmpresa);

