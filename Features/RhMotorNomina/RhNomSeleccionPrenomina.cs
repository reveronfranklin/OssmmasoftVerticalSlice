namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomSeleccionDef(string Codigo,List<string> TiposMovimiento,string PoliticaPersonal,
    List<string> EstadosPersonal,string PrefijoSiempre);
public record RhNomContextoSeleccion(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPeriodo,DateTime FechaNomina,int NumeroPeriodo);
public record RhNomPersonalSeleccion(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPersona,int CodigoRelacionCargo,string? Estado);
public record RhNomFrecuenciaSeleccion(int Id,string? Codigo);
public record RhNomConceptoSeleccion(int CodigoEmpresa,int CodigoTipoNomina,int CodigoConcepto,string? TipoConcepto);
public record RhNomFilaSeleccion(RhNomImagenMovimiento Imagen,string Resultado,string Motivo,int CoincidenciasPersonal,List<int> RelacionesElegibles,int CoincidenciasCursor);
public record RhNomSeleccionPrenominaResultado(string VersionMetodo,string Hash,RhNomContextoSeleccion Contexto,
    RhNomSeleccionDef Definicion,List<RhNomFilaSeleccion> Filas)
{
    public bool TieneBloqueos=>Filas.Any(f=>f.Resultado=="BLOQUEADA");
    public bool MovimientosAplicados=>false;
    public bool TemporalGenerado=>false;
    public bool ImportesCalculados=>false;
    public bool EntradasHistoricasVerificadas=>false;
    public bool ParidadHistoricaCertificada=>false;
}
public static class RhNomSeleccionPrenomina
{
    public const string Version="SELECCION_PRENOMINA_V1";
    // Migration templates are explicit definitions, not payroll-type ID branches.
    public static RhNomSeleccionDef Plantilla(string clase)=>clase switch
    {
        "N"=>new("ORDINARIA_FV",["F","V"],"INCLUIR",["A","I","R"],"T"),
        "E"=>new("ESPECIAL_E",["E"],"EXCLUIR",["E"],"T"),
        _=>throw new RhNomError("CLASE_SELECCION_INVALIDA","Seleccione plantilla N o E; no se convierten tipos fisicos.")
    };
    public static RhNomSeleccionPrenominaResultado Preparar(RhNomContextoSeleccion context,RhNomSeleccionDef definition,
        IReadOnlyList<RhNomImagenMovimiento> images,IReadOnlyList<RhNomPersonalSeleccion> people,
        IReadOnlyList<RhNomFrecuenciaSeleccion> frequencies,IReadOnlyList<RhNomConceptoSeleccion> concepts)
    {
        if(context is null||context.CodigoEmpresa<=0||context.CodigoTipoNomina<=0||context.CodigoPeriodo<=0||context.FechaNomina.Kind!=DateTimeKind.Unspecified||context.FechaNomina.Year<1900||context.NumeroPeriodo is <0 or >9)
            throw new RhNomError("CONTEXTO_SELECCION_INVALIDO","Contexto positivo, fecha Oracle y numero de periodo de un digito requeridos; no se trunca.");
        if(definition is null||definition.TiposMovimiento is null||definition.TiposMovimiento.Count is <1 or >3||definition.TiposMovimiento.Any(t=>t is not ("F" or "V" or "E"))||definition.TiposMovimiento.Distinct().Count()!=definition.TiposMovimiento.Count
            ||definition.PoliticaPersonal is not ("INCLUIR" or "EXCLUIR")||definition.EstadosPersonal is null||definition.EstadosPersonal.Count is <1 or >20
            ||definition.EstadosPersonal.Any(s=>string.IsNullOrEmpty(s)||s.Length!=1)||definition.EstadosPersonal.Distinct().Count()!=definition.EstadosPersonal.Count||string.IsNullOrWhiteSpace(definition.PrefijoSiempre)||definition.PrefijoSiempre.Length!=1)
            throw new RhNomError("DEFINICION_SELECCION_INVALIDA","Tipos F/V/E, politica de personal, estados y prefijo explicitos requeridos.");
        RhNomNumeros.Codigo(definition.Codigo);
        if(images is null||people is null||frequencies is null||concepts is null||images.Count>2000||people.Count>2000||frequencies.Count>2000||concepts.Count>2000
            ||images.Any(i=>i is null)||people.Any(p=>p is null)||frequencies.Any(f=>f is null)||concepts.Any(c=>c is null))
            throw new RhNomError("FUENTES_SELECCION_INVALIDAS","Hasta 2000 filas completas por fuente; no se trunca ni ignora filas nulas.");
        if(images.Select(i=>i.Id).Distinct().Count()!=images.Count||frequencies.Any(f=>f.Id<=0)||frequencies.Select(f=>f.Id).Distinct().Count()!=frequencies.Count
            ||concepts.Any(c=>c.CodigoConcepto<=0)||concepts.Select(c=>c.CodigoConcepto).Distinct().Count()!=concepts.Count)
            throw new RhNomError("IDENTIDAD_SELECCION_AMBIGUA","Identidades de movimiento, frecuencia o concepto invalidas/repetidas; no se elige una por orden.");
        if(people.Any(p=>p.CodigoEmpresa!=context.CodigoEmpresa||p.CodigoTipoNomina!=context.CodigoTipoNomina||p.CodigoPersona<=0||p.CodigoRelacionCargo<=0)
            ||concepts.Any(c=>c.CodigoEmpresa!=context.CodigoEmpresa||c.CodigoTipoNomina!=context.CodigoTipoNomina))
            throw new RhNomError("FUENTE_SELECCION_AJENA","Metadatos ajenos al ambito explicito.");
        var personIndex=people.ToLookup(p=>p.CodigoPersona);var frequencyIndex=frequencies.ToDictionary(f=>f.Id);var conceptIndex=concepts.ToDictionary(c=>c.CodigoConcepto);
        var period=context.NumeroPeriodo.ToString(System.Globalization.CultureInfo.InvariantCulture);var rows=new List<RhNomFilaSeleccion>();
        foreach(var row in images.OrderBy(i=>i.Id))
        {
            if(row.CodigoEmpresa!=context.CodigoEmpresa||row.CodigoTipoNomina!=context.CodigoTipoNomina)throw new RhNomError("MOVIMIENTO_SELECCION_AJENO","Movimiento de otra empresa o tipo.");
            var eligible=personIndex[row.CodigoPersona].Where(p=>p.Estado is not null&&(definition.PoliticaPersonal=="INCLUIR"?definition.EstadosPersonal.Contains(p.Estado):!definition.EstadosPersonal.Contains(p.Estado))).Select(p=>p.CodigoRelacionCargo).Order().ToList();
            void Add(string result,string reason,int cursor=0)=>rows.Add(new(row,result,reason,eligible.Count,eligible,cursor));
            if(!definition.TiposMovimiento.Contains(row.Tipo??"")){Add("EXCLUIDA","TIPO_MOVIMIENTO");continue;}
            if(row.FrecuenciaId is not {} id||!frequencyIndex.TryGetValue(id,out var frequency)||string.IsNullOrEmpty(frequency.Codigo)){Add("BLOQUEADA","FRECUENCIA_NO_RESUELTA");continue;}
            // Oracle SUBSTR comparison is case sensitive; NULL status never matches <> E.
            var prefix=frequency.Codigo[..1];if(prefix!=definition.PrefijoSiempre&&prefix!=period){Add("EXCLUIDA","FRECUENCIA_PERIODO");continue;}
            if(eligible.Count==0){Add("EXCLUIDA","PERSONAL_NO_ELEGIBLE");continue;}
            if(!conceptIndex.TryGetValue(row.CodigoConcepto,out var concept)){Add("BLOQUEADA","CONCEPTO_NO_RESUELTO");continue;}
            if(eligible.Count>1){Add("BLOQUEADA","PERSONAL_MULTIPLE",eligible.Count);continue;}
            if(concept.TipoConcepto is not ("A" or "D")){Add("BLOQUEADA","CLASIFICACION_NO_RESUELTA",1);continue;}
            try{RhNomPlanesFinancieros.Hash(row);}
            catch(RhNomError){Add("BLOQUEADA","IDENTIDAD_IMAGEN_INVALIDA",1);continue;}
            try
            {
                var sign=RhNomTransaccion.Numero(row.Monto!).CompareTo(new Oracle.ManagedDataAccess.Types.OracleDecimal(0));
                if(concept.TipoConcepto=="A"&&sign<0||concept.TipoConcepto=="D"&&sign>0){Add("BLOQUEADA","SIGNO_INCOMPATIBLE",1);continue;}
            }
            catch(RhNomError){Add("BLOQUEADA","IMPORTE_NO_REPRESENTABLE",1);continue;}
            Add("INCLUIDA","CRITERIOS_CUMPLIDOS",1);
        }
        var orderedDefinition=definition with{TiposMovimiento=definition.TiposMovimiento.Order(StringComparer.Ordinal).ToList(),EstadosPersonal=definition.EstadosPersonal.Order(StringComparer.Ordinal).ToList()};
        var hash=RhNomRecalculosHistoricos.Hash(new{Version,Contexto=context,Definicion=orderedDefinition,Filas=rows,
            Personal=people.OrderBy(p=>p.CodigoPersona).ThenBy(p=>p.CodigoRelacionCargo).ThenBy(p=>p.Estado,StringComparer.Ordinal).ToList(),
            Frecuencias=frequencies.OrderBy(f=>f.Id).ToList(),Conceptos=concepts.OrderBy(c=>c.CodigoConcepto).ToList()});
        return new(Version,hash,context,orderedDefinition,rows);
    }
}
