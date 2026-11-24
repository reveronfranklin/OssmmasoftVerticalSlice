using System.Data;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomCfgDb
{
    public static async Task<ResultDto<T>> Resultado<T>(Func<Task<ResultDto<T>>> operation)
    {
        try { return await operation(); }
        catch (RhNomError ex) { return new(default!) { IsValid=false, Message=ex.Message }; }
        catch (OracleException ex) when (ex.Number is 20001 or 20002 or 20003 or 20004 or 54 or 1)
        {
            var message = ex.Number switch
            {
                20002 => "La version cambio. Recargue antes de guardar.",
                20003 => "Modelo, tipo de nomina o variable no encontrados en la empresa.",
                54 => "La configuracion esta siendo editada. Reintente despues de recargar.",
                20004 or 1 => "Codigo duplicado o intento de modificar una revision/codigo inmutable.",
                _ => "Configuracion invalida. Revise los campos enviados."
            };
            return new(default!) { IsValid=false, Message=message };
        }
    }
    public static async Task<OracleConnection> Abrir(ConnectionDB db, CancellationToken ct)
    {
        var cn = db.GetRhConnection();
        try { await cn.OpenAsync(ct); return cn; }
        catch (OperationCanceledException) { cn.Dispose(); throw; }
        catch { cn.Dispose(); throw new RhNomError("CONEXION_RH", "No se pudo abrir la conexion RH para configuracion."); }
    }
    public static OracleCommand Comando(OracleConnection cn, string operation, OracleTransaction? transaction = null)
    {
        return new OracleCommand("RH.RH_PKG_NOM_CFG." + operation, cn)
        { BindByName = true, CommandType = CommandType.StoredProcedure, CommandTimeout = 20, Transaction = transaction };
    }
    private static object Valor(object? value) => value ?? DBNull.Value;
    public static void Add(OracleCommand cmd, string name, OracleDbType type, object? value) => cmd.Parameters.Add(name, type).Value = Valor(value);
    public static OracleParameter Out(OracleCommand cmd, string name) => cmd.Parameters.Add(name, OracleDbType.Int32, ParameterDirection.Output);
    public static void Paginacion(OracleCommand cmd, int company, RhNomListaQuery value)
    {
        Add(cmd, "p_EMPRESA", OracleDbType.Int32, company);
        Add(cmd, "p_PAGE", OracleDbType.Int32, value.PageNumber); Add(cmd, "p_SIZE", OracleDbType.Int32, value.PageSize);
        Add(cmd, "p_SEARCH", OracleDbType.Varchar2, SupportDb.StringDbValue(value.SearchText));
        cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
    }
    public static async Task<T> Guardar<T>(OracleConnection cn, Func<OracleTransaction, Task<T>> action, CancellationToken ct)
    {
        using var tx = cn.BeginTransaction();
        try
        {
            var result = await action(tx);
            ct.ThrowIfCancellationRequested();
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch { /* Preserve the original failure. */ }
            throw;
        }
    }
    public static int Int(IDataReader r, string name) => SupportDb.ToInt32(r.GetValue(r.GetOrdinal(name)));
    public static string Text(IDataReader r, string name) => r.IsDBNull(r.GetOrdinal(name)) ? "" : r.GetString(r.GetOrdinal(name));
    public static RhNomVariable Variable(IDataReader r) => new(Int(r,"ID_VAR"),Text(r,"CODIGO"),Text(r,"DESCRIPCION"),
        Text(r,"TIPO"),Text(r,"UNIDAD"),Text(r,"ORIGEN"),Int(r,"ACTIVA")==1,Int(r,"VERSION"));
    public static ResultDto<List<T>> Pagina<T>(List<T> rows, RhNomListaQuery query, OracleParameter total)
    {
        var count = SupportDb.GetIntOutput(total);
        return new(rows) { IsValid = true, Message = "OK", Page = query.PageNumber, CantidadRegistros = count,
            TotalPage = (int)Math.Ceiling(count / (double)query.PageSize) };
    }
    public static async Task<RhNomModeloVersion> Revision(OracleConnection cn, int company, int model, int version, CancellationToken ct)
    {
        if (model <= 0 || version <= 0) throw new RhNomError("VERSION_INVALIDA", "Indique modelo y revision mayores que cero.");
        using var cmd = new OracleCommand("RH.SP_RH_NOM_VER_DOC",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
        Add(cmd,"p_EMPRESA",OracleDbType.Int32,company); Add(cmd,"p_MODELO",OracleDbType.Int32,model); Add(cmd,"p_VERSION",OracleDbType.Int32,version);
        cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new RhNomError("REVISION_NO_ENCONTRADA", "La revision no existe en la empresa autorizada.");
        var hash = Text(reader,"CONFIG_HASH");
        var count = Int(reader,"CONFIG_LARGO");
        if (count is < 1 or > RhNomConfiguracion.MaxBytes) throw new RhNomError("CONFIGURACION_INVALIDA", "Longitud de configuracion invalida.");
        var type = Int(reader,"CODIGO_TIPO_NOMINA"); var scenario = Text(reader,"ESCENARIO"); var description = Text(reader,"DESCRIPCION");
        var savedVersion = Int(reader,"VERSION"); var actor = Int(reader,"USUARIO_INS"); var date = reader.GetDateTime(reader.GetOrdinal("FECHA_INS"));
        using var clob=reader.GetOracleClob(reader.GetOrdinal("DOCUMENTO"));var text=clob.Value;
        if(text.Length!=count||System.Text.Encoding.UTF8.GetByteCount(text)>RhNomConfiguracion.MaxBytes||savedVersion!=version||Int(reader,"ID_MODELO")!=model||Int(reader,"CODIGO_EMPRESA")!=company||await reader.ReadAsync(ct))throw new RhNomError("CONFIGURACION_INVALIDA","Documento de revision incompleto o fuera de contexto.");
        var snapshot = RhNomConfiguracion.Leer(text,hash,company,model);
        if (snapshot.CodigoTipoNomina != type || snapshot.Escenario != scenario
            || snapshot.Descripcion != description)
            throw new RhNomError("CONFIGURACION_ALTERADA", "Cabecera y contenido de la revision no coinciden.");
        return new(model,savedVersion,type,scenario,description,hash,actor,date,snapshot);
    }
}
