using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhDocumentos;

public record RhDocumentoResponse(
    int CodigoPersona,
    int CodigoDocumento,
    int TipoDocumentoId,
    string TipoDocumento,
    string NumeroDocumento,
    DateTime? FechaVencimiento,
    int? TipoGradoId,
    string TipoGrado,
    int? GradoId,
    string Grado,
    string Extra1,
    string Extra2,
    string Extra3,
    int UsuarioIns,
    DateTime? FechaIns,
    int? UsuarioUpd,
    DateTime? FechaUpd,
    int CodigoEmpresa,
    string Persona
);

internal static class RhDocumentosDb
{
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        empresa = 0;
        errorMessage = string.Empty;
        var empresaString = config["settings:EmpresaConfig"];

        if (string.IsNullOrEmpty(empresaString))
        {
            errorMessage = "Configuración 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(empresaString, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un número válido.";
            return false;
        }

        return true;
    }

    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "success", StringComparison.OrdinalIgnoreCase);
    }

    public static object DbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }

    public static object PositiveDbValue(int? value)
    {
        return value.HasValue && value.Value > 0 ? value.Value : DBNull.Value;
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : Convert.ToInt32(parameter.Value.ToString());
    }

    public static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    public static int? SafeGetNullableInt32(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    public static RhDocumentoResponse MapDocumento(IDataReader reader)
    {
        return new RhDocumentoResponse(
            reader.SafeGetInt32("CODIGO_PERSONA"),
            reader.SafeGetInt32("CODIGO_DOCUMENTO"),
            reader.SafeGetInt32("TIPO_DOCUMENTO_ID"),
            reader.SafeGetString("TIPO_DOCUMENTO"),
            reader.SafeGetString("NUMERO_DOCUMENTO"),
            SafeGetNullableDateTime(reader, "FECHA_VENCIMIENTO"),
            SafeGetNullableInt32(reader, "TIPO_GRADO_ID"),
            reader.SafeGetString("TIPO_GRADO"),
            SafeGetNullableInt32(reader, "GRADO_ID"),
            reader.SafeGetString("GRADO"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            reader.SafeGetInt32("USUARIO_INS"),
            SafeGetNullableDateTime(reader, "FECHA_INS"),
            SafeGetNullableInt32(reader, "USUARIO_UPD"),
            SafeGetNullableDateTime(reader, "FECHA_UPD"),
            reader.SafeGetInt32("CODIGO_EMPRESA"),
            reader.SafeGetString("PERSONA")
        );
    }
}
