namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntCuentaBancoGetAllQuery(int UsuarioId, int? CodigoBanco = null, bool SoloConfiguradas = false, string SearchText = "");

public record CntCuentaBancoResponse(
    int CodigoCuentaBanco,
    int CodigoBanco,
    string Banco,
    int? TipoCuentaId,
    string NoCuenta,
    string FormatoMascara,
    int? DenominacionFuncionalId,
    string DenominacionFuncional,
    string Codigo,
    bool Principal,
    bool Recaudadora,
    int? CodigoMayor,
    string Mayor,
    int? CodigoAuxiliar,
    string Auxiliar,
    string SearchText,
    int? CodigoEmpresa);
