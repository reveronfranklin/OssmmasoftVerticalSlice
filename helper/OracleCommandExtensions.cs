using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace OssmmasoftVerticalSlice.Helpers;

public static class OracleCommandExtensions
{
    /// <summary>
    /// Añade un parámetro de entrada de forma rápida
    /// </summary>
    public static void AddInput(this OracleCommand cmd, string name, OracleDbType type, object value)
    {
        cmd.Parameters.Add(name, type).Value = value ?? DBNull.Value;
    }

    /// <summary>
    /// Añade un parámetro de salida y devuelve la referencia para leerlo después
    /// </summary>
    public static OracleParameter AddOutput(this OracleCommand cmd, string name, OracleDbType type, int size = 0)
    {
        var param = size > 0 
            ? new OracleParameter(name, type, size) 
            : new OracleParameter(name, type);
            
        param.Direction = ParameterDirection.Output;
        cmd.Parameters.Add(param);
        return param;
    }

    /// <summary>
    /// Configura el parámetro obligatorio para retornar datos en Oracle (SYS_REFCURSOR)
    /// </summary>
    public static void AddResultSet(this OracleCommand cmd, string name = "p_ResultSet")
    {
        cmd.Parameters.Add(name, OracleDbType.RefCursor).Direction = ParameterDirection.Output;
    }
}