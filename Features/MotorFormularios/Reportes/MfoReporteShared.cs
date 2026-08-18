namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

// =============================================================================
// DTOs del modo "parametros de reporte" (requerimiento 16, Fase 9).
//
// Igual que en el resto del motor: flags como bool, CodigoEmpresa nunca en un
// request -se resuelve desde settings:EmpresaConfig-, y terminologia de negocio
// en español.
// =============================================================================

/// <summary>
/// Un reporte enlazado a un formulario.
///
/// <c>Registrado</c> no viene de la base: dice si <c>ClaveRegistro</c> existe en
/// la lista blanca de <see cref="MfoRegistroReportes"/>. Se expone a proposito
/// para que la pantalla pueda avisar "este reporte esta configurado pero no
/// habilitado en el backend" en vez de dejar que el usuario descubra el fallo al
/// pulsar Generar. Habilitar uno nuevo requiere despliegue, y eso tiene que ser
/// visible.
/// </summary>
public record MfoReporteResponse(
    int ReporteId,
    int FormularioId,
    string Alias,
    string Clave,
    string Nombre,
    string Descripcion,
    string TipoEjec,
    string ClaveRegistro,
    string TituloReporte,
    string Orientacion,
    int? MaxFilas,
    int? TimeoutSeg,
    int Orden,
    bool Activo,
    string ModoUso,
    bool RegistraEjec,
    int Parametros,
    int Columnas,
    bool Registrado
);

/// <summary>
/// Mapeo de una entrada del formulario a un parametro del reporte.
///
/// <c>ClaveCampo</c> es lo que usa el resolutor para emparejar; <c>CampoId</c>
/// se expone solo para diagnostico. Ver la nota de versionado en
/// SP_MFO_REP_GET_BY_FORM.sql.
/// </summary>
public record MfoRepParamResponse(
    int RepParamId,
    int ReporteId,
    string NombreParam,
    string Origen,
    int CampoId,
    string ClaveCampo,
    string EtiquetaCampo,
    string ValorFijo,
    string ClaveSistema,
    string TipoDato,
    string Formato,
    bool Obligatorio,
    string ValorDefecto,
    int Orden
);

public record MfoRepColumnaResponse(
    int RepColumnaId,
    int ReporteId,
    string NombreCol,
    string Titulo,
    int Orden,
    int AnchoRel,
    string Alineacion,
    string Formato,
    bool Totalizar,
    bool Agrupar,
    bool Visible
);

/// <summary>
/// Todo lo que la pantalla de parametros necesita de una vez: el selector de
/// reportes y, para cada uno, su mapeo y sus columnas.
/// </summary>
public record MfoReporteDetalle(
    List<MfoReporteResponse> Reportes,
    List<MfoRepParamResponse> Parametros,
    List<MfoRepColumnaResponse> Columnas
);

public record MfoRepEjecResponse(
    int RepEjecId,
    int ReporteId,
    string ClaveReporte,
    string Reporte,
    int FormularioId,
    string Alias,
    string Formulario,
    int RespuestaId,
    string Usuario,
    DateTime? FechaInicio,
    int Milisegundos,
    int Filas,
    string Resultado,
    string Mensaje,
    string IpOrigen,
    bool TieneParams
);

/// <summary>
/// Una ejecucion anterior con sus parametros, para poder recargarla en el
/// formulario. <c>ParamsJson</c> es el contenido crudo de PARAMS_CLB.
/// </summary>
public record MfoRepUltimoResponse(
    int RepEjecId,
    int ReporteId,
    string ClaveReporte,
    string Reporte,
    int FormularioId,
    string Alias,
    DateTime? FechaInicio,
    int Milisegundos,
    int Filas,
    string Resultado,
    string Mensaje,
    string ParamsJson
);

// ----------------------------------------------------------------------------
// Requests
// ----------------------------------------------------------------------------

/// <summary>
/// El payload de ejecucion es el mismo que el de una respuesta: pares
/// clave/valor del formulario de parametros. Reutilizar el formato es lo que
/// permite que el renderizador de la Fase 6 sirva sin modificarlo.
/// </summary>
public record MfoReporteEjecutarRequest(
    int ReporteId,
    List<MfoValorRequest> Valores
);

public record MfoReporteUpsertRequest(
    int? ReporteId,
    int FormularioId,
    string Clave,
    string Nombre,
    string? Descripcion = null,
    string TipoEjec = "ENDPOINT",
    string ClaveRegistro = "",
    string? TituloReporte = null,
    string Orientacion = "VERTICAL",
    int? MaxFilas = null,
    int? TimeoutSeg = null,
    int Orden = 10,
    bool Activo = true
);

public record MfoRepParamUpsertRequest(
    int? RepParamId,
    int ReporteId,
    string NombreParam,
    string Origen,
    string? ClaveCampo = null,
    string? ValorFijo = null,
    string? ClaveSistema = null,
    string TipoDato = "TEXTO",
    string? Formato = null,
    bool Obligatorio = false,
    string? ValorDefecto = null,
    int Orden = 10
);

public record MfoRepColumnaUpsertRequest(
    int? RepColumnaId,
    int ReporteId,
    string NombreCol,
    string Titulo,
    int Orden = 10,
    int AnchoRel = 1,
    string Alineacion = "IZQ",
    string? Formato = null,
    bool Totalizar = false,
    bool Agrupar = false,
    bool Visible = true
);

public record MfoRepEjecFilterRequest(
    int? FormularioId = null,
    int? ReporteId = null,
    string? Usuario = null,
    string? Resultado = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int Page = 1,
    int PageSize = 50
);

public record MfoRepUltimosRequest(
    int? ReporteId = null,
    int? FormularioId = null,
    int Cantidad = 10
);
