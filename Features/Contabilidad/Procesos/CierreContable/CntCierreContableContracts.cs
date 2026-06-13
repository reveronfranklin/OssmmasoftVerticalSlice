namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntCierrePeriodoGetQuery(int UsuarioId, int? AnoPeriodo = null, bool SoloPendientes = false, string SearchText = "");

public record CntCierreActionCommand(int UsuarioId, int CodigoPeriodo);

public record CntCierreModificacionesQuery(int UsuarioId, int CodigoPeriodo);

public record CntCierrePeriodoResponse(
    int CodigoPeriodo,
    string NombrePeriodo,
    DateTime FechaDesde,
    DateTime FechaHasta,
    int AnoPeriodo,
    int NumeroPeriodo,
    DateTime? FechaPrecierre,
    int? UsuarioPrecierre,
    DateTime? FechaCierre,
    int? UsuarioCierre,
    string Estado,
    int CantidadTmpSaldos,
    int CantidadTmpAnalitico,
    int CantidadSaldos,
    int CantidadHistAnalitico,
    int CantidadModificaciones,
    int? CodigoEmpresa);

public record CntCierreActionResponse(
    int CodigoPeriodo,
    string Estado,
    string Mensaje,
    int CantidadSaldos,
    int CantidadAnalitico);

public record CntCierreModificacionesResponse(int CodigoPeriodo, int CantidadModificaciones);
