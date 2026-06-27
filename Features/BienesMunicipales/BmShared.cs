using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

public record BmFechaDto(int Year, int Month, int Day);

public record BmIcpResponse(int CodigoIcp, string UnidadTrabajo);

public record BmPlacaResponse(string NumeroPlaca, string Articulo, string SearchText);

public record Bm1FilterRequest(List<BmIcpResponse>? ListIcpSeleccionado, DateTime? FechaDesde, DateTime? FechaHasta);

public record Bm1Response(
    string UnidadTrabajo,
    string CodigoGrupo,
    string CodigoNivel1,
    string CodigoNivel2,
    string NumeroLote,
    int Cantidad,
    string NumeroPlaca,
    decimal ValorActual,
    string Articulo,
    string Especificacion,
    string Servicio,
    string ResponsableBien,
    string SearchText,
    string LinkData,
    int CodigoBien,
    int CodigoMovBien,
    DateTime? FechaMovimiento,
    int Year,
    int Month,
    string NroPlaca
);

public record BmProductMobileRequest(int CodigoBmConteo, int CodigoDirBien);

public record BmProductMobileResponse(
    int Id,
    string Key,
    string Articulo,
    string Descripcion,
    string Responsable,
    string NroPlaca,
    int CodigoDepartamentoResponsable,
    string DescripcionDepartamentoResponsable,
    int CodigoDirBien,
    List<string> Images
);

public record BmDescriptivaByTituloRequest(int DescripcionId, int TituloId);

public record BmDescriptivaByFkRequest(int DescripcionFkId);

public record BmDescriptivaResponse(int Id, int DescripcionId, int TituloId, string Descripcion, string Codigo, string Extra1, string Extra2, string Extra3);

public record BmConteoDetalleResumenResponse(int CodigoBmConteo, int Conteo, int TotalCantidad, int TotalCantidadContada, int TotalDiferencia);

public record BmConteoResponse(
    int CodigoBmConteo,
    string Titulo,
    string Comentario,
    int CodigoPersonaResponsable,
    string NombrePersonaResponsable,
    int ConteoId,
    DateTime? Fecha,
    string FechaString,
    BmFechaDto? FechaObj,
    List<BmConteoDetalleResumenResponse> ResumenConteo,
    string FechaView,
    int TotalCantidad,
    int TotalCantidadContado,
    int TotalDiferencia
);

public record BmConteoUpsertRequest(
    int CodigoBmConteo,
    string Titulo,
    string Comentario,
    int CodigoPersonaResponsable,
    int ConteoId,
    DateTime? Fecha,
    string FechaString,
    BmFechaDto? FechaObj,
    List<BmIcpResponse>? ListIcpSeleccionado
);

public record BmConteoDeleteRequest(int CodigoBmConteo);

public record BmConteoCerrarRequest(int CodigoBmConteo, string Comentario);

public record BmConteoDetalleFilterRequest(int CodigoBmConteo);

public record BmConteoDetalleResponse(
    int CodigoBmConteoDetalle,
    int CodigoBmConteo,
    int Conteo,
    int CodigoIcp,
    string UnidadTrabajo,
    string Comentario,
    string CodigoPlaca,
    int Cantidad,
    int CantidadContada,
    int CantidadContadaOtroConteo,
    int Diferencia,
    string CodigoGrupo,
    string CodigoNivel1,
    string CodigoNivel2,
    string NumeroLote,
    string NumeroPlaca,
    decimal ValorActual,
    string Articulo,
    string Especificacion,
    string Servicio,
    string ResponsableBien,
    DateTime? FechaMovimiento,
    string FechaMovimientoString,
    BmFechaDto? FechaMovimientoObj,
    int CodigoBien,
    int CodigoMovBien,
    DateTime? Fecha,
    string FechaString,
    BmFechaDto? FechaObj,
    bool ReplicarComentario
);

public record BmConteoDetalleUpdateRequest(int CodigoBmConteoDetalle, int CantidadContada, string Comentario, bool ReplicarComentario);

public record BmConteoRecibeItemRequest(
    string Articulo,
    int CodigoDirBien,
    int Id,
    string KeyUbicacionResponsable,
    string NroPlaca,
    string UnidadEjecutora,
    int UbicacionFisica
);

public record BmUbicacionResponsableRequest(int CodigoUsuario);

public record BmUbicacionResponsableResponse(
    int CodigoBmConteo,
    int Conteo,
    string Titulo,
    int CodigoDirBien,
    int CodigoIcp,
    string UnidadEjecutora,
    int CodigoUsuario,
    int CodigoPersona,
    string Login,
    int Cedula,
    string Descripcion,
    string KeyUbicacionResponsable
);

public record BmPlacaCuarentenaResponse(int CodigoPlacaCuarentena, string NumeroPlaca, string Articulo, string SearchText);

public record BmPlacaCuarentenaRequest(int CodigoPlacaCuarentena, string NumeroPlaca);

public record BmBienFilterRequest(string? SearchText, int Page = 1, int PageSize = 25);

public record BmBienByIdRequest(int CodigoBien);

public record BmBienByPlacaRequest(string NumeroPlaca);

public record BmBienResponse(
    int CodigoBien,
    int CodigoArticulo,
    string Articulo,
    string NumeroPlaca,
    string NumeroLote,
    decimal ValorInicial,
    decimal ValorActual,
    DateTime? FechaCompra,
    string FechaCompraString,
    DateTime? FechaFactura,
    string FechaFacturaString,
    string NumeroFactura,
    string NumeroOrdenCompra,
    int CodigoProveedor,
    string Proveedor,
    int OrigenId,
    string Origen,
    int TipoImpuestoId,
    string TipoImpuesto,
    string Especificacion,
    string Servicio,
    string ResponsableBien,
    string UnidadTrabajo,
    string SearchText
);

public record BmBienUpsertRequest(
    int CodigoBien,
    int CodigoArticulo,
    int CodigoProveedor,
    int CodigoOrdenCompra,
    int OrigenId,
    DateTime? FechaFabricacion,
    string NumeroOrdenCompra,
    DateTime? FechaCompra,
    string NumeroPlaca,
    string NumeroLote,
    decimal ValorInicial,
    decimal ValorActual,
    string NumeroFactura,
    DateTime? FechaFactura,
    int TipoImpuestoId,
    int Cantidad = 1,
    int CodigoDirBien = 0,
    int UsuarioId = 0
);

public record BmDetalleBienRequest(int CodigoBien);

public record BmDetalleBienResponse(
    int CodigoDetalleBien,
    int CodigoBien,
    int TipoEspecificacionId,
    string TipoEspecificacion,
    int EspecificacionId,
    string EspecificacionIdDescripcion,
    string Especificacion
);

public record BmDetalleBienUpsertRequest(
    int CodigoDetalleBien,
    int CodigoBien,
    int TipoEspecificacionId,
    int EspecificacionId,
    string Especificacion,
    int UsuarioId = 0
);

public record BmBienFotoByPlacaRequest(string NumeroPlaca);

public record BmBienFotoDeleteRequest(int CodigoBienFoto);

public record BmBienFotoResponse(
    int CodigoBienFoto,
    int CodigoBien,
    string NumeroPlaca,
    string Foto,
    string Titulo,
    string Patch
);

public record BmCatalogFilterRequest(string? SearchText, int Page = 1, int PageSize = 50);

public record BmTituloResponse(int TituloId, int TituloFkId, string Titulo, string Codigo, string Extra1, string Extra2, string Extra3);

public record BmTituloUpsertRequest(int TituloId, int TituloFkId, string Titulo, string Codigo, string Extra1, string Extra2, string Extra3);

public record BmDescriptivaFilterRequest(int TituloId, string? SearchText, int Page = 1, int PageSize = 50);

public record BmDescriptivaUpsertRequest(int DescripcionId, int DescripcionFkId, int TituloId, string Descripcion, string Codigo, string Extra1, string Extra2, string Extra3);

public record BmClasificacionResponse(
    int CodigoClasificacionBien,
    string CodigoGrupo,
    string CodigoNivel1,
    string CodigoNivel2,
    string CodigoNivel3,
    string Denominacion,
    string Descripcion,
    DateTime? FechaIni,
    DateTime? FechaFin
);

public record BmClasificacionUpsertRequest(
    int CodigoClasificacionBien,
    string CodigoGrupo,
    string CodigoNivel1,
    string CodigoNivel2,
    string CodigoNivel3,
    string Denominacion,
    string Descripcion,
    DateTime? FechaIni,
    DateTime? FechaFin
);

public record BmArticuloResponse(
    int CodigoArticulo,
    int CodigoClasificacionBien,
    string Codigo,
    string Denominacion,
    string Descripcion,
    string CodigoGrupo,
    string CodigoNivel1,
    string CodigoNivel2,
    string CodigoNivel3,
    string Clasificacion
);

public record BmArticuloUpsertRequest(
    int CodigoArticulo,
    int CodigoClasificacionBien,
    string Codigo,
    string Denominacion,
    string Descripcion
);

public record BmDetalleArticuloFilterRequest(int CodigoArticulo);

public record BmDetalleArticuloResponse(
    int CodigoDetalleArticulo,
    int CodigoArticulo,
    int TipoEspecificacionId,
    string TipoEspecificacion
);

public record BmDetalleArticuloUpsertRequest(int CodigoDetalleArticulo, int CodigoArticulo, int TipoEspecificacionId);

public record BmUbicacionFilterRequest(string? SearchText, int Page = 1, int PageSize = 50);

public record BmUbicacionByIcpRequest(int CodigoIcp);

public record BmUbicacionHistoricoRequest(int CodigoDirBien);

public record BmUbicacionResponse(
    int CodigoDirBien,
    int CodigoIcp,
    string UnidadEjecutora,
    int PaisId,
    int EstadoId,
    int MunicipioId,
    int CiudadId,
    int ParroquiaId,
    int SectorId,
    int UrbanizacionId,
    int ManzanaId,
    int ParcelaId,
    int VialidadId,
    string Vialidad,
    int TipoViviendaId,
    string Vivienda,
    int TipoNivelId,
    string Nivel,
    int TipoUnidadId,
    string NumeroUnidad,
    string ComplementoDir,
    int TenenciaId,
    int CodigoPostal,
    DateTime? FechaIni,
    DateTime? FechaFin,
    int UnidadTrabajoId,
    string Direccion,
    string SearchText
);

public record BmUbicacionUpsertRequest(
    int CodigoDirBien,
    int CodigoIcp,
    int PaisId,
    int EstadoId,
    int MunicipioId,
    int CiudadId,
    int ParroquiaId,
    int SectorId,
    int UrbanizacionId,
    int ManzanaId,
    int ParcelaId,
    int VialidadId,
    string Vialidad,
    int TipoViviendaId,
    string Vivienda,
    int TipoNivelId,
    string Nivel,
    int TipoUnidadId,
    string NumeroUnidad,
    string ComplementoDir,
    int TenenciaId,
    int CodigoPostal,
    DateTime? FechaIni,
    DateTime? FechaFin,
    int UnidadTrabajoId
);

public record BmUbicacionHistoricoResponse(
    int CodigoHDirBien,
    int CodigoDirBien,
    int CodigoIcp,
    string UnidadEjecutora,
    string Direccion,
    DateTime? FechaIni,
    DateTime? FechaFin,
    DateTime? FechaHIns
);

public record BmMovimientoByBienRequest(int CodigoBien);

public record BmMovimientoResponse(
    int CodigoMovBien,
    int CodigoBien,
    string NumeroPlaca,
    string Articulo,
    string TipoMovimiento,
    string TipoMovimientoDescripcion,
    DateTime? FechaMovimiento,
    string FechaMovimientoString,
    int CodigoDirBien,
    int CodigoIcp,
    string UnidadEjecutora,
    int ConceptoMovId,
    string ConceptoMovimiento,
    int CodigoSolMovBien,
    bool EsMovimientoFinal,
    string Extra1,
    string Extra2,
    string Extra3
);

public record BmMovimientoCreateRequest(
    int CodigoBien,
    string TipoMovimiento,
    DateTime? FechaMovimiento,
    int CodigoDirBien,
    int ConceptoMovId,
    int CodigoSolMovBien,
    string Extra1,
    string Extra2,
    string Extra3
);

public record BmSolicitudMovimientoFilterRequest(
    int Aprobado,
    string? SearchText,
    string? TipoMovimiento = "",
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int CodigoDirBien = 0,
    int Page = 1,
    int PageSize = 50
);

public record BmSolicitudMovimientoCreateRequest(
    int CodigoBien,
    string TipoMovimiento,
    DateTime? FechaMovimiento,
    int CodigoDirBien,
    int ConceptoMovId,
    string NumeroSolicitud,
    int UsuarioSolicita,
    DateTime? FechaIncidencia,
    string NotaIncidencia,
    string Extra1,
    string Extra2,
    string Extra3
);

public record BmSolicitudMovimientoAprobarRequest(int CodigoSolMovBien);

public record BmProcesoMasivoRequest(
    int CodigoIcp,
    int CodigoDirOrigen,
    int CodigoArticulo,
    string PlacasCsv,
    string ResponsableText,
    int CodigoDirDestino,
    int ConceptoMovId,
    DateTime? FechaMovimiento,
    int UsuarioId,
    string Observacion
);

public record BmProcesoMasivoResponse(
    int CodigoProcesoMasivo,
    int CodigoProcesoMasivoDet,
    int CodigoBien,
    string NumeroPlaca,
    string Articulo,
    int CodigoDirOrigen,
    int CodigoIcpOrigen,
    string UnidadOrigen,
    int CodigoDirDestino,
    string UnidadDestino,
    string Estado,
    string Mensaje,
    int CodigoMovBien,
    int TotalProcesados,
    int TotalExitosos,
    int TotalRechazados
);

public record BmSolicitudMovimientoResponse(
    int CodigoSolMovBien,
    int CodigoBien,
    string NumeroPlaca,
    string Articulo,
    string TipoMovimiento,
    string TipoMovimientoDescripcion,
    DateTime? FechaMovimiento,
    string FechaMovimientoString,
    int CodigoDirBien,
    int CodigoIcp,
    string UnidadEjecutora,
    int ConceptoMovId,
    string ConceptoMovimiento,
    string NumeroSolicitud,
    bool Aprobado,
    int UsuarioSolicita,
    DateTime? FechaSolicita,
    string FechaSolicitaString,
    DateTime? FechaIncidencia,
    string NotaIncidencia
);

public record BmReportePlacaRequest(string NumeroPlaca);

public record BmReporteLoteRequest(string NumeroLote);

public record BmReporteUbicacionRequest(int CodigoIcp);

public record BmReporteMovimientoRequest(int CodigoBien);

public record BmReporteMovimientoFiltroRequest(string TipoMovimiento, DateTime? FechaDesde, DateTime? FechaHasta, int CodigoIcp);

public record BmReporteSolicitudRequest(int Aprobado, string TipoMovimiento, DateTime? FechaDesde, DateTime? FechaHasta);

public record BmReporteProcesoMasivoRequest(int CodigoProcesoMasivo, DateTime? FechaDesde, DateTime? FechaHasta);

public record BmReporteConteoRequest(int CodigoBmConteo);

public record BmReporteConteoHistRequest(DateTime? FechaDesde, DateTime? FechaHasta);

public record BmReportePlacaResponse(
    int CodigoBien,
    string NumeroPlaca,
    string Articulo,
    string Especificacion,
    decimal ValorInicial,
    decimal ValorActual,
    int CodigoMovBien,
    string TipoMovimiento,
    DateTime? FechaMovimiento,
    int CodigoDirBien,
    int CodigoIcp,
    string UnidadEjecutora,
    string ResponsableBien,
    string EstadoOperativo
);

public record BmReporteLoteResponse(
    int CodigoBien,
    string NumeroPlaca,
    string NumeroLote,
    string Articulo,
    DateTime? FechaIns,
    DateTime? FechaCompra,
    decimal ValorInicial,
    decimal ValorActual,
    int CodigoIcp,
    string UnidadEjecutora,
    string ResponsableBien
);

public record BmReporteFichaResponse(
    string Seccion,
    string Referencia,
    string Descripcion,
    DateTime? Fecha,
    string Unidad,
    string Observacion
);

public record BmReporteSolicitudResponse(
    int CodigoSolMovBien,
    string NumeroSolicitud,
    string NumeroPlaca,
    string Articulo,
    string TipoMovimiento,
    string TipoMovimientoDescripcion,
    DateTime? FechaMovimiento,
    bool Aprobado,
    string UnidadEjecutora,
    string ConceptoMovimiento,
    string NotaIncidencia
);

public record BmReporteUbicacionResponse(
    int CodigoIcp,
    string UnidadEjecutora,
    int CodigoDirBien,
    string Direccion,
    int TotalBienes,
    decimal ValorTotal
);

public record BmReporteConteoDifResponse(
    int CodigoBmConteo,
    int CodigoBmConteoDetalle,
    int Conteo,
    int CodigoIcp,
    string UnidadTrabajo,
    string NumeroPlaca,
    string Articulo,
    int Cantidad,
    int CantidadContada,
    int Diferencia,
    string Comentario
);

public record BmReporteConteoHistResponse(
    int CodigoBmConteo,
    string Titulo,
    DateTime? Fecha,
    DateTime? FechaCierre,
    int TotalCantidad,
    int TotalCantidadContada,
    int TotalDiferencia,
    string Comentario
);

internal static class BmDb
{
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        empresa = 0;
        errorMessage = string.Empty;
        var empresaString = config["settings:EmpresaConfig"];

        if (string.IsNullOrWhiteSpace(empresaString))
        {
            errorMessage = "Configuracion 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(empresaString, NumberStyles.Integer, CultureInfo.InvariantCulture, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un numero valido.";
            return false;
        }

        return true;
    }

    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    public static object DbValue(DateTime? value)
    {
        return value.HasValue ? value.Value.Date : DBNull.Value;
    }

    public static string GetBmFilesPath(IConfiguration config)
    {
        var configuredPath = config["settings:BmFiles"];
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "BmFiles")
            : configuredPath;
    }

    public static string BuildSafeFileName(int codigoBien, string numeroPlaca, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var placa = new string(numeroPlaca.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(placa))
        {
            placa = "SINPLACA";
        }

        return $"{codigoBien}_{placa}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : Convert.ToInt32(parameter.Value.ToString(), CultureInfo.InvariantCulture);
    }

    public static string ToCsv(IEnumerable<int>? values)
    {
        if (values is null)
        {
            return string.Empty;
        }

        return string.Join(",", values.Where(value => value > 0).Distinct().OrderBy(value => value));
    }

    public static string ToIcpCsv(IEnumerable<BmIcpResponse>? values)
    {
        return ToCsv(values?.Select(value => value.CodigoIcp));
    }

    public static DateTime? GetDate(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static BmFechaDto? ToFechaDto(DateTime? value)
    {
        return value.HasValue ? new BmFechaDto(value.Value.Year, value.Value.Month, value.Value.Day) : null;
    }

    public static string ToDateString(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
    }

    public static ResultDto<List<T>> InvalidList<T>(string message)
    {
        return new ResultDto<List<T>>(new List<T>())
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }

    public static OracleCommand StoredProcedure(string name, OracleConnection cn)
    {
        return new OracleCommand(name, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
    }

    public static async Task<string?> TryOpenAsync(OracleConnection cn, string domain)
    {
        try
        {
            await cn.OpenAsync();
            return null;
        }
        catch (Exception ex)
        {
            return $"Error tecnico al abrir conexion {domain}: {ex.Message}";
        }
    }

    public static async Task<ResultDto<List<T>>> ExecuteListAsync<T>(
        OracleCommand cmd,
        Func<IDataReader, T> map,
        int page = 0)
    {
        var list = new List<T>();
        var pMessage = cmd.Parameters.Contains("p_Message")
            ? cmd.Parameters["p_Message"]
            : cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Contains("p_TotalRecords")
            ? cmd.Parameters["p_TotalRecords"]
            : cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(map(reader));
            }
        }

        var message = GetMessage(pMessage);
        var isSuccess = IsSuccessMessage(message);

        return new ResultDto<List<T>>(list)
        {
            Data = isSuccess ? list : null,
            CantidadRegistros = GetIntOutput(pTotalRecords),
            Page = page,
            IsValid = isSuccess,
            Message = message
        };
    }
}
