using System.Data;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomBasePeriodoDoc(int Formato,RhNomCargoHistorico Cargo,RhNomAcumuladorActual Filtros,
    List<int> SalidasModelo,List<RhNomPartidaAcumHist> Partidas);
public record RhNomBasePeriodo(RhNomBasePeriodoDoc Documento,List<RhNomFilaAcum> Filas,string Total,string Hash)
{
    public string VersionMetodo=>RhNomBasesPeriodo.Version;
    public string Origen=>"RH.RH_HISTORICO_NOMINA_PERIODO";
    public bool ClasificacionHistoricaVerificada=>false;
    public bool IncluyeSalidasModelo=>false;
    public bool ParidadCertificada=>false;
}
// Historical input preparation only. The model's own expected outputs are never
// read into this base. Generated values must come from SUMA_SALIDAS in the graph.
public static class RhNomBasesPeriodo
{
    public const string Version="BASE_PERIODO_HISTORICO_V1";
    public static List<int> Salidas(IReadOnlyList<int> ids)
    {
        if(ids.Count is <1 or >100||ids.Any(id=>id<=0)||ids.Distinct().Count()!=ids.Count)
            throw new RhNomError("SALIDAS_BASE_REQUERIDAS","Declare de 1 a 100 conceptos de salida distintos para impedir usar resultados esperados como entradas.");
        return ids.Order().ToList();
    }
    public static RhNomBasePeriodo Capturar(RhNomBasePeriodoDoc value,CancellationToken ct=default)
    {
        if(value.Formato!=1||value.Cargo is null||value.Filtros is null||value.Partidas is null||value.SalidasModelo is null)
            throw new RhNomError("BASE_PERIODO_INVALIDA","Documento de base historica incompleto o incompatible.");
        var cargo=value.Cargo;var context=cargo.Contexto;
        if(context.CodigoPeriodo is null or <=0)throw new RhNomError("PERIODO_BASE_REQUERIDO","Periodo historico explicito requerido.");
        RhNomCargoSnapshot.Validar(new(context.CodigoTipoNomina,context.CodigoPeriodo.Value,context.FechaNomina.ToString("yyyy-MM-dd'T'HH:mm:ss"),cargo.CodigoPersona,cargo.CodigoRelacionCargo),context.CodigoEmpresa,[cargo]);
        var filter=RhNomAcumuladores.Validar(value.Filtros);var excluded=Salidas(value.SalidasModelo);
        // Group is frozen model metadata, not claimed to be its original historical state.
        if(filter.Grupo is {} group)RhNomGruposAcum.ValidarUso(group,context.CodigoEmpresa,context.CodigoTipoNomina,group.FechaReferencia);
        if(value.Partidas.Count>RhNomAcumuladores.MaxFilas)throw new RhNomError("LIMITE_BASE_PERIODO","Hasta 2000 partidas por base; no se devuelve una suma truncada.");
        if(value.Partidas.Any(r=>r is null||r.Partida is null))throw new RhNomError("PARTIDA_BASE_INVALIDA","Partida historica nula.");
        var ids=new HashSet<int>();var rows=new List<RhNomFilaAcum>();decimal total=0;var included=0;
        foreach(var item in value.Partidas.OrderBy(r=>r.Partida.Id))
        {
            ct.ThrowIfCancellationRequested();var r=item.Partida;
            if(item.CodigoPeriodo!=context.CodigoPeriodo||item.FechaNomina!=context.FechaNomina||item.FechaCierre is null||r.Id is null or <=0||!ids.Add(r.Id.Value)||r.CodigoEmpresa!=context.CodigoEmpresa||r.CodigoTipoNomina!=context.CodigoTipoNomina||r.CodigoPersona!=cargo.CodigoPersona||r.CodigoConcepto is null||!filter.Conceptos.Contains(r.CodigoConcepto.Value))
                throw new RhNomError("PARTIDA_BASE_INVALIDA","Partida historica duplicada o ajena al periodo/persona/conceptos; no se deduplica.");
            if(excluded.Contains(r.CodigoConcepto.Value))throw new RhNomError("SALIDA_ESPERADA_EN_BASE","La base contiene una salida del modelo; no se usan importes esperados como entradas.");
            var reason=filter.ConceptosExcluidos!.Contains(r.CodigoConcepto.Value)?"CONCEPTO_EXCLUIDO":!filter.Estados.Contains(r.Estado??"<NULL>")?"ESTADO_EXCLUIDO":!filter.TiposMovimiento.Contains(r.TipoMovimiento??"")?"MOVIMIENTO_EXCLUIDO":filter.PoliticaFrecuencias=="LISTA"&&(r.FrecuenciaId is null||!filter.Frecuencias!.Contains(r.FrecuenciaId.Value))?"FRECUENCIA_EXCLUIDA":"INCLUIDA";
            int? multiplicity=filter.Grupo?.PoliticaDatos=="LEGACY_OBSERVADO"?filter.Grupo.Filas.Count(f=>f.CodigoConcepto==r.CodigoConcepto):null;
            if(reason=="INCLUIDA"){
                if(r.Monto is null)throw new RhNomError("MONTO_BASE_NULO","Una partida incluida tiene monto nulo; no se sustituye por cero.");
                var amount=RhNomNumeros.Leer(r.Monto);for(var i=0;i<(multiplicity??1);i++)total=RhNomAcumuladores.SumarExacto(total,amount);included++;
            }
            rows.Add(new(r,reason=="INCLUIDA",reason,multiplicity));
        }
        if(included==0&&filter.SinFilas=="ERROR")throw new RhNomError("BASE_PERIODO_VACIA","Sin partidas incluidas y sin cero explicito.");
        var canonical=value with{Filtros=filter,SalidasModelo=excluded,Partidas=value.Partidas.OrderBy(r=>r.Partida.Id).ToList()};
        var result=new RhNomBasePeriodo(canonical,rows,RhNomNumeros.Texto(total),"");
        return result with{Hash=RhNomRecalculosHistoricos.Hash(new{result.Documento,result.Filas,result.Total,Version})};
    }
    public static void Verificar(RhNomBasePeriodo value)
    {
        var restored=Capturar(value.Documento);
        if(value.Hash!=restored.Hash||value.Total!=restored.Total||JsonSerializer.Serialize(value.Filas)!=JsonSerializer.Serialize(restored.Filas))
            throw new RhNomError("BASE_PERIODO_ALTERADA","Base historica, filtros o traza distintos de su huella.");
    }
    public static async Task<RhNomBasePeriodo> Leer(OracleConnection cn,RhNomCargoHistorico cargo,RhNomAcumuladorActual filters,List<int> outputs,CancellationToken ct)
    {
        var q=RhNomAcumuladores.Validar(filters);var excluded=Salidas(outputs);var context=cargo.Contexto;
        if(context.CodigoPeriodo is null or <=0)throw new RhNomError("PERIODO_BASE_REQUERIDO","Periodo historico explicito requerido.");
        RhNomCargoSnapshot.Validar(new(context.CodigoTipoNomina,context.CodigoPeriodo.Value,context.FechaNomina.ToString("yyyy-MM-dd'T'HH:mm:ss"),cargo.CodigoPersona,cargo.CodigoRelacionCargo),context.CodigoEmpresa,[cargo]);
        using var c=new OracleCommand("RH.SP_RH_NOM_BASE_PER",cn){CommandType=CommandType.StoredProcedure,BindByName=true,CommandTimeout=25};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,context.CodigoEmpresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,context.CodigoTipoNomina);
        RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,context.CodigoPeriodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,context.FechaNomina);
        RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,cargo.CodigoPersona);RhNomCfgDb.Add(c,"p_RELACION",OracleDbType.Int32,cargo.CodigoRelacionCargo);
        RhNomCfgDb.Add(c,"p_CONCEPTOS",OracleDbType.Varchar2,string.Join(",",q.Conceptos));RhNomCfgDb.Add(c,"p_SALIDAS",OracleDbType.Varchar2,string.Join(",",excluded));
        c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);var rows=new List<RhNomPartidaAcumHist>();
        using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))
        {
            if(rows.Count>=RhNomAcumuladores.MaxFilas)throw new RhNomError("LIMITE_BASE_PERIODO","Mas de 2000 partidas; reduzca conceptos.");
            int? I(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Int(r,n);string? S(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Text(r,n);
            rows.Add(new(context.CodigoPeriodo!.Value,context.FechaNomina,r.GetDateTime(r.GetOrdinal("FECHA_CIERRE")),new(I("ID_FILA"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_TIPO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_PERSONA"),I("CODIGO_CONCEPTO"),I("FRECUENCIA_ID"),S("TIPO"),S("STATUS"),null,RhNomReferencias.NormalizarMontoOracle(S("MONTO_TEXTO")))));
        }
        return Capturar(new(1,cargo,q,excluded,rows),ct);
    }
}
