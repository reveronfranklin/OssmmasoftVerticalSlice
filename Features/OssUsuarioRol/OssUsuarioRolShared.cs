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

        return new OssUsuarioRolResponse(
            reader.SafeGetInt32("CODIGO_USUARIO_ROL"),
            reader.SafeGetString("USUARIO"),
            reader.SafeGetInt32("CODIGO_USUARIO"),
            reader.SafeGetString("DESCRIPCION"),
            ParseJsonMenu(jsonMenuText)
        );
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
        int ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        if (reader is OracleDataReader oracleReader)
        {
            using OracleClob clob = oracleReader.GetOracleClob(ordinal);
            return clob.IsNull ? string.Empty : clob.Value;
        }

        return reader.GetValue(ordinal).ToString() ?? string.Empty;
    }
}
