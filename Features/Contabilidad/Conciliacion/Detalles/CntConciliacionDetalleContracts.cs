namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntConciliacionDetalleGetQuery(int UsuarioId, int CodigoConciliacion, bool SoloPendientes = false, string SearchText = "");

public record CntConciliacionTemporalGetQuery(int UsuarioId, int CodigoConciliacion, string SearchText = "");

public record CntConciliacionBancoMovimientoResponse(
    int CodigoDetalleEdoCta,
    int CodigoEstadoCuenta,
    string NumeroEstadoCuenta,
    int? TipoTransaccionId,
    string TipoTransaccion,
    string NumeroTransaccion,
    DateTime FechaTransaccion,
    string Descripcion,
    decimal Monto,
    string Status,
    int? CodigoTmpConciliacion,
    bool EnTemporal,
    int? CodigoEmpresa);

public record CntConciliacionLibroMovimientoResponse(
    int CodigoDetalleLibro,
    int CodigoLibro,
    DateTime FechaLibro,
    int TipoDocumentoId,
    string TipoDocumento,
    int? CodigoCheque,
    int? CodigoIdentificador,
    int? OrigenId,
    string NumeroDocumento,
    string Descripcion,
    decimal Monto,
    string Status,
    int? CodigoTmpConciliacion,
    bool EnTemporal,
    int? CodigoEmpresa);

public record CntConciliacionTemporalResponse(
    int CodigoTmpConciliacion,
    int CodigoConciliacion,
    int CodigoPeriodo,
    int CodigoCuentaBanco,
    int? CodigoDetalleLibro,
    int? CodigoDetalleEdoCta,
    DateTime Fecha,
    string Numero,
    decimal Monto,
    string Tipo,
    string BancoDescripcion,
    string LibroDescripcion,
    int? CodigoEmpresa);
