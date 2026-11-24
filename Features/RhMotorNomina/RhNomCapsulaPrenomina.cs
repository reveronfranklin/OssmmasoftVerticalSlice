using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomAmbitoCapsula(RhNomContextoSeleccion Periodo,int CodigoPresupuesto,int CodigoPersona);
public record RhNomMetadatosCapsula(List<RhNomPersonalSeleccion> Personal,List<RhNomFrecuenciaSeleccion> Frecuencias,List<RhNomConceptoSeleccion> Conceptos);
public record RhNomPeriodoCapturado(string Metodo,string Clase,int? PresupuestoPeriodo,DateTime? FechaPreCierre,string Aislamiento,string PoliticaPresupuesto="COINCIDIR_PERIODO",string? MotivoPresupuesto=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomContratoPresupuesto? ContratoPresupuesto=null);
public record RhNomFuentesModeloCapturadas(int ModeloId,int Version,string HashModelo,string VersionFuentes,RhNomCargoActual Fuente,List<RhNomEntradaActual> Entradas);
public record RhNomCapsulaDoc(string Version,RhNomAmbitoCapsula Ambito,RhNomSeleccionDef Definicion,
    List<RhNomImagenMovimiento> Imagenes,RhNomMetadatosCapsula Metadatos,bool HayAmbitoAjeno,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] List<RhNomDeclaracionManualE>? DeclaracionesManuales=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? VersionDeclaracionesManuales=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomPeriodoCapturado? PeriodoCapturado=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomFuentesModeloCapturadas? FuentesModelo=null);
public record RhNomCapsula(string Json,string Hash);
public record RhNomPostEstadoPrenomina(string HashCapsula,string HashImagenesPosteriores,RhNomTotalesPrenominaResultado Totales,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomTratamientoEspecial? TratamientoEspecial=null)
{
    public bool TieneTratamientoEPendiente=>TratamientoEspecial?.Entradas.Any(e=>e.Tratamiento=="REQUIERE_REGLA_EXPLICITA"
        &&Totales.Seleccion.Filas.Any(f=>f.Imagen.Id==e.Imagen.Id&&f.Resultado=="INCLUIDA"))==true;
    public bool TieneBloqueos=>Totales.TieneBloqueos||TieneTratamientoEPendiente;
    public bool MovimientosAplicados=>false;
    public bool PropiedadFinancieraAcreditada=>false;
    public bool ProductorEspecialAcreditado=>false;
    public bool PeriodoOriginalDelMovimientoAcreditado=>false;
    public bool PresupuestoOriginalDelMovimientoAcreditado=>false;
    public bool ParidadHistoricaCertificada=>false;
}
public static class RhNomCapsulasPrenomina
{
    public const string Version="CAPSULA_PRENOMINA_V1";
    public const string VersionManuales="CAPSULA_PRENOMINA_V2";
    public const string VersionServidor="CAPSULA_PRENOMINA_V3";
    public const string VersionModelo="CAPSULA_PRENOMINA_V4";
    public const string VersionContrato="CAPSULA_PRENOMINA_V5";
    public const int MaxBytes=2*1024*1024;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,PropertyNameCaseInsensitive=false};
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static RhNomMetadatosCapsula Ordenar(RhNomMetadatosCapsula m)=>new(
        m.Personal.OrderBy(p=>p.CodigoPersona).ThenBy(p=>p.CodigoRelacionCargo).ThenBy(p=>p.Estado,StringComparer.Ordinal).ToList(),
        m.Frecuencias.OrderBy(f=>f.Id).ToList(),m.Conceptos.OrderBy(c=>c.CodigoConcepto).ToList());
    public static RhNomCapsula Capturar(RhNomAmbitoCapsula scope,RhNomSeleccionDef definition,RhNomCapturaMovimientos capture,RhNomMetadatosCapsula metadata,IReadOnlyList<RhNomDeclaracionManualE>? declarations=null,RhNomPeriodoCapturado? period=null,RhNomFuentesModeloCapturadas? modelSources=null)
    {
        if(scope?.Periodo is null||scope.CodigoPresupuesto<=0||scope.CodigoPersona<=0||capture?.Imagenes is null||metadata?.Personal is null||metadata.Frecuencias is null||metadata.Conceptos is null)
            throw new RhNomError("CAPSULA_CONTEXTO_INVALIDO","Contexto, presupuesto, persona, imagenes y metadatos completos requeridos.");
        if(capture.Imagenes.Any(i=>i is null||i.CodigoPersona!=scope.CodigoPersona)||metadata.Personal.Any(p=>p is null||p.CodigoPersona!=scope.CodigoPersona))
            throw new RhNomError("CAPSULA_PERSONA_AJENA","Las fuentes no pertenecen a la persona explicita.");
        // Reuse source/selection validation; preserve unresolved rows for diagnosis.
        var check=RhNomTotalesPrenomina.Preparar(scope.Periodo,definition,capture.Imagenes,metadata.Personal,metadata.Frecuencias,metadata.Conceptos);
        var manual=RhNomManualesEspeciales.Validar(scope.Periodo,declarations,metadata.Conceptos);
        if(period is not null&&(period.Metodo!="CAPTURA_SERVIDOR_V1"||period.Aislamiento!="SERIALIZABLE"||period.Clase is not ("N" or "E")
            ||!RhNomPresupuestos.PoliticaValida(period.PoliticaPresupuesto)||period.MotivoPresupuesto?.Length>500
            ||period.MotivoPresupuesto is {} reason&&(string.IsNullOrWhiteSpace(reason)||reason!=reason.Trim())
            ||period.PresupuestoPeriodo!=scope.CodigoPresupuesto&&period.PoliticaPresupuesto!=RhNomPresupuestos.PoliticaContrato&&(period.PoliticaPresupuesto!="TIPO_ACTUAL_EXPLICITO"||string.IsNullOrWhiteSpace(period.MotivoPresupuesto)||period.MotivoPresupuesto.Length<10)
            ||period.FechaPreCierre is {} pre&&pre.Kind!=DateTimeKind.Unspecified
            ||RhNomRecalculosHistoricos.Hash(check.Seleccion.Definicion)!=RhNomRecalculosHistoricos.Hash(PlantillaCanonica(period.Clase))))
            throw new RhNomError("CAPSULA_PERIODO_INVALIDO","Metodo, clase, aislamiento, presupuesto o fecha del periodo capturado invalidos.");
        if(period?.PoliticaPresupuesto==RhNomPresupuestos.PoliticaContrato? !RhNomPresupuestos.ContratoVerificado(period,scope.CodigoPresupuesto):period?.ContratoPresupuesto is not null)
            throw new RhNomError("CAPSULA_CONTRATO_PRESUPUESTO","Contrato de origen ausente, alterado o incompatible con el presupuesto del periodo.");
        if(modelSources is not null)
        {
            var f=modelSources.Fuente;
            if(period is null||modelSources.ModeloId<=0||modelSources.Version<=0||modelSources.VersionFuentes!=RhNomFuentesActuales.Version
                ||modelSources.HashModelo is null||modelSources.HashModelo.Length!=64||!modelSources.HashModelo.All(c=>char.IsAsciiHexDigitUpper(c))
                ||f is null||f.VersionFuente!=modelSources.VersionFuentes||f.CodigoEmpresa!=scope.Periodo.CodigoEmpresa||f.CodigoTipoNomina!=scope.Periodo.CodigoTipoNomina
                ||f.CodigoPeriodo!=scope.Periodo.CodigoPeriodo||f.FechaNomina!=scope.Periodo.FechaNomina||f.CodigoPresupuesto!=scope.CodigoPresupuesto||f.CodigoPersona!=scope.CodigoPersona
                ||f.FechaPreCierre!=period.FechaPreCierre||f.PresupuestoPeriodo!=period.PresupuestoPeriodo||f.CodigoRelacionCargo<=0
                ||!metadata.Personal.Any(p=>p.CodigoRelacionCargo==f.CodigoRelacionCargo)||modelSources.Entradas is null||modelSources.Entradas.Count>1000
                ||modelSources.Entradas.Any(e=>e is null)||modelSources.Entradas.Select(e=>e.CodigoVariable).Distinct(StringComparer.Ordinal).Count()!=modelSources.Entradas.Count)
                throw new RhNomError("CAPSULA_FUENTES_MODELO_INVALIDAS","Revision, contexto, relacion y evidencias del modelo deben pertenecer a la misma captura.");
        }
        var doc=new RhNomCapsulaDoc(period?.ContratoPresupuesto is not null?VersionContrato:modelSources is not null?VersionModelo:period is not null?VersionServidor:manual.Count>0?VersionManuales:Version,scope,check.Seleccion.Definicion,capture.Imagenes.OrderBy(i=>i.Id).ToList(),Ordenar(metadata),capture.HayAmbitoAjeno,manual.Count>0?manual:null,manual.Count>0?RhNomManualesEspeciales.Version:null,period,modelSources);
        var text=JsonSerializer.Serialize(doc,Json);if(Encoding.UTF8.GetByteCount(text)>MaxBytes)throw new RhNomError("CAPSULA_LIMITE","La capsula excede 2 MiB; no se trunca.");
        return new(text,Hash(text));
    }
    public static RhNomCapsulaDoc Leer(RhNomCapsula capsule)
    {
        if(capsule?.Json is null||capsule.Hash is null||Encoding.UTF8.GetByteCount(capsule.Json)>MaxBytes||Hash(capsule.Json)!=capsule.Hash)
            throw new RhNomError("CAPSULA_ALTERADA","Documento incompleto o distinto de su huella.");
        RhNomCapsulaDoc doc;
        try{doc=JsonSerializer.Deserialize<RhNomCapsulaDoc>(capsule.Json,Json)??throw new JsonException();}
        catch(JsonException){throw new RhNomError("CAPSULA_JSON_INVALIDO","Documento de capsula invalido o con campos desconocidos.");}
        var hasManual=doc.DeclaracionesManuales?.Count>0;
        var expectedVersion=doc.PeriodoCapturado?.ContratoPresupuesto is not null?VersionContrato:doc.FuentesModelo is not null?VersionModelo:doc.PeriodoCapturado is not null?VersionServidor:hasManual?VersionManuales:Version;
        if(doc.Version!=expectedVersion||(hasManual?doc.VersionDeclaracionesManuales!=RhNomManualesEspeciales.Version:doc.DeclaracionesManuales is not null||doc.VersionDeclaracionesManuales is not null))throw new RhNomError("CAPSULA_VERSION_INVALIDA","Version de capsula o declaracion manual no soportada.");
        var rebuilt=Capturar(doc.Ambito,doc.Definicion,new(doc.Imagenes,doc.HayAmbitoAjeno),doc.Metadatos,doc.DeclaracionesManuales,doc.PeriodoCapturado,doc.FuentesModelo);
        if(rebuilt.Json!=capsule.Json)throw new RhNomError("CAPSULA_NO_CANONICA","Documento distinto de su representacion canonica validada.");
        return doc;
    }
    public static RhNomTotalesPrenominaResultado Reproducir(RhNomCapsula capsule)
    {
        var d=Leer(capsule);return RhNomTotalesPrenomina.Preparar(d.Ambito.Periodo,d.Definicion,d.Imagenes,d.Metadatos.Personal,d.Metadatos.Frecuencias,d.Metadatos.Conceptos);
    }
    private static RhNomSeleccionDef PlantillaCanonica(string clase)
    {
        var d=RhNomSeleccionPrenomina.Plantilla(clase);
        return d with{TiposMovimiento=d.TiposMovimiento.Order(StringComparer.Ordinal).ToList(),EstadosPersonal=d.EstadosPersonal.Order(StringComparer.Ordinal).ToList()};
    }
    // Internal postcondition only: neither a writer nor an authorization/ownership boundary.
    public static RhNomPostEstadoPrenomina EvaluarDespues(RhNomCapsula capsule,RhNomAmbitoCapsula scope,
        IReadOnlyList<RhNomCambioAplicado> ordinaryChanges,RhNomCapturaMovimientos actual,RhNomMetadatosCapsula freshMetadata)
    {
        var d=Leer(capsule);
        if(scope?.Periodo is null||scope.Periodo.FechaNomina.Kind!=DateTimeKind.Unspecified||d.Ambito!=scope)throw new RhNomError("CAPSULA_OTRO_CONTEXTO","La capsula no pertenece a persona, presupuesto o periodo solicitados.");
        if(d.HayAmbitoAjeno||actual?.Imagenes is null||actual.HayAmbitoAjeno||actual.Imagenes.Any(i=>i is null||i.CodigoEmpresa!=scope.Periodo.CodigoEmpresa||i.CodigoTipoNomina!=scope.Periodo.CodigoTipoNomina||i.CodigoPersona!=scope.CodigoPersona||i.FechaIns is {} fi&&fi.Kind!=DateTimeKind.Unspecified||i.FechaUpd is {} fu&&fu.Kind!=DateTimeKind.Unspecified))
            throw new RhNomError("CAPSULA_POST_AMBITO_AJENO","La comprobacion posterior requiere imagenes del mismo ambito sin colisiones ajenas.");
        if(freshMetadata?.Personal is null||freshMetadata.Frecuencias is null||freshMetadata.Conceptos is null
            ||freshMetadata.Personal.Count>2000||freshMetadata.Frecuencias.Count>2000||freshMetadata.Conceptos.Count>2000||freshMetadata.Personal.Any(p=>p is null)||freshMetadata.Frecuencias.Any(f=>f is null)||freshMetadata.Conceptos.Any(c=>c is null)
            ||RhNomRecalculosHistoricos.Hash(Ordenar(d.Metadatos))!=RhNomRecalculosHistoricos.Hash(Ordenar(freshMetadata)))
            throw new RhNomError("CAPSULA_METADATOS_CAMBIARON","Los metadatos cambiaron; requiere captura y evaluacion nuevas.");
        if(ordinaryChanges is null||ordinaryChanges.Count>2000||ordinaryChanges.Any(c=>c is null))throw new RhNomError("CAMBIOS_ORDINARIOS_INVALIDOS","Hasta 2000 cambios ordinarios completos requeridos.");
        foreach(var c in ordinaryChanges)
        {
            if(c.Accion switch{"CREAR"=>c.Antes is not null||c.Despues is null,"RETIRAR"=>c.Antes is null||c.Despues is not null,"ACTUALIZAR"=>c.Antes is null||c.Despues is null,_=>true})
                throw new RhNomError("CAMBIO_ORDINARIO_INVALIDO","Accion e imagenes anteriores/posteriores incompatibles.");
            foreach(var image in new[]{c.Antes,c.Despues}.OfType<RhNomImagenMovimiento>())
            {
                if(image.Tipo is not ("F" or "V"))throw new RhNomError("ENTRADA_E_PROTEGIDA","Un cambio ordinario no puede crear, modificar o retirar E.");
                RhNomPlanesFinancieros.Hash(image);
                if(image.CodigoEmpresa!=scope.Periodo.CodigoEmpresa||image.CodigoTipoNomina!=scope.Periodo.CodigoTipoNomina||image.CodigoPersona!=scope.CodigoPersona)throw new RhNomError("CAMBIO_ORDINARIO_AJENO","Cambio de otra empresa, tipo o persona.");
            }
            if(c.Antes is {} a&&c.Despues is {} b&&(a.Id,a.CodigoConcepto,a.FrecuenciaId,a.Tipo)!=(b.Id,b.CodigoConcepto,b.FrecuenciaId,b.Tipo))throw new RhNomError("CAMBIO_ORDINARIO_RECLASIFICA","No se cambia identidad ni destino fisico en una actualizacion.");
        }
        var protectedBefore=d.Imagenes.Where(i=>i.Tipo=="E").OrderBy(i=>i.Id).ToList();var protectedAfter=actual.Imagenes.Where(i=>i.Tipo=="E").OrderBy(i=>i.Id).ToList();
        if(RhNomRecalculosHistoricos.Hash(protectedBefore)!=RhNomRecalculosHistoricos.Hash(protectedAfter))throw new RhNomError("ENTRADAS_E_CAMBIARON","Se agregaron, retiraron o alteraron imagenes E protegidas.");
        RhNomCambiosFinancieros.VerificarResultado(d.Imagenes,ordinaryChanges,actual);
        var totals=RhNomTotalesPrenomina.Preparar(scope.Periodo,d.Definicion,actual.Imagenes,freshMetadata.Personal,freshMetadata.Frecuencias,freshMetadata.Conceptos);
        var treatment=RhNomManualesEspeciales.Proyectar(scope.Periodo,d.DeclaracionesManuales,freshMetadata.Conceptos,actual.Imagenes);
        return new(capsule.Hash,RhNomRecalculosHistoricos.Hash(actual.Imagenes.OrderBy(i=>i.Id).ToList()),totals,treatment);
    }
}
