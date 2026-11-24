using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomFuenteActualBinding(string CodigoVariable,string Proveedor,string? ValorSiNulo=null,int? DiaSemanaIso=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAcumuladorActual? Acumulador=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomParametroConceptoBinding? ParametroConcepto=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomControlBinding? Control=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAcumHistoricoDef? AcumuladorHistorico=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] List<string>? ValoresPermitidos=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAgregadosPhDef? AgregadosPh=null);
public record RhNomSimularActual(int ModeloId,int Version,int CodigoPeriodo,string FechaNomina,int CodigoPresupuesto,
    int CodigoPersona,int CodigoRelacionCargo,List<RhNomFuenteActualBinding>? Fuentes,string PoliticaPresupuesto="",string? MotivoPresupuesto=null);
public record RhNomCargoActual(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPeriodo,DateTime FechaNomina,int CodigoPresupuesto,
    int CodigoPersona,int CodigoRelacionCargo,int? CodigoCargo,string Sueldo,string? Extra1,
    DateTime? FechaPreCierre,DateTime? FechaIni,DateTime? FechaIniVigencia,DateTime? FechaFinVigencia,string SiglasTipoNomina,int? FrecuenciaPagoId,int? PresupuestoPeriodo=null)
{
    public string Origen=>"RH_RELACION_CARGOS_ACTUAL";
    public string VersionFuente { get; init; } = RhNomFuentesActuales.Version;
    public bool EstadoHistoricoVerificado=>false;
}
public record RhNomEntradaActual(string CodigoVariable,string Valor,string Unidad,string Proveedor,bool DefaultExplicito,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAcumulacion? Acumulacion=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomParametroConceptoCapturado? ParametroConcepto=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomControlCapturado? Control=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAcumHistorico? AcumulacionHistorica=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomPersonalCapturado? DatosPersonales=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAgregadosPhCapturados? AgregadosPh=null);
public record RhNomResultadoActual(RhNomCargoActual Fuente,string HashFuente,List<RhNomEntradaActual> Entradas,RhNomResultadoVigente Simulacion,string PoliticaPresupuesto,string? MotivoPresupuesto)
{
    public bool MovimientosAplicados=>false;
    public bool ParidadHistoricaCertificada=>false;
}
public static class RhNomFuentesActuales
{
    public const string Version="FUENTES_ACTUALES_V12";
    public static readonly IReadOnlyList<RhNomProveedor> Catalogo=Array.AsReadOnly(new RhNomProveedor[]{
        new(RhNomDatosPersonales.UltimoOSinRegistro,"NUMERO","Ultimo titulo unico o ausencia explicita 0; no resuelve empates ni titulos nulos",false,Version,true),
        new("TITULO_EDUC_ULTIMO_REG","NUMERO","Titulo de educacion con ultima fecha final registrada unica (incluye futura)",false,Version,true),
        new("TITULO_EDUC_FECHA_NOMINA","NUMERO","Titulo con ultima fecha final no posterior a nomina, unica",false,Version,true),
        new("FECHA_INGRESO_ADMIN_UNICO","FECHA","Ingreso de unico administrativo de empresa explicita, sin inferir STATUS",false,Version,true),
        new("FECHA_NOMINA_CONTEXTO","FECHA","Fecha calendario de nomina (yyyy-MM-dd)",false,Version,true),
        new("FECHA_INICIO_RELACION","FECHA","Fecha de inicio de relacion de cargo; nulo bloqueante",false,Version,true),
        new("TIENE_PRECIERRE_CONTEXTO","BOOLEANO","Flag de existencia de fecha de precierre, no autorizacion de ejecucion",false,Version,true),
        new("SIGLAS_TIPO_NOMINA_ACTUAL","TEXTO","Siglas exactas conservadas del tipo de nomina actual",false,Version,true),
        new(RhNomAcumuladoresHistoricos.ProveedorMes,"DINERO","Suma del mismo mes calendario, otros periodos cerrados; no acredita entradas historicas originales",false,Version,true),
        new(RhNomAcumuladoresHistoricos.Proveedor,"DINERO","Suma historica de meses anteriores completos con filtros explicitos",false,Version,true),
        new(RhNomControles.Proveedor,"NUMERO","Control actual: 1 omite, 0 no omite; alcance y politicas explicitos",false,Version,true),
        new("PARAM_CON_PORCENTAJE","PORCENTAJE","Porcentaje de configuracion actual del concepto",false,Version,true),
        new("PARAM_CON_TOPE","DINERO","Monto tope de configuracion actual del concepto",false,Version,true),
        new("PARAM_CON_PATRONAL","PORCENTAJE","Porcentaje patronal actual del concepto",false,Version,true),
        new("PARAM_CON_EXTRA2","NUMERO","EXTRA2 numerico de configuracion del concepto",false,Version,true),
        new("PARAM_CON_EXTRA3","NUMERO","EXTRA3 numerico de configuracion del concepto",false,Version,true),
        new("ACUMULADOR_MOV_ACTUAL","DINERO","Suma de movimientos actuales con conceptos/filtros explicitos y traza",false,Version,true),
        new("SUELDO_RELACION_ACTUAL","DINERO","Sueldo de relacion de cargo exacta, periodo abierto",false,Version,true),
        new("EXTRA1_RELACION_ACTUAL","NUMERO","EXTRA1 numerico de relacion (suplencia segun reglas configuradas)",false,Version,true),
        new("TIPO_NOMINA_CONTEXTO","NUMERO","Identificador de tipo de nomina del contexto",false,Version,true),
        new("FRECUENCIA_PAGO_ACTUAL","NUMERO","Identificador de frecuencia de pago actual",false,Version,true),
        new("ANIO_FECHA_NOMINA","NUMERO","Anio de fecha de nomina",false,Version,true),
        new("MES_FECHA_NOMINA","NUMERO","Mes de fecha de nomina",false,Version,true),
        new("DIAS_MES_NOMINA","DIAS","Dias calendario del mes",false,Version,true),
        new("DIA_ISO_MES_NOMINA","CANTIDAD","Ocurrencias de dia ISO en el mes",true,Version,true)
    }.Concat(RhNomAgregadosPh.Proveedores.Select(p=>new RhNomProveedor(p.Key,"DINERO",RhNomAgregadosPh.Descripcion(p.Key),false,Version,true))).ToArray());
    public static DateTime Validar(RhNomSimularActual q,int company)
    {
        if(company<=0||q.ModeloId<=0||q.Version<=0||q.CodigoPeriodo<=0||q.CodigoPresupuesto<=0||q.CodigoPersona<=0||q.CodigoRelacionCargo<=0)
            throw new RhNomError("CONTEXTO_ACTUAL_INVALIDO","Modelo/revision/periodo/presupuesto/persona/relacion positivos requeridos.");
        if(q.Fuentes?.Count>200)throw new RhNomError("FUENTES_INVALIDAS","Indique hasta 200 proveedores registrados.");
        if(!RhNomPresupuestos.PoliticaValida(q.PoliticaPresupuesto))throw new RhNomError("POLITICA_PRESUPUESTO_REQUERIDA","Elija presupuesto coincidente con periodo o tipo actual explicito.");
        if(q.MotivoPresupuesto?.Length>500)throw new RhNomError("MOTIVO_INVALIDO","Motivo hasta 500 caracteres.");
        return RhNomFuentesHistoricas.Fecha(q.FechaNomina);
    }
    public static List<RhNomEntradaActual> Resolver(RhNomSnapshot snapshot,RhNomCargoActual source,IReadOnlyList<RhNomFuenteActualBinding> bindings,CancellationToken ct=default,string politicaPresupuesto="COINCIDIR_PERIODO",string? motivoPresupuesto=null,IReadOnlyDictionary<string,RhNomAcumulacion>? acumuladores=null,IReadOnlyDictionary<string,RhNomParametroConceptoCapturado>? parametrosConcepto=null,IReadOnlyDictionary<string,RhNomControlCapturado>? controles=null,IReadOnlyDictionary<string,RhNomAcumHistorico>? historicos=null,IReadOnlyDictionary<string,RhNomPersonalCapturado>? personales=null,IReadOnlyDictionary<string,RhNomAgregadosPhCapturados>? agregadosPh=null)
    {
        if(source.CodigoEmpresa!=snapshot.CodigoEmpresa||source.CodigoTipoNomina!=snapshot.CodigoTipoNomina||source.CodigoPeriodo<=0||source.CodigoPresupuesto<=0||source.CodigoPersona<=0||source.CodigoRelacionCargo<=0||source.FechaNomina.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("CONTEXTO_DISTINTO","La fuente actual debe coincidir con el modelo y un contexto explicito.");
        RhNomPresupuestos.Validar(source.PresupuestoPeriodo,source.CodigoPresupuesto,politicaPresupuesto,motivoPresupuesto);
        if(source.FechaIni is {} start&&start>source.FechaNomina||source.FechaIniVigencia is {} validFrom&&validFrom>source.FechaNomina||source.FechaFinVigencia is {} validTo&&validTo<source.FechaNomina)
            throw new RhNomError("CARGO_FUERA_VIGENCIA","Las fechas de la relacion/sueldo no cubren la fecha solicitada.");
        if(bindings is null||bindings.Count>200)throw new RhNomError("FUENTES_INVALIDAS","Fuentes requeridas, hasta 200.");
        var variables=snapshot.Variables.ToDictionary(v=>v.Codigo,StringComparer.Ordinal);var keys=new HashSet<string>(StringComparer.Ordinal);var results=new List<RhNomEntradaActual>();
        foreach(var b in bindings)
        {
            ct.ThrowIfCancellationRequested();if(b is null)throw new RhNomError("FUENTE_INVALIDA","Proveedor nulo.");
            var code=RhNomNumeros.Codigo(b.CodigoVariable);var provider=Catalogo.SingleOrDefault(p=>p.Codigo==b.Proveedor);
            if(!keys.Add(code)||!variables.TryGetValue(code,out var variable)||variable.Origen!="ENTRADA"||provider is null||variable.Unidad!=provider.Unidad)
                throw new RhNomError("FUENTE_INCOMPATIBLE","Fuente duplicada, no registrada o incompatible con la ENTRADA/unidad.");
            if(b.ValorSiNulo is not null&&b.Proveedor!="EXTRA1_RELACION_ACTUAL")throw new RhNomError("DEFAULT_NO_ADMITIDO","Solo EXTRA1 admite valor si nulo explicito; no se sustituye sueldo ni contexto.");
            if(b.ValorSiNulo is not null)RhNomNumeros.Leer(b.ValorSiNulo);
            if(provider.RequiereDiaIso?(b.DiaSemanaIso is null or <1 or >7):b.DiaSemanaIso is not null)
                throw new RhNomError("DIA_ISO_INVALIDO","Solo DIA_ISO_MES_NOMINA requiere dia ISO 1..7.");
            RhNomAcumulacion? accumulation=null;
            if(b.Proveedor=="ACUMULADOR_MOV_ACTUAL")
            {
                if(b.Acumulador is null||acumuladores is null||!acumuladores.TryGetValue(code,out accumulation))throw new RhNomError("ACUMULADOR_NO_CAPTURADO","Defina y capture el acumulador de la ENTRADA.");
                RhNomAcumuladores.Verificar(accumulation,source);
                if(JsonSerializer.Serialize(RhNomAcumuladores.Validar(b.Acumulador))!=JsonSerializer.Serialize(accumulation.Definicion))throw new RhNomError("ACUMULADOR_DISTINTO","La definicion capturada difiere de la solicitada.");
            }
            else if(b.Acumulador is not null)throw new RhNomError("ACUMULADOR_NO_ADMITIDO","Solo el proveedor de acumulador admite su definicion.");
            RhNomParametroConceptoCapturado? parameter=null;string? parameterValue=null;
            if(RhNomParametrosConcepto.Unidades.ContainsKey(b.Proveedor))
            {
                if(b.ParametroConcepto is null||parametrosConcepto is null||!parametrosConcepto.TryGetValue(code,out parameter))throw new RhNomError("PARAMETRO_NO_CAPTURADO","Defina y capture el parametro de concepto.");
                parameterValue=RhNomParametrosConcepto.Valor(b.Proveedor,b.ParametroConcepto,parameter,source);
            }
            else if(b.ParametroConcepto is not null)throw new RhNomError("PARAMETRO_NO_ADMITIDO","Solo proveedores PARAM_CON admiten parametro de concepto.");
            RhNomControlCapturado? control=null;
            if(b.Proveedor==RhNomControles.Proveedor)
            {
                if(b.Control is null||controles is null||!controles.TryGetValue(code,out control))throw new RhNomError("CONTROL_NO_CAPTURADO","Defina y capture controles para esta entrada.");
                RhNomControles.Verificar(control,source);
                if(b.Control!=control.Definicion)throw new RhNomError("CONTROL_DISTINTO","El control capturado no coincide con la definicion guardada.");
            }
            else if(b.Control is not null)throw new RhNomError("CONTROL_NO_ADMITIDO","Control adjunto a un proveedor distinto.");
            RhNomAcumHistorico? history=null;
            if(RhNomAcumuladoresHistoricos.EsProveedor(b.Proveedor))
            {
                if(b.AcumuladorHistorico is null||historicos is null||!historicos.TryGetValue(code,out history))throw new RhNomError("ACUMULADOR_HIST_NO_CAPTURADO","Falta acumulador historico capturado.");
                RhNomAcumuladoresHistoricos.ValidarProveedor(b.Proveedor,b.AcumuladorHistorico);RhNomAcumuladoresHistoricos.Verificar(history,RhNomAcumuladoresHistoricos.Contexto(source),source.CodigoPersona);
                if(JsonSerializer.Serialize(RhNomAcumuladoresHistoricos.Validar(b.AcumuladorHistorico))!=JsonSerializer.Serialize(history.Definicion))throw new RhNomError("ACUMULADOR_HIST_DISTINTO","Los filtros historicos capturados difieren del modelo.");
            }
            else if(b.AcumuladorHistorico is not null)throw new RhNomError("ACUMULADOR_HIST_NO_ADMITIDO","Definicion historica ajena al proveedor.");
            RhNomPersonalCapturado? personal=null;
            if(RhNomDatosPersonales.Proveedores.ContainsKey(b.Proveedor))
            {
                if(personales is null||!personales.TryGetValue(code,out personal))throw new RhNomError("DATOS_PERSONALES_NO_CAPTURADOS","Falta evidencia de la fuente personal.");
                RhNomDatosPersonales.Verificar(personal,source,b.Proveedor);
            }
            RhNomAgregadosPhCapturados? ph=null;
            if(RhNomAgregadosPh.Proveedores.ContainsKey(b.Proveedor))
            {
                if(b.AgregadosPh is null||agregadosPh is null||!agregadosPh.TryGetValue(code,out ph))throw new RhNomError("AGREGADOS_PH_NO_CAPTURADOS","Defina y capture los agregados PH de la entrada.");
                RhNomAgregadosPh.Verificar(ph,source);
                if(JsonSerializer.Serialize(RhNomAgregadosPh.Validar(b.AgregadosPh,source))!=JsonSerializer.Serialize(ph.Documento.Definicion))throw new RhNomError("AGREGADOS_PH_DISTINTOS","La definicion PH capturada no coincide con el modelo.");
            }
            else if(b.AgregadosPh is not null)throw new RhNomError("AGREGADOS_PH_NO_ADMITIDOS","Definicion PH ajena al proveedor.");
            var date=source.FechaNomina;var fallback=false;
            string? value=b.Proveedor switch {
                "FECHA_NOMINA_CONTEXTO" or "FECHA_INICIO_RELACION" or "TIENE_PRECIERRE_CONTEXTO" or "SIGLAS_TIPO_NOMINA_ACTUAL"=>ValorTipado(b.Proveedor,source),
                RhNomAcumuladoresHistoricos.Proveedor or RhNomAcumuladoresHistoricos.ProveedorMes=>history?.Calculo.Total,
                RhNomControles.Proveedor=>control?.Omitir==true?"1":"0",
                "ACUMULADOR_MOV_ACTUAL"=>accumulation?.Total,
                "SUELDO_RELACION_ACTUAL"=>source.Sueldo,"EXTRA1_RELACION_ACTUAL"=>source.Extra1,
                "TIPO_NOMINA_CONTEXTO"=>RhNomNumeros.Texto(source.CodigoTipoNomina),"FRECUENCIA_PAGO_ACTUAL"=>source.FrecuenciaPagoId is {} f?RhNomNumeros.Texto(f):null,
                "ANIO_FECHA_NOMINA"=>RhNomNumeros.Texto(date.Year),"MES_FECHA_NOMINA"=>RhNomNumeros.Texto(date.Month),
                "DIAS_MES_NOMINA"=>RhNomNumeros.Texto(DateTime.DaysInMonth(date.Year,date.Month)),
                "DIA_ISO_MES_NOMINA"=>RhNomNumeros.Texto(Enumerable.Range(1,DateTime.DaysInMonth(date.Year,date.Month)).Count(d=>((int)new DateTime(date.Year,date.Month,d).DayOfWeek+6)%7+1==b.DiaSemanaIso)),_=>null};
            if(ph is not null)value=ph.Valores[RhNomAgregadosPh.Proveedores[b.Proveedor]];
            if(personal is not null)value=personal.Valor;
            if(parameterValue is not null)value=parameterValue;
            if(value is null&&b.ValorSiNulo is not null){value=b.ValorSiNulo;fallback=true;}
            var parsed=RhNomValores.Normalizar(variable.Tipo,value);RhNomFuentesModelo.ValidarValor(b,variable,parsed);results.Add(new(code,parsed,variable.Unidad,b.Proveedor,fallback,accumulation,parameter,control,history,personal,ph));
        }
        var expected=snapshot.Variables.Where(v=>v.Origen=="ENTRADA").Select(v=>v.Codigo).ToHashSet(StringComparer.Ordinal);
        if(!expected.SetEquals(keys))throw new RhNomError("ENTRADAS_ACTUALES_INCOMPLETAS","Configure exactamente una fuente por ENTRADA; no se infieren valores de salidas esperadas.");
        return results.OrderBy(r=>r.CodigoVariable,StringComparer.Ordinal).ToList();
    }
    public static bool EsTipado(string provider)=>provider is "FECHA_NOMINA_CONTEXTO" or "FECHA_INICIO_RELACION" or "TIENE_PRECIERRE_CONTEXTO" or "SIGLAS_TIPO_NOMINA_ACTUAL";
    public static string? ValorTipado(string provider,RhNomCargoActual source)=>provider switch {
        "FECHA_NOMINA_CONTEXTO"=>source.FechaNomina.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),
        "FECHA_INICIO_RELACION"=>source.FechaIni?.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),
        "TIENE_PRECIERRE_CONTEXTO"=>source.FechaPreCierre is null?"false":"true",
        "SIGLAS_TIPO_NOMINA_ACTUAL"=>source.SiglasTipoNomina,
        _=>throw new RhNomError("PROVEEDOR_NO_REGISTRADO","Proveedor tipado no registrado.")};
    public static string Hash(RhNomCargoActual source)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
}
public record RhNomEntradasCapturadas(RhNomCargoActual Fuente,List<RhNomEntradaActual> Entradas);
public sealed class RhNomFuentesActualesHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomResultadoActual>> Handle(RhNomSimularActual value,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        using var cn=await RhNomCfgDb.Abrir(db,ct);return await Capturar(cn,value,company,ct);
    });
    public static async Task<ResultDto<RhNomResultadoActual>> Capturar(OracleConnection cn,RhNomSimularActual value,int company,CancellationToken ct)
    {
        if(value.PoliticaPresupuesto==RhNomPresupuestos.PoliticaContrato)throw new RhNomError("CONTRATO_REQUIERE_CAPTURA","El contrato de presupuesto requiere el flujo de captura V5 del servidor.");
        var saved=await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
        var captured=await LeerEntradas(cn,saved,value,company,ct);
        var simulation=RhNomParametros.Simular(saved,DateOnly.FromDateTime(captured.Fuente.FechaNomina),captured.Entradas.Select(v=>new RhNomValorEntrada(v.CodigoVariable,v.Valor)).ToList(),ct);
        return new ResultDto<RhNomResultadoActual>(new(captured.Fuente,RhNomFuentesActuales.Hash(captured.Fuente),captured.Entradas,simulation,value.PoliticaPresupuesto,value.MotivoPresupuesto?.Trim())){IsValid=simulation.Simulacion.Errores.Count==0,Message=simulation.Simulacion.Errores.Count==0?"Contexto actual leido y simulado sin movimientos. No certifica paridad historica.":simulation.Simulacion.Errores[0].Mensaje};
    }
    // Uses the caller's connection/transaction; reads evidence without evaluating formulas.
    public static async Task<RhNomEntradasCapturadas> LeerEntradas(OracleConnection cn,RhNomModeloVersion saved,RhNomSimularActual value,int company,CancellationToken ct)
    {
        var date=RhNomFuentesActuales.Validar(value,company);
        if(saved.ModeloId!=value.ModeloId||saved.Version!=value.Version||saved.Configuracion.CodigoEmpresa!=company)throw new RhNomError("MODELO_FUENTE_DISTINTO","La revision no coincide con la captura solicitada.");
        var bindings=RhNomFuentesModelo.Resolver(saved.Configuracion,value.Fuentes);
        if(saved.Configuracion.FuentesActuales is not {Count:>0})await RhNomGruposAcum.Revalidar(cn,bindings,company,saved.CodigoTipoNomina,ct);
        RhNomCargoActual source;
        using(var cmd=new OracleCommand("RH.SP_RH_NOM_CAR_ACT",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20})
        {
            RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Int32,saved.CodigoTipoNomina);
            RhNomCfgDb.Add(cmd,"p_PERIODO",OracleDbType.Int32,value.CodigoPeriodo);RhNomCfgDb.Add(cmd,"p_FECHA",OracleDbType.Date,date);RhNomCfgDb.Add(cmd,"p_PRESUPUESTO",OracleDbType.Int32,value.CodigoPresupuesto);
            RhNomCfgDb.Add(cmd,"p_PERSONA",OracleDbType.Int32,value.CodigoPersona);RhNomCfgDb.Add(cmd,"p_RELACION",OracleDbType.Int32,value.CodigoRelacionCargo);cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new RhNomError("CARGO_ACTUAL_NO_ENCONTRADO","No hay relacion/periodo/presupuesto actual con las claves indicadas.");
            DateTime? D(string name)=>r.IsDBNull(r.GetOrdinal(name))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(name)),DateTimeKind.Unspecified);
            int? N(string name)=>r.IsDBNull(r.GetOrdinal(name))?null:RhNomCfgDb.Int(r,name);
            if(D("FECHA_CIERRE") is not null)throw new RhNomError("PERIODO_CERRADO","Fuentes actuales solo para periodo abierto; no reconstruyen nominas cerradas.");
            source=new(RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_TIPO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_PERIODO"),D("FECHA_NOMINA")!.Value,RhNomCfgDb.Int(r,"CODIGO_PRESUPUESTO"),RhNomCfgDb.Int(r,"CODIGO_PERSONA"),RhNomCfgDb.Int(r,"CODIGO_RELACION_CARGO"),N("CODIGO_CARGO"),
                RhNomReferencias.NormalizarMontoOracle(r.IsDBNull(r.GetOrdinal("SUELDO_TEXTO"))?null:RhNomCfgDb.Text(r,"SUELDO_TEXTO"))??throw new RhNomError("SUELDO_NULO","El sueldo actual es nulo."),
                r.IsDBNull(r.GetOrdinal("EXTRA1"))?null:RhNomReferencias.NormalizarMontoOracle(RhNomCfgDb.Text(r,"EXTRA1")),D("FECHA_PRECIERRE"),D("FECHA_INI"),D("FECHA_INI_VIGENCIA"),D("FECHA_FIN_VIGENCIA"),RhNomCfgDb.Text(r,"SIGLAS_TIPO_NOMINA"),N("FRECUENCIA_PAGO_ID"),N("PRESUPUESTO_PERIODO"));
            if(await r.ReadAsync(ct))throw new RhNomError("CARGO_ACTUAL_DUPLICADO","Contexto/relacion duplicados; no se elige ni suma arbitrariamente.");
        }
        if(source.CodigoEmpresa!=company||source.CodigoTipoNomina!=saved.CodigoTipoNomina||source.CodigoPeriodo!=value.CodigoPeriodo||source.FechaNomina!=date||source.CodigoPresupuesto!=value.CodigoPresupuesto||source.CodigoPersona!=value.CodigoPersona||source.CodigoRelacionCargo!=value.CodigoRelacionCargo)
            throw new RhNomError("CONTEXTO_DISTINTO","La lectura no coincide exactamente con el contexto solicitado.");
        var accumulated=new Dictionary<string,RhNomAcumulacion>(StringComparer.Ordinal);
        var definitions=bindings.Where(b=>b?.Proveedor=="ACUMULADOR_MOV_ACTUAL").ToList();
        if(definitions.Count>RhNomAcumuladores.MaxDefiniciones)throw new RhNomError("LIMITE_ACUMULADORES","Hasta 10 acumuladores por contexto.");
        var rowCount=0;
        foreach(var b in definitions)
        {
            var code=RhNomNumeros.Codigo(b.CodigoVariable);
            if(b.Acumulador is null||accumulated.ContainsKey(code))throw new RhNomError("ACUMULADOR_INVALIDO","Definicion requerida y variable unica.");
            var read=await RhNomAcumuladores.Leer(cn,source,b.Acumulador,ct);rowCount+=read.Filas.Count;
            if(rowCount>RhNomAcumuladores.MaxFilasGlobal)throw new RhNomError("LIMITE_ACUMULADORES","Las lecturas exceden 5000 partidas por contexto.");
            accumulated.Add(code,read);
        }
        var parameters=new Dictionary<string,RhNomParametroConceptoCapturado>(StringComparer.Ordinal);
        var cache=new Dictionary<RhNomParametroConceptoBinding,RhNomParametroConceptoCapturado>();
        foreach(var b in bindings.Where(b=>RhNomParametrosConcepto.Unidades.ContainsKey(b.Proveedor)))
        {
            var def=RhNomParametrosConcepto.Validar(b.ParametroConcepto!);
            if(!cache.TryGetValue(def,out var read)){read=await RhNomParametrosConcepto.Leer(cn,source,def,ct);cache.Add(def,read);}
            parameters.Add(b.CodigoVariable,read);
        }
        var controls=new Dictionary<string,RhNomControlCapturado>(StringComparer.Ordinal);var controlCache=new Dictionary<RhNomControlBinding,RhNomControlCapturado>();var controlRows=0;
        foreach(var b in bindings.Where(b=>b.Proveedor==RhNomControles.Proveedor))
        {
            var def=RhNomControles.Validar(b.Control!);
            if(!controlCache.TryGetValue(def,out var read))
            {
                read=await RhNomControles.Leer(cn,source,def,ct);controlCache.Add(def,read);controlRows+=read.Filas.Count;
                if(controlRows>RhNomControles.MaxFilasGlobal)throw new RhNomError("LIMITE_CONTROLES","Las lecturas exceden 1000 controles por contexto.");
            }
            controls.Add(b.CodigoVariable,read);
        }
        var histories=new Dictionary<string,RhNomAcumHistorico>(StringComparer.Ordinal);var historyRows=0;
        foreach(var b in bindings.Where(b=>RhNomAcumuladoresHistoricos.EsProveedor(b.Proveedor)))
        {
            RhNomAcumuladoresHistoricos.ValidarProveedor(b.Proveedor,b.AcumuladorHistorico!);var read=await RhNomAcumuladoresHistoricos.Leer(cn,RhNomAcumuladoresHistoricos.Contexto(source),source.CodigoPersona,b.AcumuladorHistorico!,ct);historyRows+=read.Partidas.Count;
            if(historyRows>RhNomAcumuladores.MaxFilasGlobal)throw new RhNomError("LIMITE_ACUMULADORES","Hasta 5000 partidas historicas por contexto.");
            histories.Add(b.CodigoVariable,read);
        }
        var personals=new Dictionary<string,RhNomPersonalCapturado>(StringComparer.Ordinal);var personalCache=new Dictionary<string,RhNomPersonalCapturado>(StringComparer.Ordinal);var personalRows=0;
        foreach(var b in bindings.Where(b=>RhNomDatosPersonales.Proveedores.ContainsKey(b.Proveedor)))
        {
            if(!personalCache.TryGetValue(b.Proveedor,out var read))
            {
                read=await RhNomDatosPersonales.Leer(cn,source,b.Proveedor,ct);personalRows+=read.Documento.Filas.Count;
                if(personalRows>600)throw new RhNomError("LIMITE_DATOS_PERSONALES","Hasta 600 registros personales por contexto.");
                personalCache.Add(b.Proveedor,read);
            }
            personals.Add(b.CodigoVariable,read);
        }
        var phInputs=new Dictionary<string,RhNomAgregadosPhCapturados>(StringComparer.Ordinal);var phCache=new Dictionary<string,RhNomAgregadosPhCapturados>(StringComparer.Ordinal);var phRows=0;
        foreach(var binding in bindings.Where(b=>RhNomAgregadosPh.Proveedores.ContainsKey(b.Proveedor)))
        {
            var def=RhNomAgregadosPh.Validar(binding.AgregadosPh!,source);var key=JsonSerializer.Serialize(def);
            if(!phCache.TryGetValue(key,out var captured))
            {
                if(phCache.Count>=4)throw new RhNomError("LIMITE_DEFINICIONES_PH","Hasta cuatro definiciones PH distintas por contexto.");
                captured=await RhNomAgregadosPh.Leer(cn,source,def,ct);phRows+=captured.Documento.Actuales.Count+captured.Documento.Historicas.Count;
                if(phRows>5000)throw new RhNomError("LIMITE_FILAS_PH","Hasta 5000 filas PH entre definiciones distintas; no se trunca.");phCache.Add(key,captured);
            }
            phInputs.Add(binding.CodigoVariable,captured);
        }
        var inputs=RhNomFuentesActuales.Resolver(saved.Configuracion,source,bindings,ct,value.PoliticaPresupuesto,value.MotivoPresupuesto,accumulated,parameters,controls,histories,personals,phInputs);
        return new(source,inputs);
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("fuentesActualesCatalogo")]
    public Task<IActionResult> CurrentSourcesCatalog()=>Run("rh.nomina.configuracion.leer",_=>new ResultDto<IReadOnlyList<RhNomProveedor>>(RhNomFuentesActuales.Catalogo){IsValid=true,Message="Fuentes actuales registradas del contexto explicito."});
    [HttpPost("simularContextoActual")]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> SimulateCurrent(RhNomSimularActual value)=>RunAsync("rh.nomina.fuentes.datos",(company,_,ct)=>new RhNomFuentesActualesHandler(db).Handle(value,company,ct),"rh.nomina.simular");
}
