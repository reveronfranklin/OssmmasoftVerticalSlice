namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntLibroBancoGetQuery(
    int UsuarioId,
    int? CodigoBanco = null,
    int? CodigoCuentaBanco = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string Status = "",
    string SearchText = "");

public record CntLibroBancoDetalleGetQuery(
    int UsuarioId,
    int CodigoLibro,
    string Status = "",
    string SearchText = "");

public record CntLibroBancoGenerateCommand(
    int UsuarioId,
    int CodigoCuentaBanco,
    DateTime FechaDesde,
    DateTime FechaHasta);

public record CntLibroBancoGenerateResponse(int CantidadLibros, int CantidadMovimientos);

public record CntLibroBancoResponse(
    int CodigoLibro,
    int CodigoCuentaBanco,
    string NoCuenta,
    int CodigoBanco,
    string Banco,
    DateTime FechaLibro,
    string Status,
    int CantidadMovimientos,
    decimal MontoMovimientos,
    int? CodigoEmpresa);

public record CntLibroBancoDetalleResponse(
    int CodigoDetalleLibro,
    int CodigoLibro,
    int TipoDocumentoId,
    string TipoDocumento,
    int? CodigoCheque,
    int? CodigoIdentificador,
    int? OrigenId,
    string NumeroDocumento,
    string Descripcion,
    decimal Monto,
    string Status,
    int? CodigoEmpresa);
