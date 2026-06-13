namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public record CntCatalogGetAllQuery(string Catalogo, int UsuarioId = 0, string SearchText = "");
public record CntTituloGetAllQuery(int UsuarioId, string SearchText = "");
public record CntTituloSaveCommand(int UsuarioId, int? TituloId, int? TituloFkId, string Titulo, string? Codigo, string? Extra1, string? Extra2, string? Extra3);
public record CntTituloDeleteCommand(int UsuarioId, int TituloId);
public record CntDescriptivaGetAllQuery(int UsuarioId, int? TituloId = null, string SearchText = "");
public record CntDescriptivaSaveCommand(int UsuarioId, int? DescripcionId, int? DescripcionFkId, int TituloId, string Descripcion, string? Codigo, string? Extra1, string? Extra2, string? Extra3);
public record CntDescriptivaDeleteCommand(int UsuarioId, int DescripcionId);
public record CntDescriptivaUsedByQuery(int UsuarioId, int DescripcionId);
public record CntRubroGetAllQuery(int UsuarioId, string SearchText = "");
public record CntRubroSaveCommand(int UsuarioId, int? CodigoRubro, string NumeroRubro, string Denominacion, string? Descripcion, string? Extra1, string? Extra2, string? Extra3);
public record CntRubroDeleteCommand(int UsuarioId, int CodigoRubro);
public record CntBalanceGetAllQuery(int UsuarioId, int? CodigoRubro = null, string SearchText = "");
public record CntBalanceSaveCommand(int UsuarioId, int? CodigoBalance, string NumeroBalance, string Denominacion, string? Descripcion, string? Extra1, string? Extra2, string? Extra3, int? CodigoRubro);
public record CntBalanceDeleteCommand(int UsuarioId, int CodigoBalance);
public record CntMayorGetAllQuery(int UsuarioId, int? CodigoBalance = null, string SearchText = "");
public record CntMayorSaveCommand(int UsuarioId, int? CodigoMayor, string NumeroMayor, string Denominacion, string? Descripcion, int CodigoBalance, string? ColumnaBalance, string? Extra1, string? Extra2, string? Extra3);
public record CntMayorDeleteCommand(int UsuarioId, int CodigoMayor);
public record CntMayorUsedByQuery(int UsuarioId, int CodigoMayor);
public record CntAuxiliarGetAllQuery(int UsuarioId, int? CodigoMayor = null, bool SoloVigentes = false, string SearchText = "");
public record CntAuxiliarSaveCommand(int UsuarioId, int? CodigoAuxiliar, int CodigoMayor, string? Segmento1, string? Segmento2, string? Segmento3, string? Segmento4, string? Segmento5, string? Segmento6, string? Segmento7, string? Segmento8, string? Segmento9, string? Segmento10, string Denominacion, string? Descripcion, string? Extra1, string? Extra2, string? Extra3, DateTime? FechaFinVigencia, int? CodigoProveedor);
public record CntAuxiliarDeleteCommand(int UsuarioId, int CodigoAuxiliar);
public record CntAuxiliarUsedByQuery(int UsuarioId, int CodigoAuxiliar);
public record CntAuxiliarPucGetAllQuery(int UsuarioId, int? CodigoAuxiliar = null, int? CodigoPuc = null, string SearchText = "");
public record CntAuxiliarPucSaveCommand(int UsuarioId, int? CodigoAuxiliarPuc, int CodigoAuxiliar, int CodigoPuc, string? TipoDocumentoId);
public record CntAuxiliarPucDeleteCommand(int UsuarioId, int CodigoAuxiliarPuc);
public record CntPeriodoAdminGetAllQuery(int UsuarioId, int? AnoPeriodo = null, bool SoloAbiertos = false, string SearchText = "");
public record CntPeriodoSaveCommand(int UsuarioId, int? CodigoPeriodo, string NombrePeriodo, DateTime FechaDesde, DateTime FechaHasta, int AnoPeriodo, int NumeroPeriodo, bool Cerrado, string? Extra1, string? Extra2, string? Extra3);
public record CntPeriodoDeleteCommand(int UsuarioId, int CodigoPeriodo);
public record CntPeriodoGenerateYearCommand(int UsuarioId, int AnoPeriodo);
public record CntRelacionDocumentoGetAllQuery(int UsuarioId, int? TipoDocumentoId = null, int? TipoTransaccionId = null, string SearchText = "");
public record CntRelacionDocumentoSaveCommand(int UsuarioId, int? CodigoRelacionDocumento, int TipoDocumentoId, int TipoTransaccionId, string? Extra1, string? Extra2, string? Extra3);
public record CntRelacionDocumentoDeleteCommand(int UsuarioId, int CodigoRelacionDocumento);
public record CntSaldoGetAllQuery(int UsuarioId, int? CodigoPeriodo = null, int? CodigoMayor = null, int? CodigoAuxiliar = null, string SearchText = "");
public record CntSaldoSaveCommand(int UsuarioId, int? CodigoSaldo, int CodigoPeriodo, int CodigoMayor, int CodigoAuxiliar, decimal Debitos, decimal Creditos, string? Extra1, string? Extra2, string? Extra3);
public record CntSaldoDeleteCommand(int UsuarioId, int CodigoSaldo);
public record CntCloneDescriptivasCommand(int UsuarioId, int EmpresaOrigen);
public record CntClonePlanCuentasCommand(int UsuarioId, int EmpresaOrigen);
public record CntPeriodoGetAllQuery(int UsuarioId = 0, bool SoloAbiertos = false, DateTime? Fecha = null);
public record CntMayorSearchQuery(int UsuarioId = 0, string SearchText = "", int PageSize = 20);
public record CntAuxiliarSearchQuery(int UsuarioId = 0, string SearchText = "", int? CodigoMayor = null, int PageSize = 20);
public record CntComprobanteGetAllQuery(int UsuarioId, int PageSize = 10, int PageNumber = 1, string SearchText = "", int? CodigoPeriodo = null, int? TipoComprobanteId = null, int? OrigenId = null, DateTime? FechaDesde = null, DateTime? FechaHasta = null);
public record CntComprobanteGetByIdQuery(int CodigoComprobante, int UsuarioId);
public record CntComprobantePrintQuery(int CodigoComprobante, int UsuarioId);
public record CntComprobanteNumberQuery(int UsuarioId, int CodigoPeriodo, int TipoComprobanteId, DateTime FechaComprobante);
public record CntComprobanteCreateCommand(int UsuarioId, int CodigoPeriodo, int TipoComprobanteId, DateTime FechaComprobante, int? OrigenId, string? Observacion, List<CntDetalleCreateCommand> Detalles);
public record CntComprobanteUpdateCommand(int CodigoComprobante, int UsuarioId, int CodigoPeriodo, int TipoComprobanteId, DateTime FechaComprobante, int? OrigenId, string? Observacion, List<CntDetalleCreateCommand> Detalles);
public record CntComprobanteDeleteCommand(int CodigoComprobante, int UsuarioId);
public record CntComprobanteReorderCommand(int UsuarioId, int CodigoPeriodo, int TipoComprobanteId);
public record CntDetalleGetByComprobanteQuery(int CodigoComprobante, int UsuarioId);
public record CntDetalleCreateCommand(int CodigoMayor, int CodigoAuxiliar, string? Referencia1, string? Referencia2, string? Referencia3, string? Descripcion, decimal Monto);
public record CntDetalleAddCommand(int CodigoComprobante, int UsuarioId, int CodigoMayor, int CodigoAuxiliar, string? Referencia1, string? Referencia2, string? Referencia3, string? Descripcion, decimal Monto);
public record CntDetalleUpdateCommand(int CodigoDetalleComprobante, int UsuarioId, int CodigoMayor, int CodigoAuxiliar, string? Referencia1, string? Referencia2, string? Referencia3, string? Descripcion, decimal Monto);
public record CntDetalleDeleteCommand(int CodigoDetalleComprobante, int UsuarioId);
public record CntAutomaticPreviewCommand(int UsuarioId, int CodigoPeriodo, int TipoComprobanteId, int OrigenId, DateTime FechaDesde, DateTime FechaHasta);
public record CntAutomaticConfirmCommand(int UsuarioId, int CodigoPeriodo, int TipoComprobanteId, int OrigenId, DateTime FechaDesde, DateTime FechaHasta, DateTime FechaComprobante, string? Observacion);
public record CntMayorAnaliticoQuery(int UsuarioId, int PageSize = 10, int PageNumber = 1, string SearchText = "", int? CodigoPeriodo = null, int? CodigoMayor = null, int? CodigoAuxiliar = null, DateTime? FechaDesde = null, DateTime? FechaHasta = null);
public record CntMovimientoAuxiliarQuery(int UsuarioId, int PageSize = 10, int PageNumber = 1, string SearchText = "", int? CodigoPeriodo = null, int? CodigoAuxiliar = null, DateTime? FechaDesde = null, DateTime? FechaHasta = null);
public record CntPermissionQuery(int UsuarioId, string Permission);

public record CntCatalogResponse(int Id, string Codigo, string Descripcion, string Extra1, string Extra2, string Extra3);
public record CntTituloResponse(int TituloId, int? TituloFkId, string Titulo, string Codigo, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa);
public record CntDescriptivaResponse(int DescripcionId, int? DescripcionFkId, int TituloId, string Titulo, string Descripcion, string Codigo, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa);
public record CntRubroResponse(int CodigoRubro, string NumeroRubro, string Denominacion, string Descripcion, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa);
public record CntBalanceResponse(int CodigoBalance, string NumeroBalance, string Denominacion, string Descripcion, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa, int? CodigoRubro, string NumeroRubro, string Rubro);
public record CntMayorAdminResponse(int CodigoMayor, string NumeroMayor, string Denominacion, string Descripcion, int? CodigoBalance, string NumeroBalance, string Balance, int? CodigoRubro, string NumeroRubro, string Rubro, string ColumnaBalance, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa);
public record CntAuxiliarAdminResponse(int CodigoAuxiliar, int CodigoMayor, string NumeroMayor, string Mayor, string Segmento1, string Segmento2, string Segmento3, string Segmento4, string Segmento5, string Segmento6, string Segmento7, string Segmento8, string Segmento9, string Segmento10, string Denominacion, string Descripcion, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa, DateTime? FechaFinVigencia, int? CodigoProveedor, bool Vigente);
public record CntAuxiliarPucResponse(int CodigoAuxiliarPuc, int CodigoAuxiliar, string Auxiliar, int CodigoMayor, string Mayor, int CodigoPuc, string TipoDocumentoId, int? CodigoEmpresa);
public record CntPeriodoAdminResponse(int CodigoPeriodo, string NombrePeriodo, DateTime FechaDesde, DateTime FechaHasta, int AnoPeriodo, int NumeroPeriodo, DateTime? FechaCierre, bool Cerrado, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa);
public record CntRelacionDocumentoResponse(int CodigoRelacionDocumento, int TipoDocumentoId, string TipoDocumentoCodigo, string TipoDocumento, int TipoDocumentoTituloId, int TipoTransaccionId, string TipoTransaccionCodigo, string TipoTransaccion, int TipoTransaccionTituloId, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa);
public record CntSaldoResponse(int CodigoSaldo, int CodigoPeriodo, string Periodo, int CodigoMayor, string Mayor, int CodigoAuxiliar, string Auxiliar, decimal Debitos, decimal Creditos, decimal Monto, string Extra1, string Extra2, string Extra3, int? CodigoEmpresa);
public record CntPeriodoResponse(int CodigoPeriodo, string NombrePeriodo, DateTime FechaDesde, DateTime FechaHasta, int AnoPeriodo, int NumeroPeriodo, bool Cerrado);
public record CntMayorResponse(int CodigoMayor, string NumeroMayor, string Denominacion, string Descripcion, string ColumnaBalance);
public record CntAuxiliarResponse(int CodigoAuxiliar, int CodigoMayor, string Segmento1, string Segmento2, string Denominacion, string Descripcion);
public record CntComprobanteNumberResponse(string NumeroComprobante);
public record CntComprobanteResponse(
    int CodigoComprobante,
    int CodigoPeriodo,
    string Periodo,
    int TipoComprobanteId,
    string TipoComprobante,
    string NumeroComprobante,
    DateTime FechaComprobante,
    int? OrigenId,
    string Origen,
    string Observacion,
    decimal TotalDebe,
    decimal TotalHaber,
    decimal Diferencia,
    bool EsAutomatico,
    int CodigoEmpresa);

public record CntDetalleResponse(
    int CodigoDetalleComprobante,
    int CodigoComprobante,
    int CodigoMayor,
    string Mayor,
    int CodigoAuxiliar,
    string Auxiliar,
    string Referencia1,
    string Referencia2,
    string Referencia3,
    string Descripcion,
    decimal Monto,
    decimal Debe,
    decimal Haber,
    int CodigoEmpresa);

public record CntComprobantePrintResponse(
    CntComprobanteResponse Encabezado,
    List<CntDetalleResponse> Detalles);

public record CntComprobanteReorderResponse(int Cantidad);
public record CntPermissionResponse(bool HasPermission, string Permission);
public record CntCloneDescriptivasResponse(int Titulos, int Descriptivas);
public record CntClonePlanCuentasResponse(int Rubros, int Balances, int Mayores, int Auxiliares, int RelacionesPuc);

public record CntAutomaticLineResponse(
    int Secuencia,
    int CodigoMayor,
    string Mayor,
    int CodigoAuxiliar,
    string Auxiliar,
    string Referencia1,
    string Referencia2,
    string Referencia3,
    string Descripcion,
    decimal Monto,
    decimal Debe,
    decimal Haber);

public record CntAutomaticPreviewResponse(
    int CodigoPeriodo,
    int TipoComprobanteId,
    int OrigenId,
    DateTime FechaDesde,
    DateTime FechaHasta,
    decimal TotalDebe,
    decimal TotalHaber,
    decimal Diferencia,
    List<CntAutomaticLineResponse> Lineas);

public record CntAutomaticConfirmResponse(
    int CodigoComprobante,
    string NumeroComprobante,
    int CantidadLineas,
    decimal TotalDebe,
    decimal TotalHaber,
    decimal Diferencia);

public record CntMayorAnaliticoResponse(
    int? CodigoComprobante,
    int CodigoMayor,
    int CodigoAuxiliar,
    string CodigoCuenta,
    string DenominacionCuenta,
    string NumeroComprobante,
    DateTime? FechaComprobante,
    string Descripcion,
    string Referencia1,
    string Referencia2,
    decimal Monto,
    decimal Debe,
    decimal Haber,
    int CodigoEmpresa);

public record CntMovimientoAuxiliarResponse(
    int? CodigoComprobante,
    int CodigoAuxiliar,
    string NumeroContable,
    string NombreAuxiliar,
    string NumeroComprobante,
    DateTime? FechaComprobante,
    string Descripcion,
    string Referencia1,
    string Referencia2,
    decimal Monto,
    decimal Debe,
    decimal Haber,
    int CodigoEmpresa);
