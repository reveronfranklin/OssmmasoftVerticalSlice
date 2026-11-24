using System.Text.Json;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomFuentesModelo
{
    public const string Version="FUENTES_MODELO_V11";
    public static List<RhNomFuenteActualBinding> Validar(IReadOnlyList<RhNomFuenteActualBinding>? values,IReadOnlyList<RhNomVariable> variables)
    {
        if(values is null||values.Count==0)return [];
        if(values.Count>200)throw new RhNomError("LIMITE_FUENTES_MODELO","Hasta 200 fuentes por modelo.");
        var entries=variables.Where(v=>v.Origen=="ENTRADA").ToDictionary(v=>v.Codigo,StringComparer.Ordinal);var keys=new HashSet<string>(StringComparer.Ordinal);var rows=new List<RhNomFuenteActualBinding>();var accumulators=0;var histories=0;var phDefinitions=new HashSet<string>(StringComparer.Ordinal);var conceptParameters=new HashSet<RhNomParametroConceptoBinding>();var controls=new HashSet<RhNomControlBinding>();
        foreach(var row in values)
        {
            if(row is null)throw new RhNomError("FUENTE_MODELO_INVALIDA","Una fuente de modelo es nula.");
            var code=RhNomNumeros.Codigo(row.CodigoVariable);var provider=RhNomFuentesActuales.Catalogo.SingleOrDefault(p=>p.Codigo==row.Proveedor);
            if(!keys.Add(code)||!entries.TryGetValue(code,out var variable)||provider is null||provider.Unidad!=variable.Unidad)throw new RhNomError("FUENTE_MODELO_INCOMPATIBLE","Fuente duplicada, no registrada o incompatible con ENTRADA/unidad.");
            if(provider.RequiereDiaIso?(row.DiaSemanaIso is null or <1 or >7):row.DiaSemanaIso is not null)throw new RhNomError("DIA_ISO_INVALIDO","Dia ISO solo para el proveedor de calendario, rango 1..7.");
            if(row.ValorSiNulo is not null&&row.Proveedor!="EXTRA1_RELACION_ACTUAL")throw new RhNomError("DEFAULT_NO_ADMITIDO","Solo EXTRA1 admite valor si nulo explicito.");
            var fallback=row.ValorSiNulo is null?null:RhNomNumeros.Texto(RhNomNumeros.Leer(row.ValorSiNulo));RhNomAcumuladorActual? definition=null;
            if(row.Proveedor=="ACUMULADOR_MOV_ACTUAL")
            {
                if(row.Acumulador is null)throw new RhNomError("ACUMULADOR_REQUERIDO","Definicion requerida para fuente de acumulador.");
                definition=RhNomAcumuladores.Validar(row.Acumulador);if(++accumulators>RhNomAcumuladores.MaxDefiniciones)throw new RhNomError("LIMITE_ACUMULADORES","Hasta diez acumuladores por modelo.");
            }
            else if(row.Acumulador is not null)throw new RhNomError("ACUMULADOR_NO_ADMITIDO","Definicion de acumulador ajena al proveedor.");
            if(RhNomParametrosConcepto.Unidades.ContainsKey(row.Proveedor))
            {if(row.ParametroConcepto is null)throw new RhNomError("PARAMETRO_CONCEPTO_REQUERIDO","Defina concepto y politica de vigencia.");RhNomParametrosConcepto.Validar(row.ParametroConcepto);conceptParameters.Add(row.ParametroConcepto);if(conceptParameters.Count>20)throw new RhNomError("LIMITE_PARAMETROS_CONCEPTO","Hasta 20 conceptos/politicas de parametros por contexto.");}
            else if(row.ParametroConcepto is not null)throw new RhNomError("PARAMETRO_NO_ADMITIDO","Parametro de concepto ajeno al proveedor.");
            if(row.Proveedor==RhNomControles.Proveedor)
            {
                if(row.Control is null)throw new RhNomError("CONTROL_REQUERIDO","Defina el concepto, alcance y politicas del control.");
                RhNomControles.Validar(row.Control);controls.Add(row.Control);
                if(controls.Count>RhNomControles.MaxDefiniciones)throw new RhNomError("LIMITE_CONTROLES","Hasta 20 definiciones de control por contexto.");
            }
            else if(row.Control is not null)throw new RhNomError("CONTROL_NO_ADMITIDO","Definicion de control ajena al proveedor.");
            RhNomAcumHistoricoDef? history=null;
            if(RhNomAcumuladoresHistoricos.EsProveedor(row.Proveedor))
            {
                history=RhNomAcumuladoresHistoricos.Validar(row.AcumuladorHistorico!);RhNomAcumuladoresHistoricos.ValidarProveedor(row.Proveedor,history);
                if(++histories>RhNomAcumuladores.MaxDefiniciones)throw new RhNomError("LIMITE_ACUMULADORES","Hasta diez acumuladores historicos por modelo.");
            }
            else if(row.AcumuladorHistorico is not null)throw new RhNomError("ACUMULADOR_HIST_NO_ADMITIDO","Definicion historica ajena al proveedor.");
            RhNomAgregadosPhDef? ph=null;
            if(RhNomAgregadosPh.Proveedores.ContainsKey(row.Proveedor))
            {
                ph=RhNomAgregadosPh.ValidarDefinicion(row.AgregadosPh!);phDefinitions.Add(JsonSerializer.Serialize(ph));if(phDefinitions.Count>4)throw new RhNomError("LIMITE_DEFINICIONES_PH","Hasta cuatro definiciones PH distintas por modelo.");
            }
            else if(row.AgregadosPh is not null)throw new RhNomError("AGREGADOS_PH_NO_ADMITIDOS","Definicion PH ajena al proveedor.");
            List<string>? permitted=null;
            if(row.ValoresPermitidos is not null)
            {
                if(row.ValoresPermitidos.Count is <1 or >50)throw new RhNomError("VALORES_PERMITIDOS_INVALIDOS","Declare 1..50 valores permitidos o quite la restriccion.");
                permitted=row.ValoresPermitidos.Select(v=>RhNomValores.Normalizar(variable.Tipo,v)).Order(StringComparer.Ordinal).ToList();
                if(permitted.Distinct(StringComparer.Ordinal).Count()!=permitted.Count)throw new RhNomError("VALOR_PERMITIDO_DUPLICADO","Valores permitidos repetidos tras normalizar.");
            }
            rows.Add(row with{CodigoVariable=code,ValorSiNulo=fallback,Acumulador=definition,AcumuladorHistorico=history,ValoresPermitidos=permitted,AgregadosPh=ph});
        }
        if(!keys.SetEquals(entries.Keys))throw new RhNomError("FUENTES_MODELO_INCOMPLETAS","Si configura fuentes, indique exactamente una por cada ENTRADA del modelo.");
        return rows.OrderBy(r=>r.CodigoVariable,StringComparer.Ordinal).ToList();
    }
    public static void ValidarValor(RhNomFuenteActualBinding binding,RhNomVariable variable,string value)
    {
        if(binding.ValoresPermitidos is not null&&!binding.ValoresPermitidos.Select(v=>RhNomValores.Normalizar(variable.Tipo,v)).Contains(RhNomValores.Normalizar(variable.Tipo,value),StringComparer.Ordinal))
            throw new RhNomError("VALOR_FUENTE_NO_PERMITIDO",$"[{variable.Codigo}] queda fuera del alcance explicito de esta revision.",variable:variable.Codigo);
    }
    public static List<RhNomFuenteActualBinding> Resolver(RhNomSnapshot model,IReadOnlyList<RhNomFuenteActualBinding>? request)
    {
        var frozen=Validar(model.FuentesActuales,model.Variables);
        if(frozen.Count==0)return Validar(request,model.Variables);
        if(request is {Count:>0}&&JsonSerializer.Serialize(Validar(request,model.Variables))!=JsonSerializer.Serialize(frozen))
            throw new RhNomError("FUENTES_MODELO_DISTINTAS","La revision fija sus proveedores. Cree otra revision para cambiar fuentes o filtros.");
        return frozen;
    }
}
