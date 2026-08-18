using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

// =============================================================================
// DTOs del Motor de Formularios (requerimiento 16, Fase 4).
//
// Los flags de la base son CHAR(1) con dominio 'S'/'N'. Aqui se exponen como
// bool: el frontend no tiene por que conocer esa convencion de Oracle, y un
// bool no admite el tercer valor invalido que si admite un string.
//
// CodigoEmpresa nunca aparece en un DTO de request: se resuelve en el backend
// desde settings:EmpresaConfig.
// =============================================================================

public record MfoTipoCampoResponse(
    int TipoCampoId,
    string Codigo,
    string Nombre,
    string ColumnaValor,
    bool AdmiteOpciones,
    bool AdmiteMultiple,
    bool EsPresentacion,
    bool AdmiteArchivo,
    string Componente,
    int Orden,
    string Icono,
    bool Activo
);

public record MfoFormularioResponse(
    int FormularioId,
    string Alias,
    string Nombre,
    string Descripcion,
    string Categoria,
    string Estado,
    int VersionPublId,
    string EntidadDestino,
    int MaxRespUsuario,
    bool PermiteBorrador,
    string ModoUso,
    bool RegistraEjec,
    int VersionNumero,
    int Borradores
);

public record MfoVersionResponse(
    int VersionId,
    int Numero,
    string Estado,
    string Notas,
    string HashDef,
    int VersionOrigenId,
    DateTime? FechaPubl,
    string UsuarioPubl,
    DateTime? FechaArch,
    int Campos,
    int Respuestas
);

public record MfoSeccionResponse(
    int SeccionId,
    string Clave,
    string Titulo,
    string Descripcion,
    int Orden,
    int Columnas,
    bool EsPaso,
    bool Repetible,
    int? MinFilas,
    int? MaxFilas,
    bool Colapsable
);

public record MfoCampoResponse(
    int CampoId,
    int SeccionId,
    string Clave,
    string Etiqueta,
    string Ayuda,
    string Placeholder,
    int Orden,
    int Ancho,
    bool Requerido,
    bool SoloLectura,
    string ValorDefecto,
    string OrigenOpciones,
    string CatalogoClave,
    string Mascara,
    string Unidad,
    int TipoCampoId,
    string TipoCodigo,
    string Componente,
    string ColumnaValor,
    bool AdmiteOpciones,
    bool AdmiteMultiple,
    bool EsPresentacion,
    bool AdmiteArchivo
);

public record MfoOpcionResponse(
    int OpcionId,
    int CampoId,
    string ClaveCampo,
    string Valor,
    string Etiqueta,
    int Orden,
    string Grupo,
    bool EsDefecto,
    bool Activo
);

public record MfoReglaResponse(
    int ReglaId,
    int CampoId,
    string ClaveCampo,
    string TipoRegla,
    string Param1,
    string Param2,
    string Mensaje,
    int Orden,
    bool Activo
);

public record MfoCondicionResponse(
    int CondicionId,
    string Accion,
    string DestinoTipo,
    int DestinoId,
    string ClaveDestino,
    int CampoOrigenId,
    string ClaveOrigen,
    string Operador,
    string ValorCompara,
    int Grupo,
    string Conector,
    int Orden
);

public record MfoHallazgoResponse(
    string Severidad,
    string Codigo,
    string Entidad,
    int EntidadId,
    string Clave,
    string Mensaje
);

public record MfoPermisoResponse(
    int PermisoId,
    int FormularioId,
    string RolCodigo,
    string Accion
);

/// <summary>
/// La definicion completa de una version, en un solo objeto anidado.
/// El frontend hace UNA llamada por formulario: seis viajes para armar una sola
/// pantalla serian seis oportunidades de que la definicion llegue incoherente.
/// </summary>
public record MfoDefinicionResponse(
    int VersionId,
    int FormularioId,
    int Numero,
    string Estado,
    string HashDef,
    string Alias,
    string Nombre,
    string Descripcion,
    string Categoria,
    string ModoUso,
    bool RegistraEjec,
    bool PermiteBorrador,
    string EntidadDestino,
    List<MfoSeccionDefinicion> Secciones,
    List<MfoCondicionResponse> Condiciones
);

public record MfoSeccionDefinicion(
    MfoSeccionResponse Seccion,
    List<MfoCampoDefinicion> Campos
);

public record MfoCampoDefinicion(
    MfoCampoResponse Campo,
    List<MfoOpcionResponse> Opciones,
    List<MfoReglaResponse> Reglas
);

// ----------------------------------------------------------------------------
// Requests
// ----------------------------------------------------------------------------

public record MfoFormularioFilterRequest(
    string? SearchText = null,
    string? Estado = null,
    string? ModoUso = null,
    int Page = 1,
    int PageSize = 50
);

public record MfoFormularioCreateRequest(
    string Alias,
    string Nombre,
    string? Descripcion = null,
    string? Categoria = null,
    string? EntidadDestino = null,
    int? MaxRespUsuario = null,
    bool PermiteBorrador = true,
    string ModoUso = "CAPTURA",
    bool RegistraEjec = false
);

public record MfoFormularioUpdateRequest(
    int FormularioId,
    string Nombre,
    string? Descripcion = null,
    string? Categoria = null,
    string? EntidadDestino = null,
    int? MaxRespUsuario = null,
    bool? PermiteBorrador = null,
    string? ModoUso = null,
    bool? RegistraEjec = null
);

public record MfoFormularioEstadoRequest(int FormularioId, string Estado);

public record MfoVersionCreateRequest(int FormularioId, string? Notas = null);

public record MfoVersionCloneRequest(int VersionOrigenId, string? Notas = null);

public record MfoVersionIdRequest(int VersionId);

public record MfoSeccionUpsertRequest(
    int? SeccionId,
    int VersionId,
    string Clave,
    string? Titulo = null,
    string? Descripcion = null,
    int Orden = 10,
    int Columnas = 1,
    bool EsPaso = false,
    bool Repetible = false,
    int? MinFilas = null,
    int? MaxFilas = null,
    bool Colapsable = false
);

public record MfoCampoUpsertRequest(
    int? CampoId,
    int SeccionId,
    string TipoCodigo,
    string Clave,
    string Etiqueta,
    string? Ayuda = null,
    string? Placeholder = null,
    int Orden = 10,
    int Ancho = 12,
    bool Requerido = false,
    bool SoloLectura = false,
    string? ValorDefecto = null,
    string? OrigenOpciones = null,
    string? CatalogoClave = null,
    string? Mascara = null,
    string? Unidad = null
);

public record MfoCampoReorderRequest(int SeccionId, List<int> CamposId);

public record MfoOpcionUpsertRequest(
    int? OpcionId,
    int CampoId,
    string Valor,
    string Etiqueta,
    int Orden = 10,
    string? Grupo = null,
    bool EsDefecto = false,
    bool Activo = true
);

public record MfoReglaUpsertRequest(
    int? ReglaId,
    int CampoId,
    string TipoRegla,
    string? Param1 = null,
    string? Param2 = null,
    string Mensaje = "",
    int Orden = 10,
    bool Activo = true
);

public record MfoCondicionUpsertRequest(
    int? CondicionId,
    int VersionId,
    string Accion,
    string DestinoTipo,
    string ClaveDestino,
    string ClaveOrigen,
    string Operador,
    string? ValorCompara = null,
    int Grupo = 1,
    string Conector = "Y",
    int Orden = 10
);

public record MfoIdRequest(int Id);

public record MfoPermisoSetRequest(int FormularioId, string RolCodigo, List<string> Acciones);
