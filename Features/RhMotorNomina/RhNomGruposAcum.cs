using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomGrupoListaQuery(int CodigoTipoNomina);
public record RhNomGrupoQuery(int CodigoTipoNomina,int TipoAcumuladoId,string PoliticaVigencia,string FechaReferencia,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? PoliticaDatos=null);
public record RhNomGrupoCatalogo(int TipoAcumuladoId,string Codigo);
public record RhNomGrupoFila(int? Id,int? CodigoEmpresa,int CodigoConcepto,string TipoAcumuladoId,int ConceptosCoincidentes,
    DateTime? Desde,DateTime? Hasta,int? ConceptoAsociado,string? Extra1,string? Extra2,string? Extra3,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? ReferenciaFila=null);
public record RhNomGrupoAcum(int CodigoEmpresa,int CodigoTipoNomina,int TipoAcumuladoId,string Codigo,string PoliticaVigencia,
    DateTime FechaReferencia,List<RhNomGrupoFila> Filas,List<int> Conceptos,string Hash,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? PoliticaDatos=null)
{
    public string Origen=>"RH_CONCEPTOS_ACUMULA_CAPTURADO";
    public string VersionMetodo {get;init;}="GRUPOS_ACUMULACION_V1";
    public bool EstadoHistoricoVerificado=>false;
    public bool ResolucionDinamica=>false;
}
public static class RhNomGruposAcum
{
    public const string Version="GRUPOS_ACUMULACION_V2";
    public static DateTime Validar(RhNomGrupoQuery q)
    {
        if(q is null||q.CodigoTipoNomina<=0||q.TipoAcumuladoId<=0||q.PoliticaVigencia is not ("HASTA_NULA" or "FECHA_CONTEXTO"))throw new RhNomError("GRUPO_INVALIDO","Tipo, grupo positivo y politica HASTA_NULA/FECHA_CONTEXTO requeridos.");
        if(q.PoliticaDatos is not (null or "LEGACY_OBSERVADO"))throw new RhNomError("POLITICA_GRUPO_INVALIDA","Politica de datos de grupo no admitida.");
        return RhNomFuentesHistoricas.Fecha(q.FechaReferencia);
    }
    public static RhNomGrupoAcum Capturar(RhNomGrupoQuery q,int company,string code,IReadOnlyList<RhNomGrupoFila> rows,CancellationToken ct=default)
    {
        var date=Validar(q);
        if(rows is null||company<=0||string.IsNullOrWhiteSpace(code)||code.Length>10||rows.Count is <1 or >100)throw new RhNomError("GRUPO_VACIO_O_EXCESIVO","Grupo identificado con 1..100 asociaciones requerido, sin truncar.");
        var ids=new HashSet<int>();var references=new HashSet<string>(StringComparer.Ordinal);var concepts=new HashSet<int>();var legacy=q.PoliticaDatos=="LEGACY_OBSERVADO";
        foreach(var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if(row is null||row.CodigoConcepto<=0||row.ConceptosCoincidentes!=1
                ||!int.TryParse(row.TipoAcumuladoId?.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out var id)||id!=q.TipoAcumuladoId)
                throw new RhNomError("ASOCIACION_GRUPO_AMBIGUA","Asociacion ajena al grupo, empresa/tipo o concepto no unico.");
            if(legacy)
            {
                if(row.CodigoEmpresa is {} owner&&owner!=company||row.Id is <=0||row.ReferenciaFila is null||row.ReferenciaFila.Length!=18||row.ReferenciaFila.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('+' or '/'))||!references.Add(row.ReferenciaFila))
                    throw new RhNomError("REFERENCIA_GRUPO_INVALIDA","LEGACY_OBSERVADO requiere referencia Oracle unica y concepto unico de la empresa/tipo; conserva nulos originales.");
                concepts.Add(row.CodigoConcepto);
            }
            else if(row.Id is null or <=0||!ids.Add(row.Id.Value)||row.CodigoEmpresa!=company||!concepts.Add(row.CodigoConcepto)||row.ReferenciaFila is not null)
                throw new RhNomError("ASOCIACION_GRUPO_AMBIGUA","Asociacion sin empresa/id o concepto duplicado; seleccione una politica explicita sin corregir los datos.");
            if(row.Desde is {} from&&from.Kind!=DateTimeKind.Unspecified||row.Hasta is {} until&&until.Kind!=DateTimeKind.Unspecified||row.Desde is {} a&&row.Hasta is {} b&&a>b)
                throw new RhNomError("VIGENCIA_GRUPO_INVALIDA","Vigencia de asociacion invalida.");
            if(q.PoliticaVigencia=="HASTA_NULA"?row.Hasta is not null:row.Desde>date||row.Hasta<date)throw new RhNomError("GRUPO_FUERA_VIGENCIA","Asociacion fuera de la politica seleccionada.");
            if(new[]{row.Extra1,row.Extra2,row.Extra3}.Any(s=>s?.Length>100))throw new RhNomError("GRUPO_INVALIDO","Extras de asociacion exceden 100 caracteres.");
        }
        var result=new RhNomGrupoAcum(company,q.CodigoTipoNomina,q.TipoAcumuladoId,code,q.PoliticaVigencia,date,rows.OrderBy(r=>r.Id).ThenBy(r=>r.ReferenciaFila,StringComparer.Ordinal).ToList(),concepts.Order().ToList(),"",q.PoliticaDatos){VersionMetodo=legacy?Version:"GRUPOS_ACUMULACION_V1"};
        return result with{Hash=Huella(result)};
    }
    public static string Huella(RhNomGrupoAcum r)
    {
        var group=new{r.CodigoEmpresa,r.CodigoTipoNomina,r.TipoAcumuladoId,r.Codigo,r.PoliticaVigencia,r.FechaReferencia,r.Filas,r.Conceptos,r.Origen,r.VersionMetodo};
        object value=r.PoliticaDatos is null?group:new{Grupo=group,r.PoliticaDatos};
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }
    public static RhNomGrupoQuery Solicitud(RhNomGrupoAcum r)=>new(r.CodigoTipoNomina,r.TipoAcumuladoId,r.PoliticaVigencia,r.FechaReferencia.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture),r.PoliticaDatos);
    public static void Verificar(RhNomGrupoAcum r)
    {
        if(r is null)throw new RhNomError("GRUPO_INVALIDO","Grupo capturado requerido.");
        var restored=Capturar(Solicitud(r),r.CodigoEmpresa,r.Codigo,r.Filas);
        if(r.Hash!=Huella(r)||r.Hash!=restored.Hash)throw new RhNomError("GRUPO_ALTERADO","Grupo capturado, conceptos o contexto difieren de su huella.");
    }
    public static void ValidarUso(RhNomGrupoAcum? r,int company,int type,DateTime date,bool historico=false)
    {
        if(r is null)return;Verificar(r);
        if(r.CodigoEmpresa!=company||r.CodigoTipoNomina!=type)throw new RhNomError("GRUPO_AJENO","Grupo capturado ajeno a empresa/tipo.");
        if(r.PoliticaVigencia=="FECHA_CONTEXTO"&&r.FechaReferencia!=date)throw new RhNomError("FECHA_GRUPO_DISTINTA","Grupo capturado por fecha requiere exactamente esa fecha de contexto; cree otra revision.");
        if(historico&&r.PoliticaVigencia!="FECHA_CONTEXTO")throw new RhNomError("GRUPO_HISTORICO_NO_ADMITIDO","Recalculo cerrado requiere grupo FECHA_CONTEXTO, no catalogo abierto HASTA_NULA.");
    }
    private static OracleCommand Comando(OracleConnection cn,int company,int type,int? group=null,string? policy=null,DateTime? date=null)
    {
        var c=new OracleCommand("RH.SP_RH_NOM_GRP_ACU",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,type);RhNomCfgDb.Add(c,"p_GRUPO",OracleDbType.Int32,group);RhNomCfgDb.Add(c,"p_POLITICA",OracleDbType.Varchar2,policy);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,date);
        c.Parameters.Add("p_CODIGO",OracleDbType.Varchar2,20).Direction=ParameterDirection.Output;c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);return c;
    }
    public static async Task<List<RhNomGrupoCatalogo>> Catalogo(OracleConnection cn,int company,int type,CancellationToken ct)
    {
        if(company<=0||type<=0)throw new RhNomError("TIPO_NOMINA_REQUERIDO","Empresa y tipo positivos requeridos.");
        using var c=Comando(cn,company,type);var rows=new List<RhNomGrupoCatalogo>();using var r=await c.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)){if(rows.Count>=200)throw new RhNomError("LIMITE_GRUPOS","Catalogo supera 200 grupos; no se trunca.");rows.Add(new(RhNomCfgDb.Int(r,"DESCRIPCION_ID"),RhNomCfgDb.Text(r,"CODIGO")));}return rows;
    }
    public static async Task<RhNomGrupoAcum> Leer(OracleConnection cn,RhNomGrupoQuery q,int company,CancellationToken ct)
    {
        var date=Validar(q);using var c=Comando(cn,company,q.CodigoTipoNomina,q.TipoAcumuladoId,q.PoliticaVigencia,date);var rows=new List<RhNomGrupoFila>();
        using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))
        {
            if(rows.Count>=100)throw new RhNomError("LIMITE_GRUPO","Grupo supera 100 asociaciones; no se trunca.");
            int? N(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Int(r,n);string? S(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Text(r,n);DateTime? D(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(n)),DateTimeKind.Unspecified);
            rows.Add(new(N("CODIGO_CONCEPTO_ACUMULA"),N("CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_CONCEPTO"),RhNomCfgDb.Text(r,"TIPO_ACUMULADO_ID"),RhNomCfgDb.Int(r,"CONCEPTOS"),D("FECHA_DESDE"),D("FECHA_HASTA"),N("CODIGO_CONCEPTO_ASOCIADO"),S("EXTRA1"),S("EXTRA2"),S("EXTRA3"),q.PoliticaDatos is null?null:S("REFERENCIA_FILA")));
        }
        var code=c.Parameters["p_CODIGO"].Value;return Capturar(q,company,code is OracleString os?(os.IsNull?"":os.Value):Convert.ToString(code)??"",rows,ct);
    }
    public static async Task Revalidar(OracleConnection cn,IReadOnlyList<RhNomFuenteActualBinding>? bindings,int company,int type,CancellationToken ct)
    {
        if(bindings?.Count>200||bindings?.Any(b=>b is null)==true)throw new RhNomError("FUENTES_INVALIDAS","Hasta 200 fuentes no nulas.");
        var groups=(bindings??[]).SelectMany(b=>new[]{b.Acumulador?.Grupo,b.AcumuladorHistorico?.Filtros?.Grupo,b.AgregadosPh?.GrupoSi}).Where(g=>g is not null).Select(g=>g!).ToList();
        if(groups.Count>20)throw new RhNomError("LIMITE_GRUPOS","Hasta 20 grupos capturados por revision.");
        var seen=new HashSet<string>();foreach(var group in groups)
        {
            ValidarUso(group,company,type,group.FechaReferencia);
            if(!seen.Add(group.Hash))continue;
            var current=await Leer(cn,Solicitud(group),company,ct);
            if(current.Hash!=group.Hash)throw new RhNomError("GRUPO_DESACTUALIZADO","Grupo distinto del catalogo; recapture antes de guardar esta revision.");
        }
    }
}
public sealed class RhNomGruposAcumHandler(ConnectionDB db)
{
    public Task<ResultDto<List<RhNomGrupoCatalogo>>> Catalogo(RhNomGrupoListaQuery q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{using var cn=await RhNomCfgDb.Abrir(db,ct);return new ResultDto<List<RhNomGrupoCatalogo>>(await RhNomGruposAcum.Catalogo(cn,company,q.CodigoTipoNomina,ct)){IsValid=true,Message="Grupos visibles para el tipo. La captura valida asociaciones y empresa."};});
    public Task<ResultDto<RhNomGrupoAcum>> Capturar(RhNomGrupoQuery q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{using var cn=await RhNomCfgDb.Abrir(db,ct);return new ResultDto<RhNomGrupoAcum>(await RhNomGruposAcum.Leer(cn,q,company,ct)){IsValid=true,Message="Grupo capturado para configurar borrador; no acredita procedencia historica."};});
}
public sealed partial class RhNominaController
{
    [HttpPost("gruposAcumulacionCatalogo")]
    public Task<IActionResult> GroupCatalog(RhNomGrupoListaQuery q)=>RunAsync("rh.nomina.configuracion.leer",(e,_,ct)=>new RhNomGruposAcumHandler(db).Catalogo(q,e,ct));
    [HttpPost("grupoAcumulacionCapturar")][RequestSizeLimit(16384)]
    public Task<IActionResult> GroupCapture(RhNomGrupoQuery q)=>RunAsync("rh.nomina.configuracion.leer",(e,_,ct)=>new RhNomGruposAcumHandler(db).Capturar(q,e,ct));
}
