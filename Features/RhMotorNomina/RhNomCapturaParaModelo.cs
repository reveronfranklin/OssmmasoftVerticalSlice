using System.Data;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomCapturarParaModelo(string ClaveSolicitud,int ModeloId,int Version,int CodigoPeriodo,int CodigoPersona,int CodigoRelacionCargo,
    string PoliticaPresupuesto="COINCIDIR_PERIODO",string? MotivoPresupuesto=null);
public sealed class RhNomCapturaParaModeloHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomResumenCaptura>> Crear(RhNomCapturarParaModelo q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        RhNomArchivoCapsulas.Alcance(company,actor);
        if(q is null||q.ModeloId<=0||q.Version<=0||q.CodigoRelacionCargo<=0)throw new RhNomError("CAPTURA_MODELO_INVALIDA","Modelo, revision y relacion de cargo positivos requeridos.");
        await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);
        var request=new RhNomCapturarPrenomina(q.ClaveSolicitud,model.CodigoTipoNomina,q.CodigoPeriodo,q.CodigoPersona,q.PoliticaPresupuesto,q.MotivoPresupuesto);
        RhNomCapturasServidor.Validar(request,company,actor);RhNomPresupuestos.Habilitada(config,q.PoliticaPresupuesto);
        var previous=await RhNomArchivoCapsulasDb.PorClave(cn,company,actor,q.ClaveSolicitud,ct);
        if(previous is not null)
        {
            RhNomCapturasServidor.Reintento(request,previous,company,actor);
            var original=await RhNomArchivoCapsulasDb.Leer(cn,company,actor,previous.IdCapsula,ct);RhNomCapturasServidor.PoliticaReintento(request,original.Capsula);
            var sources=RhNomCapsulasPrenomina.Leer(original.Capsula).FuentesModelo;
            if(sources is null||sources.ModeloId!=q.ModeloId||sources.Version!=q.Version||sources.HashModelo!=model.Hash||sources.Fuente.CodigoRelacionCargo!=q.CodigoRelacionCargo)
                throw new RhNomError("CAPTURA_CLAVE_REUTILIZADA","La clave identifica otra revision, relacion o flujo de captura.");
            return new ResultDto<RhNomResumenCaptura>(RhNomCapturasServidor.Resumir(original,company,actor,true)){IsValid=true,Message="Captura de modelo original recuperada sin releer fuentes actuales."};
        }
        var manual=RhNomCapturasServidor.Politica(config,model.CodigoTipoNomina);RhNomCapsula capsule;
        using(var tx=cn.BeginTransaction(IsolationLevel.Serializable))
        {
            try
            {
                var source=await RhNomCapturaFuentesDb.Leer(cn,tx,request,company,manual,ct);
                var sourceRequest=new RhNomSimularActual(q.ModeloId,q.Version,q.CodigoPeriodo,source.Ambito.Periodo.FechaNomina.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture),source.Ambito.CodigoPresupuesto,q.CodigoPersona,q.CodigoRelacionCargo,null,q.PoliticaPresupuesto,RhNomCapturasServidor.Motivo(request));
                var inputs=await RhNomFuentesActualesHandler.LeerEntradas(cn,model,sourceRequest,company,ct);
                var frozen=new RhNomFuentesModeloCapturadas(model.ModeloId,model.Version,model.Hash,RhNomFuentesActuales.Version,inputs.Fuente,inputs.Entradas);
                capsule=RhNomCapsulasPrenomina.Capturar(source.Ambito,RhNomSeleccionPrenomina.Plantilla(source.Clase),source.Movimientos,source.Metadatos,manual,source.Periodo,frozen);
                // Verify provider evidence and model binding before archiving. No formulas are evaluated here.
                RhNomModelosDesdeCaptura.VerificarFuentes(model,RhNomCapsulasPrenomina.Leer(capsule),actor);
            }
            finally{await tx.RollbackAsync(CancellationToken.None);}
        }
        var saved=await RhNomArchivoCapsulasDb.Guardar(cn,company,actor,q.ClaveSolicitud,capsule,ct);
        return new ResultDto<RhNomResumenCaptura>(RhNomCapturasServidor.Resumir(saved,company,actor,false)){IsValid=true,Message="Entradas y movimientos del modelo archivados en una lectura serializable; formulas pendientes del worker."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("capturaPrenominaCrearParaModelo")][RequestSizeLimit(8192)]
    public Task<IActionResult> CaptureCreateForModel(RhNomCapturarParaModelo q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCapturaParaModeloHandler(db,config).Crear(q,e,a,ct),"rh.nomina.fuentes.datos");
}
