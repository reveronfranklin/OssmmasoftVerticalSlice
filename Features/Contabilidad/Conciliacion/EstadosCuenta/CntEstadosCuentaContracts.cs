namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntEstadosCuentaGetQuery(
    int UsuarioId,
    int? CodigoBanco = null,
    int? CodigoCuentaBanco = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string SearchText = "");

public record CntEstadoCuentaDetalleGetQuery(
    int UsuarioId,
    int CodigoEstadoCuenta,
    string Status = "",
    string SearchText = "");

public record CntEstadoCuentaResponse(
    int CodigoEstadoCuenta,
    int CodigoCuentaBanco,
    string NoCuenta,
    int CodigoBanco,
    string Banco,
    string NumeroEstadoCuenta,
    DateTime FechaDesde,
    DateTime FechaHasta,
    decimal SaldoInicial,
    decimal SaldoFinal,
    int CantidadMovimientos,
    decimal MontoMovimientos,
    int? CodigoEmpresa);

public record CntEstadoCuentaDetalleResponse(
    int CodigoDetalleEdoCta,
    int CodigoEstadoCuenta,
    int? TipoTransaccionId,
    string TipoTransaccion,
    string NumeroTransaccion,
    DateTime FechaTransaccion,
    string Descripcion,
    decimal Monto,
    string Status,
    int? CodigoEmpresa);
