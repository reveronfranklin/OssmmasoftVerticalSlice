using System.Data;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomCapturarPrenomina(string ClaveSolicitud,int CodigoTipoNomina,int CodigoPeriodo,int CodigoPersona,string PoliticaPresupuesto="COINCIDIR_PERIODO",string? MotivoPresupuesto=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomCapsulaId(int IdCapsula);
public record RhNomManualServidor(int CodigoTipoNomina,int CodigoConcepto,string HashClasificacionEsperado,string ReferenciaDecision);
public record RhNomFuenteCaptura(RhNomAmbitoCapsula Ambito,string Clase,RhNomCapturaMovimientos Movimientos,RhNomMetadatosCapsula Metadatos,RhNomPeriodoCapturado? Periodo=null);
public record RhNomResumenCaptura(RhNomCapsulaCabecera Archivo,string Clase,string Estado,string HashSeleccion,string HashTotales,
    int Movimientos,int Incluidos,int Excluidos,int Bloqueados,int ManualesDeclaradas,int EIncluidosSinRegla,List<RhNomTotalPersona> Totales,bool Recuperada,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomPeriodoCapturado? PeriodoCapturado=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomPresupuestoCapturado? Presupuesto=null)
{
    public bool MovimientosAplicados=>false;
    public bool FormulasNominaRecalculadas=>false;
    public bool ParidadHistoricaCertificada=>false;
    public bool PropiedadFinancieraAcreditada=>false;
}
public static class RhNomCapturasServidor
{
    public static void Validar(RhNomCapturarPrenomina q,int company,int actor)
    {
        RhNomArchivoCapsulas.Alcance(company,actor);
        if(q is null||q.CodigoTipoNomina<=0||q.CodigoPeriodo<=0||q.CodigoPersona<=0)
            throw new RhNomError("CAPTURA_SOLICITUD_INVALIDA","Tipo, periodo y persona positivos requeridos.");
        RhNomArchivoCapsulas.Clave(q.ClaveSolicitud);
        if(!RhNomPresupuestos.PoliticaValida(q.PoliticaPresupuesto)||q.MotivoPresupuesto?.Length>500)
            throw new RhNomError("CAPTURA_POLITICA_PRESUPUESTO","Politica COINCIDIR_PERIODO/TIPO_ACTUAL_EXPLICITO y motivo de hasta 500 caracteres requeridos.");
    }
    public static List<RhNomDeclaracionManualE> Politica(IConfiguration config,int type)
    {
        List<RhNomManualServidor> all;
        try{all=config.GetSection("RhMotorNomina:BonosManualesE").Get<List<RhNomManualServidor>>()??[];}
        catch(Exception ex) when(ex is InvalidOperationException or FormatException){throw new RhNomError("POLITICA_MANUAL_INVALIDA","No se pudo interpretar la politica manual del servidor.");}
        if(type<=0||all.Count>1000||all.Any(d=>d is null||d.CodigoTipoNomina<=0||d.CodigoConcepto<=0
            ||d.HashClasificacionEsperado is null||!Regex.IsMatch(d.HashClasificacionEsperado,@"\A[A-F0-9]{64}\z")
            ||string.IsNullOrWhiteSpace(d.ReferenciaDecision)||d.ReferenciaDecision!=d.ReferenciaDecision.Trim()||d.ReferenciaDecision.Length>200)
            ||all.Select(d=>(d.CodigoTipoNomina,d.CodigoConcepto)).Distinct().Count()!=all.Count
            ||all.GroupBy(d=>d.CodigoTipoNomina).Any(g=>g.Count()>100))
            throw new RhNomError("POLITICA_MANUAL_INVALIDA","Declaraciones manuales distintas, acotadas y con huella/referencia requeridas en el servidor.");
        return all.Where(d=>d.CodigoTipoNomina==type).OrderBy(d=>d.CodigoConcepto)
            .Select(d=>new RhNomDeclaracionManualE(d.CodigoConcepto,d.HashClasificacionEsperado,d.ReferenciaDecision)).ToList();
    }
    public static RhNomCapsula Capturar(RhNomCapturarPrenomina q,int company,int actor,RhNomFuenteCaptura source,IReadOnlyList<RhNomDeclaracionManualE> declarations)
    {
        Validar(q,company,actor);
        if(source?.Ambito?.Periodo is null||source.Ambito.Periodo.CodigoEmpresa!=company||source.Ambito.Periodo.CodigoTipoNomina!=q.CodigoTipoNomina
            ||source.Ambito.Periodo.CodigoPeriodo!=q.CodigoPeriodo||source.Ambito.CodigoPersona!=q.CodigoPersona||source.Clase is not ("N" or "E")||source.Periodo is null||source.Periodo.Clase!=source.Clase
            ||source.Periodo.PoliticaPresupuesto!=q.PoliticaPresupuesto||source.Periodo.MotivoPresupuesto!=Motivo(q))
            throw new RhNomError("CAPTURA_FUENTE_AJENA","Fuentes fuera del contexto solicitado.");
        return RhNomCapsulasPrenomina.Capturar(source.Ambito,RhNomSeleccionPrenomina.Plantilla(source.Clase),source.Movimientos,source.Metadatos,declarations,source.Periodo);
    }
    public static string? Motivo(RhNomCapturarPrenomina q)=>string.IsNullOrWhiteSpace(q.MotivoPresupuesto)?null:q.MotivoPresupuesto.Trim();
    public static void PoliticaReintento(RhNomCapturarPrenomina q,RhNomCapsula capsule)
    {
        var p=RhNomCapsulasPrenomina.Leer(capsule).PeriodoCapturado;
        if(p is null||p.PoliticaPresupuesto!=q.PoliticaPresupuesto||p.MotivoPresupuesto!=Motivo(q))
            throw new RhNomError("CAPTURA_CLAVE_REUTILIZADA","La clave identifica otra politica/motivo o un archivo de otro flujo.");
    }
    public static void Reintento(RhNomCapturarPrenomina q,RhNomCapsulaCabecera h,int company,int actor)
    {
        Validar(q,company,actor);
        if(h?.Ambito?.Periodo is null||h.CodigoEmpresa!=company||h.Actor!=actor||h.ClaveSolicitud!=q.ClaveSolicitud||h.Ambito.Periodo.CodigoTipoNomina!=q.CodigoTipoNomina||h.Ambito.Periodo.CodigoPeriodo!=q.CodigoPeriodo||h.Ambito.CodigoPersona!=q.CodigoPersona)
            throw new RhNomError("CAPTURA_CLAVE_REUTILIZADA","La clave identifica otra persona, tipo o periodo; use una clave nueva.");
    }
    public static RhNomResumenCaptura Resumir(RhNomCapsulaArchivada saved,int company,int actor,bool recovered)
    {
        RhNomArchivoCapsulas.Verificar(saved.Cabecera,saved.Capsula,company,actor);
        var d=RhNomCapsulasPrenomina.Leer(saved.Capsula);var totals=RhNomCapsulasPrenomina.Reproducir(saved.Capsula);
        var treatment=RhNomManualesEspeciales.Proyectar(d.Ambito.Periodo,d.DeclaracionesManuales,d.Metadatos.Conceptos,d.Imagenes);
        var rows=totals.Seleccion.Filas;var pending=treatment.Entradas.Count(e=>e.Tratamiento=="REQUIERE_REGLA_EXPLICITA"&&rows.Any(f=>f.Imagen.Id==e.Imagen.Id&&f.Resultado=="INCLUIDA"));
        var state=totals.TieneBloqueos||pending>0||d.HayAmbitoAjeno||d.PeriodoCapturado is {} period&&period.PresupuestoPeriodo!=d.Ambito.CodigoPresupuesto&&!RhNomPresupuestos.ContratoVerificado(period,d.Ambito.CodigoPresupuesto)?"BLOQUEADO":totals.Personas.All(p=>p.Estado=="SIN_PARTIDAS")?"SIN_PARTIDAS":"DIAGNOSTICO";
        var clase=d.Definicion.Codigo=="ORDINARIA_FV"?"N":d.Definicion.Codigo=="ESPECIAL_E"?"E":"CONFIGURADA";
        return new(saved.Cabecera,clase,state,totals.Seleccion.Hash,totals.Hash,rows.Count,rows.Count(r=>r.Resultado=="INCLUIDA"),rows.Count(r=>r.Resultado=="EXCLUIDA"),
            rows.Count(r=>r.Resultado=="BLOQUEADA"),treatment.ManualesDeclaradas,pending,totals.Personas,recovered,d.PeriodoCapturado,RhNomPresupuestos.ContratoVerificado(d.PeriodoCapturado,d.Ambito.CodigoPresupuesto)?new(d.Ambito.CodigoPresupuesto,d.PeriodoCapturado!.PresupuestoPeriodo,RhNomPresupuestos.Contrato().Origen,d.PeriodoCapturado.PresupuestoPeriodo==d.Ambito.CodigoPresupuesto?"COINCIDE":"PERIODO_NO_INFORMADO",RhNomPresupuestos.Contrato().Referencia):null);
    }
}
public static class RhNomCapturaFuentesDb
{
    public static async Task<RhNomFuenteCaptura> Leer(OracleConnection cn,OracleTransaction tx,RhNomCapturarPrenomina q,int company,IReadOnlyList<RhNomDeclaracionManualE> declarations,CancellationToken ct)
    {
        if(tx.IsolationLevel!=IsolationLevel.Serializable)throw new RhNomError("CAPTURA_AISLAMIENTO","La captura requiere una transaccion serializable.");
        using var c=new OracleCommand("RH.SP_RH_NOM_CAP_FUE",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25,Transaction=tx};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,q.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,q.CodigoPeriodo);RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,q.CodigoPersona);
        RhNomCfgDb.Add(c,"p_MANUALES",OracleDbType.Varchar2,string.Join(",",declarations.Select(d=>d.CodigoConcepto.ToString(CultureInfo.InvariantCulture))));
        var foreign=RhNomCfgDb.Out(c,"p_AJENOS");
        OracleParameter Cursor(string name)=>c.Parameters.Add(name,OracleDbType.RefCursor,ParameterDirection.Output);
        var context=Cursor("p_CONTEXTO");var movements=Cursor("p_MOVIMIENTOS");var people=Cursor("p_PERSONAL");var frequencies=Cursor("p_FRECUENCIAS");var concepts=Cursor("p_CONCEPTOS");
        try
        {
        await c.ExecuteNonQueryAsync(ct);
        int I(OracleDataReader r,string name)=>RhNomCfgDb.Int(r,name);
        int? N(OracleDataReader r,string name)=>r.IsDBNull(r.GetOrdinal(name))?null:I(r,name);
        string? S(OracleDataReader r,string name)=>r.IsDBNull(r.GetOrdinal(name))?null:r.GetString(r.GetOrdinal(name));
        DateTime? D(OracleDataReader r,string name)=>r.IsDBNull(r.GetOrdinal(name))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(name)),DateTimeKind.Unspecified);
        async Task<List<T>> Read<T>(OracleParameter output,Func<OracleDataReader,T> map)
        {
            var cursor=(OracleRefCursor)output.Value;using var reader=cursor.GetDataReader();var rows=new List<T>();
            while(await reader.ReadAsync(ct)){if(rows.Count==2000)throw new RhNomError("CAPTURA_FUENTES_EXCESIVAS","Mas de 2000 filas en una fuente; no se trunca.");rows.Add(map(reader));}return rows;
        }
        var contexts=await Read(context,r=>new{Empresa=I(r,"CODIGO_EMPRESA"),Tipo=I(r,"CODIGO_TIPO_NOMINA"),Periodo=I(r,"CODIGO_PERIODO"),Fecha=D(r,"FECHA_NOMINA"),Numero=N(r,"NUM_PERIODO"),Clase=S(r,"TIPO_NOMINA"),Cierre=D(r,"FECHA_CIERRE"),Precierre=D(r,"FECHA_PRECIERRE"),Presupuesto=N(r,"CODIGO_PRESUPUESTO_ACTUAL"),PresupuestoPeriodo=N(r,"PRESUPUESTO_PERIODO")});
        if(contexts.Count!=1)throw new RhNomError("CAPTURA_PERIODO_AMBIGUO","Periodo/tipo no encontrados de forma unica en la empresa.");
        var p=contexts[0];
        if(p.Fecha is null||p.Numero is null||p.Presupuesto is null or <=0||p.Cierre is not null||p.Clase is not ("N" or "E"))
            throw new RhNomError("CAPTURA_PERIODO_NO_ABIERTO","Se requiere un periodo N/E sin cierre y presupuesto actual valido.");
        RhNomPresupuestos.Validar(p.PresupuestoPeriodo,p.Presupuesto.Value,q.PoliticaPresupuesto,RhNomCapturasServidor.Motivo(q));
        var images=await Read(movements,r=>new RhNomImagenMovimiento(I(r,"CODIGO_MOV_NOMINA"),I(r,"CODIGO_EMPRESA"),I(r,"CODIGO_TIPO_NOMINA"),I(r,"CODIGO_PERSONA"),I(r,"CODIGO_CONCEPTO"),
            N(r,"FRECUENCIA_ID"),S(r,"TIPO"),S(r,"MONTO_TEXTO"),S(r,"STATUS"),S(r,"COMPLEMENTO_CONCEPTO"),S(r,"EXTRA1"),S(r,"EXTRA2"),S(r,"EXTRA3"),N(r,"USUARIO_INS"),D(r,"FECHA_INS"),N(r,"USUARIO_UPD"),D(r,"FECHA_UPD")));
        var personal=await Read(people,r=>new RhNomPersonalSeleccion(I(r,"CODIGO_EMPRESA"),I(r,"CODIGO_TIPO_NOMINA"),I(r,"CODIGO_PERSONA"),I(r,"CODIGO_RELACION_CARGO"),S(r,"STATUS")));
        var freq=await Read(frequencies,r=>new RhNomFrecuenciaSeleccion(I(r,"DESCRIPCION_ID"),S(r,"CODIGO")));
        var con=await Read(concepts,r=>new RhNomConceptoSeleccion(I(r,"CODIGO_EMPRESA"),I(r,"CODIGO_TIPO_NOMINA"),I(r,"CODIGO_CONCEPTO"),S(r,"TIPO_CONCEPTO")));
        if(personal.Count==0&&images.Count==0)throw new RhNomError("CAPTURA_PERSONA_SIN_FUENTES","Persona sin fuentes para el tipo de nomina solicitado.");
        return new(new(new(p.Empresa,p.Tipo,p.Periodo,p.Fecha.Value,p.Numero.Value),p.Presupuesto.Value,q.CodigoPersona),p.Clase!,new(images,((OracleDecimal)foreign.Value).ToInt32()>0),new(personal,freq,con),new("CAPTURA_SERVIDOR_V1",p.Clase!,p.PresupuestoPeriodo,p.Precierre,"SERIALIZABLE",q.PoliticaPresupuesto,RhNomCapturasServidor.Motivo(q),q.PoliticaPresupuesto==RhNomPresupuestos.PoliticaContrato?RhNomPresupuestos.Contrato():null));
        }
        finally
        {
            foreach(var output in new[]{context,movements,people,frequencies,concepts})if(output.Value is OracleRefCursor cursor)cursor.Dispose();
        }
    }
}
public sealed class RhNomCapturaPrenominaHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomResumenCaptura>> Crear(RhNomCapturarPrenomina q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        RhNomCapturasServidor.Validar(q,company,actor);RhNomPresupuestos.Habilitada(config,q.PoliticaPresupuesto);await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var previous=await RhNomArchivoCapsulasDb.PorClave(cn,company,actor,q.ClaveSolicitud,ct);
        if(previous is not null)
        {
            RhNomCapturasServidor.Reintento(q,previous,company,actor);
            var original=await RhNomArchivoCapsulasDb.Leer(cn,company,actor,previous.IdCapsula,ct);RhNomCapturasServidor.PoliticaReintento(q,original.Capsula);
            return new ResultDto<RhNomResumenCaptura>(RhNomCapturasServidor.Resumir(original,company,actor,true)){IsValid=true,Message="Captura original recuperada; no se releen fuentes actuales."};
        }
        var manual=RhNomCapturasServidor.Politica(config,q.CodigoTipoNomina);RhNomCapsula capsule;
        using(var tx=cn.BeginTransaction(IsolationLevel.Serializable))
        {
            try{var source=await RhNomCapturaFuentesDb.Leer(cn,tx,q,company,manual,ct);capsule=RhNomCapturasServidor.Capturar(q,company,actor,source,manual);}
            finally{await tx.RollbackAsync(CancellationToken.None);}
        }
        var saved=await RhNomArchivoCapsulasDb.Guardar(cn,company,actor,q.ClaveSolicitud,capsule,ct);
        return new ResultDto<RhNomResumenCaptura>(RhNomCapturasServidor.Resumir(saved,company,actor,false)){IsValid=true,Message="Captura del servidor archivada para diagnostico; no se calcularon formulas ni se modificaron movimientos."};
    });
    public Task<ResultDto<RhNomCapsulaCabecera>> Get(RhNomCapsulaId q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        if(q is null)throw new RhNomError("ARCHIVO_CAPSULA_ID","Indique una capsula.");await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var saved=await RhNomArchivoCapsulasDb.Leer(cn,company,actor,q.IdCapsula,ct);return new ResultDto<RhNomCapsulaCabecera>(saved.Cabecera){IsValid=true,Message="Cabecera y documento verificados en el alcance del usuario."};
    });
    public Task<ResultDto<RhNomResumenCaptura>> Reproducir(RhNomCapsulaId q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        if(q is null)throw new RhNomError("ARCHIVO_CAPSULA_ID","Indique una capsula.");await using var cn=await RhNomCfgDb.Abrir(db,ct);
        return new ResultDto<RhNomResumenCaptura>(RhNomCapturasServidor.Resumir(await RhNomArchivoCapsulasDb.Leer(cn,company,actor,q.IdCapsula,ct),company,actor,true)){IsValid=true,Message="Seleccion y totales reproducidos desde el archivo; no se releen movimientos."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("capturaPrenominaCrear")][RequestSizeLimit(8192)]
    public Task<IActionResult> CaptureCreate(RhNomCapturarPrenomina q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCapturaPrenominaHandler(db,config).Crear(q,e,a,ct),"rh.nomina.fuentes.datos");
    [HttpPost("capturaPrenominaGet")][RequestSizeLimit(4096)]
    public Task<IActionResult> CaptureGet(RhNomCapsulaId q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCapturaPrenominaHandler(db,config).Get(q,e,a,ct),"rh.nomina.fuentes.datos");
    [HttpPost("capturaPrenominaReproducir")][RequestSizeLimit(4096)]
    public Task<IActionResult> CaptureReplay(RhNomCapsulaId q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCapturaPrenominaHandler(db,config).Reproducir(q,e,a,ct),"rh.nomina.fuentes.datos");
}
