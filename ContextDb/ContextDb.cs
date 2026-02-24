using Oracle.ManagedDataAccess.Client;
using Microsoft.Data.SqlClient;
using System.Data;

namespace OssmmasoftVerticalSlice.ContextDB;

public class ConnectionDB(IConfiguration _config)
{
    public SqlConnection GetSQLRRD()
    {
        return new SqlConnection(_config.GetConnectionString("rrdConecction"));
    }

    public OracleConnection GetOracleConnection()
    {
        return new OracleConnection(_config.GetConnectionString("oracleConnection"));
    }
     public OracleConnection GetPresupuestoConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionPRE"));
    }
}