using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomRecalcularHistorico(int ModeloId,int Version,int CodigoPeriodo,string FechaNomina,int CodigoPersona,int CodigoRelacionCargo,
    string PoliticaFuentes,bool Comparar=false,string? HashReferenciaEsperado=null);
public record RhNomEntradaHistorica(string CodigoVariable,string Valor,string Unidad,string ProveedorModelo,string ProveedorUsado,string Evidencia,string HuellaFuente,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomParametroConceptoCapturado? ParametroConcepto=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAcumHistorico? AcumulacionHistorica=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomBasePeriodo? BasePeriodo=null);
public record RhNomRecalculoHistorico(int ModeloId,int Version,string HashModelo,RhNomContextoFuente Contexto,RhNomCargoHistorico Cargo,
    List<RhNomEntradaHistorica> Entradas,string HashEntradas,RhNomResultadoVigente Simulacion,RhNomComparacionPrueba? Comparacion,List<string> Observaciones)
{
    public string VersionMetodo=>RhNomRecalculosHistoricos.Version;
    public string OrigenEntradas=>"FUENTES_HISTORICAS_RECONSTRUIDAS";
    public bool ParidadCertificada=>false;
    public bool MovimientosAplicados=>false;
}
public static class RhNomRecalculosHistoricos
{
    public const string Version="RECALCULO_HISTORICO_V6";
    public static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    public static List<RhNomFuenteActualBinding> Proveedores(RhNomModeloVersion model)
    {
        if(model.Configuracion.AccionesControl?.Count>0)throw new RhNomError("ACCIONES_CONTROL_HISTORICAS_NO_DISPONIBLES","Falta procedencia historica de controles para esta revision; no se ignoran las decisiones.");
        var sources=RhNomFuentesModelo.Validar(model.Configuracion.FuentesActuales,model.Configuracion.Variables);
        if(sources.Count==0)throw new RhNomError("MODELO_SIN_FUENTES","Guarde proveedores en una revision antes de reconstruir entradas historicas.");
        foreach(var b in sources)ValidarProveedor(b,model);
        return sources;
    }
    internal static string ValidarProveedor(RhNomFuenteActualBinding b,RhNomModeloVersion model)
    {
        if(b.Proveedor=="ACUMULADOR_MOV_ACTUAL"&&(model.Configuracion.ReglasConcepto is null||model.Configuracion.ReglasConcepto.Count==0))
            throw new RhNomError("SALIDAS_BASE_REQUERIDAS","El modelo necesita reglas de salida para excluir sus importes esperados de las bases.");
        if(b.AcumuladorHistorico?.Filtros.Grupo is {} group)RhNomGruposAcum.ValidarUso(group,model.Configuracion.CodigoEmpresa,model.CodigoTipoNomina,group.FechaReferencia,true);
        if(RhNomParametrosConcepto.Unidades.ContainsKey(b.Proveedor))
        {if(b.ParametroConcepto?.PoliticaVigencia!="FECHA_CONTEXTO")throw new RhNomError("VIGENCIA_HISTORICA_REQUERIDA","Parametros reconstruidos requieren FECHA_CONTEXTO en el modelo, sin cambiarla por solicitud.");}
        else if(b.Proveedor!="ACUMULADOR_MOV_ACTUAL"&&b.Proveedor!=RhNomAcumuladoresHistoricos.Proveedor&&b.Proveedor is not ("FECHA_INGRESO_ADMIN_UNICO" or "FRECUENCIA_PAGO_ACTUAL" or "FECHA_NOMINA_CONTEXTO" or "SUELDO_RELACION_ACTUAL" or "TIPO_NOMINA_CONTEXTO" or "ANIO_FECHA_NOMINA" or "MES_FECHA_NOMINA" or "DIAS_MES_NOMINA" or "DIA_ISO_MES_NOMINA"))
            throw new RhNomError("FUENTE_SIN_EQUIVALENTE_HISTORICO",$"[{b.CodigoVariable}] no tiene equivalente historico registrado; no se usa una lectura actual ni un valor esperado.");
        return b.Proveedor switch
        {
            "ACUMULADOR_MOV_ACTUAL"=>"BASE_PERIODO_HISTORICO",
            "SUELDO_RELACION_ACTUAL"=>"SUELDO_CARGO_SNAPSHOT",
            "FRECUENCIA_PAGO_ACTUAL"=>"FRECUENCIA_CARGO_SNAPSHOT",
            "FECHA_INGRESO_ADMIN_UNICO"=>"FECHA_INGRESO_CARGO_SNAPSHOT",
            _=>RhNomParametrosConcepto.Unidades.ContainsKey(b.Proveedor)?"PARAMETRO_CONCEPTO_FECHA":b.Proveedor
        };
    }
    public static List<RhNomEntradaHistorica> Resolver(RhNomModeloVersion model,RhNomContextoFuente context,RhNomCargoHistorico cargo,IReadOnlyDictionary<string,RhNomParametroConceptoCapturado> parameters,CancellationToken ct=default,IReadOnlyDictionary<string,RhNomAcumHistorico>? historicos=null,IReadOnlyDictionary<string,RhNomBasePeriodo>? basesPeriodo=null)
    {
        if(context.Contexto.CodigoEmpresa!=model.Configuracion.CodigoEmpresa||context.Contexto.CodigoTipoNomina!=model.CodigoTipoNomina||model.Configuracion.CodigoTipoNomina!=model.CodigoTipoNomina||cargo.Contexto!=context.Contexto)
            throw new RhNomError("CONTEXTO_DISTINTO","Modelo, periodo cerrado y cargo historico deben coincidir.");
        RhNomFuentesHistoricas.Resolver(model.Configuracion,context,[],ct,cargo);
        cargo=RhNomCargoSnapshot.Validar(new(model.CodigoTipoNomina,context.Contexto.CodigoPeriodo!.Value,context.Contexto.FechaNomina.ToString("yyyy-MM-dd'T'HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture),cargo.CodigoPersona,cargo.CodigoRelacionCargo),model.Configuracion.CodigoEmpresa,[cargo]);
        var result=new List<RhNomEntradaHistorica>();var variables=model.Configuracion.Variables.ToDictionary(v=>v.Codigo,StringComparer.Ordinal);
        foreach(var b in Proveedores(model))
        {
            ct.ThrowIfCancellationRequested();var variable=variables[b.CodigoVariable];
            if(b.Proveedor=="ACUMULADOR_MOV_ACTUAL")
            {
                if(basesPeriodo is null||!basesPeriodo.TryGetValue(b.CodigoVariable,out var captured))throw new RhNomError("BASE_PERIODO_NO_CAPTURADA","Falta evidencia de la base del periodo historico.");
                RhNomBasesPeriodo.Verificar(captured);
                var outputs=model.Configuracion.ReglasConcepto!.Select(r=>r.Concepto.CodigoConcepto).Distinct().Order().ToList();
                if(captured.Documento.Cargo!=cargo||!captured.Documento.SalidasModelo.SequenceEqual(outputs)||JsonSerializer.Serialize(captured.Documento.Filtros)!=JsonSerializer.Serialize(RhNomAcumuladores.Validar(b.Acumulador!)))
                    throw new RhNomError("BASE_PERIODO_DISTINTA","Cargo, filtros o conceptos de salida distintos del modelo historico.");
                result.Add(new(b.CodigoVariable,captured.Total,variable.Unidad,b.Proveedor,"BASE_PERIODO_HISTORICO",captured.Origen,captured.Hash,BasePeriodo:captured));continue;
            }
            if(RhNomParametrosConcepto.Unidades.ContainsKey(b.Proveedor))
            {
                if(!parameters.TryGetValue(b.CodigoVariable,out var captured))throw new RhNomError("PARAMETRO_NO_CAPTURADO","Falta evidencia del parametro reconstruido.");
                var value=RhNomParametrosConcepto.Valor(b.Proveedor,b.ParametroConcepto!,captured,context.Contexto);
                result.Add(new(b.CodigoVariable,value,variable.Unidad,b.Proveedor,"PARAMETRO_CONCEPTO_FECHA","RH.RH_FORMULA_CONCEPTOS",captured.Hash,captured));continue;
            }
            if(b.Proveedor==RhNomAcumuladoresHistoricos.Proveedor)
            {
                if(historicos is null||!historicos.TryGetValue(b.CodigoVariable,out var captured))throw new RhNomError("ACUMULADOR_HIST_NO_CAPTURADO","Falta evidencia del acumulador historico.");
                RhNomAcumuladoresHistoricos.Verificar(captured,context.Contexto,cargo.CodigoPersona);
                if(JsonSerializer.Serialize(captured.Definicion)!=JsonSerializer.Serialize(RhNomAcumuladoresHistoricos.Validar(b.AcumuladorHistorico!)))throw new RhNomError("ACUMULADOR_HIST_DISTINTO","Filtros capturados distintos del modelo.");
                result.Add(new(b.CodigoVariable,captured.Calculo.Total,variable.Unidad,b.Proveedor,b.Proveedor,captured.Origen,captured.Hash,AcumulacionHistorica:captured));continue;
            }
            if(b.Proveedor=="TIPO_NOMINA_CONTEXTO")
            {var value=RhNomNumeros.Texto(context.Contexto.CodigoTipoNomina);result.Add(new(b.CodigoVariable,value,variable.Unidad,b.Proveedor,b.Proveedor,"RH.RH_PERIODOS",Hash(context.Contexto)));continue;}
            if(b.Proveedor=="FECHA_INGRESO_ADMIN_UNICO"&&cargo.FechaIngreso is {} ingreso&&(ingreso.Year<1900||ingreso>context.Contexto.FechaNomina))
                throw new RhNomError("INGRESO_ADMIN_HIST_INVALIDO","El ingreso archivado debe ser desde 1900 y no posterior al contexto; no se usa FECHA_INI ni la fecha actual.");
            var provider=ValidarProveedor(b,model);
            var read=RhNomFuentesHistoricas.Resolver(model.Configuracion,context,[new(b.CodigoVariable,provider,b.DiaSemanaIso)],ct,cargo).Single();
            result.Add(new(b.CodigoVariable,read.Valor,read.Unidad,b.Proveedor,provider,read.Evidencia,provider is "SUELDO_CARGO_SNAPSHOT" or "FRECUENCIA_CARGO_SNAPSHOT" or "FECHA_INGRESO_CARGO_SNAPSHOT"?Hash(cargo):Hash(read)));
        }
        foreach(var e in result)RhNomFuentesModelo.ValidarValor(model.Configuracion.FuentesActuales!.Single(b=>b.CodigoVariable==e.CodigoVariable),variables[e.CodigoVariable],e.Valor);
        return result;
    }
}
public sealed class RhNomRecalculoHistoricoHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomRecalculoHistorico>> Handle(RhNomRecalcularHistorico q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        if(company<=0||q.ModeloId<=0||q.Version<=0||q.CodigoPeriodo<=0||q.CodigoPersona<=0||q.CodigoRelacionCargo<=0||q.PoliticaFuentes!="EQUIVALENTES_GUARDADOS")throw new RhNomError("CONTEXTO_INVALIDO","Indique ids positivos, fecha y politica EQUIVALENTES_GUARDADOS explicita.");
        var date=RhNomFuentesHistoricas.Fecha(q.FechaNomina);
        if(q.Comparar&&(q.HashReferenciaEsperado is null||q.HashReferenciaEsperado.Length!=64||q.HashReferenciaEsperado.Any(c=>!char.IsAsciiHexDigit(c))))throw new RhNomError("HASH_REFERENCIA_REQUERIDO","Capture primero la referencia para comparar contra su huella esperada.");
        using var cn=await RhNomCfgDb.Abrir(db,ct);var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);var sources=RhNomRecalculosHistoricos.Proveedores(model);
        var context=await RhNomHistoricoFuentesHandler.Leer(cn,new(model.CodigoTipoNomina,q.CodigoPeriodo,q.FechaNomina),company,date,ct);
        var cargo=await RhNomCargoHistoricoHandler.Leer(cn,new(model.CodigoTipoNomina,q.CodigoPeriodo,q.FechaNomina,q.CodigoPersona,q.CodigoRelacionCargo),company,date,ct);
        var parameters=new Dictionary<string,RhNomParametroConceptoCapturado>(StringComparer.Ordinal);var cache=new Dictionary<RhNomParametroConceptoBinding,RhNomParametroConceptoCapturado>();
        foreach(var b in sources.Where(b=>RhNomParametrosConcepto.Unidades.ContainsKey(b.Proveedor)))
        {var def=b.ParametroConcepto!;if(!cache.TryGetValue(def,out var read)){read=await RhNomParametrosConcepto.Leer(cn,context.Contexto,def,ct);cache.Add(def,read);}parameters.Add(b.CodigoVariable,read);}
        var histories=new Dictionary<string,RhNomAcumHistorico>(StringComparer.Ordinal);var historyRows=0;
        foreach(var b in sources.Where(b=>b.Proveedor==RhNomAcumuladoresHistoricos.Proveedor))
        {
            var read=await RhNomAcumuladoresHistoricos.Leer(cn,context.Contexto,cargo.CodigoPersona,b.AcumuladorHistorico!,ct);historyRows+=read.Partidas.Count;
            if(historyRows>RhNomAcumuladores.MaxFilasGlobal)throw new RhNomError("LIMITE_ACUMULADORES","Hasta 5000 partidas historicas por contexto.");
            histories.Add(b.CodigoVariable,read);
        }
        var basesPeriodo=new Dictionary<string,RhNomBasePeriodo>(StringComparer.Ordinal);
        foreach(var b in sources.Where(b=>b.Proveedor=="ACUMULADOR_MOV_ACTUAL"))
        {
            var read=await RhNomBasesPeriodo.Leer(cn,cargo,b.Acumulador!,model.Configuracion.ReglasConcepto!.Select(r=>r.Concepto.CodigoConcepto).Distinct().ToList(),ct);historyRows+=read.Filas.Count;
            if(historyRows>RhNomAcumuladores.MaxFilasGlobal)throw new RhNomError("LIMITE_BASES_HISTORICAS","Hasta 5000 partidas de bases y acumuladores por contexto.");
            basesPeriodo.Add(b.CodigoVariable,read);
        }
        var entries=RhNomRecalculosHistoricos.Resolver(model,context,cargo,parameters,ct,histories,basesPeriodo);var values=entries.Select(e=>new RhNomValorEntrada(e.CodigoVariable,e.Valor)).ToList();
        // Expected payroll rows are not read until after independent source resolution/evaluation.
        var simulation=RhNomParametros.Simular(model,DateOnly.FromDateTime(date),values,ct);
        if(simulation.Simulacion.Errores.Count>0){var error=simulation.Simulacion.Errores[0];throw new RhNomError(error.Codigo,error.Mensaje,error.Posicion,error.Variable);}
        RhNomComparacionPrueba? comparison=null;
        if(q.Comparar)
        {
            var rules=model.Configuracion.ReglasConcepto;if(rules is null||rules.Count==0)throw new RhNomError("REGLAS_COMPARACION_REQUERIDAS","Configure asociaciones de conceptos en la revision; no se infieren de importes.");
            var reference=await new RhNomReferenciaHistoricaHandler(db).Get(new(model.CodigoTipoNomina,q.CodigoPeriodo,q.FechaNomina,q.CodigoPersona),company,ct);
            if(!reference.IsValid||reference.Data is null)throw new RhNomError("REFERENCIA_NO_DISPONIBLE",reference.Message);
            var query=new RhNomCompararPruebaQuery(q.ModeloId,q.Version,q.CodigoPeriodo,q.FechaNomina,q.CodigoPersona,q.HashReferenciaEsperado!,values,rules.Select(r=>new RhNomAsociacionPrueba(r.VariableSalida,r.Concepto.CodigoConcepto,r.Concepto.FrecuenciaId!.Value,r.TipoMovimiento)).ToList(),model.Configuracion.Formato==8?RhNomValores.Version:null);
            comparison=RhNomComparacionesPrueba.Comparar(model,query,reference.Data,ct) with{OrigenEntradas="FUENTES_HISTORICAS_RECONSTRUIDAS"};
        }
        var hash=RhNomRecalculosHistoricos.Hash(new{model.Hash,Contexto=context,Cargo=cargo,Entradas=entries});
        return new ResultDto<RhNomRecalculoHistorico>(new(q.ModeloId,q.Version,model.Hash,context,cargo,entries,hash,simulation,comparison,["El snapshot de cargo es fuente observada; su procedencia como entrada original no esta verificada.","La vigencia conservada de tasas/topes no acredita por si sola el estado original de todos los datos.","No se reconstruyen controles ni clasificacion temporal no registrados; las bases del periodo excluyen todas las salidas del modelo; los otros acumuladores conservan sus ventanas declaradas. No certifica paridad integral."])){IsValid=true,Message="Recalculo diagnostico con fuentes reconstruidas. Sin movimientos ni certificacion de paridad."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoRecalcular")][RequestSizeLimit(8192)]
    public Task<IActionResult> RecalculateHistorical(RhNomRecalcularHistorico q)=>RunAsync("rh.nomina.historico.datos",(e,_,ct)=>new RhNomRecalculoHistoricoHandler(db).Handle(q,e,ct),"rh.nomina.simular");
}
