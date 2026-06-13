using System;
using System.Data;
using System.Globalization;
using Oracle.ManagedDataAccess.Types;

public static class DataReaderExtensions
{
    public static string SafeGetString(this IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : ConvertDbString(reader.GetValue(ordinal));
    }

    public static int SafeGetInt32(this IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0 : ConvertDbInt32(reader.GetValue(ordinal));
    }

    public static decimal SafeGetDecimal(this IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0m : ConvertDbDecimal(reader.GetValue(ordinal));
    }

    public static bool SafeGetBoolean(this IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return !reader.IsDBNull(ordinal) && ConvertDbBoolean(reader.GetValue(ordinal));
    }
    public static long SafeGetInt64(this IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0L : ConvertDbInt64(reader.GetValue(ordinal));
    }

    private static int ConvertDbInt32(object value)
    {
        return Convert.ToInt32(ConvertDbDecimal(value));
    }

    private static long ConvertDbInt64(object value)
    {
        return Convert.ToInt64(ConvertDbDecimal(value));
    }

    private static decimal ConvertDbDecimal(object value)
    {
        if (value is OracleDecimal oracleDecimal)
        {
            return oracleDecimal.IsNull
                ? 0m
                : decimal.Parse(oracleDecimal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static bool ConvertDbBoolean(object value)
    {
        if (value is bool boolean)
        {
            return boolean;
        }

        if (value is OracleDecimal or OracleString)
        {
            var stringValue = ConvertDbString(value);
            return stringValue == "1"
                || string.Equals(stringValue, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stringValue, "s", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stringValue, "y", StringComparison.OrdinalIgnoreCase);
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static string ConvertDbString(object value)
    {
        return value switch
        {
            OracleString oracleString => oracleString.IsNull ? string.Empty : oracleString.Value,
            OracleClob oracleClob => oracleClob.IsNull ? string.Empty : oracleClob.Value,
            OracleDecimal oracleDecimal => oracleDecimal.IsNull ? string.Empty : oracleDecimal.ToString(),
            _ => value.ToString() ?? string.Empty
        };
    }
}
