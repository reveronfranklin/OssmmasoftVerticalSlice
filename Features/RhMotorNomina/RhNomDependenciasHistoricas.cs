using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomDependenciaHistorica(string CodigoVariable,string ProveedorModelo,bool CompatibleDiagnostico,
    string? ProveedorHistorico,string Estado,string Mensaje);
public record RhNomDependenciasHistoricas(int ModeloId,int Version,string HashModelo,int CodigoTipoNomina,
    string Escenario,List<RhNomDependenciaHistorica> Entradas,List<RhNomDiagnostico> BloqueosModelo)
{
    public string VersionMetodo=>RhNomDependencias.Version;
    public string VersionRecalculo=>RhNomRecalculosHistoricos.Version;
    public bool TieneBloqueos=>BloqueosModelo.Count>0||Entradas.Any(e=>!e.CompatibleDiagnostico);
    public bool DatosHistoricosLeidos=>false;
    public bool ParidadCertificada=>false;
    public bool AplicacionDisponible=>false;
    public bool MovimientosAplicados=>false;
}
public static class RhNomDependencias
{
    public const string Version="DEPENDENCIAS_HISTORICAS_V1";
    public static RhNomDependenciasHistoricas Analizar(RhNomModeloVersion model,CancellationToken ct=default)
    {
        if(model.Configuracion.CodigoTipoNomina!=model.CodigoTipoNomina||model.Configuracion.ModeloId!=model.ModeloId)
            throw new RhNomError("CONTEXTO_DISTINTO","La revision y su configuracion no corresponden al mismo modelo/tipo.");
        var sources=RhNomFuentesModelo.Validar(model.Configuracion.FuentesActuales,model.Configuracion.Variables);
        var entries=new List<RhNomDependenciaHistorica>();var blocks=new List<RhNomDiagnostico>();
        if(sources.Count==0)blocks.Add(new("MODELO_SIN_FUENTES","Guarde proveedores en una revision antes de reconstruir entradas historicas."));
        foreach(var source in sources.OrderBy(s=>s.CodigoVariable,StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var provider=RhNomRecalculosHistoricos.ValidarProveedor(source,model);
                entries.Add(new(source.CodigoVariable,source.Proveedor,true,provider,"REQUIERE_LECTURA_HISTORICA",
                    "Equivalente registrado para diagnostico; falta comprobar sus datos en el contexto solicitado. No acredita procedencia original ni paridad."));
            }
            catch(RhNomError ex)
            {entries.Add(new(source.CodigoVariable,source.Proveedor,false,null,ex.Diagnostico.Codigo,ex.Message));}
        }
        return new(model.ModeloId,model.Version,model.Hash,model.CodigoTipoNomina,model.Escenario,entries,blocks);
    }
}
public sealed class RhNomDependenciasHistoricasHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomDependenciasHistoricas>> Handle(RhNomModeloVersionQuery q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        if(company<=0||q.ModeloId<=0||q.Version<=0)throw new RhNomError("MODELO_REQUERIDO","Indique modelo y revision positivos.");
        using var cn=await RhNomCfgDb.Abrir(db,ct);var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);
        var report=RhNomDependencias.Analizar(model,ct);
        return new ResultDto<RhNomDependenciasHistoricas>(report){IsValid=true,Message=report.TieneBloqueos?
            "Dependencias revisadas con fuentes sin equivalente o politicas pendientes. No se leyeron datos de nomina.":
            "Equivalentes de diagnostico identificados. Debe verificar la disponibilidad de datos en cada contexto historico."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoDependencias")][RequestSizeLimit(4096)]
    public Task<IActionResult> HistoricalDependencies(RhNomModeloVersionQuery q)=>RunAsync("rh.nomina.historico.datos",
        (e,_,ct)=>new RhNomDependenciasHistoricasHandler(db).Handle(q,e,ct),"rh.nomina.configuracion.leer");
}
