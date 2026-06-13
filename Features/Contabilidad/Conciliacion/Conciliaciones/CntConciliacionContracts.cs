namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntConciliacionGetAllQuery(int UsuarioId, int? CodigoPeriodo = null, int? CodigoBanco = null, int? CodigoCuentaBanco = null, string Estado = "", string SearchText = "");

public record CntConciliacionGetByIdQuery(int UsuarioId, int CodigoConciliacion);

public record CntConciliacionCreateCommand(int UsuarioId, int CodigoPeriodo, int CodigoCuentaBanco);

public record CntConciliacionPrecloseCommand(int UsuarioId, int CodigoConciliacion);

public record CntConciliacionCloseCommand(int UsuarioId, int CodigoConciliacion, bool ForzarDiferencia = false);

public record CntConciliacionReverseCommand(int UsuarioId, int CodigoConciliacion);

public record CntConciliacionResponse(
    int CodigoConciliacion,
    int CodigoPeriodo,
    string NombrePeriodo,
    int AnoPeriodo,
    int NumeroPeriodo,
    int CodigoCuentaBanco,
    int CodigoBanco,
    string Banco,
    string NoCuenta,
    string DenominacionFuncional,
    decimal SaldoBanco,
    decimal SaldoLibro,
    DateTime? FechaPrecierre,
    DateTime? FechaCierre,
    string Estado,
    int? CodigoEmpresa);
