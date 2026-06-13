using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntBancoFormatosHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntBancoFormatoResponse>>> HandleAsync(CntBancoFormatoGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAnyAsync(
            connectionDB,
            config,
            user,
            request,
            value.UsuarioId,
            CntSecurity.ConciliacionFormatsView,
            CntSecurity.ConciliacionFormatsEdit,
            CntSecurity.ConciliacionImport,
            CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<List<CntBancoFormatoResponse>>(null!) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(@"
                SELECT
                    F.CODIGO_FORMATO,
                    F.CODIGO_BANCO,
                    NVL(B.NOMBRE, TO_CHAR(F.CODIGO_BANCO)) BANCO,
                    F.CODIGO_CUENTA_BANCO,
                    NVL(C.NO_CUENTA, '') CUENTA,
                    F.NOMBRE_FORMATO,
                    F.TIPO_FORMATO,
                    NVL(F.DELIMITADOR, '') DELIMITADOR,
                    F.TIENE_ENCABEZADO,
                    F.FILA_INICIO,
                    NVL(F.HOJA_EXCEL, '') HOJA_EXCEL,
                    F.MAPEO_JSON,
                    F.REGLAS_JSON,
                    F.ACTIVO,
                    F.CODIGO_EMPRESA
                FROM CNT.CNT_BANCO_FORMATO F
                LEFT JOIN SIS.SIS_BANCOS B ON B.CODIGO_BANCO = F.CODIGO_BANCO AND B.CODIGO_EMPRESA = F.CODIGO_EMPRESA
                LEFT JOIN SIS.SIS_CUENTAS_BANCOS C ON C.CODIGO_CUENTA_BANCO = F.CODIGO_CUENTA_BANCO AND C.CODIGO_EMPRESA = F.CODIGO_EMPRESA
                WHERE F.CODIGO_EMPRESA = :p_EMPRESA
                  AND (:p_CODIGO_BANCO IS NULL OR F.CODIGO_BANCO = :p_CODIGO_BANCO)
                  AND (:p_CODIGO_CUENTA_BANCO IS NULL OR F.CODIGO_CUENTA_BANCO = :p_CODIGO_CUENTA_BANCO)
                  AND (:p_TIPO_FORMATO IS NULL OR F.TIPO_FORMATO = :p_TIPO_FORMATO)
                  AND (:p_SOLO_ACTIVOS = 0 OR F.ACTIVO = 1)
                  AND (
                    :p_SEARCH_TEXT IS NULL
                    OR UPPER(F.NOMBRE_FORMATO) LIKE '%' || UPPER(:p_SEARCH_TEXT) || '%'
                    OR UPPER(F.TIPO_FORMATO) LIKE '%' || UPPER(:p_SEARCH_TEXT) || '%'
                  )
                ORDER BY F.ACTIVO DESC, F.NOMBRE_FORMATO", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoBanco);
            cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoCuentaBanco);
            cmd.Parameters.Add("p_TIPO_FORMATO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.TipoFormato);
            cmd.Parameters.Add("p_SOLO_ACTIVOS", OracleDbType.Int32).Value = value.SoloActivos ? 1 : 0;
            cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);

            var list = new List<CntBancoFormatoResponse>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(CntBancoFormatoMapper.Map(reader));
            }

            return new ResultDto<List<CntBancoFormatoResponse>>(list) { Data = list, IsValid = true, Message = "OK" };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntBancoFormatoResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class SaveCntBancoFormatoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "CSV_TXT",
        "XLSX",
        "TEXTO_DELIMITADO",
        "PDF_TEXTO",
        "PDF_OCR",
        "IMAGEN_OCR",
        "TEXTO_LIBRE"
    };

    public async Task<ResultDto<int>> HandleAsync(CntBancoFormatoSaveCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAnyAsync(
            connectionDB,
            config,
            user,
            request,
            value.UsuarioId,
            CntSecurity.ConciliacionFormatsEdit,
            CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = validation.Message };
        }

        var tipoFormato = (value.TipoFormato ?? string.Empty).Trim().ToUpperInvariant();
        if (value.CodigoBanco <= 0 || string.IsNullOrWhiteSpace(value.NombreFormato) || !AllowedFormats.Contains(tipoFormato))
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = "Banco, nombre y tipo de formato valido son requeridos." };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            var isCreate = !value.CodigoFormato.HasValue || value.CodigoFormato.Value <= 0;
            var sql = isCreate ? @"
                INSERT INTO CNT.CNT_BANCO_FORMATO (
                    CODIGO_FORMATO, CODIGO_BANCO, CODIGO_CUENTA_BANCO, NOMBRE_FORMATO,
                    TIPO_FORMATO, DELIMITADOR, TIENE_ENCABEZADO, FILA_INICIO, HOJA_EXCEL,
                    MAPEO_JSON, REGLAS_JSON, ACTIVO, USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
                ) VALUES (
                    CNT.SEQ_CNT_BCO_FORMATO.NEXTVAL, :p_CODIGO_BANCO, :p_CODIGO_CUENTA_BANCO, :p_NOMBRE_FORMATO,
                    :p_TIPO_FORMATO, :p_DELIMITADOR, :p_TIENE_ENCABEZADO, :p_FILA_INICIO, :p_HOJA_EXCEL,
                    :p_MAPEO_JSON, :p_REGLAS_JSON, :p_ACTIVO, :p_USUARIO_ID, SYSDATE, :p_EMPRESA
                ) RETURNING CODIGO_FORMATO INTO :p_CODIGO_OUT" : @"
                UPDATE CNT.CNT_BANCO_FORMATO
                   SET CODIGO_BANCO = :p_CODIGO_BANCO,
                       CODIGO_CUENTA_BANCO = :p_CODIGO_CUENTA_BANCO,
                       NOMBRE_FORMATO = :p_NOMBRE_FORMATO,
                       TIPO_FORMATO = :p_TIPO_FORMATO,
                       DELIMITADOR = :p_DELIMITADOR,
                       TIENE_ENCABEZADO = :p_TIENE_ENCABEZADO,
                       FILA_INICIO = :p_FILA_INICIO,
                       HOJA_EXCEL = :p_HOJA_EXCEL,
                       MAPEO_JSON = :p_MAPEO_JSON,
                       REGLAS_JSON = :p_REGLAS_JSON,
                       ACTIVO = :p_ACTIVO,
                       USUARIO_UPD = :p_USUARIO_ID,
                       FECHA_UPD = SYSDATE
                 WHERE CODIGO_FORMATO = :p_CODIGO_FORMATO
                   AND CODIGO_EMPRESA = :p_EMPRESA";

            using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            AddSaveParameters(cmd, value, tipoFormato, validation.Empresa);

            OracleParameter? output = null;
            if (isCreate)
            {
                output = cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
            }
            else
            {
                cmd.Parameters.Add("p_CODIGO_FORMATO", OracleDbType.Int32).Value = value.CodigoFormato!.Value;
            }

            var rows = await cmd.ExecuteNonQueryAsync();
            var id = isCreate ? CntDb.GetIntOutput(output!) : value.CodigoFormato!.Value;
            var success = isCreate ? id > 0 : rows > 0;

            return new ResultDto<int>(id) { Data = success ? id : 0, IsValid = success, Message = success ? "OK" : "No se actualizo el formato." };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }

    private static void AddSaveParameters(OracleCommand cmd, CntBancoFormatoSaveCommand value, string tipoFormato, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = value.CodigoBanco;
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoCuentaBanco);
        cmd.Parameters.Add("p_NOMBRE_FORMATO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.NombreFormato);
        cmd.Parameters.Add("p_TIPO_FORMATO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(tipoFormato);
        cmd.Parameters.Add("p_DELIMITADOR", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Delimitador);
        cmd.Parameters.Add("p_TIENE_ENCABEZADO", OracleDbType.Int32).Value = value.TieneEncabezado ? 1 : 0;
        cmd.Parameters.Add("p_FILA_INICIO", OracleDbType.Int32).Value = value.FilaInicio <= 0 ? 1 : value.FilaInicio;
        cmd.Parameters.Add("p_HOJA_EXCEL", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.HojaExcel);
        cmd.Parameters.Add("p_MAPEO_JSON", OracleDbType.Clob).Value = CntDb.StringDbValue(value.MapeoJson);
        cmd.Parameters.Add("p_REGLAS_JSON", OracleDbType.Clob).Value = CntDb.StringDbValue(value.ReglasJson);
        cmd.Parameters.Add("p_ACTIVO", OracleDbType.Int32).Value = value.Activo ? 1 : 0;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}

public class DeleteCntBancoFormatoHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntBancoFormatoDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var validation = await CntCatalogAdminSupport.ValidateAnyAsync(
            connectionDB,
            config,
            user,
            request,
            value.UsuarioId,
            CntSecurity.ConciliacionFormatsEdit,
            CntSecurity.ConciliacionAdmin);
        if (!validation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = validation.Message };
        }

        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(@"
                UPDATE CNT.CNT_BANCO_FORMATO
                   SET ACTIVO = 0,
                       USUARIO_UPD = :p_USUARIO_ID,
                       FECHA_UPD = SYSDATE
                 WHERE CODIGO_FORMATO = :p_CODIGO_FORMATO
                   AND CODIGO_EMPRESA = :p_EMPRESA", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_CODIGO_FORMATO", OracleDbType.Int32).Value = value.CodigoFormato;
            cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = validation.Empresa;
            var rows = await cmd.ExecuteNonQueryAsync();

            return new ResultDto<string>("OK") { Data = rows > 0 ? "OK" : null, IsValid = rows > 0, Message = rows > 0 ? "OK" : "No se encontro el formato." };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
