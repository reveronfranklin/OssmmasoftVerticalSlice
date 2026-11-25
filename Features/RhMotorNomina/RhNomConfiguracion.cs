using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomVariableSave(int Id, int VersionEsperada, string Codigo, string Descripcion,
    string Tipo, string Unidad, string Origen, bool Activa = true);
public record RhNomVariable(int Id, string Codigo, string Descripcion, string Tipo, string Unidad,
    string Origen, bool Activa, int Version);
public record RhNomListaQuery(int PageNumber = 1, int PageSize = 20, string SearchText = "");
public record RhNomVariableRef(int Id, int VersionEsperada);
public record RhNomModeloSave(int ModeloId, int VersionEsperada, int CodigoTipoNomina, string Escenario,
    string Descripcion, List<RhNomVariableRef> Variables, List<RhNomFormula> Formulas, List<RhNomParametro>? Parametros = null, bool ValidarUnidades = false, string? VersionCondicionesCliente = null, List<RhNomTablaTramos>? TablasTramos = null, string? VersionTramosCliente = null, List<RhNomReglaConceptoSave>? ReglasConcepto = null, string? VersionReglasCliente = null, List<RhNomFuenteActualBinding>? FuentesActuales = null, string? VersionFuentesCliente = null, List<RhNomAccionControlSave>? AccionesControl = null, string? VersionAccionesCliente = null);
public record RhNomModeloVersionQuery(int ModeloId, int Version);
public record RhNomModeloVersionSimular(int ModeloId, int Version, List<RhNomValorEntrada> Entradas);
public record RhNomModeloVersion(int ModeloId, int Version, int CodigoTipoNomina, string Escenario,
    string Descripcion, string Hash, int UsuarioIns, DateTime FechaIns, RhNomSnapshot Configuracion)
{
    public string Estado {get;init;} = "BORRADOR_NO_PUBLICADO";
}
public record RhNomModeloCatalogo(int ModeloId, string Descripcion, int UltimaVersion);
public record RhNomSnapshot(int Formato, int CodigoEmpresa, int ModeloId, int CodigoTipoNomina,
    string Escenario, string Descripcion, List<RhNomVariable> Variables, List<RhNomFormula> Formulas,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<RhNomParametro>? Parametros = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VersionUnidades = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VersionCondiciones = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<RhNomTablaTramos>? TablasTramos = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VersionTramos = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] List<RhNomReglaConcepto>? ReglasConcepto = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? VersionReglas = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] List<RhNomFuenteActualBinding>? FuentesActuales = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? VersionFuentesModelo = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] List<RhNomAccionControl>? AccionesControl = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? VersionAccionesControl = null);

public static class RhNomConfiguracion
{
    public const int MaxBytes = 1048576;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    public static RhNomVariableSave Variable(RhNomVariableSave value)
    {
        if (value.Id < 0 || value.VersionEsperada < 0 || (value.Id == 0 && value.VersionEsperada != 0))
            throw new RhNomError("VERSION_INVALIDA", "Id/version de variable invalidos.");
        var code = RhNomNumeros.Codigo(value.Codigo);
        if (string.IsNullOrWhiteSpace(value.Descripcion) || value.Descripcion.Trim().Length > 300)
            throw new RhNomError("DESCRIPCION_INVALIDA", "Descripcion requerida, hasta 300 caracteres.");
        RhNomValores.ValidarTipo(value.Tipo,value.Unidad,value.Origen);
        if (value.Origen is not ("ENTRADA" or "PARAMETRO" or "CALCULADA"))
            throw new RhNomError("ORIGEN_INVALIDO", "Origen debe ser ENTRADA, PARAMETRO o CALCULADA.");
        return value with { Codigo = code, Descripcion = value.Descripcion.Trim() };
    }
    public static RhNomListaQuery Lista(RhNomListaQuery value)
    {
        if (value.PageNumber is < 1 or > 100000 || value.PageSize is < 1 or > 100 || value.SearchText?.Length > 100)
            throw new RhNomError("PAGINACION_INVALIDA", "Pagina 1..100000, tamano 1..100 y busqueda hasta 100 caracteres.");
        return value with { SearchText = value.SearchText?.Trim() ?? "" };
    }
    public static RhNomSnapshot Snapshot(RhNomModeloSave value, IReadOnlyList<RhNomVariable> variables, int company, CancellationToken ct = default, IReadOnlyList<RhNomConceptoCfg>? conceptos = null)
    {
        if (company <= 0 || value.ModeloId <= 0 || value.VersionEsperada < 0 || value.CodigoTipoNomina <= 0)
            throw new RhNomError("CONTEXTO_INVALIDO", "Modelo, empresa, tipo de nomina o version invalidos.");
        if (value.Escenario is not ("ADJUNTAR_CARGO" or "RECALCULA_VARIACIONES"))
            throw new RhNomError("ESCENARIO_INVALIDO", "Escenario no admitido.");
        if (string.IsNullOrWhiteSpace(value.Descripcion) || value.Descripcion.Trim().Length > 300)
            throw new RhNomError("DESCRIPCION_INVALIDA", "Descripcion requerida, hasta 300 caracteres.");
        if (value.Variables is null || value.Variables.Count is < 1 or > 200 || value.Variables.Any(v => v is null || v.Id <= 0 || v.VersionEsperada <= 0)
            || value.Variables.Select(v => v.Id).Distinct().Count() != value.Variables.Count)
            throw new RhNomError("VARIABLES_INVALIDAS", "Indique 1..200 referencias distintas con id y version vigentes.");
        var byId = variables.ToDictionary(v => v.Id);
        var chosen = new List<RhNomVariable>();
        foreach (var reference in value.Variables)
        {
            ct.ThrowIfCancellationRequested();
            if (!byId.TryGetValue(reference.Id, out var variable) || !variable.Activa || variable.Version != reference.VersionEsperada)
                throw new RhNomError("VARIABLE_DESACTUALIZADA", "Una variable no pertenece al alcance, esta inactiva o cambio de version.");
            Variable(new(variable.Id, variable.Version, variable.Codigo, variable.Descripcion, variable.Tipo, variable.Unidad, variable.Origen, variable.Activa));
            chosen.Add(variable);
        }
        if (chosen.Select(v => v.Codigo).Distinct(StringComparer.Ordinal).Count() != chosen.Count)
            throw new RhNomError("VARIABLE_DUPLICADA", "Hay codigos repetidos en las variables seleccionadas.");
        var inputs = chosen.Where(v => v.Origen != "CALCULADA").Select(v => v.Codigo).ToList();
        var plan = RhNomModelos.Planificar(value.Formulas, inputs, ct);
        var outputs = chosen.Where(v => v.Origen == "CALCULADA").Select(v => v.Codigo).ToHashSet(StringComparer.Ordinal);
        if (!outputs.SetEquals(plan.Formulas.Keys))
            throw new RhNomError("SALIDAS_INVALIDAS", "Cada variable CALCULADA requiere una formula, y toda salida debe estar catalogada como CALCULADA.");
        var typed=chosen.Any(v=>v.Tipo!="NUMERO");
        var conditional = plan.Condiciones.Count > 0;
        if(typed&&(!value.ValidarUnidades||value.VersionCondicionesCliente!=RhNomCondiciones.Version))throw new RhNomError("CLIENTE_TIPADO_REQUERIDO","Variables tipadas requieren unidades validadas y cliente de condiciones V2.");
        ValidarCliente(value, null);
        if (conditional && !value.ValidarUnidades)
            throw new RhNomError("VALIDACION_UNIDADES_REQUERIDA", "Las condiciones requieren validacion de unidades.");
        var originals = value.Formulas.ToDictionary(f => RhNomNumeros.Codigo(f.VariableSalida), StringComparer.Ordinal);
        var ordered = plan.Orden.Select(name => new RhNomFormula(name, originals[name].Expresion.Trim(),
            plan.Condiciones.GetValueOrDefault(name), originals[name].ExpresionAlternativa?.Trim())).ToList();
        if(!value.ValidarUnidades && plan.Formulas.Values.Concat(plan.Alternativas.Values).Any(f=>f.UsaConversionUnidades||f.SalidasSumadas.Count>0))
            throw new RhNomError("VALIDACION_UNIDADES_REQUERIDA","Las conversiones TASA/PORCENTAJE requieren validacion de unidades en la revision.");
        if(value.ValidarUnidades)RhNomUnidades.Validar(RhNomValores.Catalogo(chosen),ordered,ct);
        var parameters = RhNomParametros.Validar(value.Parametros, chosen, value.CodigoTipoNomina, ct);
        var tables=RhNomTramos.Validar(value.TablasTramos,chosen,value.CodigoTipoNomina,parameters,ct);
        if(tables.Count>0&&!value.ValidarUnidades)throw new RhNomError("VALIDACION_UNIDADES_REQUERIDA","Las tablas de tramos requieren validacion de unidades.");
        var rules=RhNomReglasConcepto.Validar(value.ReglasConcepto,chosen,conceptos,company,value.CodigoTipoNomina,ct);
        if(rules.Count>0&&!value.ValidarUnidades)throw new RhNomError("VALIDACION_UNIDADES_REQUERIDA","Las reglas requieren validacion de unidades.");
        var sources=RhNomFuentesModelo.Validar(value.FuentesActuales,chosen);
        foreach(var group in sources.SelectMany(b=>new[]{b.Acumulador?.Grupo,b.AcumuladorHistorico?.Filtros?.Grupo,b.AgregadosPh?.GrupoSi}).Where(g=>g is not null))RhNomGruposAcum.ValidarUso(group,company,value.CodigoTipoNomina,group!.FechaReferencia);
        if(sources.Count>0&&!value.ValidarUnidades)throw new RhNomError("VALIDACION_UNIDADES_REQUERIDA","Los proveedores versionados requieren unidades validadas.");
        var actions=RhNomAccionesControl.Validar(value.AccionesControl,chosen,sources,conceptos,company,value.CodigoTipoNomina,rules);
        if(actions.Count>0&&(!value.ValidarUnidades||value.Escenario!="RECALCULA_VARIACIONES"))throw new RhNomError("CONTEXTO_ACCIONES_INVALIDO","Decisiones de control requieren unidades verificadas y escenario de recalculo.");
        return new(actions.Count>0 ? 9 : typed ? 8 : sources.Count>0 ? 7 : rules.Count>0 ? 6 : tables.Count>0 ? 5 : conditional ? 4 : value.ValidarUnidades ? 3 : parameters.Count > 0 ? 2 : 1, company, value.ModeloId, value.CodigoTipoNomina, value.Escenario, value.Descripcion.Trim(),
            chosen.OrderBy(v => v.Codigo, StringComparer.Ordinal).ToList(), ordered, parameters.Count > 0 ? parameters : null, value.ValidarUnidades ? RhNomUnidades.Version : null, conditional ? RhNomCondiciones.Version : null, tables.Count>0 ? tables : null, tables.Count>0 ? RhNomTramos.Version : null, rules.Count>0 ? rules : null, rules.Count>0 ? RhNomReglasConcepto.Version : null, sources.Count>0 ? sources : null, sources.Count>0 ? RhNomFuentesModelo.Version : null, actions.Count>0 ? actions : null, actions.Count>0 ? RhNomAccionesControl.Version : null);
    }
    public static void ValidarCliente(RhNomModeloSave value, RhNomSnapshot? anterior)
    {
        if(value.VersionAccionesCliente is not null&&value.VersionAccionesCliente!=RhNomAccionesControl.Version)throw new RhNomError("VERSION_ACCIONES_INVALIDA","Version de acciones del cliente no soportada.");
        if((value.AccionesControl?.Count>0||anterior?.VersionAccionesControl is not null)&&value.VersionAccionesCliente!=RhNomAccionesControl.Version)throw new RhNomError("CLIENTE_ACCIONES_REQUERIDO","Actualice el cliente para conservar las decisiones de control de la revision.");
        if(value.VersionFuentesCliente is not null&&value.VersionFuentesCliente!=RhNomFuentesModelo.Version)throw new RhNomError("VERSION_FUENTES_MODELO_INVALIDA","Version de proveedores del cliente no soportada.");
        if((value.FuentesActuales?.Count>0||anterior?.VersionFuentesModelo is not null)&&value.VersionFuentesCliente!=RhNomFuentesModelo.Version)throw new RhNomError("CLIENTE_FUENTES_REQUERIDO","Actualice el cliente para editar proveedores versionados sin perderlos.");
        if(value.VersionReglasCliente is not null&&value.VersionReglasCliente!=RhNomReglasConcepto.Version)throw new RhNomError("VERSION_REGLAS_INVALIDA","Version de reglas no soportada.");
        if((value.ReglasConcepto?.Count>0||anterior?.VersionReglas is not null)&&value.VersionReglasCliente!=RhNomReglasConcepto.Version)
            throw new RhNomError("CLIENTE_REGLAS_REQUERIDO","Actualice el cliente para editar reglas sin perderlas.");
        if(value.VersionTramosCliente is not null&&value.VersionTramosCliente!=RhNomTramos.Version)
            throw new RhNomError("VERSION_TRAMOS_INVALIDA","Version de tramos del cliente no soportada.");
        if((value.TablasTramos?.Count>0||anterior?.VersionTramos is not null)&&value.VersionTramosCliente!=RhNomTramos.Version)
            throw new RhNomError("CLIENTE_TRAMOS_REQUERIDO","Actualice el cliente para editar tablas de tramos sin perderlas.");
        if (value.VersionCondicionesCliente is not null && value.VersionCondicionesCliente != RhNomCondiciones.Version)
            throw new RhNomError("VERSION_CONDICIONES_INVALIDA", "Version de condiciones del cliente no soportada.");
        if ((value.Formulas?.Any(f => f?.Condicion is not null) == true || anterior?.VersionCondiciones is not null)
            && value.VersionCondicionesCliente != RhNomCondiciones.Version)
            throw new RhNomError("CLIENTE_CONDICIONES_REQUERIDO", "Actualice el cliente para editar revisiones con condiciones sin perderlas.");
    }
    public static (string Text, string Hash) Serializar(RhNomSnapshot snapshot)
    {
        var text = JsonSerializer.Serialize(snapshot, Json);
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new RhNomError("MODELO_DEMASIADO_GRANDE", "La configuracion supera 1 MiB.");
        return (text, Hash(text));
    }
    public static RhNomSnapshot Leer(string text, string hash, int company, int model)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes || !string.Equals(Hash(text), hash, StringComparison.Ordinal))
            throw new RhNomError("CONFIGURACION_ALTERADA", "La configuracion no coincide con su hash o supera el limite.");
        try
        {
            var snapshot = JsonSerializer.Deserialize<RhNomSnapshot>(text, Json);
            if (snapshot is null || snapshot.Formato is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9) || snapshot.CodigoEmpresa != company || snapshot.ModeloId != model)
                throw new RhNomError("CONTEXTO_DISTINTO", "Formato, modelo o empresa de la revision incompatibles.");
            if(snapshot.VersionUnidades is not null && snapshot.VersionUnidades!=RhNomUnidades.Version)
                throw new RhNomError("VERSION_UNIDADES_INVALIDA","Version de analisis de unidades no soportada.");
            if (snapshot.VersionCondiciones is not null && snapshot.VersionCondiciones is not (RhNomCondiciones.Version or RhNomCondiciones.VersionAnterior))
                throw new RhNomError("VERSION_CONDICIONES_INVALIDA", "Version de condiciones no soportada.");
            if(snapshot.Formato!=9&&(snapshot.Formato==8)!=snapshot.Variables.Any(v=>v.Tipo!="NUMERO"))throw new RhNomError("FORMATO_TIPADO_INVALIDO","Variables no numericas requieren formato 8.");
            if(snapshot.Formato==8&&snapshot.VersionCondiciones==RhNomCondiciones.VersionAnterior)throw new RhNomError("VERSION_CONDICIONES_INVALIDA","Condiciones numericas V1 no admiten variables tipadas.");
            if ((snapshot.Formulas.Any(f=>f.Condicion is not null)) != (snapshot.VersionCondiciones is not null))
                throw new RhNomError("CONFIGURACION_INVALIDA", "Formato y version de condiciones incompatibles.");
            if(snapshot.VersionTramos is not null&&snapshot.VersionTramos!=RhNomTramos.Version)
                throw new RhNomError("VERSION_TRAMOS_INVALIDA","Version de tramos no soportada.");
            if((snapshot.TablasTramos?.Count>0)!=(snapshot.VersionTramos is not null))
                throw new RhNomError("CONFIGURACION_INVALIDA","Formato y version de tramos incompatibles.");
            if(snapshot.VersionReglas is not null&&snapshot.VersionReglas!=RhNomReglasConcepto.Version)throw new RhNomError("VERSION_REGLAS_INVALIDA","Version de reglas no soportada.");
            if((snapshot.ReglasConcepto?.Count>0)!=(snapshot.VersionReglas is not null))throw new RhNomError("CONFIGURACION_INVALIDA","Formato y reglas incompatibles.");
            if(snapshot.ReglasConcepto?.Any(r=>r is null||r.Concepto is null||r.HashConceptoCapturado!=r.Concepto.Huella)==true)throw new RhNomError("REGLAS_ALTERADAS","Metadata de concepto alterada.");
            if(snapshot.VersionFuentesModelo is not null&&snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V1" or "FUENTES_MODELO_V2" or "FUENTES_MODELO_V3" or "FUENTES_MODELO_V4" or "FUENTES_MODELO_V5" or "FUENTES_MODELO_V6" or "FUENTES_MODELO_V7" or "FUENTES_MODELO_V8" or "FUENTES_MODELO_V9" or "FUENTES_MODELO_V10" or RhNomFuentesModelo.Version))throw new RhNomError("VERSION_FUENTES_MODELO_INVALIDA","Version de proveedores del modelo no soportada.");
            if(snapshot.VersionFuentesModelo=="FUENTES_MODELO_V1"&&snapshot.FuentesActuales?.Any(b=>b.ParametroConcepto is not null)==true)throw new RhNomError("CONFIGURACION_INVALIDA","La version 1 de proveedores no admite parametros de concepto.");
            if(snapshot.VersionFuentesModelo is "FUENTES_MODELO_V1" or "FUENTES_MODELO_V2"&&snapshot.FuentesActuales?.Any(b=>b.Control is not null)==true)throw new RhNomError("CONFIGURACION_INVALIDA","Los esquemas de proveedores anteriores no admiten controles.");
            if(snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V4" or "FUENTES_MODELO_V5" or "FUENTES_MODELO_V6" or "FUENTES_MODELO_V7" or "FUENTES_MODELO_V8" or "FUENTES_MODELO_V9" or "FUENTES_MODELO_V10" or RhNomFuentesModelo.Version)&&snapshot.FuentesActuales?.Any(b=>b.AcumuladorHistorico is not null)==true)throw new RhNomError("CONFIGURACION_INVALIDA","Esquemas previos no admiten acumuladores historicos.");
            if(snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V5" or "FUENTES_MODELO_V6" or "FUENTES_MODELO_V7" or "FUENTES_MODELO_V8" or "FUENTES_MODELO_V9" or "FUENTES_MODELO_V10" or RhNomFuentesModelo.Version)&&snapshot.FuentesActuales?.Any(b=>b.Acumulador?.Grupo is not null||b.AcumuladorHistorico?.Filtros?.Grupo is not null)==true)throw new RhNomError("CONFIGURACION_INVALIDA","Esquemas previos no admiten grupos capturados.");
            if(snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V6" or "FUENTES_MODELO_V7" or "FUENTES_MODELO_V8" or "FUENTES_MODELO_V9" or "FUENTES_MODELO_V10" or RhNomFuentesModelo.Version)&&snapshot.Variables.Any(v=>v.Tipo!="NUMERO")&&snapshot.FuentesActuales?.Count>0)throw new RhNomError("CONFIGURACION_INVALIDA","Proveedores tipados requieren esquema V6.");
            if(snapshot.Formato==7&&snapshot.VersionFuentesModelo is null||snapshot.VersionFuentesModelo is not null&&snapshot.Formato is not (7 or 8 or 9)||(snapshot.FuentesActuales?.Count>0)!=(snapshot.VersionFuentesModelo is not null))throw new RhNomError("CONFIGURACION_INVALIDA","Formato y proveedores versionados incompatibles.");
            if(snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V7" or "FUENTES_MODELO_V8" or "FUENTES_MODELO_V9" or "FUENTES_MODELO_V10" or RhNomFuentesModelo.Version)&&snapshot.FuentesActuales?.Any(b=>RhNomDatosPersonales.Proveedores.ContainsKey(b.Proveedor))==true)throw new RhNomError("VERSION_FUENTES_MODELO_INVALIDA","Las fuentes personales requieren esquema V7.");
            if(snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V8" or "FUENTES_MODELO_V9" or "FUENTES_MODELO_V10" or RhNomFuentesModelo.Version)&&snapshot.FuentesActuales?.Any(b=>b.ValoresPermitidos is not null||b.Acumulador?.TiposMovimiento.Contains("E")==true||b.AcumuladorHistorico?.Filtros.TiposMovimiento.Contains("E")==true||b.Acumulador?.Grupo?.PoliticaDatos is not null||b.AcumuladorHistorico?.Filtros.Grupo?.PoliticaDatos is not null)==true)throw new RhNomError("VERSION_FUENTES_MODELO_INVALIDA","Restricciones de entrada y acumulacion E requieren esquema V8.");
            if(snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V9" or "FUENTES_MODELO_V10" or RhNomFuentesModelo.Version)&&snapshot.FuentesActuales?.Any(b=>b.Proveedor==RhNomAcumuladoresHistoricos.ProveedorMes||b.AcumuladorHistorico?.Ventana is not null)==true)throw new RhNomError("VERSION_FUENTES_MODELO_INVALIDA","La ventana del mismo mes requiere esquema V9.");
            if(snapshot.VersionFuentesModelo is not ("FUENTES_MODELO_V10" or RhNomFuentesModelo.Version)&&snapshot.FuentesActuales?.Any(b=>b.AgregadosPh is not null||RhNomAgregadosPh.Proveedores.ContainsKey(b.Proveedor))==true)throw new RhNomError("VERSION_FUENTES_MODELO_INVALIDA","Los agregados PH requieren esquema V10.");
            if(snapshot.VersionFuentesModelo!=RhNomFuentesModelo.Version&&snapshot.FuentesActuales?.Any(b=>b.Proveedor==RhNomDatosPersonales.UltimoOSinRegistro)==true)throw new RhNomError("VERSION_FUENTES_MODELO_INVALIDA","La ausencia de educacion explicita requiere esquema V11.");
            if((snapshot.AccionesControl?.Count>0)!=(snapshot.Formato==9)||(snapshot.AccionesControl?.Count>0)!=(snapshot.VersionAccionesControl is not null)||snapshot.VersionAccionesControl is not null&&snapshot.VersionAccionesControl!=RhNomAccionesControl.Version)throw new RhNomError("VERSION_ACCIONES_INVALIDA","Formato y version de decisiones de control incompatibles.");
            if(snapshot.AccionesControl?.Any(a=>a is null||a.Concepto is null||a.HashConceptoCapturado!=a.Concepto.Huella)==true)throw new RhNomError("ACCIONES_ALTERADAS","Metadata de decisiones distinta de su huella.");
            // Revalidate the graph and schema; the hash is an integrity check, not an authorization grant.
            var save = new RhNomModeloSave(model, 0, snapshot.CodigoTipoNomina, snapshot.Escenario, snapshot.Descripcion,
                snapshot.Variables.Select(v => new RhNomVariableRef(v.Id, v.Version)).ToList(), snapshot.Formulas, snapshot.Parametros, snapshot.VersionUnidades is not null, snapshot.VersionCondiciones is not null||snapshot.Variables.Any(v=>v.Tipo!="NUMERO")?RhNomCondiciones.Version:null, snapshot.TablasTramos, snapshot.VersionTramos, snapshot.ReglasConcepto?.Select(RhNomReglasConcepto.Solicitud).ToList(), snapshot.VersionReglas, snapshot.FuentesActuales, snapshot.VersionFuentesModelo is null ? null : RhNomFuentesModelo.Version, snapshot.AccionesControl?.Select(RhNomAccionesControl.Solicitud).ToList(),snapshot.VersionAccionesControl);
            var validated = Snapshot(save, snapshot.Variables, company, conceptos:(snapshot.ReglasConcepto??[]).Select(r=>r.Concepto).Concat((snapshot.AccionesControl??[]).Select(a=>a.Concepto)).Distinct().ToList());
            if (validated.Formato != snapshot.Formato)
                throw new RhNomError("CONFIGURACION_INVALIDA", "Formato de revision incompatible con sus parametros.");
            return snapshot;
        }
        catch (Exception ex) when (ex is JsonException or NullReferenceException or ArgumentException)
        { throw new RhNomError("CONFIGURACION_INVALIDA", "No se pudo interpretar la configuracion almacenada."); }
    }
    public static RhNomResultadoSimulacion Simular(RhNomSnapshot snapshot, IReadOnlyList<RhNomValorEntrada> entries, CancellationToken ct = default)
    {
        if(snapshot.VersionUnidades is not null)
        {
            if(snapshot.VersionUnidades!=RhNomUnidades.Version)throw new RhNomError("VERSION_UNIDADES_INVALIDA","Version de unidades no soportada.");
            RhNomUnidades.Validar(RhNomValores.Catalogo(snapshot.Variables),snapshot.Formulas,ct);
        }
        var inputs = RhNomValores.Entradas(entries,RhNomValores.Catalogo(snapshot.Variables));
        var expected = snapshot.Variables.Where(v => v.Origen != "CALCULADA").Select(v => v.Codigo).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(inputs.Keys))
            throw new RhNomError("ENTRADAS_INCOMPATIBLES", "Envie exactamente las entradas/parametros declarados en la revision, sin valores adicionales.");
        if(snapshot.FuentesActuales?.Any(b=>b.ValoresPermitidos is not null)==true)
        {
            var normalized=RhNomValores.Entradas(entries,RhNomValores.Catalogo(snapshot.Variables));
            foreach(var b in snapshot.FuentesActuales)RhNomFuentesModelo.ValidarValor(b,snapshot.Variables.Single(v=>v.Codigo==b.CodigoVariable),normalized[b.CodigoVariable].Valor);
        }
        return RhNomModelos.Simular(snapshot.Formulas, entries, ct,RhNomValores.Catalogo(snapshot.Variables));
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
