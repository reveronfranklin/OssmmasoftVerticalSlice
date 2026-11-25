using System.Data;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomPrenominaProcessor
{
    public static async Task<bool> Tick(ConnectionDB db,IConfiguration config,int company,CancellationToken ct,ILogger? logger=null)
    {
        if(!config.GetValue<bool>("RhMotorNomina:PrenominaEnabled"))return false;
        await using var cn=await RhNomCfgDb.Abrir(db,ct);int id,actor;
        using(var c=RhNomPrenominaDb.Cmd(cn,"RH.RH_PKG_NOM_PRE.PENDIENTE")){RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return false;id=RhNomCfgDb.Int(r,"ID_LOTE");actor=RhNomCfgDb.Int(r,"ACTOR");}
        var lot=(await RhNomPrenominaDb.Leer(cn,company,actor,id,null,ct))!;
        try{
            if(lot.Documento.Estado=="CALCULANDO"){
                var result=await new RhNomPrenominaHandler(db,config).Avanzar(new(id,lot.Version),company,actor,ct);
                if(!result.IsValid){RhNomPrenominaIncidencias.Registrar(company,actor,id,result.Message??"No se pudo avanzar la prenomina; se reintentara.");logger?.LogWarning("Prenomina lote {Lote}: avance rechazado.",id);return false;}
            }else if(lot.Documento.Estado=="GUARDANDO"){
                if(!config.GetValue<bool>("RhMotorNomina:PrenominaTemporalEnabled"))return false;
                try{await RhNomPrenominaTemporal.Guardar(db,cn,lot,config,lot.Documento.UsuarioAuditoria,ct);}
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(RhNomError ex){await RhNomPrenominaDb.Guardar(cn,lot,lot.Documento with{Estado="TEMP_ERROR",ErrorTemporal=ex.Message},ct);}
                catch(OracleException ex)when(ex.Number is 54 or 20001 or 20002){await RhNomPrenominaDb.Guardar(cn,lot,lot.Documento with{Estado="TEMP_ERROR",ErrorTemporal=ex.Number==54?"Fuentes ocupadas; no se escribio el temporal.":"Oracle rechazo el guardado; no se escribio el temporal."},ct);}
            }
            RhNomPrenominaIncidencias.Limpiar(company,actor,id);
        }catch(OracleException ex){
            RhNomPrenominaIncidencias.Oracle(company,actor,id,ex.Number);
            logger?.LogWarning("Prenomina lote {Lote}: Oracle {OracleCode}; reintento pendiente.",id,ex.Number);return false;
        }
        return true;
    }
}
