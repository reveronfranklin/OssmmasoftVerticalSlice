using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomCapturaMovimientos(List<RhNomImagenMovimiento> Imagenes,bool HayAmbitoAjeno);
public record RhNomDiagnosticoFinanciero(RhNomConsolidacion Consolidacion,RhNomPlanFinanciero Plan,
    string HashMovimientos,int CantidadMovimientos)
{
    public bool PropiedadRegistrada=>false;
    public bool PlanPersistido=>false;
    public bool ParidadCertificada=>false;
    public bool AplicacionDisponible=>false;
    public string Advertencia=>"Plan previo de solo lectura. No acredita vigencia transaccional ni propiedad sobre movimientos existentes.";
}
public static class RhNomMovimientosPlanDb
{
    public static async Task<RhNomCapturaMovimientos> Leer(OracleConnection cn,int company,int type,int person,CancellationToken ct)
    {
        using var cmd=new OracleCommand("RH.SP_RH_NOM_MOV_PLAN",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
        RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Int32,type);
        RhNomCfgDb.Add(cmd,"p_PERSONA",OracleDbType.Int32,person);
        var foreign=cmd.Parameters.Add("p_AJENOS",OracleDbType.Int32,ParameterDirection.Output);
        cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        var rows=new List<RhNomImagenMovimiento>();
        using(var reader=await cmd.ExecuteReaderAsync(ct))
        {
            while(await reader.ReadAsync(ct))
            {
                if(rows.Count==2000)throw new RhNomError("LIMITE_MOVIMIENTOS","Mas de 2000 movimientos; no se prepara un plan parcial.");
                int? Number(string n)=>reader.IsDBNull(reader.GetOrdinal(n))?null:RhNomCfgDb.Int(reader,n);
                int Required(string n)=>Number(n)??throw new RhNomError("IDENTIDAD_MOVIMIENTO_NULA","Movimiento sin identidad o contexto requerido.");
                string? Text(string n)=>reader.IsDBNull(reader.GetOrdinal(n))?null:reader.GetString(reader.GetOrdinal(n));
                DateTime? Date(string n)=>reader.IsDBNull(reader.GetOrdinal(n))?null:DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(n)),DateTimeKind.Unspecified);
                rows.Add(new(Required("CODIGO_MOV_NOMINA"),Required("CODIGO_EMPRESA"),Required("CODIGO_TIPO_NOMINA"),Required("CODIGO_PERSONA"),
                    Required("CODIGO_CONCEPTO"),Number("FRECUENCIA_ID"),Text("TIPO"),Text("MONTO_TEXTO"),Text("STATUS"),Text("COMPLEMENTO_CONCEPTO"),
                    Text("EXTRA1"),Text("EXTRA2"),Text("EXTRA3"),Number("USUARIO_INS"),Date("FECHA_INS"),Number("USUARIO_UPD"),Date("FECHA_UPD")));
            }
        }
        var count=((OracleDecimal)foreign.Value).ToInt32();
        return new(rows,count>0);
    }
    public static RhNomDiagnosticoFinanciero Preparar(RhNomConsolidacion consolidated,RhNomCapturaMovimientos capture,CancellationToken ct=default)
    {
        // Until an immutable transactional ownership ledger exists, every existing
        // movement remains unowned. Never accept ownership/images from HTTP.
        var plan=RhNomPlanesFinancieros.Preparar(consolidated,capture.Imagenes,[],ct);
        if(capture.HayAmbitoAjeno)
            plan=plan with{Pasos=plan.Pasos.Select(p=>p with{Accion="BLOQUEADO",MontoNuevo=null,Bloqueo="INDICE_UNICO_OTRO_AMBITO"}).ToList()};
        return new(consolidated,plan,RhNomRecalculosHistoricos.Hash(capture.Imagenes.OrderBy(r=>r.Id).ToArray()),capture.Imagenes.Count);
    }
}
public sealed class RhNomDiagnosticoFinancieroHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomDiagnosticoFinanciero>> Handle(RhNomConsolidarQuery q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var result=await new RhNomConsolidacionHandler(db,config).Handle(q,company,actor,ct);
        if(!result.IsValid||result.Data is null)return new ResultDto<RhNomDiagnosticoFinanciero>(default!){IsValid=false,Message=result.Message};
        await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var capture=await RhNomMovimientosPlanDb.Leer(cn,company,result.Data.CodigoTipoNomina,result.Data.CodigoPersona,ct);
        var report=RhNomMovimientosPlanDb.Preparar(result.Data,capture,ct);
        return new ResultDto<RhNomDiagnosticoFinanciero>(report){IsValid=true,Message=report.Plan.TieneBloqueos?"Plan previo con bloqueos. No se modificaron movimientos.":"Plan previo preparado. No autoriza ni aplica movimientos."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("corridasPlanFinanciero")][RequestSizeLimit(16384)]
    public Task<IActionResult> FinancialPlan(RhNomConsolidarQuery q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomDiagnosticoFinancieroHandler(db,config).Handle(q,e,a,ct),"rh.nomina.fuentes.datos");
}
