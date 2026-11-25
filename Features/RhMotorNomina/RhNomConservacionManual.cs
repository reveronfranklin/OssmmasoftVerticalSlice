using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomMovimientosManualQuery(int CodigoTipoNomina,int CodigoPersona);
public record RhNomCapturaManual(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPersona,
    string HashCaptura,List<RhNomImagenMovimiento> Movimientos,bool HayAmbitoAjeno)
{
    public string VersionMetodo=>RhNomConservacionManual.Version;
    public string Origen=>"RH_MOV_NOMINA_ACTUAL";
    public string Tratamiento=>"CONSERVAR_IMPORTES_EXISTENTES";
    public bool CalculoAutomaticoRealizado=>false;
    public bool MovimientosAplicados=>false;
    public bool PropiedadFinancieraAcreditada=>false;
    public bool EstadoHistoricoVerificado=>false;
    public bool ParidadHistoricaCertificada=>false;
}
public static class RhNomConservacionManual
{
    public const string Version="CONSERVACION_MANUAL_V1";
    public static void Validar(RhNomMovimientosManualQuery q,int company,IConfiguration config)
    {
        if(q is null||company<=0||q.CodigoTipoNomina<=0||q.CodigoPersona<=0)
            throw new RhNomError("CONTEXTO_MANUAL_INVALIDO","Tipo de nomina y persona positivos requeridos.");
        if(!RhNomPoliticaOperacion.TiposManuales(config).Contains(q.CodigoTipoNomina))
            throw new RhNomError("TIPO_NO_MANUAL","El tipo no esta configurado para conservar importes manuales.");
    }
    public static RhNomCapturaManual Preparar(RhNomMovimientosManualQuery q,int company,IConfiguration config,RhNomCapturaMovimientos capture)
    {
        Validar(q,company,config);
        if(capture?.Imagenes is null||capture.Imagenes.Count>2000||capture.Imagenes.Any(r=>r is null))
            throw new RhNomError("CAPTURA_MANUAL_INVALIDA","Hasta 2000 imagenes completas requeridas; no se devuelve una captura parcial.");
        if(capture.Imagenes.Select(r=>r.Id).Distinct().Count()!=capture.Imagenes.Count)
            throw new RhNomError("IMAGEN_MANUAL_DUPLICADA","La captura contiene identidades repetidas; no se deduplican importes.");
        foreach(var row in capture.Imagenes)
        {
            // Preserve the complete image, including NULL, sign, NUMBER text, kind and audit.
            RhNomPlanesFinancieros.Hash(row);
            if(row.CodigoEmpresa!=company||row.CodigoTipoNomina!=q.CodigoTipoNomina||row.CodigoPersona!=q.CodigoPersona)
                throw new RhNomError("IMAGEN_MANUAL_AJENA","La imagen no pertenece a la empresa, tipo y persona solicitados.");
        }
        var images=capture.Imagenes.OrderBy(r=>r.Id).ToList();
        var hash=RhNomRecalculosHistoricos.Hash(new{Version,CodigoEmpresa=company,q.CodigoTipoNomina,q.CodigoPersona,
            TiposManuales=RhNomPoliticaOperacion.TiposManuales(config),capture.HayAmbitoAjeno,Movimientos=images});
        return new(company,q.CodigoTipoNomina,q.CodigoPersona,hash,images,capture.HayAmbitoAjeno);
    }
}
public sealed class RhNomConservacionManualHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomCapturaManual>> Handle(RhNomMovimientosManualQuery q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        RhNomConservacionManual.Validar(q,company,config);
        await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var capture=await RhNomMovimientosPlanDb.Leer(cn,company,q.CodigoTipoNomina,q.CodigoPersona,ct);
        var result=RhNomConservacionManual.Preparar(q,company,config,capture);
        return new ResultDto<RhNomCapturaManual>(result){IsValid=true,Message="Importes actuales consultados sin realizar calculo automatico ni modificar movimientos."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("manualMovimientos")][RequestSizeLimit(8192)]
    public Task<IActionResult> ManualMovements(RhNomMovimientosManualQuery q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomConservacionManualHandler(db,config).Handle(q,e,ct),"rh.nomina.fuentes.datos");
}
