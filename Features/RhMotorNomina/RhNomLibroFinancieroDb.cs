using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomLibroFinancieroDb
{
    public const int MaxDocumento=512*1024,MaxCadena=8*1024*1024;
    sealed record Cabecera(int Id,int Empresa,int Tipo,int Persona,int Plan,int Actor,DateTime Fecha,int? Anterior,string? HashAnterior,string HashDoc,string HashEvento,string HashPlan,int EmpresaPlan);
    static OracleCommand Cmd(OracleConnection cn,string operation)=>new("RH.RH_PKG_NOM_FIN_EVT."+operation,cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
    public static async Task<RhNomLibroEstado> Leer(OracleConnection cn,int company,int type,int person,CancellationToken ct)=>RhNomLibroFinanciero.Reconstruir(await Eventos(cn,company,type,person,ct),company,type,person,ct);
    public static async Task<List<RhNomLibroEvento>> Eventos(OracleConnection cn,int company,int type,int person,CancellationToken ct)
    {
        if(company<=0||type<=0||person<=0)throw new RhNomError("LIBRO_CONTEXTO_INVALIDO","Contexto financiero positivo requerido.");
        var headers=new List<Cabecera>();
        using(var c=Cmd(cn,"CADENA"))
        {
            RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,type);RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,person);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            using var r=await c.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))
            {
                if(headers.Count==RhNomLibroFinanciero.MaxEventos)throw new RhNomError("LIBRO_LIMITE_EVENTOS","El historial excede 200 eventos; no se reconstruye propiedad con una cadena parcial.");
                int N(string n)=>RhNomCfgDb.Int(r,n);string S(string n)=>RhNomCfgDb.Text(r,n);
                headers.Add(new(N("ID_EVT"),N("CODIGO_EMPRESA"),N("CODIGO_TIPO_NOMINA"),N("CODIGO_PERSONA"),N("ID_PLAN"),N("ACTOR"),
                    DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("FECHA_BD")),DateTimeKind.Unspecified),r.IsDBNull(r.GetOrdinal("ID_ANT"))?null:N("ID_ANT"),r.IsDBNull(r.GetOrdinal("HASH_ANT"))?null:S("HASH_ANT"),S("HASH_DOC"),S("HASH_EVT"),S("HASH_PLAN"),N("EMPRESA_PLAN")));
            }
        }
        var events=new List<RhNomLibroEvento>();var bytes=0;
        foreach(var h in headers)
        {
            if(h.Empresa!=company||h.EmpresaPlan!=company||h.Tipo!=type||h.Persona!=person)throw new RhNomError("LIBRO_CABECERA_AJENA","Cabecera de evento/plan ajena al contexto.");
            using var c=Cmd(cn,"DOCUMENTO");RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,h.Id);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            var text=new StringBuilder();int? length=null;var part=1;
            using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))
            {
                var count=RhNomCfgDb.Int(r,"LARGO");var chunk=RhNomCfgDb.Text(r,"FRAGMENTO");
                if(count is <1 or >MaxDocumento||length is not null&&length!=count||RhNomCfgDb.Int(r,"PARTE")!=part++||chunk.Length==0||text.Length+chunk.Length>count||RhNomCfgDb.Text(r,"HUELLA")!=h.HashDoc)
                    throw new RhNomError("LIBRO_FRAGMENTOS_INVALIDOS","Documento financiero incompleto o fuera de limite.");
                bytes+=Encoding.UTF8.GetByteCount(chunk);if(bytes>MaxCadena)throw new RhNomError("LIBRO_LIMITE_BYTES","La cadena supera 8 MiB; no se devuelve propiedad parcial.");
                length=count;text.Append(chunk);
            }
            if(length is null||text.Length!=length||Encoding.UTF8.GetByteCount(text.ToString())>MaxDocumento||RhNomCorridas.Hash(text.ToString())!=h.HashDoc)
                throw new RhNomError("LIBRO_DOCUMENTO_ALTERADO","Documento financiero incompleto o alterado.");
            RhNomLibroDocumento d;try{d=JsonSerializer.Deserialize<RhNomLibroDocumento>(text.ToString(),RhNomCorridas.Json)??throw new JsonException();}catch(JsonException){throw new RhNomError("LIBRO_DOCUMENTO_INVALIDO","Documento financiero no interpretable.");}
            if(d.IdPlan!=h.Plan||d.Actor!=h.Actor||d.HashPlan!=h.HashPlan)throw new RhNomError("LIBRO_PLAN_DISTINTO","Documento financiero distinto de su cabecera o plan inmutable.");
            // The document's hash is over canonical JSON, while HASH_DOC also verifies
            // the stored bytes. The future writer must store this canonical form.
            events.Add(new(h.Id,h.Anterior,h.HashAnterior,h.Fecha,h.HashDoc,h.HashEvento,d));
        }
        RhNomLibroFinanciero.Reconstruir(events,company,type,person,ct);return events;
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("planesLibroFinanciero")][RequestSizeLimit(4096)]
    public Task<IActionResult> FinancialBook(RhNomLeerPlan q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var saved=await new RhNomPlanesGuardadosHandler(db,config).Leer(q,e,a,ct);
        if(!saved.IsValid||saved.Data is null)return new ResultDto<RhNomLibroEstado>(default!){IsValid=false,Message=saved.Message};
        var context=saved.Data.Diagnostico.Consolidacion;await using var cn=await RhNomCfgDb.Abrir(db,ct);
        return new ResultDto<RhNomLibroEstado>(await RhNomLibroFinancieroDb.Leer(cn,e,context.CodigoTipoNomina,context.CodigoPersona,ct)){IsValid=true,Message="Ledger reconstruido para el contexto del plan. Guardar planes no crea aplicaciones ni propiedad."};
    }),"rh.nomina.fuentes.datos");
}
