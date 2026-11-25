using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomIdentidadPrenomina(string Base,string Servidor,string Usuario,string Servicio);
public static class RhNomPrenominaIdentidades
{
    public static void Validar(RhNomIdentidadPrenomina writer,RhNomIdentidadPrenomina source)
    {
        var a=new[]{writer.Base,writer.Servidor,writer.Usuario,writer.Servicio};
        var b=new[]{source.Base,source.Servidor,source.Usuario,source.Servicio};
        if(a.Concat(b).Any(string.IsNullOrWhiteSpace)||!a.SequenceEqual(b,StringComparer.OrdinalIgnoreCase))
            throw new RhNomError("PRENOMINA_BASE_DISTINTA","La lectura y la escritura deben usar la misma base, servidor, usuario y servicio Oracle.");
    }
    public static async Task<RhNomIdentidadPrenomina> Leer(OracleConnection cn,CancellationToken ct)
    {
        using var c=cn.CreateCommand();c.CommandTimeout=15;
        c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER'),SYS_CONTEXT('USERENV','SERVICE_NAME') FROM DUAL";
        using var r=await c.ExecuteReaderAsync(ct);await r.ReadAsync(ct);
        string S(int n)=>r.IsDBNull(n)?"":r.GetString(n);
        return new(S(0),S(1),S(2),S(3));
    }
}
