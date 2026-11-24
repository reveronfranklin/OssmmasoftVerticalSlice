using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public sealed class RhNomCorridaDesdeCapturaHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomCorrida>> Crear(RhNomCrearCorridaCaptura q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        RhNomModelosDesdeCaptura.Validar(q,company,actor);
        if(!config.GetValue<bool>("RhMotorNomina:CorridasEnabled")||!config.GetValue<bool>("RhMotorNomina:CorridasCapturaEnabled"))
            throw new RhNomError("CORRIDAS_CAPTURA_DESHABILITADAS","Las corridas desde captura no estan habilitadas en este ambiente.");
        var hash=RhNomModelosDesdeCaptura.HashSolicitud(q,company,actor);await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var previous=await RhNomCorridasDb.PorClave(cn,company,actor,q.ClaveSolicitud,ct);
        if(previous is not null)
        {
            if(previous.Modo!=RhNomModelosDesdeCaptura.Modo||previous.HashSolicitud!=hash)throw new RhNomError("CLAVE_REUTILIZADA","La clave identifica otra revision, captura o flujo.");
            return new ResultDto<RhNomCorrida>(previous){IsValid=true,Message="Corrida original recuperada; no se recapturan fuentes ni se cambia la revision."};
        }
        var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);
        var captured=await RhNomArchivoCapsulasDb.Leer(cn,company,actor,q.IdCapsula,ct);
        var source=new RhNomCorridaCaptura(captured);RhNomModelosDesdeCaptura.Verificar(model,source,company,actor);
        var doc=new RhNomCorridaDocumento(2,company,actor,model,RhNomModelosDesdeCaptura.Modo,null,null,null,null,null,null,null,source);
        var text=RhNomCorridas.Serializar(doc,RhNomCorridas.MaxDatosBytes);RhNomCorridas.Leer(text,RhNomCorridas.Hash(text),company,actor);
        var id=await RhNomCorridasDb.Crear(cn,company,actor,q.ClaveSolicitud,hash,doc,ct);
        return new ResultDto<RhNomCorrida>(await RhNomCorridasDb.Leer(cn,company,actor,id,ct)){IsValid=true,Message="Corrida diagnostica pendiente desde revision y captura congeladas; no apta para aplicacion financiera."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("corridasCrearDesdeCaptura")][RequestSizeLimit(8192)]
    public Task<IActionResult> RunCreateFromCapture(RhNomCrearCorridaCaptura q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCorridaDesdeCapturaHandler(db,config).Crear(q,e,a,ct),"rh.nomina.fuentes.datos");
}
