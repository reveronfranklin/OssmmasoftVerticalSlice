namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Explicit migration input. Evidence is retained by the migration report; it is
// not certification of historical metadata or permission to adopt legacy rows.
public record RhNomBorradoPrevio(int CodigoConcepto,string HashConcepto,string Origen,string HashEvidencia,bool RetirarCero=false);
public static class RhNomRetiroPorOmision
{
    public static List<RhNomReglaConceptoSave> Aplicar(IReadOnlyList<RhNomReglaConceptoSave> rules,
        IReadOnlyList<RhNomBorradoPrevio> confirmed)
    {
        if(rules is null||confirmed is null||confirmed.Count>100||confirmed.Any(c=>c is null||c.CodigoConcepto<=0
            ||c.Origen is not ("GRUPO_DL" or "MANIFIESTO_EXPLICITO")||c.HashEvidencia is null||c.HashEvidencia.Length!=64||c.HashEvidencia.Any(x=>!char.IsAsciiHexDigit(x)))
            ||confirmed.Select(c=>c.CodigoConcepto).Distinct().Count()!=confirmed.Count)
            throw new RhNomError("BORRADOS_PREVIOS_INVALIDOS","Declare conceptos unicos con origen de borrado y huella de evidencia explicitos.");
        foreach(var c in confirmed)
            if(rules.Count(r=>r.CodigoConcepto==c.CodigoConcepto&&r.HashConceptoEsperado==c.HashConcepto)!=1)
                throw new RhNomError("BORRADO_SIN_REGLA","La evidencia debe corresponder al concepto y huella de una unica regla.");
        var targets=confirmed.ToDictionary(c=>c.CodigoConcepto);
        return rules.Select(r=>targets.TryGetValue(r.CodigoConcepto,out var policy)?r with{AccionNoAplica="RETIRAR_GENERADO",AccionCero=policy.RetirarCero?"RETIRAR_GENERADO":r.AccionCero}:r).ToList();
    }
}
