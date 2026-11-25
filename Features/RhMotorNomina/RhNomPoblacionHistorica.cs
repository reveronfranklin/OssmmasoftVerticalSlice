using System.Data;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomPoblacionHistoricaQuery(int CodigoTipoNomina,int CodigoPeriodo,string FechaNomina);
public record RhNomPersonaArchivo(int? CodigoPersona,int FilasCargo,int Relaciones,int RelacionesInvalidas,
    int? CodigoRelacionCargo,int Partidas);
public record RhNomPersonaPoblacion(RhNomPersonaArchivo Archivo,List<string> Bloqueos)
{
    public bool CandidatoUnCargo=>Bloqueos.Count==0;
}
public record RhNomPoblacionHistorica(RhNomContextoFuente Fuente,List<RhNomPersonaPoblacion> Personas,
    int FilasCargo,int Partidas,int CandidatosUnCargo,int PersonasConBloqueos,string HashPoblacion)
{
    public string VersionMetodo=>RhNomPoblaciones.Version;
    public bool PoblacionCompleta=>true;
    public bool EntradasOriginalesVerificadas=>false;
    public bool ParidadCertificada=>false;
    public bool MovimientosAplicados=>false;
}
public static class RhNomPoblaciones
{
    public const string Version="POBLACION_HISTORICA_V1";
    public const int MaxPersonas=5000;
    public static int Entero(OracleDecimal value)
    {
        if(value.IsNull||!value.IsInt||value<new OracleDecimal(int.MinValue)||value>new OracleDecimal(int.MaxValue))
            throw new RhNomError("POBLACION_INCONSISTENTE","Las claves y contadores deben ser enteros representables; no se redondean ni truncan.");
        return value.ToInt32();
    }
    public static RhNomPoblacionHistorica Analizar(RhNomContextoFuente source,IReadOnlyList<RhNomPersonaArchivo> rows)
    {
        RhNomFuentesHistoricas.Validar(new(source.Contexto.CodigoTipoNomina,source.Contexto.CodigoPeriodo??0,
            source.Contexto.FechaNomina.ToString("yyyy-MM-dd'T'HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture)),source.Contexto.CodigoEmpresa);
        if(source.FechaCierre==default||source.Contexto.FechaNomina.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("PERIODO_NO_CERRADO","La poblacion requiere contexto cerrado sin conversion de zona.");
        if(rows.Count>MaxPersonas)throw new RhNomError("LIMITE_POBLACION_HISTORICA","El universo supera 5000 grupos de persona; no se entrega una poblacion truncada.");
        if(rows.GroupBy(r=>r.CodigoPersona).Any(g=>g.Count()>1))
            throw new RhNomError("POBLACION_INCONSISTENTE","Una persona aparece mas de una vez en el universo agregado.");
        var persons=new List<RhNomPersonaPoblacion>();
        foreach(var r in rows.OrderBy(r=>r.CodigoPersona))
        {
            if(r.FilasCargo<0||r.Partidas<0||r.Relaciones<0||r.Relaciones>r.FilasCargo
                ||r.RelacionesInvalidas<0||r.RelacionesInvalidas>r.FilasCargo
                ||(long)r.FilasCargo+r.Partidas==0
                ||(r.CodigoRelacionCargo is not null&&(r.FilasCargo!=1||r.Relaciones!=1||r.RelacionesInvalidas!=0||r.CodigoRelacionCargo<=0))
                ||(r.FilasCargo==1&&r.RelacionesInvalidas==0&&(r.CodigoRelacionCargo is null||r.Relaciones!=1)))
                throw new RhNomError("POBLACION_INCONSISTENTE","Contadores o relacion unica inconsistentes; no se infieren cargos.");
            var blocks=new List<string>();
            if(r.CodigoPersona is null or <=0)blocks.Add("PERSONA_INVALIDA");
            if(r.FilasCargo==0)blocks.Add("PARTIDAS_SIN_CARGO");
            if(r.Partidas==0)blocks.Add("CARGO_SIN_PARTIDAS");
            if(r.RelacionesInvalidas>0)blocks.Add("RELACION_INVALIDA");
            if(r.Relaciones>1)blocks.Add("MULTIPLES_CARGOS_REQUIEREN_SEGMENTOS");
            if(r.FilasCargo>(long)r.Relaciones+r.RelacionesInvalidas)blocks.Add("SNAPSHOT_DUPLICADO");
            persons.Add(new(r,blocks));
        }
        var partidas=persons.Sum(p=>(long)p.Archivo.Partidas);var cargos=persons.Sum(p=>(long)p.Archivo.FilasCargo);
        if(partidas!=source.Partidas||persons.Count(p=>p.Archivo.CodigoPersona is not null&&p.Archivo.Partidas>0)!=source.Personas||cargos>int.MaxValue)
            throw new RhNomError("POBLACION_CAMBIO","Los contadores no coinciden con el contexto; vuelva a capturar el universo completo.");
        return new(source,persons,(int)cargos,(int)partidas,persons.Count(p=>p.CandidatoUnCargo),persons.Count(p=>!p.CandidatoUnCargo),
            RhNomRecalculosHistoricos.Hash(new{Version,Fuente=source,Personas=persons}));
    }
}
public sealed class RhNomPoblacionHistoricaHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomPoblacionHistorica>> Handle(RhNomPoblacionHistoricaQuery q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var query=new RhNomContextoQuery(q.CodigoTipoNomina,q.CodigoPeriodo,q.FechaNomina);
        var date=RhNomFuentesHistoricas.Validar(query,company);
        using var cn=await RhNomCfgDb.Abrir(db,ct);
        var context=await RhNomHistoricoFuentesHandler.Leer(cn,query,company,date,ct);
        using var cmd=new OracleCommand("RH.SP_RH_NOM_POB_HIST",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
        RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Int32,q.CodigoTipoNomina);
        RhNomCfgDb.Add(cmd,"p_PERIODO",OracleDbType.Int32,q.CodigoPeriodo);RhNomCfgDb.Add(cmd,"p_FECHA",OracleDbType.Date,date);
        cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var reader=await cmd.ExecuteReaderAsync(ct);var rows=new List<RhNomPersonaArchivo>();
        int Exact(string name)=>RhNomPoblaciones.Entero(reader.GetOracleDecimal(reader.GetOrdinal(name)));
        int? Optional(string name)=>reader.IsDBNull(reader.GetOrdinal(name))?null:Exact(name);
        while(await reader.ReadAsync(ct))
        {
            if(rows.Count==RhNomPoblaciones.MaxPersonas)throw new RhNomError("LIMITE_POBLACION_HISTORICA","El universo supera 5000 grupos de persona; no se entrega una poblacion truncada.");
            rows.Add(new(Optional("CODIGO_PERSONA"),Exact("FILAS_CARGO"),Exact("RELACIONES"),
                Exact("REL_INVALIDAS"),Optional("RELACION_UNICA"),Exact("PARTIDAS")));
        }
        return new ResultDto<RhNomPoblacionHistorica>(RhNomPoblaciones.Analizar(context,rows))
            {IsValid=true,Message="Universo historico completo recuperado. Candidato un cargo no acredita entradas originales ni comparacion de resultados."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoPoblacion")][RequestSizeLimit(4096)]
    public Task<IActionResult> HistoricalPopulation(RhNomPoblacionHistoricaQuery q)=>RunAsync("rh.nomina.historico.datos",
        (e,_,ct)=>new RhNomPoblacionHistoricaHandler(db).Handle(q,e,ct));
}
