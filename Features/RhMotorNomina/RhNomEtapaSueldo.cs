namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// An independent, replayable diagnostic stage. It never rewrites the originating
// capture or feeds its output into PH/recalculation without an explicit stage plan.
public record RhNomDocumentoEtapaSueldo(RhNomModeloVersion ModeloOrigen,
    RhNomCorridaCaptura CapturaOrigen,RhNomModeloVersion ModeloSueldo);
public record RhNomResultadoEtapaSueldo(string VersionMetodo,int IdCapsula,string HashCapsula,
    string HashModeloOrigen,string HashModeloSueldo,string HashCargo,
    List<RhNomEntradaActual> Entradas,RhNomResultadoVigente Simulacion)
{
    public bool MovimientosAplicados=>false;
    public bool AptaParaAplicacion=>false;
    public bool BasesPosterioresIntegradas=>false;
    public bool ParidadHistoricaCertificada=>false;
}
public static class RhNomEtapasSueldo
{
    public const string Version="ETAPA_SUELDO_CAPTURADA_V1";
    public static RhNomCapsulaDoc Verificar(RhNomDocumentoEtapaSueldo value,
        int company,int actor,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();
        if(value?.ModeloSueldo is null)throw new RhNomError("ETAPA_SUELDO_INCOMPLETA","Revision de sueldo y captura de origen requeridas.");
        var origin=value.ModeloOrigen;var source=value.CapturaOrigen;var salary=value.ModeloSueldo;
        RhNomModelosDesdeCaptura.Verificar(origin,source,company,actor);
        var doc=RhNomCapsulasPrenomina.Leer(source.Archivo.Capsula);
        RhNomModelosDesdeCaptura.VerificarFuentes(origin,doc,actor);
        if(doc.PeriodoCapturado!.Clase!="N"||salary.Escenario!="ADJUNTAR_CARGO"
            ||salary.CodigoTipoNomina!=origin.CodigoTipoNomina||salary.Configuracion.CodigoEmpresa!=company
            ||salary.Configuracion.CodigoTipoNomina!=salary.CodigoTipoNomina||salary.Configuracion.ModeloId!=salary.ModeloId)
            throw new RhNomError("ETAPA_SUELDO_CONTEXTO","La etapa requiere nomina ordinaria y revision ADJUNTAR_CARGO de la misma empresa/tipo.");
        var serialized=RhNomConfiguracion.Serializar(salary.Configuracion);
        RhNomConfiguracion.Leer(serialized.Text,salary.Hash,company,salary.ModeloId);
        var bindings=RhNomFuentesModelo.Resolver(salary.Configuracion,null);
        if(bindings.Count!=2||bindings.Count(b=>b.Proveedor=="SUELDO_RELACION_ACTUAL")!=1
            ||bindings.Count(b=>b.Proveedor=="FRECUENCIA_PAGO_ACTUAL")!=1
            ||(salary.Configuracion.AccionesControl?.Count??0)!=0
            ||salary.Configuracion.ReglasConcepto?.Count!=1)
            throw new RhNomError("ETAPA_SUELDO_FUENTES","Esta etapa admite sueldo/frecuencia capturados y un destino monetario; otros proveedores requieren otra etapa.");
        var rule=salary.Configuracion.ReglasConcepto.Single();
        if(rule.TipoMovimiento!="F"||!(origin.Configuracion.AccionesControl??[]).Any(a=>
            a.TipoMovimiento=="F"&&a.Concepto.CodigoConcepto==rule.Concepto.CodigoConcepto
            &&a.HashConceptoCapturado==rule.HashConceptoCapturado))
            throw new RhNomError("ETAPA_SUELDO_DESTINO","El sueldo debe corresponder al destino F y metadata del control de sueldo ya capturado.");
        return doc;
    }
    public static RhNomResultadoEtapaSueldo Evaluar(RhNomDocumentoEtapaSueldo value,int company,int actor,CancellationToken ct=default)
    {
        var doc=Verificar(value,company,actor,ct);var salary=value.ModeloSueldo;var origin=value.ModeloOrigen;var source=value.CapturaOrigen;
        var bindings=RhNomFuentesModelo.Resolver(salary.Configuracion,null);
        var cargo=doc.FuentesModelo!.Fuente;
        var entries=RhNomFuentesActuales.Resolver(salary.Configuracion,cargo,bindings,ct,
            doc.PeriodoCapturado!.PoliticaPresupuesto,doc.PeriodoCapturado.MotivoPresupuesto);
        var simulation=RhNomParametros.Simular(salary,DateOnly.FromDateTime(cargo.FechaNomina),
            entries.Select(e=>new RhNomValorEntrada(e.CodigoVariable,e.Valor)).ToList(),ct);
        return new(Version,source.Archivo.Cabecera.IdCapsula,source.Archivo.Capsula.Hash,
            origin.Hash,salary.Hash,RhNomFuentesActuales.Hash(cargo),entries,simulation);
    }
}
