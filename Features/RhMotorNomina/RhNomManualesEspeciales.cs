namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomDeclaracionManualE(int CodigoConcepto,string HashClasificacionEsperado,string ReferenciaDecision);
public record RhNomEntradaETratada(RhNomImagenMovimiento Imagen,string Tratamiento,string? ReferenciaDecision);
public record RhNomTratamientoEspecial(List<RhNomEntradaETratada> Entradas)
{
    public string VersionMetodo=>RhNomManualesEspeciales.Version;
    public int ManualesDeclaradas=>Entradas.Count(e=>e.Tratamiento=="CONSERVAR_VARIACION_MANUAL");
    public int PendientesDeRegla=>Entradas.Count(e=>e.Tratamiento=="REQUIERE_REGLA_EXPLICITA");
    public bool OrigenHistoricoIndependienteVerificado=>false;
    public bool PropiedadFinancieraAcreditada=>false;
}
public static class RhNomManualesEspeciales
{
    public const string Version="MANUALES_ESPECIALES_V1";
    public static List<RhNomDeclaracionManualE> Validar(RhNomContextoSeleccion scope,IReadOnlyList<RhNomDeclaracionManualE>? declarations,IReadOnlyList<RhNomConceptoSeleccion> concepts)
    {
        if(scope is null||scope.CodigoEmpresa<=0||scope.CodigoTipoNomina<=0)
            throw new RhNomError("AMBITO_MANUAL_E_INVALIDO","Empresa y tipo de nomina positivos requeridos.");
        if(concepts is null||concepts.Count>2000||concepts.Any(c=>c is null||c.CodigoConcepto<=0||c.CodigoEmpresa!=scope.CodigoEmpresa||c.CodigoTipoNomina!=scope.CodigoTipoNomina)
            ||concepts.Select(c=>c.CodigoConcepto).Distinct().Count()!=concepts.Count)
            throw new RhNomError("METADATOS_MANUAL_E_INVALIDOS","Hasta 2000 conceptos distintos del ambito requeridos; no se elige entre duplicados.");
        if(declarations is null||declarations.Count==0)return [];
        if(declarations.Count>100||declarations.Any(d=>d is null)||declarations.Select(d=>d.CodigoConcepto).Distinct().Count()!=declarations.Count)
            throw new RhNomError("DECLARACION_E_INVALIDA","Hasta cien conceptos manuales distintos con declaracion explicita requeridos.");
        foreach(var d in declarations)
        {
            var c=concepts.SingleOrDefault(c=>c.CodigoConcepto==d.CodigoConcepto);
            if(c is null||c.CodigoEmpresa!=scope.CodigoEmpresa||c.CodigoTipoNomina!=scope.CodigoTipoNomina||c.CodigoConcepto<=0||c.TipoConcepto is not ("A" or "D")||RhNomRecalculosHistoricos.Hash(c)!=d.HashClasificacionEsperado)
                throw new RhNomError("DECLARACION_E_METADATOS","Concepto del ambito y huella de clasificacion coincidentes requeridos.");
            if(string.IsNullOrWhiteSpace(d.ReferenciaDecision)||d.ReferenciaDecision!=d.ReferenciaDecision.Trim()||d.ReferenciaDecision.Length>200)
                throw new RhNomError("DECLARACION_E_SIN_REFERENCIA","Referencia de decision explicita de hasta 200 caracteres requerida.");
        }
        return declarations.OrderBy(d=>d.CodigoConcepto).ToList();
    }
    public static RhNomTratamientoEspecial Proyectar(RhNomContextoSeleccion scope,IReadOnlyList<RhNomDeclaracionManualE>? declarations,IReadOnlyList<RhNomConceptoSeleccion> concepts,IReadOnlyList<RhNomImagenMovimiento> images)
    {
        var declared=Validar(scope,declarations,concepts).ToDictionary(d=>d.CodigoConcepto);
        if(images is null||images.Count>2000||images.Any(i=>i is null)||images.Select(i=>i.Id).Distinct().Count()!=images.Count)
            throw new RhNomError("ENTRADAS_MANUAL_E_INVALIDAS","Hasta 2000 imagenes distintas y completas requeridas; no se trunca.");
        if(images.Any(i=>i.CodigoEmpresa!=scope.CodigoEmpresa||i.CodigoTipoNomina!=scope.CodigoTipoNomina))
            throw new RhNomError("ENTRADA_MANUAL_E_AJENA","Entrada fuera de la empresa/tipo declarados.");
        return new(images.Where(i=>i.Tipo=="E").OrderBy(i=>i.Id).Select(i=>
        {
            return declared.TryGetValue(i.CodigoConcepto,out var d)?new RhNomEntradaETratada(i,"CONSERVAR_VARIACION_MANUAL",d.ReferenciaDecision):new RhNomEntradaETratada(i,"REQUIERE_REGLA_EXPLICITA",null);
        }).ToList());
    }
}
