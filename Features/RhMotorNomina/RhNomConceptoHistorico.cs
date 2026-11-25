using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomConceptoHistoricoQuery(int CodigoTipoNomina,int CodigoPeriodo,string FechaNomina,int CodigoConcepto);
public record RhNomImagenConcepto(int? CodigoAuditoria,int? CodigoEmpresa,int? CodigoConcepto,int? CodigoTipoNomina,
    DateTime? FechaEvento,DateTime? FechaInsercionOriginal,string? TipoConcepto,int? FrecuenciaId,int? ModuloId,int? Automatico);
public record RhNomAuditoriaConcepto(DateTime? FechaEvento,int EventosSinFecha,int TotalCandidatos,List<RhNomImagenConcepto> Imagenes);
public record RhNomConceptoCandidato(RhNomContextoHistorico Contexto,int CodigoConcepto,string Estado,
    RhNomAuditoriaConcepto Auditoria,RhNomImagenConcepto? Candidato,List<RhNomDiagnostico> Observaciones)
{
    public string Fuente => "RH.RH_H_CONCEPTOS";
    public string VersionMetodo => "OLD_POSTERIOR_V1";
    public bool CoberturaTemporalVerificada => false;
    public bool DisponibleParaCalculo => false;
    public bool ParidadCertificada => false;
}
public static class RhNomConceptosHistoricos
{
    public static DateTime Validar(RhNomConceptoHistoricoQuery value,int company)
    {
        var date=RhNomFuentesHistoricas.Validar(new(value.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina),company);
        if(value.CodigoConcepto<=0)throw new RhNomError("CONCEPTO_REQUERIDO","Indique un concepto positivo y explicito.");
        return date;
    }
    public static RhNomConceptoCandidato Resolver(RhNomConceptoHistoricoQuery value,int company,RhNomAuditoriaConcepto audit)
    {
        var date=Validar(value,company);
        var context=new RhNomContextoHistorico(company,value.CodigoTipoNomina,value.CodigoPeriodo,date);
        RhNomConceptoCandidato Stop(string state,string code,string message)=>new(context,value.CodigoConcepto,state,audit,null,[new(code,message)]);
        if(audit.EventosSinFecha<0 || audit.TotalCandidatos<0 || audit.Imagenes is null || audit.Imagenes.Count>3)
            throw new RhNomError("AUDITORIA_INVALIDA","Resumen de auditoria inconsistente.");
        if(audit.EventosSinFecha>0)return Stop("NO_RECONSTRUIBLE","AUDITORIA_SIN_FECHA","Hay eventos sin fecha; no se puede establecer el orden temporal.");
        if(audit.FechaEvento is null)
        {
            if(audit.TotalCandidatos!=0 || audit.Imagenes.Count!=0)throw new RhNomError("AUDITORIA_INVALIDA","Hay imagenes sin fecha de evento.");
            return Stop("SIN_EVENTO_POSTERIOR","SIN_IMAGEN_POSTERIOR","No hay imagen OLD en o despues de la referencia. No se sustituye por el catalogo actual ni por una imagen anterior.");
        }
        if(audit.FechaEvento.Value.Kind!=DateTimeKind.Unspecified || audit.FechaEvento<date)
            throw new RhNomError("AUDITORIA_INVALIDA","La fecha elegida debe estar en o despues de la referencia Oracle, sin zona.");
        if(audit.TotalCandidatos<=0 || audit.Imagenes.Count!=Math.Min(audit.TotalCandidatos,3))
            throw new RhNomError("AUDITORIA_INVALIDA","El total de candidatos no coincide con las imagenes recuperadas.");
        foreach(var image in audit.Imagenes)
            if(image is null || image.CodigoEmpresa!=company || image.CodigoConcepto!=value.CodigoConcepto || image.FechaEvento!=audit.FechaEvento || image.FechaEvento?.Kind!=DateTimeKind.Unspecified)
                throw new RhNomError("CONTEXTO_DISTINTO","Una imagen no pertenece a la empresa/concepto/fecha seleccionados.");
        if(audit.FechaEvento==date)return Stop("AMBIGUO","EVENTO_MISMO_SEGUNDO","Hay un cambio en el mismo segundo de la referencia. No se presume su orden respecto al calculo.");
        if(audit.TotalCandidatos>1)return Stop("AMBIGUO","EMPATE_AUDITORIA","Hay varias imagenes en la primera fecha posterior. No se elige por ID ni por coincidencia de valores.");
        var candidate=audit.Imagenes[0];
        if(candidate.CodigoTipoNomina!=value.CodigoTipoNomina)
            return Stop("NO_RECONSTRUIBLE","TIPO_HISTORICO_DISTINTO","La primera imagen posterior pertenece a otro tipo de nomina. No se omite para buscar otra que coincida.");
        if(candidate.CodigoAuditoria is null or <=0 || candidate.TipoConcepto is not ("A" or "D") || candidate.FrecuenciaId is null or <=0)
            return Stop("NO_RECONSTRUIBLE","IMAGEN_INCOMPLETA","La imagen tiene identificador, tipo A/D o frecuencia incompletos/no soportados.");
        if(candidate.FechaInsercionOriginal is { } inserted && (inserted.Kind!=DateTimeKind.Unspecified || inserted>date))
            return Stop("NO_RECONSTRUIBLE","INSERCION_POSTERIOR","La insercion registrada es posterior a la referencia o tiene zona incompatible.");
        return new(context,value.CodigoConcepto,"CANDIDATO_NO_CERTIFICADO",audit,candidate,
            [new("COBERTURA_AUDITORIA_PENDIENTE","Imagen OLD candidata. Falta probar continuidad de auditoria y el momento real del calculo; no habilita clasificacion ni frecuencia para ejecutar nomina.")]);
    }
}
public sealed class RhNomConceptoHistoricoHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomConceptoCandidato>> Get(RhNomConceptoHistoricoQuery value,int company,CancellationToken ct)=>
        RhNomCfgDb.Resultado(async()=>
        {
            var date=RhNomConceptosHistoricos.Validar(value,company);
            using var cn=await RhNomCfgDb.Abrir(db,ct);
            await RhNomHistoricoFuentesHandler.Leer(cn,new(value.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina),company,date,ct);
            using var cmd=new OracleCommand("RH.SP_RH_NOM_CON_HIST",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
            RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Int32,value.CodigoTipoNomina);
            RhNomCfgDb.Add(cmd,"p_PERIODO",OracleDbType.Int32,value.CodigoPeriodo);RhNomCfgDb.Add(cmd,"p_FECHA",OracleDbType.Date,date);
            RhNomCfgDb.Add(cmd,"p_CONCEPTO",OracleDbType.Int32,value.CodigoConcepto);cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            using var reader=await cmd.ExecuteReaderAsync(ct);var images=new List<RhNomImagenConcepto>();DateTime? eventDate=null;int? missing=null;int? total=null;
            int? Num(string name)=>reader.IsDBNull(reader.GetOrdinal(name))?null:RhNomCfgDb.Int(reader,name);
            DateTime? Date(string name)=>reader.IsDBNull(reader.GetOrdinal(name))?null:DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(name)),DateTimeKind.Unspecified);
            while(await reader.ReadAsync(ct))
            {
                var nextDate=Date("FECHA_EVENTO");var nextMissing=RhNomCfgDb.Int(reader,"EVENTOS_SIN_FECHA");var nextTotal=RhNomCfgDb.Int(reader,"TOTAL_CANDIDATOS");
                if(total is not null && (eventDate!=nextDate || missing!=nextMissing || total!=nextTotal))
                    throw new RhNomError("AUDITORIA_INVALIDA","Resumen de auditoria inconsistente entre filas.");
                eventDate=nextDate;missing=nextMissing;total=nextTotal;
                if(eventDate is not null)
                {
                    if(images.Count>=3)throw new RhNomError("AUDITORIA_INVALIDA","Se excedio el limite de imagenes de diagnostico.");
                    images.Add(new(Num("CODIGO_H_CONCEPTO"),Num("CODIGO_EMPRESA"),Num("CODIGO_CONCEPTO"),Num("CODIGO_TIPO_NOMINA"),
                        Date("FECHA_INS_H"),Date("FECHA_INS"),reader.IsDBNull(reader.GetOrdinal("TIPO_CONCEPTO"))?null:RhNomCfgDb.Text(reader,"TIPO_CONCEPTO"),Num("FRECUENCIA_ID"),Num("MODULO_ID"),Num("AUTOMATICO")));
                }
            }
            if(total is null)throw new RhNomError("CONCEPTO_FUERA_CONTEXTO","El concepto no tiene partidas en el contexto cerrado autorizado.");
            var result=RhNomConceptosHistoricos.Resolver(value,company,new(eventDate,missing!.Value,total.Value,images));
            return new ResultDto<RhNomConceptoCandidato>(result){IsValid=true,Message="Diagnostico de imagen anterior de concepto realizado; sin clasificacion historica certificada."};
        });
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoConceptoGet")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> HistoricalConcept(RhNomConceptoHistoricoQuery value)=>
        RunAsync("rh.nomina.historico.leer",(company,_,ct)=>new RhNomConceptoHistoricoHandler(db).Get(value,company,ct));
}
