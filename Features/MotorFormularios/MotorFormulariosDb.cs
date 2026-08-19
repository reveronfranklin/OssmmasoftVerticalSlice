using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Helpers compartidos por los slices del Motor de Formularios.
/// Sigue el patron de BmDb: conversion a DBNull, apertura de conexion con
/// mensaje propio, lectura de parametros de salida y normalizacion de flags.
/// </summary>
public static class MfoDb
{
    public const string Schema = "MFO.";

    /// <summary>
    /// El backend trata como exito tanto "success" como el legado "suscces".
    /// Los procedimientos nuevos escriben "success"; la tolerancia se mantiene
    /// porque el resto del repositorio convive con las dos formas y una consulta
    /// que cruce procedimientos viejos no debe fallar por una errata historica.
    /// </summary>
    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value || parameter.Value is null
            ? 0
            : Convert.ToInt32(parameter.Value.ToString(), CultureInfo.InvariantCulture);
    }

    public static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    public static object DbValue(int? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    public static object DbValue(DateTime? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    /// <summary>
    /// Los flags de la base son CHAR(1) 'S'/'N'. Un bool nulo del request
    /// significa "no lo cambies", asi que viaja como DBNull y el procedimiento
    /// conserva el valor actual con NVL.
    /// </summary>
    public static object DbFlag(bool? value)
    {
        return value.HasValue ? (value.Value ? "S" : "N") : DBNull.Value;
    }

    public static object DbFlag(bool value)
    {
        return value ? "S" : "N";
    }

    public static bool ToBool(IDataReader reader, string columnName)
    {
        return string.Equals(reader.SafeGetString(columnName), "S", StringComparison.OrdinalIgnoreCase);
    }

    public static int? ToNullableInt(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.SafeGetInt32(columnName);
    }

    public static DateTime? GetDate(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public static OracleCommand StoredProcedure(string name, OracleConnection cn)
    {
        return new OracleCommand(Schema + name, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
    }

    public static async Task<string?> TryOpenAsync(OracleConnection cn)
    {
        try
        {
            await cn.OpenAsync();
            return null;
        }
        catch (Exception ex)
        {
            return $"Error tecnico al abrir conexion MFO: {ex.Message}";
        }
    }

    /// <summary>
    /// El codigo de empresa sale de la configuracion, nunca del request. Es la
    /// regla del repositorio y aqui importa mas que en otros modulos: el motor
    /// expone endpoints genericos, y aceptar la empresa desde el cliente
    /// permitiria consultar los formularios de otra.
    /// </summary>
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        empresa = 0;
        errorMessage = string.Empty;

        var raw = config["settings:EmpresaConfig"];
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out empresa))
        {
            errorMessage = "No se pudo resolver el codigo de empresa desde la configuracion.";
            return false;
        }

        return true;
    }

    public static ResultDto<T> Invalid<T>(string message)
    {
        return new ResultDto<T>(default!) { Data = default, IsValid = false, Message = message };
    }

    public static ResultDto<List<T>> InvalidList<T>(string message)
    {
        return new ResultDto<List<T>>(null!) { Data = null, IsValid = false, Message = message };
    }

    // ------------------------------------------------------------------------
    // Mapeos
    // ------------------------------------------------------------------------

    public static MfoTipoCampoResponse MapTipoCampo(IDataReader r) => new(
        r.SafeGetInt32("TIPO_CAMPO_ID"),
        r.SafeGetString("CODIGO"),
        r.SafeGetString("NOMBRE"),
        r.SafeGetString("COLUMNA_VALOR"),
        ToBool(r, "ADMITE_OPCIONES"),
        ToBool(r, "ADMITE_MULTIPLE"),
        ToBool(r, "ES_PRESENTACION"),
        ToBool(r, "ADMITE_ARCHIVO"),
        r.SafeGetString("COMPONENTE"),
        r.SafeGetInt32("ORDEN"),
        r.SafeGetString("ICONO"),
        ToBool(r, "ACTIVO"));

    public static MfoFormularioResponse MapFormulario(IDataReader r, bool conConteos) => new(
        r.SafeGetInt32("FORMULARIO_ID"),
        r.SafeGetString("ALIAS"),
        r.SafeGetString("NOMBRE"),
        r.SafeGetString("DESCRIPCION"),
        r.SafeGetString("CATEGORIA"),
        r.SafeGetString("ESTADO"),
        r.SafeGetInt32("VERSION_PUBL_ID"),
        r.SafeGetString("ENTIDAD_DESTINO"),
        r.SafeGetInt32("MAX_RESP_USUARIO"),
        ToBool(r, "PERMITE_BORRADOR"),
        r.SafeGetString("MODO_USO"),
        ToBool(r, "REGISTRA_EJEC"),
        conConteos ? r.SafeGetInt32("VERSION_NUMERO") : 0,
        conConteos ? r.SafeGetInt32("BORRADORES") : 0);

    public static MfoVersionResponse MapVersion(IDataReader r) => new(
        r.SafeGetInt32("VERSION_ID"),
        r.SafeGetInt32("NUMERO"),
        r.SafeGetString("ESTADO"),
        r.SafeGetString("NOTAS"),
        r.SafeGetString("HASH_DEF"),
        r.SafeGetInt32("VERSION_ORIGEN_ID"),
        GetDate(r, "FECHA_PUBL"),
        r.SafeGetString("USUARIO_PUBL"),
        GetDate(r, "FECHA_ARCH"),
        r.SafeGetInt32("CAMPOS"),
        r.SafeGetInt32("RESPUESTAS"));

    public static MfoSeccionResponse MapSeccion(IDataReader r) => new(
        r.SafeGetInt32("SECCION_ID"),
        r.SafeGetString("CLAVE"),
        r.SafeGetString("TITULO"),
        r.SafeGetString("DESCRIPCION"),
        r.SafeGetInt32("ORDEN"),
        r.SafeGetInt32("COLUMNAS"),
        ToBool(r, "ES_PASO"),
        ToBool(r, "REPETIBLE"),
        ToNullableInt(r, "MIN_FILAS"),
        ToNullableInt(r, "MAX_FILAS"),
        ToBool(r, "COLAPSABLE"));

    public static MfoCampoResponse MapCampo(IDataReader r) => new(
        r.SafeGetInt32("CAMPO_ID"),
        r.SafeGetInt32("SECCION_ID"),
        r.SafeGetString("CLAVE"),
        r.SafeGetString("ETIQUETA"),
        r.SafeGetString("AYUDA"),
        r.SafeGetString("PLACEHOLDER"),
        r.SafeGetInt32("ORDEN"),
        r.SafeGetInt32("ANCHO"),
        ToBool(r, "REQUERIDO"),
        ToBool(r, "SOLO_LECTURA"),
        r.SafeGetString("VALOR_DEFECTO"),
        r.SafeGetString("ORIGEN_OPCIONES"),
        r.SafeGetString("CATALOGO_CLAVE"),
        r.SafeGetString("MASCARA"),
        r.SafeGetString("UNIDAD"),
        r.SafeGetInt32("TIPO_CAMPO_ID"),
        r.SafeGetString("TIPO_CODIGO"),
        r.SafeGetString("COMPONENTE"),
        r.SafeGetString("COLUMNA_VALOR"),
        ToBool(r, "ADMITE_OPCIONES"),
        ToBool(r, "ADMITE_MULTIPLE"),
        ToBool(r, "ES_PRESENTACION"),
        ToBool(r, "ADMITE_ARCHIVO"));

    public static MfoOpcionResponse MapOpcion(IDataReader r) => new(
        r.SafeGetInt32("OPCION_ID"),
        r.SafeGetInt32("CAMPO_ID"),
        r.SafeGetString("CLAVE_CAMPO"),
        r.SafeGetString("VALOR"),
        r.SafeGetString("ETIQUETA"),
        r.SafeGetInt32("ORDEN"),
        r.SafeGetString("GRUPO"),
        ToBool(r, "ES_DEFECTO"),
        ToBool(r, "ACTIVO"));

    public static MfoReglaResponse MapRegla(IDataReader r) => new(
        r.SafeGetInt32("REGLA_ID"),
        r.SafeGetInt32("CAMPO_ID"),
        r.SafeGetString("CLAVE_CAMPO"),
        r.SafeGetString("TIPO_REGLA"),
        r.SafeGetString("PARAM_1"),
        r.SafeGetString("PARAM_2"),
        r.SafeGetString("MENSAJE"),
        r.SafeGetInt32("ORDEN"),
        ToBool(r, "ACTIVO"));

    public static MfoCondicionResponse MapCondicion(IDataReader r) => new(
        r.SafeGetInt32("CONDICION_ID"),
        r.SafeGetString("ACCION"),
        r.SafeGetString("DESTINO_TIPO"),
        r.SafeGetInt32("DESTINO_ID"),
        r.SafeGetString("CLAVE_DESTINO"),
        r.SafeGetInt32("CAMPO_ORIGEN_ID"),
        r.SafeGetString("CLAVE_ORIGEN"),
        r.SafeGetString("OPERADOR"),
        r.SafeGetString("VALOR_COMPARA"),
        r.SafeGetInt32("GRUPO"),
        r.SafeGetString("CONECTOR"),
        r.SafeGetInt32("ORDEN"));

    public static MfoHallazgoResponse MapHallazgo(IDataReader r) => new(
        r.SafeGetString("SEVERIDAD"),
        r.SafeGetString("CODIGO"),
        r.SafeGetString("ENTIDAD"),
        r.SafeGetInt32("ENTIDAD_ID"),
        r.SafeGetString("CLAVE"),
        r.SafeGetString("MENSAJE"));

    // ------------------------------------------------------------------------
    // Ejecucion
    // ------------------------------------------------------------------------

    /// <summary>
    /// Ejecuta un procedimiento que devuelve un ref cursor y un mensaje.
    /// El parametro p_TotalRecords se agrega solo si el procedimiento lo declara.
    /// </summary>
    public static async Task<ResultDto<List<T>>> ExecuteListAsync<T>(
        OracleCommand cmd,
        Func<IDataReader, T> map,
        int page = 0,
        int pageSize = 0)
    {
        var list = new List<T>();
        var pMessage = cmd.Parameters["p_Message"];
        var pTotal = cmd.Parameters.Contains("p_TotalRecords") ? cmd.Parameters["p_TotalRecords"] : null;

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(map(reader));
            }
        }
        catch (OracleException ex)
        {
            // Un fallo del lado de Oracle -procedimiento inexistente, permisos
            // faltantes, schema sin instalar- tiene que llegar como
            // IsValid = false con el mensaje, igual que cualquier otro fallo
            // esperado. Dejarlo propagar produce un 500 sin cuerpo, que es lo
            // peor posible para diagnosticar: el usuario ve "Network Error" y
            // nadie sabe si el problema es la base, los permisos o la red.
            return InvalidList<T>($"Error de base de datos ({ex.Number}): {ex.Message}");
        }

        var message = GetMessage(pMessage);
        var isSuccess = IsSuccessMessage(message);
        var total = pTotal is null ? list.Count : GetIntOutput(pTotal);

        return new ResultDto<List<T>>(list)
        {
            Data = isSuccess ? list : null,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message,
            Page = page,
            CantidadRegistros = total,
            TotalPage = pageSize > 0 && total > 0 ? (int)Math.Ceiling(total / (double)pageSize) : 0
        };
    }

    /// <summary>
    /// Ejecuta un procedimiento sin cursor que solo devuelve p_Message.
    /// Los procedimientos del motor codifican los fallos de negocio en el
    /// mensaje, no lanzando: por eso aqui no hay try/catch de negocio, solo el
    /// tecnico.
    /// </summary>
    public static async Task<ResultDto<int>> ExecuteScalarAsync(OracleCommand cmd, string? outParam = null)
    {
        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (OracleException ex)
        {
            return Invalid<int>($"Error de base de datos ({ex.Number}): {ex.Message}");
        }

        var message = GetMessage(cmd.Parameters["p_Message"]);
        var isSuccess = IsSuccessMessage(message);
        var id = outParam is not null && cmd.Parameters.Contains(outParam)
            ? GetIntOutput(cmd.Parameters[outParam])
            : 0;

        return new ResultDto<int>(id)
        {
            Data = id,
            IsValid = isSuccess,
            Message = isSuccess ? string.Empty : message
        };
    }
}
