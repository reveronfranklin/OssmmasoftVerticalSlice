namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntConciliacionMatchCommand(
    int UsuarioId,
    int CodigoConciliacion,
    int? CodigoDetalleEdoCta = null,
    int? CodigoDetalleLibro = null);

public record CntConciliacionMatchMultiCommand(
    int UsuarioId,
    int CodigoConciliacion,
    List<int> CodigosDetalleEdoCta,
    List<int> CodigosDetalleLibro);

public record CntConciliacionSuggestionGetQuery(
    int UsuarioId,
    int CodigoConciliacion,
    int ToleranciaDias = 0,
    decimal ToleranciaMonto = 0,
    int MaxRows = 100);

public record CntConciliacionSuggestionResponse(
    int CodigoDetalleEdoCta,
    int CodigoDetalleLibro,
    DateTime BancoFecha,
    DateTime LibroFecha,
    string NumeroTransaccion,
    string NumeroDocumento,
    string BancoDescripcion,
    string LibroDescripcion,
    decimal BancoMonto,
    decimal LibroMonto,
    decimal DiferenciaMonto,
    int DiferenciaDias,
    bool MatchMonto,
    bool MatchNumero,
    bool MatchFecha,
    int Score,
    string Motivos,
    int? CodigoEmpresa);

public record CntConciliacionUnmatchCommand(int UsuarioId, int CodigoTmpConciliacion);
