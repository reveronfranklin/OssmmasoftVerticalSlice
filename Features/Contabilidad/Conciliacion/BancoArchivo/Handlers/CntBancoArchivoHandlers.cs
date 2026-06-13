using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntBancoArchivosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntBancoArchivoResponse>>> HandleAsync(CntBancoArchivoGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntBancoArchivoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_BCO_ARC_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntBancoArchivoBinder.AddGetParameters(cmd, value, validation.Empresa);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntBancoArchivoResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntBancoArchivoMapper.Map(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntBancoArchivoResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntBancoArchivoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class CreateCntBancoArchivoControlHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntBancoArchivoControlCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_BCO_ARC_CTL_INS", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntBancoArchivoBinder.AddCreateControlParameters(cmd, value, validation.Empresa);
            var pCodigo = cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = CntDb.GetIntOutput(pCodigo);
            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class CreateCntBancoArchivoDetalleHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntBancoArchivoDetalleCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_BCO_ARC_DET_INS", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntBancoArchivoBinder.AddCreateDetailParameters(cmd, value, validation.Empresa);
            var pCodigo = cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var id = CntDb.GetIntOutput(pCodigo);
            return new ResultDto<int>(id) { Data = isSuccess ? id : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntBancoArchivoDetallesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntBancoArchivoDetalleLineCommand>>> HandleAsync(CntBancoArchivoDetalleGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntBancoArchivoDetalleLineCommand>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(@"
                SELECT det.FECHA_TRANSACCION,
                       det.NUMERO_TRANSACCION,
                       det.TIPO_TRANSACCION_ID,
                       det.TIPO_TRANSACCION,
                       det.DESCRIPCION_TRANSACCION,
                       det.MONTO_TRANSACCION
                  FROM CNT.CNT_BANCO_ARCHIVO det
                  INNER JOIN CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
                     ON ctl.CODIGO_BANCO_ARCHIVO_CONTROL = det.CODIGO_BANCO_ARCHIVO_CONTROL
                    AND ctl.CODIGO_EMPRESA = det.CODIGO_EMPRESA
                 WHERE det.CODIGO_BANCO_ARCHIVO_CONTROL = :p_CODIGO_BANCO_ARCHIVO_CONTROL
                   AND det.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                 ORDER BY det.FECHA_TRANSACCION, det.CODIGO_BANCO_ARCHIVO", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_CODIGO_BANCO_ARCHIVO_CONTROL", OracleDbType.Int32).Value = value.CodigoBancoArchivoControl;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;

            var list = new List<CntBancoArchivoDetalleLineCommand>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CntBancoArchivoDetalleLineCommand(
                    CntDb.SafeGetDate(reader, "FECHA_TRANSACCION"),
                    reader.SafeGetString("NUMERO_TRANSACCION"),
                    reader.SafeGetInt32("TIPO_TRANSACCION_ID"),
                    reader.SafeGetString("TIPO_TRANSACCION"),
                    reader.SafeGetString("DESCRIPCION_TRANSACCION"),
                    reader.SafeGetDecimal("MONTO_TRANSACCION")));
            }

            return new ResultDto<List<CntBancoArchivoDetalleLineCommand>>(list) { Data = list, IsValid = true, Message = "OK" };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntBancoArchivoDetalleLineCommand>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntBancoArchivoPreviewHandler(ConnectionDB connectionDB, IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResultDto<CntBancoArchivoExtractResponse>> HandleAsync(CntBancoArchivoDetalleGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<CntBancoArchivoExtractResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();

            var tracedPreview = await TryReadTraceAsync(cn, value.CodigoBancoArchivoControl, validation.Empresa);
            if (tracedPreview is not null)
            {
                return new ResultDto<CntBancoArchivoExtractResponse>(tracedPreview) { Data = tracedPreview, IsValid = true, Message = "OK" };
            }

            var details = await ReadDetailsAsync(cn, value.CodigoBancoArchivoControl, validation.Empresa);
            var fallback = new CntBancoArchivoExtractResponse(
                "PREVIEW",
                details.Count,
                0,
                details.Count > 0 ? 1m : 0m,
                details,
                []);

            return new ResultDto<CntBancoArchivoExtractResponse>(fallback) { Data = fallback, IsValid = true, Message = "OK" };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntBancoArchivoExtractResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    private static async Task<CntBancoArchivoExtractResponse?> TryReadTraceAsync(OracleConnection cn, int controlId, int empresa)
    {
        try
        {
            using var cmd = new OracleCommand(@"
                SELECT *
                  FROM (
                        SELECT TIPO_FORMATO,
                               CONFIANZA_PROMEDIO,
                               CANTIDAD_ERRORES,
                               TEXTO_ORIGEN,
                               PAGINAS_TEXTO_JSON,
                               DET_ORIG_JSON,
                               DETALLES_JSON,
                               ERRORES_JSON
                          FROM CNT.CNT_BANCO_ARCHIVO_EXTRACCION
                         WHERE CODIGO_BANCO_ARCHIVO_CONTROL = :p_CODIGO_BANCO_ARCHIVO_CONTROL
                           AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                         ORDER BY FECHA_INS DESC, CODIGO_BCO_ARC_EXTRACCION DESC
                       )
                 WHERE ROWNUM = 1", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_CODIGO_BANCO_ARCHIVO_CONTROL", OracleDbType.Int32).Value = controlId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var details = DeserializeList<CntBancoArchivoDetalleLineCommand>(reader.SafeGetString("DETALLES_JSON"));
            if (details.Count == 0)
            {
                details = await ReadDetailsAsync(cn, controlId, empresa);
            }

            var errors = DeserializeList<CntBancoArchivoExtractError>(reader.SafeGetString("ERRORES_JSON"));
            var pages = DeserializeList<CntBancoArchivoExtractPage>(reader.SafeGetString("PAGINAS_TEXTO_JSON"));
            var originalDetails = DeserializeList<CntBancoArchivoDetalleLineCommand>(reader.SafeGetString("DET_ORIG_JSON"));
            var confidence = reader.SafeGetDecimal("CONFIANZA_PROMEDIO");

            return new CntBancoArchivoExtractResponse(
                reader.SafeGetString("TIPO_FORMATO"),
                details.Count,
                reader.SafeGetInt32("CANTIDAD_ERRORES"),
                confidence > 0 ? confidence : (details.Count > 0 ? 1m : 0m),
                details,
                errors,
                reader.SafeGetString("TEXTO_ORIGEN"),
                pages,
                originalDetails.Count > 0 ? originalDetails : details);
        }
        catch (OracleException)
        {
            return null;
        }
    }

    private static async Task<List<CntBancoArchivoDetalleLineCommand>> ReadDetailsAsync(OracleConnection cn, int controlId, int empresa)
    {
        using var cmd = new OracleCommand(@"
            SELECT det.FECHA_TRANSACCION,
                   det.NUMERO_TRANSACCION,
                   det.TIPO_TRANSACCION_ID,
                   det.TIPO_TRANSACCION,
                   det.DESCRIPCION_TRANSACCION,
                   det.MONTO_TRANSACCION
              FROM CNT.CNT_BANCO_ARCHIVO det
              INNER JOIN CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
                 ON ctl.CODIGO_BANCO_ARCHIVO_CONTROL = det.CODIGO_BANCO_ARCHIVO_CONTROL
                AND ctl.CODIGO_EMPRESA = det.CODIGO_EMPRESA
             WHERE det.CODIGO_BANCO_ARCHIVO_CONTROL = :p_CODIGO_BANCO_ARCHIVO_CONTROL
               AND det.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
             ORDER BY det.FECHA_TRANSACCION, det.CODIGO_BANCO_ARCHIVO", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_CODIGO_BANCO_ARCHIVO_CONTROL", OracleDbType.Int32).Value = controlId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var list = new List<CntBancoArchivoDetalleLineCommand>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CntBancoArchivoDetalleLineCommand(
                CntDb.SafeGetDate(reader, "FECHA_TRANSACCION"),
                reader.SafeGetString("NUMERO_TRANSACCION"),
                reader.SafeGetInt32("TIPO_TRANSACCION_ID"),
                reader.SafeGetString("TIPO_TRANSACCION"),
                reader.SafeGetString("DESCRIPCION_TRANSACCION"),
                reader.SafeGetDecimal("MONTO_TRANSACCION"),
                1m,
                []));
        }

        return list;
    }

    private static List<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public class GetCntBancoArchivoTraceHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntBancoArchivoTraceResponse>>> HandleAsync(CntBancoArchivoTraceGetQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntBancoArchivoTraceResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(@"
                SELECT ctl.CODIGO_BANCO_ARCHIVO_CONTROL,
                       ctl.NOMBRE_ARCHIVO,
                       ban.NOMBRE BANCO,
                       cta.NO_CUENTA,
                       NVL(ext.TIPO_FORMATO, 'SIN_TRAZA') TIPO_FORMATO,
                       NVL(ext.ESTADO_EXTRACCION, 'SIN_TRAZA') ESTADO_EXTRACCION,
                       NVL(ext.CONFIANZA_PROMEDIO, 0) CONFIANZA_PROMEDIO,
                       NVL(ext.CANTIDAD_ERRORES, 0) CANTIDAD_ERRORES,
                       NVL(ext.CANTIDAD_CAMBIOS, 0) CANTIDAD_CAMBIOS,
                       COUNT(det.CODIGO_BANCO_ARCHIVO) CANTIDAD_MOVIMIENTOS,
                       NVL(ext.FECHA_INS, ctl.FECHA_INS) FECHA_EXTRACCION,
                       ext.USUARIO_EXTRAE,
                       ext.USUARIO_CORRIGE,
                       ext.USUARIO_CONFIRMA,
                       ext.FECHA_CONFIRMA,
                       CASE WHEN ctl.CODIGO_ESTADO_CUENTA IS NULL THEN 0 ELSE 1 END CONFIRMADO
                  FROM CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
                  INNER JOIN CNT.CNT_CUENTAS_BANCO cta
                     ON cta.CODIGO_CUENTA_BANCO = ctl.CODIGO_CUENTA_BANCO
                    AND cta.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
                  INNER JOIN CNT.CNT_BANCOS ban
                     ON ban.CODIGO_BANCO = cta.CODIGO_BANCO
                    AND ban.CODIGO_EMPRESA = cta.CODIGO_EMPRESA
                  LEFT JOIN CNT.CNT_BANCO_ARCHIVO det
                     ON det.CODIGO_BANCO_ARCHIVO_CONTROL = ctl.CODIGO_BANCO_ARCHIVO_CONTROL
                    AND det.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
                  LEFT JOIN CNT.CNT_BANCO_ARCHIVO_EXTRACCION ext
                     ON ext.CODIGO_BANCO_ARCHIVO_CONTROL = ctl.CODIGO_BANCO_ARCHIVO_CONTROL
                    AND ext.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
                 WHERE ctl.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                   AND (:p_CODIGO_BANCO IS NULL OR ban.CODIGO_BANCO = :p_CODIGO_BANCO)
                   AND (:p_CODIGO_CUENTA_BANCO IS NULL OR ctl.CODIGO_CUENTA_BANCO = :p_CODIGO_CUENTA_BANCO)
                   AND (:p_SEARCH_TEXT IS NULL
                        OR UPPER(ctl.NOMBRE_ARCHIVO) LIKE '%' || UPPER(:p_SEARCH_TEXT) || '%'
                        OR UPPER(ban.NOMBRE) LIKE '%' || UPPER(:p_SEARCH_TEXT) || '%'
                        OR UPPER(cta.NO_CUENTA) LIKE '%' || UPPER(:p_SEARCH_TEXT) || '%')
                 GROUP BY ctl.CODIGO_BANCO_ARCHIVO_CONTROL,
                          ctl.NOMBRE_ARCHIVO,
                          ban.NOMBRE,
                          cta.NO_CUENTA,
                          ext.TIPO_FORMATO,
                          ext.ESTADO_EXTRACCION,
                          ext.CONFIANZA_PROMEDIO,
                          ext.CANTIDAD_ERRORES,
                          ext.CANTIDAD_CAMBIOS,
                          ext.FECHA_INS,
                          ext.USUARIO_EXTRAE,
                          ext.USUARIO_CORRIGE,
                          ext.USUARIO_CONFIRMA,
                          ext.FECHA_CONFIRMA,
                          ctl.FECHA_INS,
                          ctl.CODIGO_ESTADO_CUENTA
                HAVING (:p_SOLO_CON_ERRORES = 0 OR NVL(ext.CANTIDAD_ERRORES, 0) > 0 OR NVL(ext.CONFIANZA_PROMEDIO, 1) < 0.75)
                 ORDER BY FECHA_EXTRACCION DESC", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoBanco);
            cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoCuentaBanco);
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_SOLO_CON_ERRORES", OracleDbType.Int32).Value = value.SoloConErrores ? 1 : 0;

            var list = new List<CntBancoArchivoTraceResponse>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CntBancoArchivoTraceResponse(
                    reader.SafeGetInt32("CODIGO_BANCO_ARCHIVO_CONTROL"),
                    reader.SafeGetString("NOMBRE_ARCHIVO"),
                    reader.SafeGetString("BANCO"),
                    reader.SafeGetString("NO_CUENTA"),
                    reader.SafeGetString("TIPO_FORMATO"),
                    reader.SafeGetString("ESTADO_EXTRACCION"),
                    reader.SafeGetDecimal("CONFIANZA_PROMEDIO"),
                    reader.SafeGetInt32("CANTIDAD_ERRORES"),
                    reader.SafeGetInt32("CANTIDAD_CAMBIOS"),
                    reader.SafeGetInt32("CANTIDAD_MOVIMIENTOS"),
                    CntDb.SafeGetDate(reader, "FECHA_EXTRACCION"),
                    CntDb.SafeGetNullableInt(reader, "USUARIO_EXTRAE"),
                    CntDb.SafeGetNullableInt(reader, "USUARIO_CORRIGE"),
                    CntDb.SafeGetNullableInt(reader, "USUARIO_CONFIRMA"),
                    CntDb.SafeGetNullableDate(reader, "FECHA_CONFIRMA"),
                    reader.SafeGetInt32("CONFIRMADO") == 1));
            }

            return new ResultDto<List<CntBancoArchivoTraceResponse>>(list) { Data = list, IsValid = true, Message = "OK" };
        }
        catch (OracleException ex) when (ex.Number is 904 or 942)
        {
            return new ResultDto<List<CntBancoArchivoTraceResponse>>([]) { Data = [], IsValid = true, Message = "Traza no instalada o pendiente de migracion." };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntBancoArchivoTraceResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class ExtractCntBancoArchivoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntBancoArchivoExtractResponse>> HandleAsync(CntBancoArchivoExtractCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<CntBancoArchivoExtractResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            var formatConfig = value.CodigoFormato.HasValue && value.CodigoFormato.Value > 0
                ? await GetFormatConfigAsync(value.CodigoFormato.Value, validation.Empresa)
                : null;
            var result = CntBancoArchivoExtractEngine.Extract(value, formatConfig);
            var isSuccess = result.CantidadLineas > 0;
            var message = isSuccess
                ? $"OK. {result.CantidadLineas} movimientos extraidos."
                : "No se pudieron extraer movimientos validos.";

            return new ResultDto<CntBancoArchivoExtractResponse>(result)
            {
                Data = result,
                IsValid = isSuccess,
                Message = message
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntBancoArchivoExtractResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    private async Task<CntBancoArchivoFormatConfig?> GetFormatConfigAsync(int codigoFormato, int empresa)
    {
        using var cn = connectionDB.GetCntConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(@"
            SELECT TIPO_FORMATO,
                   DELIMITADOR,
                   TIENE_ENCABEZADO,
                   FILA_INICIO,
                   HOJA_EXCEL,
                   MAPEO_JSON
              FROM CNT.CNT_BANCO_FORMATO
             WHERE CODIGO_FORMATO = :p_CODIGO_FORMATO
               AND CODIGO_EMPRESA = :p_EMPRESA
               AND ACTIVO = 1", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_CODIGO_FORMATO", OracleDbType.Int32).Value = codigoFormato;
        cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Configuracion de formato no encontrada o inactiva.");
        }

        return CntBancoArchivoFormatConfig.FromDatabase(
            reader.SafeGetString("TIPO_FORMATO"),
            reader.SafeGetString("DELIMITADOR"),
            reader.SafeGetInt32("TIENE_ENCABEZADO") == 1,
            reader.SafeGetInt32("FILA_INICIO"),
            reader.SafeGetString("HOJA_EXCEL"),
            reader["MAPEO_JSON"]?.ToString());
    }
}

public class CreateCntBancoArchivoBatchHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntBancoArchivoBatchCreateResponse>> HandleAsync(CntBancoArchivoBatchCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<CntBancoArchivoBatchCreateResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        if (value.Detalles is null || value.Detalles.Count == 0)
        {
            return new ResultDto<CntBancoArchivoBatchCreateResponse>(null!) { Data = null, IsValid = false, Message = "Debe cargar al menos un movimiento bancario." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();

            var duplicateMessage = await FindHistoricDuplicatesAsync(cn, value, validation.Empresa);
            if (!string.IsNullOrWhiteSpace(duplicateMessage))
            {
                return new ResultDto<CntBancoArchivoBatchCreateResponse>(null!) { Data = null, IsValid = false, Message = duplicateMessage };
            }

            using var transaction = cn.BeginTransaction();

            try
            {
                using var controlCmd = new OracleCommand("CNT.SP_CNT_BCO_ARC_CTL_INS", cn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true,
                    Transaction = transaction
                };
                CntBancoArchivoBinder.AddCreateControlParameters(controlCmd, new CntBancoArchivoControlCreateCommand(
                    value.UsuarioId,
                    value.CodigoBanco,
                    value.CodigoCuentaBanco,
                    value.NombreArchivo,
                    value.FechaDesde,
                    value.FechaHasta,
                    value.SaldoInicial,
                    value.SaldoFinal), validation.Empresa);
                var pControl = controlCmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
                var pControlMessage = controlCmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

                await controlCmd.ExecuteNonQueryAsync();

                var controlMessage = CntDb.GetMessage(pControlMessage);
                if (!CntDb.IsSuccessMessage(controlMessage))
                {
                    transaction.Rollback();
                    return new ResultDto<CntBancoArchivoBatchCreateResponse>(null!) { Data = null, IsValid = false, Message = controlMessage };
                }

                var controlId = CntDb.GetIntOutput(pControl);
                await SaveExtractionTraceAsync(cn, transaction, value, controlId, validation.Empresa);
                var count = 0;
                foreach (var detail in value.Detalles)
                {
                    using var detailCmd = new OracleCommand("CNT.SP_CNT_BCO_ARC_DET_INS", cn)
                    {
                        CommandType = CommandType.StoredProcedure,
                        BindByName = true,
                        Transaction = transaction
                    };
                    CntBancoArchivoBinder.AddCreateDetailParameters(detailCmd, new CntBancoArchivoDetalleCreateCommand(
                        value.UsuarioId,
                        controlId,
                        detail.FechaTransaccion,
                        detail.NumeroTransaccion,
                        detail.TipoTransaccionId,
                        detail.TipoTransaccion,
                        detail.DescripcionTransaccion,
                        detail.MontoTransaccion), validation.Empresa);
                    detailCmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
                    var pDetailMessage = detailCmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

                    await detailCmd.ExecuteNonQueryAsync();

                    var detailMessage = CntDb.GetMessage(pDetailMessage);
                    if (!CntDb.IsSuccessMessage(detailMessage))
                    {
                        transaction.Rollback();
                        return new ResultDto<CntBancoArchivoBatchCreateResponse>(null!) { Data = null, IsValid = false, Message = $"Linea {count + 1}: {detailMessage}" };
                    }

                    count++;
                }

                transaction.Commit();
                var result = new CntBancoArchivoBatchCreateResponse(controlId, count);
                return new ResultDto<CntBancoArchivoBatchCreateResponse>(result) { Data = result, IsValid = true, Message = "OK" };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new ResultDto<CntBancoArchivoBatchCreateResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    private static async Task<string?> FindHistoricDuplicatesAsync(OracleConnection cn, CntBancoArchivoBatchCreateCommand value, int empresa)
    {
        using var cmd = new OracleCommand(@"
            SELECT COUNT(1)
              FROM CNT.CNT_BANCO_ARCHIVO det
              INNER JOIN CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
                 ON ctl.CODIGO_BANCO_ARCHIVO_CONTROL = det.CODIGO_BANCO_ARCHIVO_CONTROL
                AND ctl.CODIGO_EMPRESA = det.CODIGO_EMPRESA
             WHERE ctl.CODIGO_CUENTA_BANCO = :p_CODIGO_CUENTA_BANCO
               AND det.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
               AND TRUNC(det.FECHA_TRANSACCION) = TRUNC(:p_FECHA_TRANSACCION)
               AND UPPER(TRIM(det.NUMERO_TRANSACCION)) = UPPER(TRIM(:p_NUMERO_TRANSACCION))
               AND det.MONTO_TRANSACCION = :p_MONTO_TRANSACCION", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = value.CodigoCuentaBanco;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FECHA_TRANSACCION", OracleDbType.Date);
        cmd.Parameters.Add("p_NUMERO_TRANSACCION", OracleDbType.Varchar2);
        cmd.Parameters.Add("p_MONTO_TRANSACCION", OracleDbType.Decimal);

        var duplicates = new List<string>();
        var totalDuplicates = 0;
        foreach (var detail in value.Detalles)
        {
            cmd.Parameters["p_FECHA_TRANSACCION"].Value = detail.FechaTransaccion;
            cmd.Parameters["p_NUMERO_TRANSACCION"].Value = CntDb.StringDbValue(detail.NumeroTransaccion);
            cmd.Parameters["p_MONTO_TRANSACCION"].Value = detail.MontoTransaccion;

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            if (count > 0)
            {
                totalDuplicates++;
                if (duplicates.Count < 10)
                {
                    duplicates.Add($"{detail.FechaTransaccion:yyyy-MM-dd} / {detail.NumeroTransaccion} / {detail.MontoTransaccion:N2}");
                }
            }
        }

        return totalDuplicates == 0
            ? null
            : $"Existen {totalDuplicates} movimiento(s) historico(s) duplicado(s) para la cuenta: {string.Join("; ", duplicates)}.";
    }

    private static async Task SaveExtractionTraceAsync(OracleConnection cn, OracleTransaction transaction, CntBancoArchivoBatchCreateCommand value, int controlId, int empresa)
    {
        using var cmd = new OracleCommand(@"
            INSERT INTO CNT.CNT_BANCO_ARCHIVO_EXTRACCION (
                CODIGO_BCO_ARC_EXTRACCION,
                CODIGO_BANCO_ARCHIVO_CONTROL,
                CODIGO_FORMATO,
                TIPO_FORMATO,
                ESTADO_EXTRACCION,
                CONFIANZA_PROMEDIO,
                CANTIDAD_ERRORES,
                CANTIDAD_CAMBIOS,
                CONTENIDO_BASE64,
                TEXTO_ORIGEN,
                PAGINAS_TEXTO_JSON,
                DET_ORIG_JSON,
                DETALLES_JSON,
                ERRORES_JSON,
                CAMBIOS_JSON,
                USUARIO_EXTRAE,
                USUARIO_CORRIGE,
                USUARIO_INS,
                FECHA_INS,
                CODIGO_EMPRESA
            ) VALUES (
                CNT.SEQ_CNT_BCO_ARC_EXTRACCION.NEXTVAL,
                :p_CODIGO_BANCO_ARCHIVO_CONTROL,
                :p_CODIGO_FORMATO,
                :p_TIPO_FORMATO,
                :p_ESTADO_EXTRACCION,
                :p_CONFIANZA_PROMEDIO,
                :p_CANTIDAD_ERRORES,
                :p_CANTIDAD_CAMBIOS,
                :p_CONTENIDO_BASE64,
                :p_TEXTO_ORIGEN,
                :p_PAGINAS_TEXTO_JSON,
                :p_DET_ORIG_JSON,
                :p_DETALLES_JSON,
                :p_ERRORES_JSON,
                :p_CAMBIOS_JSON,
                :p_USUARIO_EXTRAE,
                :p_USUARIO_CORRIGE,
                :p_USUARIO_ID,
                SYSDATE,
                :p_CODIGO_EMPRESA
            )", cn)
        {
            BindByName = true,
            Transaction = transaction
        };

        var errorsJson = value.Errores is { Count: > 0 }
            ? JsonSerializer.Serialize(value.Errores)
            : null;
        var pagesJson = value.PaginasTexto is { Count: > 0 }
            ? JsonSerializer.Serialize(value.PaginasTexto)
            : null;
        var detailsJson = value.Detalles is { Count: > 0 }
            ? JsonSerializer.Serialize(value.Detalles)
            : null;
        var originalDetails = value.DetallesOriginales is { Count: > 0 }
            ? value.DetallesOriginales
            : value.Detalles;
        var originalDetailsJson = originalDetails is { Count: > 0 }
            ? JsonSerializer.Serialize(originalDetails)
            : null;
        var changes = BuildLineChanges(originalDetails, value.Detalles);
        var changesJson = changes.Count > 0
            ? JsonSerializer.Serialize(changes)
            : null;

        cmd.Parameters.Add("p_CODIGO_BANCO_ARCHIVO_CONTROL", OracleDbType.Int32).Value = controlId;
        cmd.Parameters.Add("p_CODIGO_FORMATO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoFormato);
        cmd.Parameters.Add("p_TIPO_FORMATO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.TipoFormato);
        cmd.Parameters.Add("p_ESTADO_EXTRACCION", OracleDbType.Varchar2).Value = "PREVIEW";
        cmd.Parameters.Add("p_CONFIANZA_PROMEDIO", OracleDbType.Decimal).Value = CntDb.DbValue(value.ConfianzaPromedio);
        cmd.Parameters.Add("p_CANTIDAD_ERRORES", OracleDbType.Int32).Value = value.Errores?.Count ?? 0;
        cmd.Parameters.Add("p_CANTIDAD_CAMBIOS", OracleDbType.Int32).Value = changes.Count;
        cmd.Parameters.Add("p_CONTENIDO_BASE64", OracleDbType.Clob).Value = CntDb.StringDbValue(value.ContenidoBase64);
        cmd.Parameters.Add("p_TEXTO_ORIGEN", OracleDbType.Clob).Value = CntDb.StringDbValue(value.TextoOrigen);
        cmd.Parameters.Add("p_PAGINAS_TEXTO_JSON", OracleDbType.Clob).Value = CntDb.StringDbValue(pagesJson);
        cmd.Parameters.Add("p_DET_ORIG_JSON", OracleDbType.Clob).Value = CntDb.StringDbValue(originalDetailsJson);
        cmd.Parameters.Add("p_DETALLES_JSON", OracleDbType.Clob).Value = CntDb.StringDbValue(detailsJson);
        cmd.Parameters.Add("p_ERRORES_JSON", OracleDbType.Clob).Value = CntDb.StringDbValue(errorsJson);
        cmd.Parameters.Add("p_CAMBIOS_JSON", OracleDbType.Clob).Value = CntDb.StringDbValue(changesJson);
        cmd.Parameters.Add("p_USUARIO_EXTRAE", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_USUARIO_CORRIGE", OracleDbType.Int32).Value = changes.Count > 0 ? value.UsuarioId : DBNull.Value;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        await cmd.ExecuteNonQueryAsync();
    }

    private static List<CntBancoArchivoLineChange> BuildLineChanges(
        List<CntBancoArchivoDetalleLineCommand>? originalDetails,
        List<CntBancoArchivoDetalleLineCommand> finalDetails)
    {
        var changes = new List<CntBancoArchivoLineChange>();
        if (originalDetails is null || originalDetails.Count == 0 || finalDetails.Count == 0)
        {
            return changes;
        }

        var max = Math.Min(originalDetails.Count, finalDetails.Count);
        for (var index = 0; index < max; index++)
        {
            var original = originalDetails[index];
            var final = finalDetails[index];
            AddChange(changes, index + 1, "fechaTransaccion", original.FechaTransaccion.ToString("yyyy-MM-dd"), final.FechaTransaccion.ToString("yyyy-MM-dd"));
            AddChange(changes, index + 1, "numeroTransaccion", original.NumeroTransaccion, final.NumeroTransaccion);
            AddChange(changes, index + 1, "tipoTransaccionId", original.TipoTransaccionId.ToString(), final.TipoTransaccionId.ToString());
            AddChange(changes, index + 1, "tipoTransaccion", original.TipoTransaccion, final.TipoTransaccion);
            AddChange(changes, index + 1, "descripcion", original.DescripcionTransaccion, final.DescripcionTransaccion);
            AddChange(changes, index + 1, "montoTransaccion", original.MontoTransaccion.ToString("0.######", CultureInfo.InvariantCulture), final.MontoTransaccion.ToString("0.######", CultureInfo.InvariantCulture));
        }

        if (originalDetails.Count != finalDetails.Count)
        {
            changes.Add(new CntBancoArchivoLineChange(0, "cantidadFilas", originalDetails.Count.ToString(), finalDetails.Count.ToString()));
        }

        return changes;
    }

    private static void AddChange(List<CntBancoArchivoLineChange> changes, int lineNumber, string field, string? originalValue, string? finalValue)
    {
        var original = (originalValue ?? string.Empty).Trim();
        var final = (finalValue ?? string.Empty).Trim();
        if (!string.Equals(original, final, StringComparison.Ordinal))
        {
            changes.Add(new CntBancoArchivoLineChange(lineNumber, field, original, final));
        }
    }
}

public class ConfirmCntBancoArchivoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntBancoArchivoConfirmResponse>> HandleAsync(CntBancoArchivoConfirmCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAsync(connectionDB, config, user, request, value.UsuarioId, CntSecurity.ConciliacionImport);
        if (!validation.IsValid)
        {
            return new ResultDto<CntBancoArchivoConfirmResponse>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_BCO_ARC_CONFIRM", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            CntBancoArchivoBinder.AddConfirmParameters(cmd, value, validation.Empresa);
            var pEstadoCuenta = cmd.Parameters.Add("p_CODIGO_ESTADO_CUENTA_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var result = new CntBancoArchivoConfirmResponse(CntDb.GetIntOutput(pEstadoCuenta), CntDb.GetIntOutput(pCantidad));
            if (isSuccess)
            {
                await TryMarkTraceConfirmedAsync(cn, value, validation.Empresa);
            }

            return new ResultDto<CntBancoArchivoConfirmResponse>(result) { Data = isSuccess ? result : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntBancoArchivoConfirmResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }

    private static async Task TryMarkTraceConfirmedAsync(OracleConnection cn, CntBancoArchivoConfirmCommand value, int empresa)
    {
        try
        {
            using var cmd = new OracleCommand(@"
                UPDATE CNT.CNT_BANCO_ARCHIVO_EXTRACCION
                   SET ESTADO_EXTRACCION = 'CONFIRMADO',
                       USUARIO_CONFIRMA = :p_USUARIO_ID,
                       FECHA_CONFIRMA = SYSDATE,
                       USUARIO_UPD = :p_USUARIO_ID,
                       FECHA_UPD = SYSDATE
                 WHERE CODIGO_BANCO_ARCHIVO_CONTROL = :p_CODIGO_BANCO_ARCHIVO_CONTROL
                   AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_CODIGO_BANCO_ARCHIVO_CONTROL", OracleDbType.Int32).Value = value.CodigoBancoArchivoControl;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

            await cmd.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (ex.Number is 904 or 942)
        {
            // La confirmacion contable no debe fallar si la traza incremental aun no fue aplicada.
        }
    }
}
