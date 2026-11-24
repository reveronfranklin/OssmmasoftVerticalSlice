using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomCrearCorrida(string IdempotencyKey,RhNomLoteQuery? Lote=null,RhNomSimularActual? Actual=null,RhNomCompararPruebaQuery? Comparacion=null);
public record RhNomCorridaId(int IdRun);
public record RhNomCorridaDetalleQuery(int IdRun,string Modo);
public record RhNomCancelarCorrida(int IdRun,int VersionEsperada);
public record RhNomCorrida(int IdRun,int CodigoEmpresa,int Actor,int ModeloId,int VersionModelo,string Modo,string Estado,
    string IdempotencyKey,string HashSolicitud,string HashDatos,string? HashResultado,string? Mensaje,int Version,bool Cancelar,int Intentos,
    DateTime FechaIns,DateTime? FechaInicio,DateTime? FechaFin)
{
    public bool MovimientosAplicados=>false;
}
public record RhNomCorridaDocumento(int Formato,int CodigoEmpresa,int Actor,RhNomModeloVersion Modelo,string Modo,
    RhNomLoteQuery? Lote,RhNomCargoActual? FuenteActual,List<RhNomEntradaActual>? EntradasActuales,string? PoliticaPresupuesto,string? MotivoPresupuesto,
    RhNomCompararPruebaQuery? Comparacion,RhNomReferenciaCapturada? Referencia,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomCorridaCaptura? Captura=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomConfigEnlaceSueldo? EnlaceSueldo=null);
public record RhNomCorridaResultado(string Modo,bool IsValid,RhNomResultadoLote? Lote=null,RhNomResultadoActual? Actual=null,
    RhNomComparacionPrueba? Comparacion=null,List<RhNomDiagnostico>? Errores=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomResultadoModeloCaptura? Captura=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomResultadoEnlaceSueldo? EnlaceSueldo=null)
{
    public bool MovimientosAplicados=>false;
    public bool ParidadCertificada=>false;
}
public record RhNomCorridaDetalle(RhNomCorrida Corrida,RhNomCorridaDocumento Entrada,RhNomCorridaResultado? Resultado);
public static class RhNomCorridas
{
    public const int MaxDatosBytes=4194304,MaxResultadoBytes=2097152;
    public const string Version="CORRIDAS_SIMULACION_V1";
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    public static string Serializar<T>(T value,int maxBytes)
    {
        var text=JsonSerializer.Serialize(value,Json);if(System.Text.Encoding.UTF8.GetByteCount(text)>maxBytes)throw new RhNomError("LIMITE_DOCUMENTO","El documento excede el limite; reduzca casos o traza.");return text;
    }
    public static string Modo(RhNomCrearCorrida q)
    {
        if(string.IsNullOrWhiteSpace(q.IdempotencyKey)||q.IdempotencyKey.Length>80||!Regex.IsMatch(q.IdempotencyKey,@"\A[A-Za-z0-9:_-]+\z",RegexOptions.CultureInvariant))throw new RhNomError("CLAVE_SOLICITUD_INVALIDA","Clave idempotente de 1..80 caracteres ASCII alfanumericos, : _ -.");
        if((q.Lote is null?0:1)+(q.Actual is null?0:1)+(q.Comparacion is null?0:1)!=1)throw new RhNomError("MODO_CORRIDA_INVALIDO","Indique exactamente un lote manual, contexto actual o comparacion de prueba.");
        if(q.Lote is not null){RhNomLotes.Validar(q.Lote);return "LOTE_MANUAL";}
        if(q.Comparacion is not null){RhNomComparacionesPrueba.Validar(q.Comparacion);return "COMPARACION_PRUEBA";}
        return "ACTUAL";
    }
    public static string HashSolicitud(RhNomCrearCorrida q,int company,int actor)
    {
        var mode=Modo(q);var normalized=q.Lote is null?q:q with{Lote=q.Lote with{Casos=RhNomLotes.Validar(q.Lote)}};
        return Hash(Serializar(new{CodigoEmpresa=company,Actor=actor,Modo=mode,Solicitud=normalized},MaxDatosBytes));
    }
    public static RhNomCorridaDocumento Leer(string text,string hash,int company,int actor)
    {
        if(System.Text.Encoding.UTF8.GetByteCount(text)>MaxDatosBytes||Hash(text)!=hash)throw new RhNomError("SNAPSHOT_ALTERADO","Snapshot de corrida alterado o demasiado grande.");
        RhNomCorridaDocumento doc;
        try{doc=JsonSerializer.Deserialize<RhNomCorridaDocumento>(text,Json)??throw new JsonException();}
        catch(JsonException){throw new RhNomError("SNAPSHOT_INVALIDO","No se pudo interpretar el snapshot de corrida.");}
        if(doc.Formato is not (1 or 2 or 3)||doc.CodigoEmpresa!=company||doc.Actor!=actor||doc.Modelo is null||doc.Modelo.Configuracion is null||doc.Modelo.Configuracion.CodigoEmpresa!=company)
            throw new RhNomError("CONTEXTO_DISTINTO","Snapshot ajeno al actor/empresa o formato incompatible.");
        var model=RhNomConfiguracion.Serializar(doc.Modelo.Configuracion);RhNomConfiguracion.Leer(model.Text,doc.Modelo.Hash,company,doc.Modelo.ModeloId);
        if(doc.Formato==1&&(doc.Captura is not null||doc.EnlaceSueldo is not null||doc.Modo==RhNomModelosDesdeCaptura.Modo)
            ||doc.Formato is 2 or 3&&(doc.Modo!=RhNomModelosDesdeCaptura.Modo||doc.Captura is null||doc.Lote is not null||doc.FuenteActual is not null||doc.EntradasActuales is not null||doc.PoliticaPresupuesto is not null||doc.MotivoPresupuesto is not null||doc.Comparacion is not null||doc.Referencia is not null)
            ||doc.Formato==2&&doc.EnlaceSueldo is not null||doc.Formato==3&&doc.EnlaceSueldo is null)
            throw new RhNomError("SNAPSHOT_MODO_INCOMPATIBLE","Formato, modo y contenido de la corrida son incompatibles.");
        if(doc.Formato is 2 or 3)RhNomModelosDesdeCaptura.Verificar(doc.Modelo,doc.Captura!,company,actor);
        if(doc.Formato==3)RhNomCorridasEnlaceSueldo.Verificar(doc,company,actor);
        return doc;
    }
    public static RhNomCorridaResultado Evaluar(RhNomCorridaDocumento doc,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();
        if(doc.Modo==RhNomModelosDesdeCaptura.Modo)
        {
            if(doc.Formato is not (2 or 3)||doc.Captura is null||doc.Formato==2&&doc.EnlaceSueldo is not null||doc.Formato==3&&doc.EnlaceSueldo is null)throw new RhNomError("SNAPSHOT_CAPTURA_INCOMPLETO","La corrida de captura requiere snapshot formato2 completo.");
            if(doc.Formato==3)
            {
                var linked=RhNomCorridasEnlaceSueldo.Verificar(doc,doc.CodigoEmpresa,doc.Actor);
                var chain=RhNomEnlacesSueldo.Evaluar(linked,doc.CodigoEmpresa,doc.Actor,ct);
                return new(doc.Modo,chain.Recalculo.Simulacion.Errores.Count==0,Errores:chain.Recalculo.Simulacion.Errores,EnlaceSueldo:chain);
            }
            var result=RhNomModelosDesdeCaptura.Evaluar(doc.Modelo,doc.Captura,doc.CodigoEmpresa,doc.Actor,ct);
            return new(doc.Modo,result.Errores.Count==0,Errores:result.Errores,Captura:result);
        }
        if(doc.Modo=="LOTE_MANUAL"&&doc.Lote is not null)
        {
            var result=RhNomLotes.Simular(doc.Modelo,doc.Lote,ct);return new(doc.Modo,result.Errores.Count==0,Lote:result);
        }
        if(doc.Modo=="ACTUAL"&&doc.FuenteActual is not null&&doc.EntradasActuales is not null)
        {
            ValidarEntradasActuales(doc);var s=doc.FuenteActual;
            var result=RhNomParametros.Simular(doc.Modelo,DateOnly.FromDateTime(s.FechaNomina),doc.EntradasActuales.Select(e=>new RhNomValorEntrada(e.CodigoVariable,e.Valor)).ToList(),ct);
            return new(doc.Modo,result.Simulacion.Errores.Count==0,Actual:new(s,RhNomFuentesActuales.Hash(s),doc.EntradasActuales,result,doc.PoliticaPresupuesto??"",doc.MotivoPresupuesto));
        }
        if(doc.Modo=="COMPARACION_PRUEBA"&&doc.Comparacion is not null&&doc.Referencia is not null)
            return new(doc.Modo,true,Comparacion:RhNomComparacionesPrueba.Comparar(doc.Modelo,doc.Comparacion,doc.Referencia,ct));
        throw new RhNomError("SNAPSHOT_INVALIDO","El modo y contenido del snapshot no coinciden.");
    }
    public static void ValidarEntradasActuales(RhNomCorridaDocumento doc)
    {
        if(doc.FuenteActual is null||doc.EntradasActuales is null)throw new RhNomError("FUENTES_NO_CAPTURADAS","Fuente y entradas completas requeridas.");
            var s=doc.FuenteActual;
            if(s.CodigoEmpresa!=doc.CodigoEmpresa||s.CodigoTipoNomina!=doc.Modelo.CodigoTipoNomina)throw new RhNomError("CONTEXTO_DISTINTO","Fuente congelada ajena al modelo.");
            foreach(var input in doc.EntradasActuales.Where(e=>e.Acumulacion is not null))
            {
                RhNomAcumuladores.Verificar(input.Acumulacion!,s);
                if(input.Proveedor!="ACUMULADOR_MOV_ACTUAL"||input.Unidad!="DINERO"||input.Valor!=input.Acumulacion!.Total)throw new RhNomError("ACUMULADOR_ALTERADO","La entrada congelada difiere del acumulador.");
            }
            if(doc.EntradasActuales.Any(e=>e.Proveedor=="ACUMULADOR_MOV_ACTUAL"&&e.Acumulacion is null))throw new RhNomError("ACUMULADOR_NO_CAPTURADO","Falta evidencia congelada del acumulador.");
            foreach(var input in doc.EntradasActuales.Where(e=>e.ParametroConcepto is not null))
            {
                var d=input.ParametroConcepto!.Datos;var b=new RhNomParametroConceptoBinding(d.CodigoConcepto,d.PoliticaVigencia);
                if(!RhNomParametrosConcepto.Unidades.TryGetValue(input.Proveedor,out var unit)||unit!=input.Unidad||input.Valor!=RhNomParametrosConcepto.Valor(input.Proveedor,b,input.ParametroConcepto,s))throw new RhNomError("PARAMETRO_CONCEPTO_ALTERADO","Entrada congelada distinta del parametro capturado.");
            }
            if(doc.EntradasActuales.Any(e=>RhNomParametrosConcepto.Unidades.ContainsKey(e.Proveedor)&&e.ParametroConcepto is null))throw new RhNomError("PARAMETRO_NO_CAPTURADO","Falta evidencia de parametros de concepto.");
            foreach(var input in doc.EntradasActuales.Where(e=>e.Control is not null))
            {
                RhNomControles.Verificar(input.Control!,s);
                if(input.Proveedor!=RhNomControles.Proveedor||input.Unidad!="NUMERO"||input.Valor!=(input.Control!.Omitir?"1":"0"))throw new RhNomError("CONTROL_ALTERADO","La entrada congelada no coincide con su control.");
            }
            if(doc.EntradasActuales.Any(e=>e.Proveedor==RhNomControles.Proveedor&&e.Control is null))throw new RhNomError("CONTROL_NO_CAPTURADO","Falta evidencia congelada de controles.");
            foreach(var input in doc.EntradasActuales.Where(e=>e.AcumulacionHistorica is not null))
            {
                RhNomAcumuladoresHistoricos.Verificar(input.AcumulacionHistorica!,RhNomAcumuladoresHistoricos.Contexto(s),s.CodigoPersona);
                if(input.Proveedor!=RhNomAcumuladoresHistoricos.ProveedorPara(input.AcumulacionHistorica!.Definicion)||input.Unidad!="DINERO"||input.Valor!=input.AcumulacionHistorica!.Calculo.Total)throw new RhNomError("ACUMULADOR_HIST_ALTERADO","La entrada difiere del acumulador historico capturado.");
            }
            if(doc.EntradasActuales.Any(e=>RhNomAcumuladoresHistoricos.EsProveedor(e.Proveedor)&&e.AcumulacionHistorica is null))throw new RhNomError("ACUMULADOR_HIST_NO_CAPTURADO","Falta evidencia historica congelada.");
            foreach(var input in doc.EntradasActuales.Where(e=>e.AgregadosPh is not null))
            {
                RhNomAgregadosPh.Verificar(input.AgregadosPh!,s);
                if(!RhNomAgregadosPh.Proveedores.TryGetValue(input.Proveedor,out var field)||input.Unidad!="DINERO"||input.Valor!=input.AgregadosPh!.Valores[field])throw new RhNomError("AGREGADO_PH_ALTERADO","Entrada distinta de sus agregados PH congelados.");
            }
            if(doc.EntradasActuales.Any(e=>RhNomAgregadosPh.Proveedores.ContainsKey(e.Proveedor)&&e.AgregadosPh is null))throw new RhNomError("AGREGADOS_PH_NO_CAPTURADOS","Falta evidencia congelada de agregados PH.");
            foreach(var input in doc.EntradasActuales.Where(e=>e.DatosPersonales is not null))
            {
                RhNomDatosPersonales.Verificar(input.DatosPersonales!,s,input.Proveedor);
                if(!RhNomDatosPersonales.Proveedores.TryGetValue(input.Proveedor,out var unit)||input.Unidad!=unit||input.Valor!=input.DatosPersonales!.Valor)throw new RhNomError("DATOS_PERSONALES_ALTERADOS","Entrada distinta de su evidencia personal capturada.");
            }
            if(doc.EntradasActuales.Any(e=>RhNomDatosPersonales.Proveedores.ContainsKey(e.Proveedor)&&e.DatosPersonales is null))throw new RhNomError("DATOS_PERSONALES_NO_CAPTURADOS","Falta evidencia personal congelada.");
            foreach(var input in doc.EntradasActuales.Where(e=>RhNomFuentesActuales.EsTipado(e.Proveedor)))
            {
                var variable=doc.Modelo.Configuracion.Variables.SingleOrDefault(v=>v.Codigo==input.CodigoVariable);
                if(variable is null||input.Unidad!=variable.Unidad||input.Valor!=RhNomValores.Normalizar(variable.Tipo,RhNomFuentesActuales.ValorTipado(input.Proveedor,s)))throw new RhNomError("FUENTE_TIPADA_ALTERADA","Entrada tipada congelada distinta de la fuente capturada.");
            }
            foreach(var binding in doc.Modelo.Configuracion.FuentesActuales??[])
            {
                var variable=doc.Modelo.Configuracion.Variables.Single(v=>v.Codigo==binding.CodigoVariable);var input=doc.EntradasActuales.SingleOrDefault(e=>e.CodigoVariable==binding.CodigoVariable);
                if(input is null)throw new RhNomError("ENTRADAS_ACTUALES_INCOMPLETAS","Falta una entrada de la revision.");
                if(RhNomAgregadosPh.Proveedores.ContainsKey(binding.Proveedor))
                {
                    if(input.Proveedor!=binding.Proveedor||input.AgregadosPh is null||binding.AgregadosPh is null||JsonSerializer.Serialize(RhNomAgregadosPh.Validar(binding.AgregadosPh,s))!=JsonSerializer.Serialize(input.AgregadosPh.Documento.Definicion))throw new RhNomError("AGREGADOS_PH_DISTINTOS","La evidencia PH no corresponde a la definicion y proveedor de la revision.");
                }
                if(RhNomDatosPersonales.Proveedores.ContainsKey(binding.Proveedor)&&
                    (input.Proveedor!=binding.Proveedor||input.DatosPersonales?.Documento.Proveedor!=binding.Proveedor))
                    throw new RhNomError("DATOS_PERSONALES_DISTINTOS","La politica personal capturada no corresponde al proveedor de la revision.");
                if(binding.Proveedor==RhNomControles.Proveedor&&(input.Proveedor!=binding.Proveedor||input.Control?.Definicion!=binding.Control))
                    throw new RhNomError("CONTROL_MODELO_DISTINTO","El control capturado no corresponde al concepto y politica de la revision.");
                RhNomFuentesModelo.ValidarValor(binding,variable,input.Valor);
            }
    }

}
