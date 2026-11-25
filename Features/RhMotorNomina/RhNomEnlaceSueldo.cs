namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomDocumentoEnlaceSueldo(string Politica,RhNomDocumentoEtapaSueldo Etapa);
public record RhNomEntradaEnlaceSueldo(string CodigoVariable,string Proveedor,string ValorOriginal,string ValorDerivado,
    RhNomAcumulacion? AcumuladorOriginal=null,RhNomAcumulacion? AcumuladorDerivado=null,
    RhNomAgregadosPhCapturados? PhOriginal=null,RhNomAgregadosPhCapturados? PhDerivado=null)
{
    public string Origen=>"PROYECCION_ETAPA_SUELDO";
    public bool EsNuevaCapturaOracle=>false;
}
public record RhNomResultadoEnlaceSueldo(string VersionMetodo,string Politica,RhNomResultadoEtapaSueldo Sueldo,
    int IdMovimientoReferencia,string MontoOriginal,string MontoDerivado,List<RhNomEntradaEnlaceSueldo> Entradas,
    RhNomResultadoVigente Recalculo)
{
    public bool MovimientosAplicados=>false;
    public bool AptaParaAplicacion=>false;
    public bool PropiedadLegacyAsignada=>false;
    public bool ParidadHistoricaCertificada=>false;
}
public static class RhNomEnlacesSueldo
{
    public const string Version="ENLACE_SUELDO_CAPTURADO_V1";
    public const string Politica="SUELDO_PREVIO_A_RECALCULO";
    // Pure, explicit stage projection. Derived evidence is never archived as if
    // Oracle had supplied it or passed through the ACTUAL/capture trust boundary.
    public static RhNomResultadoEnlaceSueldo Evaluar(RhNomDocumentoEnlaceSueldo value,int company,int actor,CancellationToken ct=default)
    {
        if(value?.Politica!=Politica)throw new RhNomError("POLITICA_ENLACE_SUELDO","Declare explicitamente sueldo previo al recalculo.");
        var salary=RhNomEtapasSueldo.Evaluar(value.Etapa,company,actor,ct);
        if(salary.Simulacion.Simulacion.Errores.Count>0)throw new RhNomError("SUELDO_PREVIO_INVALIDO","El sueldo previo tiene errores; no se enlazan resultados parciales.");
        var part=salary.Simulacion.ProyeccionConceptos!.Partidas.Single();
        if(part.Accion is not ("REGISTRAR_RESULTADO" or "REGISTRAR_CERO")||part.MontoPropuesto is null)
            throw new RhNomError("SUELDO_PREVIO_SIN_IMPORTE","El enlace requiere un importe previo explicito; no representa retiros como cero.");
        var doc=RhNomCapsulasPrenomina.Leer(value.Etapa.CapturaOrigen.Archivo.Capsula);var frozen=doc.FuentesModelo!;
        var candidates=doc.Imagenes.Where(r=>r.CodigoConcepto==part.CodigoConcepto&&r.Tipo=="F").ToList();
        if(candidates.Count!=1)throw new RhNomError("SUELDO_ENLACE_AMBIGUO","Esta version requiere un movimiento F original unico; no elige filas ni inventa identidades.");
        var original=candidates[0];var originalAmount=RhNomNumeros.Texto(RhNomNumeros.Leer(RhNomReferencias.NormalizarMontoOracle(original.Monto)));
        if(original.FrecuenciaId!=part.FrecuenciaId||original.Estado!="A")throw new RhNomError("SUELDO_ENLACE_METADATA","El movimiento F debe tener frecuencia del destino y estado activo; no se reinterpretan metadatos.");
        var amount=RhNomNumeros.Texto(RhNomNumeros.Leer(part.MontoPropuesto));
        RhNomPartidaAcum Replace(RhNomPartidaAcum row)
        {
            if(row.Id!=original.Id)return row;
            if(row.CodigoEmpresa!=original.CodigoEmpresa||row.CodigoTipoNomina!=original.CodigoTipoNomina||row.CodigoPersona!=original.CodigoPersona
                ||row.CodigoConcepto!=original.CodigoConcepto||row.FrecuenciaId!=original.FrecuenciaId||row.TipoMovimiento!=original.Tipo||row.Estado!=original.Estado
                ||RhNomNumeros.Leer(row.Monto)!=RhNomNumeros.Leer(RhNomReferencias.NormalizarMontoOracle(original.Monto)))
                throw new RhNomError("SUELDO_ENLACE_IMAGEN","La referencia de sueldo difiere entre las fuentes originales.");
            return row with{Monto=amount};
        }
        var derived=new List<RhNomEntradaEnlaceSueldo>();var values=new List<RhNomValorEntrada>();
        foreach(var input in frozen.Entradas)
        {
            ct.ThrowIfCancellationRequested();var next=input.Valor;RhNomAcumulacion? accumulation=null;RhNomAgregadosPhCapturados? ph=null;
            if(input.Acumulacion is {} a)
            {
                if(a.Definicion.Conceptos.Contains(original.CodigoConcepto)&&a.Filas.Count(f=>f.Partida.Id==original.Id)!=1)
                    throw new RhNomError("SUELDO_ENLACE_COBERTURA","El acumulador no conserva la referencia original de sueldo.");
                accumulation=RhNomAcumuladores.Capturar(a.Definicion,frozen.Fuente,a.Filas.Select(f=>Replace(f.Partida)).ToList(),ct);next=accumulation.Total;
            }
            else if(input.AgregadosPh is {} p)
            {
                if(((p.Documento.Definicion.GrupoSi?.Conceptos??[]).Contains(original.CodigoConcepto)||p.Documento.Definicion.ConceptosSueldoAf.Contains(original.CodigoConcepto))
                    &&(p.Documento.Actuales.Count(r=>r.Id==original.Id)!=1||p.Documento.Actuales.Count(r=>r.CodigoConcepto==original.CodigoConcepto&&r.TipoMovimiento=="F")!=1))
                    throw new RhNomError("SUELDO_ENLACE_COBERTURA","PH no conserva una referencia F unica del sueldo original.");
                // Only the current salary changes before RH_SUELDO's equivalent.
                // Historical rows, reference month and SI/AF definitions are kept.
                ph=RhNomAgregadosPh.Capturar(p.Documento with{Actuales=p.Documento.Actuales.Select(Replace).ToList()},ct);
                next=ph.Valores[RhNomAgregadosPh.Proveedores[input.Proveedor]];
            }
            derived.Add(new(input.CodigoVariable,input.Proveedor,input.Valor,next,input.Acumulacion,accumulation,input.AgregadosPh,ph));
            values.Add(new(input.CodigoVariable,next));
        }
        var simulation=RhNomParametros.Simular(value.Etapa.ModeloOrigen,DateOnly.FromDateTime(frozen.Fuente.FechaNomina),values,ct);
        return new(Version,Politica,salary,original.Id,originalAmount,amount,derived,simulation);
    }
}
