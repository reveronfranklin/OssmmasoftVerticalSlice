using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.PrePresupuesto;

public record PrePresupuestoFinanciadoResponse(
    int FinanciadoId,
    string DescripcionFinanciado
);

public record PrePresupuestoListResponse(
    int CodigoPresupuesto,
    string Descripcion,
    int Ano,
    bool PresupuestoEnEjecucion,
    List<PrePresupuestoFinanciadoResponse> PreFinanciadoDto
);

internal static class PrePresupuestoDb
{
    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "success", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetInt(IDataRecord reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    public static string GetString(IDataRecord reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
    }
}
