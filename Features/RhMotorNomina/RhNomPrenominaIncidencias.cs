using System.Collections.Concurrent;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Transient operational notice only. Never part of a model, source or result hash.
// Storage failures cannot safely append another CLOB to the failing tablespace.
public static class RhNomPrenominaIncidencias
{
    static readonly ConcurrentDictionary<(int Empresa,int Actor,int Lote),(DateTime Fecha,string Mensaje)> Notices=new();
    public static void Registrar(int company,int actor,int lot,string message)
    {
        foreach(var pair in Notices.Where(p=>p.Value.Fecha<DateTime.UtcNow.AddHours(-1)))Notices.TryRemove(pair.Key,out _);
        if(Notices.Count>=256){var oldest=Notices.OrderBy(p=>p.Value.Fecha).FirstOrDefault();Notices.TryRemove(oldest.Key,out _);}
        Notices[(company,actor,lot)]=(DateTime.UtcNow,message.Length<=500?message:message[..500]);
    }
    public static void Oracle(int company,int actor,int lot,int number)=>Registrar(company,actor,lot,
        number is 1691 or 1653 or 1654 or 1536
        ?$"Oracle no dispone de espacio para los documentos de nomina (ORA-{number:D5}). Se reintentara automaticamente cuando haya capacidad; no se aceptan resultados parciales."
        :$"Oracle rechazo el procesamiento de la prenomina (ORA-{number:D5}). Se reintentara automaticamente; revise el estado antes de iniciar otra ejecucion.");
    public static void Limpiar(int company,int actor,int lot)=>Notices.TryRemove((company,actor,lot),out _);
    public static RhNomLotePrenomina Mostrar(RhNomLotePrenomina lot)
    {
        var d=lot.Documento;
        return d.Estado is "CALCULANDO" or "GUARDANDO"&&Notices.TryGetValue((d.Empresa,d.Actor,lot.IdLote),out var notice)&&notice.Fecha>=DateTime.UtcNow.AddHours(-1)
            ?lot with{Documento=d with{ErrorTemporal=d.ErrorTemporal??notice.Mensaje}}:lot;
    }
}
