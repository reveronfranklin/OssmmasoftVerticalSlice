using Oracle.ManagedDataAccess.Client;
using Microsoft.Data.SqlClient;
using Npgsql;
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
    public OracleConnection GetAdmConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionADM"));
    }
    public OracleConnection GetRhConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionRH"));
    }

    public OracleConnection GetRhcConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionRHC"));
    }

    public OracleConnection GetSisConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionSIS"));
    }

    public OracleConnection GetCntConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionCNT"));
    }

    public OracleConnection GetBmConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionBM"));
    }

    public OracleConnection GetBmcConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionBMC"));
    }

    public OracleConnection GetCatConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionCAT"));
    }

    public OracleConnection GetRmConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionRM"));
    }

    // Motor de Formularios (requerimiento 16). Schema propio: decision 1 de la
    // Fase 0. El motor no consulta tablas de otros schemas, asi que esta conexion
    // no necesita permisos cruzados.
    public OracleConnection GetMfoConnection()
    {
        return new OracleConnection(_config.GetConnectionString("DefaultConnectionMFO"));
    }

    // Facturacion Electronica (requerimiento 32). Unico getter PostgreSQL del
    // proyecto: el modulo no usa Oracle. Clave propia DefaultConnectionFed y no
    // DefaultConnectionPostgres, que ya existe y la usa otro consumidor. El rol
    // de la cadena es "fed", propietario del schema FED y distinto de ossmmapg,
    // para que el append-only del Art. 18.2 se pueda sostener con permisos.
    public NpgsqlConnection GetFedConnection()
    {
        return new NpgsqlConnection(_config.GetConnectionString("DefaultConnectionFed"));
    }
}
