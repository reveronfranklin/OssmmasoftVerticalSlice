using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomEstadoQuery(int ModeloId,int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomValidarRevision(int ModeloId,int Version,string HashModeloEsperado,int VersionEstadoEsperada,List<int> IdCorridas,string Motivo);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomPublicarRevision(int ModeloId,int Version,string HashModeloEsperado,int VersionEstadoEsperada,string HashValidacionEsperado,DateOnly Desde,DateOnly? Hasta,string Motivo);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomRetirarRevision(int ModeloId,int Version,string HashModeloEsperado,int VersionEstadoEsperada,string Motivo);
public record RhNomPruebaRevision(int IdRun,string Modo,string HashDatos,string HashResultado);
public record RhNomEvidenciaRevision(int ModeloId,int Version,string HashModelo,List<RhNomPruebaRevision> Pruebas,List<string> PuertasPendientes,string Hash,string Nivel="TECNICO")
{
    public string VersionMetodo {get;init;}=RhNomValidaciones.Version;
    public bool Publicable=>Nivel=="INTEGRAL"&&PuertasPendientes.Count==0;
    public bool ParidadCertificada=>Nivel=="INTEGRAL";
}
public record RhNomEstadoRevision(int ModeloId,int Version,string HashModelo,string Estado,int VersionEstado,int AutorConfiguracion,
    int? AutorValidacion,int? AutorPublicacion,DateTime? FechaValidacion,DateTime? FechaPublicacion,DateTime? FechaRetiro,
    DateOnly? Desde,DateOnly? Hasta,RhNomEvidenciaRevision? Evidencia,string? Motivo)
{
    public bool Publicable=>Estado=="VALIDADO"&&Evidencia?.Publicable==true;
    public bool MovimientosAplicados=>false;
}
public record RhNomAuditoriaRevision(int IdEvento,string Accion,string EstadoAnterior,string EstadoNuevo,int Actor,DateTime Fecha,string Motivo,string? HashValidacion);
public static class RhNomValidaciones
{
    public const string Version="VALIDACION_REVISION_V1";
    public const int MaxCorridas=20,MaxBytes=16384;
    public static string Motivo(string? reason)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length is <10 or >500)throw new RhNomError("MOTIVO_REQUERIDO","Documente el motivo (10..500 caracteres).");return reason.Trim();
    }
    public static void Contexto(int model,int version,string? hash,int stateVersion)
    {
        if(model<=0||version<=0||stateVersion<0||hash is null||hash.Length!=64||hash.Any(c=>!char.IsAsciiHexDigit(c)))throw new RhNomError("REVISION_INVALIDA","Modelo/version positivos, hash SHA-256 y version de estado no negativa requeridos.");
    }
    public static List<int> Corridas(List<int>? values)
    {
        if(values is null||values.Count is <1 or >MaxCorridas||values.Any(v=>v<=0)||values.Distinct().Count()!=values.Count)throw new RhNomError("PRUEBAS_REQUERIDAS","Indique 1..20 corridas positivas distintas completadas por el usuario validador.");return values.Order().ToList();
    }
    public static RhNomEvidenciaRevision Capturar(RhNomModeloVersion model,IReadOnlyList<(RhNomCorrida Run,RhNomCorridaDocumento Entrada,RhNomCorridaResultado Resultado)> cases,int company,int actor,bool aplicacionDisponible=false)
    {
        if(model.Configuracion.CodigoEmpresa!=company||actor<=0)throw new RhNomError("CONTEXTO_DISTINTO","Empresa/actor de validacion invalidos.");
        Corridas(cases.Select(c=>c.Run.IdRun).ToList());var proofs=new List<RhNomPruebaRevision>();
        foreach(var (run,input,result) in cases)
        {
            if(run.CodigoEmpresa!=company||run.Actor!=actor||run.ModeloId!=model.ModeloId||run.VersionModelo!=model.Version||run.Estado!="COMPLETADO"||run.Cancelar||run.HashResultado is null||!result.IsValid||input.Modelo.Hash!=model.Hash||input.Modelo.ModeloId!=model.ModeloId||input.Modelo.Version!=model.Version||input.CodigoEmpresa!=company||input.Actor!=actor||input.Modo!=run.Modo||result.Modo!=run.Modo)
                throw new RhNomError("PRUEBA_NO_ELEGIBLE","Corridas deben pertenecer al validador, modelo/hash/revision exactos y estar completadas sin error/cancelacion.");
            if(run.HashDatos.Length!=64||run.HashResultado.Length!=64)throw new RhNomError("PRUEBA_ALTERADA","Huellas de evidencia de corrida invalidas.");
            proofs.Add(new(run.IdRun,run.Modo,run.HashDatos,run.HashResultado));
        }
        var integral=cases.All(c=>c.Resultado.Comparacion?.ParidadCertificada==true);
        var gates=new List<string>();
        if(!integral){gates.Add("PARIDAD_HISTORICA_INTEGRAL_NO_CERTIFICADA");gates.Add("COBERTURA_FUNCIONAL_POR_TIPO_PENDIENTE");}
        try{RhNomComposicionGrupo.VerificarCapturas(model.Configuracion);}
        catch(RhNomError ex){gates.Add(ex.Diagnostico.Codigo);}
        if(model.Configuracion.AccionesControl?.Count>0)gates.Add("ACCIONES_CONTROL_SIN_VALIDACION_TRANSACCIONAL");
        if(!aplicacionDisponible)gates.Add("APLICACION_TRANSACCIONAL_NO_HABILITADA");
        if(model.Configuracion.ReglasConcepto is not {Count:>0})gates.Add("SIN_REGLAS_DE_CONCEPTO");
        if(model.Configuracion.FuentesActuales is not {Count:>0})gates.Add("SIN_FUENTES_VERSIONADAS");
        var evidence=new RhNomEvidenciaRevision(model.ModeloId,model.Version,model.Hash,proofs.OrderBy(p=>p.IdRun).ToList(),gates,"",integral?"INTEGRAL":"TECNICO");return evidence with{Hash=Huella(evidence)};
    }
    public static string Huella(RhNomEvidenciaRevision value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{value.ModeloId,value.Version,value.HashModelo,value.Pruebas,value.PuertasPendientes,value.Nivel,value.VersionMetodo,value.Publicable,value.ParidadCertificada},RhNomCorridas.Json)));
    public static void Verificar(RhNomEvidenciaRevision evidence,int model,int version,string modelHash)
    {
        if(evidence is null||evidence.ModeloId!=model||evidence.Version!=version||evidence.HashModelo!=modelHash||evidence.Pruebas is null||evidence.PuertasPendientes is null||evidence.VersionMetodo!=Version||evidence.Nivel is not ("TECNICO" or "INTEGRAL")||evidence.Nivel=="TECNICO"&&evidence.PuertasPendientes.Count==0)throw new RhNomError("VALIDACION_ALTERADA","Evidencia de validacion incompleta, ajena o alterada.");
        Corridas(evidence.Pruebas.Select(p=>p.IdRun).ToList());
        if(evidence.Hash!=Huella(evidence))throw new RhNomError("VALIDACION_ALTERADA","La huella de validacion no coincide.");
    }
    public static void Publicar(RhNomEstadoRevision state,RhNomPublicarRevision q,int actor)
    {
        Contexto(q.ModeloId,q.Version,q.HashModeloEsperado,q.VersionEstadoEsperada);Motivo(q.Motivo);RhNomParametros.Fecha(q.Desde);if(q.Hasta is {} until){RhNomParametros.Fecha(until);if(until<q.Desde)throw new RhNomError("VIGENCIA_INVALIDA","Hasta anterior a Desde.");}
        if(state.ModeloId!=q.ModeloId||state.Version!=q.Version||state.HashModelo!=q.HashModeloEsperado||state.VersionEstado!=q.VersionEstadoEsperada)throw new RhNomError("ESTADO_DESACTUALIZADO","Estado/hash de revision cambio; recargue antes de publicar.");
        if(state.Estado!="VALIDADO"||state.Evidencia is null)throw new RhNomError("VALIDACION_REQUERIDA","Valide la revision y sus pruebas antes de publicar.");
        Verificar(state.Evidencia,state.ModeloId,state.Version,state.HashModelo);
        if(q.HashValidacionEsperado!=state.Evidencia.Hash)throw new RhNomError("VALIDACION_DISTINTA","La validacion cambio; recargue.");
        if(actor<=0||actor==state.AutorConfiguracion)throw new RhNomError("APROBADOR_DISTINTO_REQUERIDO","El publicador debe ser distinto del configurador.");
        if(!state.Publicable)throw new RhNomError("PUBLICACION_NO_HABILITADA","Validacion tecnica registrada, pero faltan paridad historica integral, cobertura funcional y aplicacion transaccional. La revision no puede publicarse para liquidacion.");
    }
}
