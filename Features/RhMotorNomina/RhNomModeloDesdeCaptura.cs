using System.Globalization;
using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomCrearCorridaCaptura(string ClaveSolicitud,int ModeloId,int Version,int IdCapsula);
public record RhNomCorridaCaptura(RhNomCapsulaArchivada Archivo);
public record RhNomEntradaDesdeCaptura(string CodigoVariable,string Proveedor,string Tipo,string Unidad,string Valor,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAcumulacion? Acumulacion=null);
public record RhNomResultadoModeloCaptura(string VersionMetodo,int IdCapsula,string HashCapsula,string EstadoCaptura,
    List<RhNomEntradaDesdeCaptura> Entradas,List<RhNomDiagnostico> Errores,RhNomResultadoVigente? Simulacion)
{
    public bool AptaParaAplicacion=>false;
    public bool MovimientosAplicados=>false;
    public bool ParidadHistoricaCertificada=>false;
    public string OrigenEntradas=>"CAPSULA_ARCHIVADA_Y_PARAMETROS_REVISION";
}
public static class RhNomModelosDesdeCaptura
{
    public const string Modo="CAPTURA_PRENOMINA";
    public const string Version="MODELO_DESDE_CAPTURA_V1";
    public static void Validar(RhNomCrearCorridaCaptura q,int company,int actor)
    {
        RhNomArchivoCapsulas.Alcance(company,actor);
        if(q is null||q.ModeloId<=0||q.Version<=0||q.IdCapsula<=0)throw new RhNomError("CORRIDA_CAPTURA_INVALIDA","Modelo, revision y capsula positivos requeridos.");
        RhNomArchivoCapsulas.Clave(q.ClaveSolicitud);
    }
    public static string HashSolicitud(RhNomCrearCorridaCaptura q,int company,int actor)
    {Validar(q,company,actor);return RhNomRecalculosHistoricos.Hash(new{Version,CodigoEmpresa=company,Actor=actor,Solicitud=q});}
    public static void Verificar(RhNomModeloVersion model,RhNomCorridaCaptura source,int company,int actor)
    {
        if(model?.Configuracion is null||source?.Archivo is null)throw new RhNomError("CORRIDA_CAPTURA_INCOMPLETA","Revision y captura archivada requeridas.");
        RhNomArchivoCapsulas.Verificar(source.Archivo.Cabecera,source.Archivo.Capsula,company,actor);
        var doc=RhNomCapsulasPrenomina.Leer(source.Archivo.Capsula);
        if(doc.Version is not (RhNomCapsulasPrenomina.VersionServidor or RhNomCapsulasPrenomina.VersionModelo or RhNomCapsulasPrenomina.VersionContrato)||doc.PeriodoCapturado is null||model.CodigoTipoNomina!=doc.Ambito.Periodo.CodigoTipoNomina
            ||model.Configuracion.CodigoEmpresa!=company||model.Configuracion.CodigoTipoNomina!=model.CodigoTipoNomina||model.ModeloId!=model.Configuracion.ModeloId)
            throw new RhNomError("MODELO_CAPTURA_AJENO","Se requiere captura de servidor V3/V4/V5 y revision de la misma empresa/tipo.");
        var serialized=RhNomConfiguracion.Serializar(model.Configuracion);RhNomConfiguracion.Leer(serialized.Text,model.Hash,company,model.ModeloId);
    }
    public static RhNomCorridaDocumento VerificarFuentes(RhNomModeloVersion model,RhNomCapsulaDoc doc,int actor)
    {
        RhNomArchivoCapsulas.Alcance(doc.Ambito.Periodo.CodigoEmpresa,actor);
        var frozen=doc.FuentesModelo??throw new RhNomError("CAPTURA_FUENTES_REQUERIDAS","Faltan fuentes completas del modelo.");
        if(frozen.ModeloId!=model.ModeloId||frozen.Version!=model.Version||frozen.HashModelo!=model.Hash)throw new RhNomError("CAPTURA_OTRA_REVISION","Las fuentes fueron capturadas para otra revision; no se reinterpretan.");
        var input=new RhNomCorridaDocumento(1,doc.Ambito.Periodo.CodigoEmpresa,actor,model,"ACTUAL",null,frozen.Fuente,frozen.Entradas,doc.PeriodoCapturado!.PoliticaPresupuesto,doc.PeriodoCapturado.MotivoPresupuesto,null,null);
        RhNomCorridas.ValidarEntradasActuales(input);
        var bindings=RhNomFuentesModelo.Resolver(model.Configuracion,null);
        foreach(var b in bindings.Where(b=>b.Proveedor=="ACUMULADOR_MOV_ACTUAL"))
        {
            var filter=RhNomAcumuladores.Validar(b.Acumulador!);
            var rows=doc.Imagenes.Where(i=>filter.Conceptos.Contains(i.CodigoConcepto)).Select(i=>new RhNomPartidaAcum(i.Id,i.CodigoEmpresa,i.CodigoTipoNomina,i.CodigoPersona,i.CodigoConcepto,i.FrecuenciaId,i.Tipo,i.Estado,i.Complemento,RhNomReferencias.NormalizarMontoOracle(i.Monto))).ToList();
            var rebuilt=RhNomAcumuladores.Capturar(filter,frozen.Fuente,rows);
            var stored=frozen.Entradas.SingleOrDefault(e=>e.CodigoVariable==b.CodigoVariable)?.Acumulacion;
            if(stored is null||RhNomRecalculosHistoricos.Hash(stored)!=RhNomRecalculosHistoricos.Hash(rebuilt))throw new RhNomError("CAPTURA_MOVIMIENTOS_DISTINTOS","El acumulador no coincide con las imagenes capturadas en la misma transaccion.");
        }
        var all=frozen.Entradas;
        var rebuiltInputs=RhNomFuentesActuales.Resolver(model.Configuracion,frozen.Fuente,bindings,politicaPresupuesto:input.PoliticaPresupuesto!,motivoPresupuesto:input.MotivoPresupuesto,
            acumuladores:all.Where(e=>e.Acumulacion is not null).ToDictionary(e=>e.CodigoVariable,e=>e.Acumulacion!),
            parametrosConcepto:all.Where(e=>e.ParametroConcepto is not null).ToDictionary(e=>e.CodigoVariable,e=>e.ParametroConcepto!),
            controles:all.Where(e=>e.Control is not null).ToDictionary(e=>e.CodigoVariable,e=>e.Control!),
            historicos:all.Where(e=>e.AcumulacionHistorica is not null).ToDictionary(e=>e.CodigoVariable,e=>e.AcumulacionHistorica!),
            personales:all.Where(e=>e.DatosPersonales is not null).ToDictionary(e=>e.CodigoVariable,e=>e.DatosPersonales!),
            agregadosPh:all.Where(e=>e.AgregadosPh is not null).ToDictionary(e=>e.CodigoVariable,e=>e.AgregadosPh!));
        if(RhNomRecalculosHistoricos.Hash(rebuiltInputs)!=RhNomRecalculosHistoricos.Hash(all))throw new RhNomError("CAPTURA_ENTRADAS_DISTINTAS","Las entradas congeladas no coinciden con sus fuentes y politicas del modelo.");
        return input;
    }
    // Pure resolver: never calls Oracle, a legacy function or a current-data provider.
    public static RhNomResultadoModeloCaptura Evaluar(RhNomModeloVersion model,RhNomCorridaCaptura source,int company,int actor,CancellationToken ct=default)
    {
        Verificar(model,source,company,actor);var archived=source.Archivo;var doc=RhNomCapsulasPrenomina.Leer(archived.Capsula);
        var summary=RhNomCapturasServidor.Resumir(archived,company,actor,true);var snapshot=model.Configuracion;var errors=new List<RhNomDiagnostico>();var inputs=new List<RhNomEntradaDesdeCaptura>();
        var bindings=RhNomFuentesModelo.Validar(snapshot.FuentesActuales,snapshot.Variables).ToDictionary(b=>b.CodigoVariable,StringComparer.Ordinal);
        var targets=(snapshot.ReglasConcepto??[]).Select(r=>(Concepto:r.Concepto.CodigoConcepto,Tipo:r.TipoMovimiento)).ToHashSet();
        if(doc.Definicion.TiposMovimiento.Contains("E")&&(doc.DeclaracionesManuales??[]).Any(d=>targets.Contains((d.CodigoConcepto,"E"))))errors.Add(new("CAPTURA_SALIDA_MANUAL","El modelo intenta generar un concepto E declarado manual; preserve su importe fuera de las formulas."));
        if(doc.FuentesModelo is not null)
        {
            try
            {
                var frozenDoc=VerificarFuentes(model,doc,actor);
                foreach(var b in bindings.Values.Where(b=>b.Proveedor=="ACUMULADOR_MOV_ACTUAL"))
                {
                    var filter=RhNomAcumuladores.Validar(b.Acumulador!);
                    if(targets.Any(t=>filter.Conceptos.Contains(t.Concepto)&&filter.TiposMovimiento.Contains(t.Tipo)&&!filter.ConceptosExcluidos!.Contains(t.Concepto)))throw new RhNomError("CAPTURA_BASE_CONTIENE_SALIDA","La base incluye una salida del modelo sin exclusion explicita.");
                }
                if(errors.Count>0)return new(Version,archived.Cabecera.IdCapsula,archived.Capsula.Hash,summary.Estado,[],errors,null);
                var actual=RhNomCorridas.Evaluar(frozenDoc,ct).Actual!;
                var capturedInputs=actual.Entradas.Select(i=>{var v=snapshot.Variables.Single(v=>v.Codigo==i.CodigoVariable);return new RhNomEntradaDesdeCaptura(i.CodigoVariable,i.Proveedor,v.Tipo,i.Unidad,i.Valor,i.Acumulacion);}).ToList();
                return new(Version,archived.Cabecera.IdCapsula,archived.Capsula.Hash,summary.Estado,capturedInputs,actual.Simulacion.Simulacion.Errores,actual.Simulacion);
            }
            catch(RhNomError ex){errors.Add(ex.Diagnostico);return new(Version,archived.Cabecera.IdCapsula,archived.Capsula.Hash,summary.Estado,[],errors,null);}
        }
        foreach(var variable in snapshot.Variables.Where(v=>v.Origen=="ENTRADA").OrderBy(v=>v.Codigo,StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if(!bindings.TryGetValue(variable.Codigo,out var binding)){errors.Add(new("CAPTURA_VARIABLE_SIN_FUENTE","La revision no declara fuente para la entrada.",Variable:variable.Codigo));continue;}
            try
            {
                var context=doc.Ambito.Periodo;var date=context.FechaNomina;RhNomAcumulacion? accumulation=null;string value;
                switch(binding.Proveedor)
                {
                    case "FECHA_NOMINA_CONTEXTO":value=date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);break;
                    case "TIENE_PRECIERRE_CONTEXTO":value=doc.PeriodoCapturado!.FechaPreCierre is null?"false":"true";break;
                    case "TIPO_NOMINA_CONTEXTO":value=context.CodigoTipoNomina.ToString(CultureInfo.InvariantCulture);break;
                    case "ANIO_FECHA_NOMINA":value=date.Year.ToString(CultureInfo.InvariantCulture);break;
                    case "MES_FECHA_NOMINA":value=date.Month.ToString(CultureInfo.InvariantCulture);break;
                    case "DIAS_MES_NOMINA":value=DateTime.DaysInMonth(date.Year,date.Month).ToString(CultureInfo.InvariantCulture);break;
                    case "DIA_ISO_MES_NOMINA":
                        value=Enumerable.Range(1,DateTime.DaysInMonth(date.Year,date.Month)).Count(d=>(((int)new DateTime(date.Year,date.Month,d).DayOfWeek+6)%7)+1==binding.DiaSemanaIso).ToString(CultureInfo.InvariantCulture);break;
                    case "ACUMULADOR_MOV_ACTUAL":
                        var filter=RhNomAcumuladores.Validar(binding.Acumulador!);
                        if(targets.Any(t=>filter.Conceptos.Contains(t.Concepto)&&filter.TiposMovimiento.Contains(t.Tipo)&&!filter.ConceptosExcluidos!.Contains(t.Concepto)))
                            throw new RhNomError("CAPTURA_BASE_CONTIENE_SALIDA","El acumulador incluye una salida que el mismo modelo pretende generar; requiere exclusion explicita.");
                        var rows=doc.Imagenes.Where(i=>filter.Conceptos.Contains(i.CodigoConcepto)).Select(i=>new RhNomPartidaAcum(i.Id,i.CodigoEmpresa,i.CodigoTipoNomina,i.CodigoPersona,i.CodigoConcepto,i.FrecuenciaId,i.Tipo,i.Estado,i.Complemento,RhNomReferencias.NormalizarMontoOracle(i.Monto))).ToList();
                        accumulation=RhNomAcumuladores.Capturar(filter,new(context.CodigoEmpresa,context.CodigoTipoNomina,context.CodigoPeriodo,context.FechaNomina),doc.Ambito.CodigoPersona,rows,ct);value=accumulation.Total;break;
                    default:throw new RhNomError("CAPTURA_FUENTE_NO_DISPONIBLE","La capsula no conserva este proveedor; no se consulta su valor actual ni se usa cero.");
                }
                value=RhNomValores.Normalizar(variable.Tipo,value);RhNomFuentesModelo.ValidarValor(binding,variable,value);
                inputs.Add(new(variable.Codigo,binding.Proveedor,variable.Tipo,variable.Unidad,value,accumulation));
            }
            catch(RhNomError ex){errors.Add(ex.Diagnostico with{Variable=variable.Codigo});}
            catch(OverflowException){errors.Add(new("DESBORDAMIENTO","La entrada excede la precision de calculo; no se entrega un valor redondeado.",Variable:variable.Codigo));}
        }
        if(errors.Count>0)return new(Version,archived.Cabecera.IdCapsula,archived.Capsula.Hash,summary.Estado,[],errors,null);
        var simulation=RhNomParametros.Simular(model,DateOnly.FromDateTime(doc.Ambito.Periodo.FechaNomina),inputs.Select(i=>new RhNomValorEntrada(i.CodigoVariable,i.Valor)).ToList(),ct);
        return new(Version,archived.Cabecera.IdCapsula,archived.Capsula.Hash,summary.Estado,inputs,simulation.Simulacion.Errores,simulation);
    }
}
