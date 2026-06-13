using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntDb
{
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage) =>
        SupportDb.TryGetEmpresa(config, out empresa, out errorMessage);

    public static object DbValue<T>(T? value) => SupportDb.DbValue(value);

    public static object StringDbValue(string? value) => SupportDb.StringDbValue(value);

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD") =>
        SupportDb.GetMessage(parameter, defaultMessage);

    public static int GetIntOutput(OracleParameter parameter) => SupportDb.GetIntOutput(parameter);

    public static bool IsSuccessMessage(string? message) => SupportDb.IsSuccessMessage(message);

    public static DateTime SafeGetDate(IDataReader reader, string columnName) => SupportDb.SafeGetDate(reader, columnName);

    public static DateTime? SafeGetNullableDate(IDataReader reader, string columnName) => SupportDb.SafeGetNullableDate(reader, columnName);

    public static int? SafeGetNullableInt(IDataReader reader, string columnName) => SupportDb.SafeGetNullableInt(reader, columnName);

    public static CntComprobanteResponse MapComprobante(IDataReader reader)
    {
        var totalDebe = reader.SafeGetDecimal("TOTAL_DEBE");
        var totalHaber = reader.SafeGetDecimal("TOTAL_HABER");

        return new CntComprobanteResponse(
            reader.SafeGetInt32("CODIGO_COMPROBANTE"),
            reader.SafeGetInt32("CODIGO_PERIODO"),
            reader.SafeGetString("PERIODO"),
            reader.SafeGetInt32("TIPO_COMPROBANTE_ID"),
            reader.SafeGetString("TIPO_COMPROBANTE"),
            reader.SafeGetString("NUMERO_COMPROBANTE"),
            SafeGetDate(reader, "FECHA_COMPROBANTE"),
            SafeGetNullableInt(reader, "ORIGEN_ID"),
            reader.SafeGetString("ORIGEN"),
            reader.SafeGetString("OBSERVACION"),
            totalDebe,
            totalHaber,
            totalDebe - totalHaber,
            reader.SafeGetInt32("ES_AUTOMATICO") == 1,
            reader.SafeGetInt32("CODIGO_EMPRESA"));
    }

    public static CntDetalleResponse MapDetalle(IDataReader reader)
    {
        var monto = reader.SafeGetDecimal("MONTO");
        var debe = monto < 0 ? Math.Abs(monto) : 0m;
        var haber = monto > 0 ? monto : 0m;

        return new CntDetalleResponse(
            reader.SafeGetInt32("CODIGO_DETALLE_COMPROBANTE"),
            reader.SafeGetInt32("CODIGO_COMPROBANTE"),
            reader.SafeGetInt32("CODIGO_MAYOR"),
            reader.SafeGetString("MAYOR"),
            reader.SafeGetInt32("CODIGO_AUXILIAR"),
            reader.SafeGetString("AUXILIAR"),
            reader.SafeGetString("REFERENCIA1"),
            reader.SafeGetString("REFERENCIA2"),
            reader.SafeGetString("REFERENCIA3"),
            reader.SafeGetString("DESCRIPCION"),
            monto,
            debe,
            haber,
            reader.SafeGetInt32("CODIGO_EMPRESA"));
    }
}

internal static class CntSecurity
{
    public const string ComprobanteView = "contabilidad.comprobantes.ver";
    public const string ComprobanteCreate = "contabilidad.comprobantes.crear";
    public const string ComprobanteEdit = "contabilidad.comprobantes.editar";
    public const string ComprobanteEditAutomatic = "contabilidad.comprobantes.editar_automatico";
    public const string ComprobanteDelete = "contabilidad.comprobantes.eliminar";
    public const string ComprobanteAutomatic = "contabilidad.comprobantes.generar_automatico";
    public const string ComprobanteReorder = "contabilidad.comprobantes.reordenar";
    public const string CatalogView = "contabilidad.catalogos.ver";
    public const string CatalogAdmin = "contabilidad.catalogos.admin";
    public const string ReportView = "contabilidad.reportes.ver";
    public const string ConciliacionView = "contabilidad.conciliacion.ver";
    public const string ConciliacionImport = "contabilidad.conciliacion.importar";
    public const string ConciliacionAdmin = "contabilidad.conciliacion.admin";
    public const string ConciliacionForceClose = "contabilidad.conciliacion.cierre_forzado";
    public const string ConciliacionEditPreclose = "contabilidad.conciliacion.editar_precierre";
    public const string ConciliacionFormatsView = "contabilidad.conciliacion.formatos.ver";
    public const string ConciliacionFormatsEdit = "contabilidad.conciliacion.formatos.editar";
    public const string ConciliacionOcr = "contabilidad.conciliacion.ocr";
    public const string ConciliacionReprocess = "contabilidad.conciliacion.reprocesar";
    public const string CierreView = "contabilidad.cierre.ver";
    public const string CierrePrecierre = "contabilidad.cierre.precierre";
    public const string CierreCierre = "contabilidad.cierre.cierre";
    public const string CierreReverso = "contabilidad.cierre.reverso";

    public static async Task<ResultDto<int>> ResolveAuthenticatedUserAsync(ConnectionDB connectionDB, IConfiguration config, ClaimsPrincipal user, HttpRequest request)
    {
        if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        return await SupportSecurity.ResolveAuthenticatedUserAsync(cn, empresa, user, request);
    }

    public static async Task<ResultDto<string>> ValidateRequestUserAsync(ConnectionDB connectionDB, IConfiguration config, ClaimsPrincipal user, HttpRequest request, int requestUsuarioId)
    {
        var session = await ResolveAuthenticatedUserAsync(connectionDB, config, user, request);
        if (!session.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = session.Message };
        }

        return session.Data == requestUsuarioId
            ? new ResultDto<string>("OK") { IsValid = true, Message = "Success" }
            : SupportSecurity.UserMismatch<string>();
    }

    public static async Task<ResultDto<string>> CheckPermissionAsync(ConnectionDB connectionDB, IConfiguration config, int usuarioId, string permission)
    {
        if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var permissions = await SupportSecurity.GetPermissionsAsync(cn, usuarioId, empresa);
        return permissions.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
            ? new ResultDto<string>("OK") { IsValid = true, Message = "Success" }
            : SupportSecurity.Forbidden<string>(permission);
    }

    public static async Task<ResultDto<string>> CheckAnyPermissionAsync(ConnectionDB connectionDB, IConfiguration config, int usuarioId, params string[] permissions)
    {
        if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var current = await SupportSecurity.GetPermissionsAsync(cn, usuarioId, empresa);
        if (permissions.Any(permission => current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)))
        {
            return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
        }

        return SupportSecurity.Forbidden<string>(string.Join(" o ", permissions));
    }
}
