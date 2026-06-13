namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntBancoArchivoGetQuery(int UsuarioId, int? CodigoBanco = null, int? CodigoCuentaBanco = null, string SearchText = "");

public record CntBancoArchivoControlCreateCommand(
    int UsuarioId,
    int CodigoBanco,
    int CodigoCuentaBanco,
    string NombreArchivo,
    DateTime FechaDesde,
    DateTime FechaHasta,
    decimal SaldoInicial,
    decimal SaldoFinal);

public record CntBancoArchivoDetalleCreateCommand(
    int UsuarioId,
    int CodigoBancoArchivoControl,
    DateTime FechaTransaccion,
    string NumeroTransaccion,
    int TipoTransaccionId,
    string TipoTransaccion,
    string DescripcionTransaccion,
    decimal MontoTransaccion);

public record CntBancoArchivoDetalleLineCommand(
    DateTime FechaTransaccion,
    string NumeroTransaccion,
    int TipoTransaccionId,
    string TipoTransaccion,
    string DescripcionTransaccion,
    decimal MontoTransaccion,
    decimal? Confianza = null,
    List<string>? Advertencias = null);

public record CntBancoArchivoDetalleGetQuery(
    int UsuarioId,
    int CodigoBancoArchivoControl);

public record CntBancoArchivoTraceGetQuery(
    int UsuarioId,
    int? CodigoBanco = null,
    int? CodigoCuentaBanco = null,
    bool SoloConErrores = false,
    string SearchText = "");

public record CntBancoArchivoTraceResponse(
    int CodigoBancoArchivoControl,
    string NombreArchivo,
    string Banco,
    string NoCuenta,
    string TipoFormato,
    string EstadoExtraccion,
    decimal ConfianzaPromedio,
    int CantidadErrores,
    int CantidadCambios,
    int CantidadMovimientos,
    DateTime FechaExtraccion,
    int? UsuarioExtrae,
    int? UsuarioCorrige,
    int? UsuarioConfirma,
    DateTime? FechaConfirma,
    bool Confirmado);

public record CntBancoArchivoExtractCommand(
    int UsuarioId,
    int? CodigoBanco,
    int? CodigoCuentaBanco,
    int? CodigoFormato,
    string TipoFormato,
    string? NombreArchivo,
    string? ContenidoBase64,
    string? TextoPegado);

public record CntBancoArchivoExtractError(
    int NumeroLinea,
    string Campo,
    string Mensaje,
    string TextoOrigen);

public record CntBancoArchivoExtractPage(
    int NumeroPagina,
    string Texto);

public record CntBancoArchivoLineChange(
    int NumeroLinea,
    string Campo,
    string ValorOriginal,
    string ValorFinal);

public record CntBancoArchivoExtractResponse(
    string TipoFormato,
    int CantidadLineas,
    int CantidadErrores,
    decimal ConfianzaPromedio,
    List<CntBancoArchivoDetalleLineCommand> Lineas,
    List<CntBancoArchivoExtractError> Errores,
    string? TextoExtraido = null,
    List<CntBancoArchivoExtractPage>? PaginasTexto = null,
    List<CntBancoArchivoDetalleLineCommand>? DetallesOriginales = null);

public record CntBancoArchivoBatchCreateCommand(
    int UsuarioId,
    int CodigoBanco,
    int CodigoCuentaBanco,
    int? CodigoFormato,
    string? TipoFormato,
    string NombreArchivo,
    DateTime FechaDesde,
    DateTime FechaHasta,
    decimal SaldoInicial,
    decimal SaldoFinal,
    decimal? ConfianzaPromedio,
    string? ContenidoBase64,
    string? TextoOrigen,
    List<CntBancoArchivoExtractPage>? PaginasTexto,
    List<CntBancoArchivoExtractError>? Errores,
    List<CntBancoArchivoDetalleLineCommand>? DetallesOriginales,
    List<CntBancoArchivoDetalleLineCommand> Detalles);

public record CntBancoArchivoConfirmCommand(int UsuarioId, int CodigoBancoArchivoControl);

public record CntBancoArchivoConfirmResponse(int CodigoEstadoCuenta, int Cantidad);

public record CntBancoArchivoBatchCreateResponse(int CodigoBancoArchivoControl, int Cantidad);

public record CntBancoArchivoResponse(
    int CodigoBancoArchivoControl,
    int CodigoBanco,
    string Banco,
    int CodigoCuentaBanco,
    string NoCuenta,
    string NombreArchivo,
    DateTime FechaDesde,
    DateTime FechaHasta,
    decimal SaldoInicial,
    decimal SaldoFinal,
    int? CodigoEstadoCuenta,
    bool Confirmado,
    int CantidadMovimientos,
    decimal MontoMovimientos,
    int? CodigoEmpresa);
