using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.OssUsuarioRol;

public record OssUsuarioRolResponse(
    int CodigoUsuarioRol,
    string Usuario,
    int CodigoUsuario,
    string Descripcion,
    JsonElement JsonMenu
);

internal static class OssUsuarioRolDb
{
    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "success", StringComparison.OrdinalIgnoreCase);
    }

    public static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : Convert.ToInt32(parameter.Value.ToString());
    }

    public static bool TrySerializeJsonMenu(JsonElement jsonMenu, out string json, out string errorMessage)
    {
        json = string.Empty;
        errorMessage = string.Empty;

        if (jsonMenu.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            errorMessage = "El campo jsonMenu es requerido.";
            return false;
        }

        if (jsonMenu.ValueKind is not JsonValueKind.Array and not JsonValueKind.Object)
        {
            errorMessage = "El campo jsonMenu debe ser un objeto o arreglo JSON.";
            return false;
        }

        json = JsonSerializer.Serialize(jsonMenu);
        return true;
    }

    public static OssUsuarioRolResponse MapUsuarioRol(IDataReader reader)
    {
        var jsonMenuText = GetValueAsString(reader, "JSON_MENU");
        if (string.IsNullOrWhiteSpace(jsonMenuText))
        {
            jsonMenuText = GetChunkedJsonMenu(reader);
        }

        return new OssUsuarioRolResponse(
            reader.SafeGetInt32("CODIGO_USUARIO_ROL"),
            reader.SafeGetString("USUARIO"),
            reader.SafeGetInt32("CODIGO_USUARIO"),
            reader.SafeGetString("DESCRIPCION"),
            ParseJsonMenu(jsonMenuText)
        );
    }

    public static string JsonMenuSelectList(string columnName = "JSON_MENU")
    {
        return $@"
               DBMS_LOB.SUBSTR({columnName}, 4000, 1) JSON_MENU_01,
               DBMS_LOB.SUBSTR({columnName}, 4000, 4001) JSON_MENU_02,
               DBMS_LOB.SUBSTR({columnName}, 4000, 8001) JSON_MENU_03,
               DBMS_LOB.SUBSTR({columnName}, 4000, 12001) JSON_MENU_04,
               DBMS_LOB.SUBSTR({columnName}, 4000, 16001) JSON_MENU_05,
               DBMS_LOB.SUBSTR({columnName}, 4000, 20001) JSON_MENU_06,
               DBMS_LOB.SUBSTR({columnName}, 4000, 24001) JSON_MENU_07,
               DBMS_LOB.SUBSTR({columnName}, 4000, 28001) JSON_MENU_08";
    }

    private static JsonElement ParseJsonMenu(string jsonMenuText)
    {
        try
        {
            using var jsonDocument = string.IsNullOrWhiteSpace(jsonMenuText)
                ? JsonDocument.Parse("[]")
                : JsonDocument.Parse(jsonMenuText);

            return jsonDocument.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var fallback = JsonDocument.Parse("[]");
            return fallback.RootElement.Clone();
        }
    }

    private static string GetValueAsString(IDataReader reader, string columnName)
    {
        int ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0)
        {
            return string.Empty;
        }

        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        var value = reader.GetValue(ordinal);
        if (value is OracleString oracleString)
        {
            return oracleString.IsNull ? string.Empty : oracleString.Value;
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is OracleClob oracleClob)
        {
            return oracleClob.IsNull ? string.Empty : oracleClob.Value;
        }

        return value.ToString() ?? string.Empty;
    }

    private static string GetChunkedJsonMenu(IDataReader reader)
    {
        var chunks = new List<string>();
        for (var i = 1; i <= 8; i++)
        {
            var chunk = GetValueAsString(reader, $"JSON_MENU_{i:00}");
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }
        }

        return string.Concat(chunks);
    }

    private static int TryGetOrdinal(IDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
