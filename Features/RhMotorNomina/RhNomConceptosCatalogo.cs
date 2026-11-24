using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomConceptosQuery(int CodigoTipoNomina,int PageNumber=1,int PageSize=20,string SearchText="");
public sealed class RhNomConceptosCatalogoHandler(ConnectionDB db)
{
    public Task<ResultDto<List<RhNomConceptoCfg>>> Handle(RhNomConceptosQuery value,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var page=RhNomConfiguracion.Lista(new(value.PageNumber,value.PageSize,value.SearchText));
        if(value.CodigoTipoNomina<=0)throw new RhNomError("TIPO_NOMINA_REQUERIDO","Indique tipo de nomina positivo.");
        using var cn=await RhNomCfgDb.Abrir(db,ct);using var cmd=Comando(cn,company,value.CodigoTipoNomina,page,null);var total=RhNomCfgDb.Out(cmd,"p_TOTAL");var rows=await Leer(cmd,ct);
        return RhNomCfgDb.Pagina(rows,page,total);
    });
    private static OracleCommand Comando(OracleConnection cn,int company,int type,RhNomListaQuery page,string? ids)
    {
        var cmd=new OracleCommand("RH.SP_RH_NOM_CON_CAT",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
        RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(cmd,"p_NOMINA",OracleDbType.Int32,type);
        RhNomCfgDb.Add(cmd,"p_PAGE",OracleDbType.Int32,page.PageNumber);RhNomCfgDb.Add(cmd,"p_SIZE",OracleDbType.Int32,page.PageSize);
        RhNomCfgDb.Add(cmd,"p_SEARCH",OracleDbType.Varchar2,string.IsNullOrEmpty(page.SearchText)?null:page.SearchText);RhNomCfgDb.Add(cmd,"p_IDS",OracleDbType.Varchar2,ids);cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);return cmd;
    }
    private static async Task<List<RhNomConceptoCfg>> Leer(OracleCommand cmd,CancellationToken ct)
    {
        var rows=new List<RhNomConceptoCfg>();using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))
        {
            int? N(string name)=>r.IsDBNull(r.GetOrdinal(name))?null:RhNomCfgDb.Int(r,name);
            rows.Add(new(RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_TIPO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_CONCEPTO"),RhNomCfgDb.Text(r,"CODIGO"),RhNomCfgDb.Text(r,"DENOMINACION"),RhNomCfgDb.Text(r,"TIPO_CONCEPTO"),N("FRECUENCIA_ID"),RhNomCfgDb.Text(r,"STATUS"),N("AUTOMATICO")));
        }
        return rows;
    }
    public static async Task<List<RhNomConceptoCfg>> Lote(OracleConnection cn,int company,int type,IReadOnlyList<RhNomReglaConceptoSave>? rules,CancellationToken ct,IReadOnlyList<RhNomAccionControlSave>? actions=null)
    {
        rules=(rules??[]).Concat((actions??[]).Select(a=>a is null?null!:new RhNomReglaConceptoSave(a.Clave,a.CodigoVariableControl,a.CodigoConcepto,a.HashConceptoEsperado,a.TipoMovimiento,"","",""))).ToList();
        if(rules.Count==0)return [];
        if(rules.Count>100||rules.Any(r=>r is null||r.CodigoConcepto<=0))throw new RhNomError("REGLAS_INVALIDAS","Indique hasta 100 reglas con concepto positivo.");
        var ids=string.Join(",",rules.Select(r=>r.CodigoConcepto).Distinct().Order().Select(id=>id.ToString(CultureInfo.InvariantCulture)));
        using var cmd=Comando(cn,company,type,new(1,100,""),ids);RhNomCfgDb.Out(cmd,"p_TOTAL");return await Leer(cmd,ct);
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("conceptosCatalogo")]
    public Task<IActionResult> ConceptsCatalog(RhNomConceptosQuery value)=>RunAsync("rh.nomina.configuracion.leer",(company,_,ct)=>new RhNomConceptosCatalogoHandler(db).Handle(value,company,ct));
}
