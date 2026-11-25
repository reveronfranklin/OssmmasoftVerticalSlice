using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomCrearCorridaEnlaceSueldo(string ClaveSolicitud,int ModeloId,int Version,int IdCapsula,
    int ModeloSueldoId,int VersionSueldo,string PoliticaEnlace);
public record RhNomConfigEnlaceSueldo(string Politica,RhNomModeloVersion ModeloSueldo);
public static class RhNomCorridasEnlaceSueldo
{
    public const string Version="CORRIDA_ENLACE_SUELDO_V1";
    public static string HashSolicitud(RhNomCrearCorridaEnlaceSueldo q,int company,int actor)
    {
        if(q is null||q.ModeloSueldoId<=0||q.VersionSueldo<=0||q.PoliticaEnlace!=RhNomEnlacesSueldo.Politica)
            throw new RhNomError("CORRIDA_ENLACE_INVALIDA","Revision de sueldo positiva y politica explicita requeridas.");
        RhNomModelosDesdeCaptura.Validar(new(q.ClaveSolicitud,q.ModeloId,q.Version,q.IdCapsula),company,actor);
        return RhNomRecalculosHistoricos.Hash(new{Version,CodigoEmpresa=company,Actor=actor,Solicitud=q});
    }
    public static RhNomDocumentoEnlaceSueldo Verificar(RhNomCorridaDocumento doc,int company,int actor)
    {
        if(doc.Formato!=3||doc.EnlaceSueldo is null||doc.Captura is null||doc.CodigoEmpresa!=company||doc.Actor!=actor
            ||doc.EnlaceSueldo.Politica!=RhNomEnlacesSueldo.Politica)
            throw new RhNomError("CORRIDA_ENLACE_INCOMPLETA","Cadena formato3 con captura, dos revisiones y politica explicita requerida.");
        var value=new RhNomDocumentoEnlaceSueldo(doc.EnlaceSueldo.Politica,new(doc.Modelo,doc.Captura,doc.EnlaceSueldo.ModeloSueldo));
        RhNomEtapasSueldo.Verificar(value.Etapa,company,actor);return value;
    }
}
public sealed class RhNomCorridaEnlaceSueldoHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomCorrida>> Crear(RhNomCrearCorridaEnlaceSueldo q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var hash=RhNomCorridasEnlaceSueldo.HashSolicitud(q,company,actor);
        if(!config.GetValue<bool>("RhMotorNomina:CorridasEnabled")||!config.GetValue<bool>("RhMotorNomina:CorridasCapturaEnabled"))
            throw new RhNomError("CORRIDAS_CAPTURA_DESHABILITADAS","Las corridas desde captura no estan habilitadas.");
        await using var cn=await RhNomCfgDb.Abrir(db,ct);var previous=await RhNomCorridasDb.PorClave(cn,company,actor,q.ClaveSolicitud,ct);
        if(previous is not null)
        {
            if(previous.Modo!=RhNomModelosDesdeCaptura.Modo||previous.HashSolicitud!=hash)throw new RhNomError("CLAVE_REUTILIZADA","La clave identifica otro contexto, revision o flujo.");
            return new ResultDto<RhNomCorrida>(previous){IsValid=true,Message="Cadena original recuperada sin recapturar fuentes ni revisiones."};
        }
        var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);
        var salary=await RhNomCfgDb.Revision(cn,company,q.ModeloSueldoId,q.VersionSueldo,ct);
        var captured=await RhNomArchivoCapsulasDb.Leer(cn,company,actor,q.IdCapsula,ct);
        var doc=new RhNomCorridaDocumento(3,company,actor,model,RhNomModelosDesdeCaptura.Modo,null,null,null,null,null,null,null,new(captured),new(q.PoliticaEnlace,salary));
        var text=RhNomCorridas.Serializar(doc,RhNomCorridas.MaxDatosBytes);RhNomCorridas.Leer(text,RhNomCorridas.Hash(text),company,actor);
        var id=await RhNomCorridasDb.Crear(cn,company,actor,q.ClaveSolicitud,hash,doc,ct);
        return new ResultDto<RhNomCorrida>(await RhNomCorridasDb.Leer(cn,company,actor,id,ct)){IsValid=true,Message="Cadena de sueldo/bases pendiente del worker; diagnostica, sin aplicacion financiera."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("corridasCrearEnlaceSueldo")][RequestSizeLimit(8192)]
    public Task<IActionResult> RunCreateSalaryLink(RhNomCrearCorridaEnlaceSueldo q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCorridaEnlaceSueldoHandler(db,config).Crear(q,e,a,ct),"rh.nomina.fuentes.datos");
}
