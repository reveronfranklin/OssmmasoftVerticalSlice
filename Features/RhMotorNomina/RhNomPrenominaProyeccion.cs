using System.Text.Json;
using System.Text.Json.Nodes;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomSelectorPrenomina(string Campo,string Valor,List<string> Tipos,string? TipoConcepto=null);
public record RhNomConceptoPrenomina(RhNomConceptoCfg Concepto,string? Extra1,string? Modulo,string? PadreModulo);
public record RhNomPersonaPrenomina(int? Persona,int? Relacion,string? Nombre,string? Cedula,string? Estado,bool Vigente,int Coincidencias);
public record RhNomPeriodoPrenomina(int Periodo,int Tipo,DateTime Fecha,int Numero,string Clase,int Presupuesto,int? PresupuestoPeriodo,DateTime? Precierre,DateTime? Cierre);
public record RhNomContextoPrenomina(RhNomPeriodoPrenomina Periodo,List<RhNomPersonaPrenomina> Personas,List<RhNomConceptoPrenomina> Conceptos,List<RhNomFrecuenciaSeleccion> Frecuencias);
public record RhNomRetiroPrenomina(int Concepto,string Tipo);
public record RhNomFilaPrenomina(string Clave,int? MovimientoOrigen,int Persona,int Concepto,int Frecuencia,string Tipo,string Monto,string Estado,string? Complemento,string? Extra1,string? Extra2,string? Extra3,string Origen,bool Incluida,string Motivo);
public record RhNomProyeccionPrenomina(string VersionMetodo,int Persona,int Relacion,string HashCaptura,string HashPolitica,
    List<RhNomFilaPrenomina> Filas,List<RhNomImagenMovimiento> Retiradas,List<RhNomValorEntrada> EntradasPosteriores,
    RhNomResultadoVigente? Recalculo,string Asignaciones,string Deducciones,string Neto,List<string> Errores)
{
    public bool AptaParaTemporal=>Errores.Count==0;
    public bool MovimientosOriginalesModificados=>false;
    public bool PagosAutorizados=>false;
}
public static class RhNomPrenominasProyeccion
{
    public const string Version="PRENOMINA_ORDINARIA_V1";
    public static List<RhNomSelectorPrenomina> Selectores(List<RhNomSelectorPrenomina> values)
    {
        if(values is null||values.Count>20||values.Any(v=>v is null||v.Campo is not ("EXTRA1" or "PADRE_MODULO" or "CODIGO_CONCEPTO")||v.TipoConcepto is not (null or "A" or "D")||v.Campo=="CODIGO_CONCEPTO"&&(!int.TryParse(v.Valor,out var id)||id<=0||v.Valor!=id.ToString(System.Globalization.CultureInfo.InvariantCulture))||string.IsNullOrWhiteSpace(v.Valor)||v.Valor!=v.Valor.Trim()||v.Valor.Length>100
            ||v.Tipos is null||v.Tipos.Count is <1 or >3||v.Tipos.Any(t=>t is not ("F" or "V" or "E"))||v.Tipos.Distinct().Count()!=v.Tipos.Count))
            throw new RhNomError("SELECTORES_PRENOMINA_INVALIDOS","Hasta veinte selectores de catalogo con campo, valor y tipos explicitos requeridos.");
        return values.Select(v=>v with{Tipos=v.Tipos.Order().ToList()}).OrderBy(v=>v.Campo).ThenBy(v=>v.Valor).ToList();
    }
    public static List<RhNomRetiroPrenomina> Retiros(RhNomContextoPrenomina context,List<RhNomSelectorPrenomina> selectors)=>
        context.Conceptos.SelectMany(c=>Selectores(selectors).Where(s=>(s.TipoConcepto is null||s.TipoConcepto==c.Concepto.TipoConcepto)&&(s.Campo=="EXTRA1"?c.Extra1:s.Campo=="CODIGO_CONCEPTO"?c.Concepto.CodigoConcepto.ToString(System.Globalization.CultureInfo.InvariantCulture):c.PadreModulo)==s.Valor)
            .SelectMany(s=>s.Tipos.Select(t=>new RhNomRetiroPrenomina(c.Concepto.CodigoConcepto,t)))).Distinct().OrderBy(x=>x.Concepto).ThenBy(x=>x.Tipo).ToList();
    public static string HashContexto(RhNomContextoPrenomina c)=>RhNomRecalculosHistoricos.Hash(c);
    // Ignore only observation timestamps and derived fingerprints. Actual source
    // rows, update dates, definitions, provider versions and values remain bound.
    public static string HashMaterial(RhNomFuentesModeloCapturadas source)
    {
        JsonNode? Clean(JsonNode? n)=>n switch{
            JsonObject o=>new JsonObject(o.Where(p=>p.Key is not ("hash" or "fechaConsulta")).OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>KeyValuePair.Create(p.Key,Clean(p.Value)))),
            JsonArray a=>new JsonArray(a.Select(Clean).ToArray()),_=>n?.DeepClone()};
        var node=JsonSerializer.SerializeToNode(new{source.Fuente,source.Entradas},RhNomCorridas.Json);
        return RhNomCorridas.Hash(Clean(node)!.ToJsonString());
    }
    public static RhNomProyeccionPrenomina Conservar(RhNomCapsulaArchivada archived,RhNomContextoPrenomina context,int relation)
    {
        var d=RhNomCapsulasPrenomina.Leer(archived.Capsula);var totals=RhNomCapsulasPrenomina.Reproducir(archived.Capsula);
        var p=totals.Personas.SingleOrDefault(x=>x.CodigoPersona==d.Ambito.CodigoPersona);
        if(p is null)throw new RhNomError("PRENOMINA_SIN_PERSONA","La captura no contiene la persona solicitada.");
        var rows=totals.Seleccion.Filas.Select(r=>{var i=r.Imagen;return new RhNomFilaPrenomina("M:"+i.Id,i.Id,i.CodigoPersona,i.CodigoConcepto,i.FrecuenciaId??0,i.Tipo!,RhNomReferencias.NormalizarMontoOracle(i.Monto)!,i.Estado!,i.Complemento,i.Extra1,i.Extra2,i.Extra3,"MOVIMIENTO_CONSERVADO",r.Resultado=="INCLUIDA",r.Motivo);}).ToList();
        return new(Version,p.CodigoPersona,relation,archived.Capsula.Hash,HashContexto(context),rows,[],[],null,p.Asignaciones??"0",p.DeduccionesFirmadas??"0",p.Neto??"0",p.Estado=="VALIDADO"?[]:p.Motivos.Concat(new[]{p.Estado}).Distinct().ToList());
    }
    public static RhNomProyeccionPrenomina Proyectar(RhNomCorridaDocumento job,RhNomContextoPrenomina context,List<RhNomSelectorPrenomina> selectors,CancellationToken ct=default)
    {
        var chain=RhNomCorridas.Evaluar(job,ct).EnlaceSueldo??throw new RhNomError("CADENA_PRENOMINA_REQUERIDA","Se requiere cadena completa de sueldo y recalculo.");
        var doc=RhNomCapsulasPrenomina.Leer(job.Captura!.Archivo.Capsula);var cargo=doc.FuentesModelo!.Fuente;var p=context.Periodo;
        if(p.Clase!="N"||cargo.CodigoPeriodo!=p.Periodo||cargo.CodigoTipoNomina!=p.Tipo||cargo.FechaNomina!=p.Fecha||cargo.CodigoPresupuesto!=p.Presupuesto
            ||p.Precierre is not null||p.Cierre is not null)throw new RhNomError("CONTEXTO_PRENOMINA_DISTINTO","Periodo ordinario abierto y contexto coincidente requeridos.");
        var retiros=Retiros(context,selectors);bool Retira(int concept,string? type)=>retiros.Any(r=>r.Concepto==concept&&r.Tipo==type);
        var inputs=new List<RhNomValorEntrada>();
        foreach(var input in chain.Entradas)
        {
            var value=input.ValorDerivado;
            if(input.AcumuladorDerivado is {} a){
                var rows=a.Filas.Select(f=>f.Partida).Where(r=>!Retira(r.CodigoConcepto!.Value,r.TipoMovimiento)).ToList();
                value=RhNomAcumuladores.Capturar(a.Definicion,cargo,rows,ct).Total;
            }
            // PH stays at the pre-deletion stage already projected by the chain.
            inputs.Add(new(input.CodigoVariable,value));
        }
        var recalculo=RhNomParametros.Simular(job.Modelo,DateOnly.FromDateTime(p.Fecha),inputs,ct);
        if(recalculo.Simulacion.Errores.Count>0)throw new RhNomError("RECALCULO_PRENOMINA_INVALIDO","El recalculo posterior tiene errores; no se genera un temporal parcial.");
        var current=doc.Imagenes.Where(r=>!Retira(r.CodigoConcepto,r.Tipo)).ToList();var removed=doc.Imagenes.Where(r=>Retira(r.CodigoConcepto,r.Tipo)).ToList();
        var generated=new List<(RhNomPartidaPropuesta Part,RhNomImagenMovimiento? Prior)>();
        foreach(var part in (chain.Sueldo.Simulacion.ProyeccionConceptos?.Partidas??[]).Concat(recalculo.ProyeccionConceptos?.Partidas??[])){
            var matches=current.Where(r=>r.CodigoConcepto==part.CodigoConcepto&&r.FrecuenciaId==part.FrecuenciaId&&r.Tipo==part.TipoMovimiento).ToList();
            if(part.Accion is "REGISTRAR_RESULTADO" or "REGISTRAR_CERO" or "RETIRAR_GENERADO"){
                if(matches.Count>1)throw new RhNomError("DESTINO_PRENOMINA_MULTIPLE","Un destino tiene movimientos repetidos; requiere regla explicita.");
                current.RemoveAll(r=>matches.Contains(r));removed.AddRange(matches);
                if(part.MontoPropuesto is not null)generated.Add((part,matches.SingleOrDefault()));
            }
        }
        foreach(var control in recalculo.AccionesControl??[])
            if(control.Accion=="RETIRAR_GENERADO"){
                var matches=current.Where(r=>r.CodigoConcepto==control.CodigoConcepto&&r.Tipo==control.TipoMovimiento).ToList();removed.AddRange(matches);current.RemoveAll(r=>matches.Contains(r));
                generated.RemoveAll(g=>g.Part.CodigoConcepto==control.CodigoConcepto&&g.Part.TipoMovimiento==control.TipoMovimiento);
            }
        var people=doc.Metadatos.Personal;var concepts=context.Conceptos.Select(c=>new RhNomConceptoSeleccion(c.Concepto.CodigoEmpresa,c.Concepto.CodigoTipoNomina,c.Concepto.CodigoConcepto,c.Concepto.TipoConcepto)).ToList();
        var selection=RhNomSeleccionPrenomina.Preparar(doc.Ambito.Periodo,doc.Definicion,current,people,context.Frecuencias,concepts);
        var errors=selection.Filas.Where(r=>r.Resultado=="BLOQUEADA").Select(r=>r.Motivo).ToList();var rowsOut=selection.Filas.Select(r=>{
            var i=r.Imagen;return new RhNomFilaPrenomina("M:"+i.Id,i.Id,i.CodigoPersona,i.CodigoConcepto,i.FrecuenciaId??0,i.Tipo!,RhNomReferencias.NormalizarMontoOracle(i.Monto)!,i.Estado!,i.Complemento,i.Extra1,i.Extra2,i.Extra3,"MOVIMIENTO_CONSERVADO",r.Resultado=="INCLUIDA",r.Motivo);
        }).ToList();
        foreach(var (part,prior) in generated){
            var c=concepts.SingleOrDefault(c=>c.CodigoConcepto==part.CodigoConcepto);var f=context.Frecuencias.SingleOrDefault(f=>f.Id==part.FrecuenciaId);
            if(c?.TipoConcepto is not ("A" or "D")||string.IsNullOrEmpty(f?.Codigo))throw new RhNomError("METADATA_PRENOMINA_FALTANTE","Concepto y frecuencia actuales requeridos para el resultado.");
            var eligible=people.Count(x=>x.CodigoPersona==cargo.CodigoPersona&&doc.Definicion.EstadosPersonal.Contains(x.Estado??""));
            if(eligible!=1)throw new RhNomError("PERSONAL_PRENOMINA_AMBIGUO","Personal elegible unico requerido.");
            var included=f.Codigo[..1]==doc.Definicion.PrefijoSiempre||f.Codigo[..1]==p.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture);
            rowsOut.Add(new("G:"+part.CodigoConcepto+":"+part.FrecuenciaId+":"+part.TipoMovimiento,prior?.Id,cargo.CodigoPersona,part.CodigoConcepto,part.FrecuenciaId,part.TipoMovimiento,part.MontoPropuesto!,"A",prior?.Complemento,prior?.Extra1,prior?.Extra2,prior?.Extra3,"RESULTADO_CALCULADO",included,included?"CRITERIOS_CUMPLIDOS":"FRECUENCIA_PERIODO"));
        }
        var assignments=RhNomTotalesPrenomina.Exact.Zero;var deductions=RhNomTotalesPrenomina.Exact.Zero;
        foreach(var row in rowsOut.Where(r=>r.Incluida)){
            var c=concepts.Single(c=>c.CodigoConcepto==row.Concepto);var amount=RhNomTotalesPrenomina.Exact.Parse(row.Monto);
            if(c.TipoConcepto=="A"){if(RhNomTransaccion.Numero(row.Monto).CompareTo(new Oracle.ManagedDataAccess.Types.OracleDecimal(0))<0)errors.Add("SIGNO_ASIGNACION");assignments=assignments.Add(amount);}
            else{if(RhNomTransaccion.Numero(row.Monto).CompareTo(new Oracle.ManagedDataAccess.Types.OracleDecimal(0))>0)errors.Add("SIGNO_DEDUCCION");deductions=deductions.Add(amount);}
        }
        var net=assignments.Add(deductions);if(net.Units.Sign<0)errors.Add("NETO_NEGATIVO");if(!assignments.OracleRepresentable||!deductions.OracleRepresentable||!net.OracleRepresentable)errors.Add("TOTALES_FUERA_NUMBER38");
        if(!rowsOut.Any(r=>r.Incluida))errors.Add("SIN_PARTIDAS_INCLUIDAS");
        return new(Version,cargo.CodigoPersona,cargo.CodigoRelacionCargo,job.Captura.Archivo.Capsula.Hash,RhNomRecalculosHistoricos.Hash(new{Contexto=HashContexto(context),Selectores=Selectores(selectors),Retiros=retiros}),rowsOut,removed.OrderBy(r=>r.Id).ToList(),inputs,recalculo,assignments.Text(),deductions.Text(),net.Text(),errors.Distinct().Order().ToList());
    }
}
