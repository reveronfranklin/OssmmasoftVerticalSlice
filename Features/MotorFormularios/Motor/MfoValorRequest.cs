namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Un valor tal como lo envia el cliente.
///
/// <c>Valor</c> es SIEMPRE texto, aunque el campo sea numerico o de fecha. La
/// conversion al tipo declarado la hace el backend leyendo
/// <c>MFO_TIPO_CAMPO.COLUMNA_VALOR</c>: si el cliente pudiera decidir el tipo,
/// el EAV tipado dejaria de estar tipado, y ademas JSON no distingue de forma
/// fiable entre un numero, una fecha y el texto que los representa.
///
/// <c>Fila</c> es 0 salvo en secciones repetibles; <c>Orden</c> es 0 salvo en
/// campos multivalor. Nunca nulos: en Oracle una columna nula no participa de
/// una clave unica, y <c>UK_MFO_VALOR_UBIC</c> los necesita con valor.
/// </summary>
public record MfoValorRequest(
    string Clave,
    int Fila = 0,
    int Orden = 0,
    string? Valor = null,
    string? Etiqueta = null
);

/// <summary>
/// Un error de validacion, ubicado en el campo, la fila y la ocurrencia exactos.
/// El frontend tiene que poder pintarlo junto al control que lo produjo, no en
/// un toast generico.
/// </summary>
public record MfoErrorValidacion(
    string Clave,
    int Fila,
    int Orden,
    string Codigo,
    string Mensaje
);

// ----------------------------------------------------------------------------
// Requests de respuestas
// ----------------------------------------------------------------------------

public record MfoRespuestaCreateRequest(
    string Alias,
    string? ClaveIdem = null,
    string? EntidadRef = null,
    string? ClaveRef = null
);

public record MfoRespuestaSaveRequest(
    int RespuestaId,
    List<MfoValorRequest> Valores
);

public record MfoRespuestaSubmitRequest(
    int RespuestaId,
    List<MfoValorRequest> Valores
);

public record MfoRespuestaAnularRequest(int RespuestaId, string Motivo);

public record MfoRespuestaSearchRequest(
    string? Alias = null,
    string? Estado = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string? Usuario = null,
    string? EntidadRef = null,
    string? ClaveRef = null,
    string? ClaveCampo = null,
    string? ValorTexto = null,
    int Page = 1,
    int PageSize = 25
);

public record MfoRespuestaExportRequest(
    string? Alias = null,
    string? Estado = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null
);

// ----------------------------------------------------------------------------
// Responses de respuestas
// ----------------------------------------------------------------------------

public record MfoRespuestaResponse(
    int RespuestaId,
    int VersionId,
    int FormularioId,
    string Alias,
    string Formulario,
    int VersionNumero,
    string Estado,
    string ClaveIdem,
    string EntidadRef,
    string ClaveRef,
    string UsuarioLlena,
    DateTime? FechaInicio,
    DateTime? FechaEnvio,
    string MotivoAnula
);

public record MfoValorResponse(
    int ValorId,
    int CampoId,
    string ClaveCampo,
    int Fila,
    int Orden,
    string ColumnaValor,
    string Valor,
    string EtiquetaVal
);

public record MfoRespuestaDetalle(
    MfoRespuestaResponse Respuesta,
    List<MfoValorResponse> Valores
);

public record MfoRespuestaListItem(
    int RespuestaId,
    int VersionId,
    int FormularioId,
    string Alias,
    string Formulario,
    int VersionNumero,
    string Estado,
    string UsuarioLlena,
    DateTime? FechaInicio,
    DateTime? FechaEnvio,
    string EntidadRef,
    string ClaveRef,
    string MotivoAnula,
    int Valores
);

public record MfoExportItem(
    string Alias,
    int RespuestaId,
    int VersionId,
    int VersionNumero,
    string Estado,
    string UsuarioLlena,
    DateTime? FechaInicio,
    DateTime? FechaEnvio,
    string EntidadRef,
    string ClaveRef,
    string ClaveCampo,
    string Etiqueta,
    int Fila,
    int Orden,
    string TipoCampo,
    string Valor,
    string EtiquetaVal
);

public record MfoCatalogoOpcionResponse(string Valor, string Etiqueta);
