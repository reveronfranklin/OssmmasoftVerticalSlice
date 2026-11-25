namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Decisions after the monetary graph, never formula outputs or input exclusions.
public record RhNomAccionControlSave(string Clave,int CodigoConcepto,string HashConceptoEsperado,
    string CodigoVariableControl,string TipoMovimiento,string ValorRetiro);
public record RhNomAccionControl(string Clave,RhNomConceptoCfg Concepto,string HashConceptoCapturado,
    string CodigoVariableControl,string TipoMovimiento,string ValorRetiro);
public record RhNomDecisionControl(string Clave,int CodigoConcepto,int FrecuenciaId,string TipoMovimiento,
    string CodigoVariableControl,string ValorControl,string Accion);
public static class RhNomAccionesControl
{
    public const string Version="ACCIONES_CONTROL_V1";
    public static RhNomAccionControlSave Solicitud(RhNomAccionControl a)=>new(a.Clave,a.Concepto.CodigoConcepto,a.HashConceptoCapturado,a.CodigoVariableControl,a.TipoMovimiento,a.ValorRetiro);
    public static List<RhNomAccionControl> Validar(IReadOnlyList<RhNomAccionControlSave>? actions,
        IReadOnlyList<RhNomVariable> variables,IReadOnlyList<RhNomFuenteActualBinding> sources,
        IReadOnlyList<RhNomConceptoCfg>? concepts,int company,int type,IReadOnlyList<RhNomReglaConcepto> monetary)
    {
        if(actions is null||actions.Count==0)return [];
        if(actions.Count>20||actions.Any(a=>a is null)||concepts is null)
            throw new RhNomError("ACCIONES_CONTROL_INVALIDAS","Hasta veinte decisiones completas con catalogo de conceptos requerido.");
        var keys=new HashSet<string>();var destinations=new HashSet<(int,string)>();var result=new List<RhNomAccionControl>();
        foreach(var a in actions)
        {
            var key=RhNomNumeros.Codigo(a.Clave);var code=RhNomNumeros.Codigo(a.CodigoVariableControl);
            var concept=concepts.SingleOrDefault(c=>c.CodigoConcepto==a.CodigoConcepto);
            if(concept is null||!concept.Elegible||concept.CodigoEmpresa!=company||concept.CodigoTipoNomina!=type||concept.Huella!=a.HashConceptoEsperado)
                throw new RhNomError("CONCEPTO_ACCION_INVALIDO","La decision requiere concepto automatico elegible de la empresa/tipo y huella coincidente.");
            if(!keys.Add(key)||!destinations.Add((a.CodigoConcepto,a.TipoMovimiento))||monetary.Any(r=>r.Concepto.CodigoConcepto==a.CodigoConcepto))
                throw new RhNomError("DESTINO_ACCION_DUPLICADO","Un destino no puede tener calculo monetario y decision de control ni acciones repetidas.");
            if(a.TipoMovimiento is not ("F" or "V")||a.ValorRetiro is not ("0" or "1"))
                throw new RhNomError("POLITICA_ACCION_INVALIDA","Movimiento F/V y valor de retiro explicito 0/1 requeridos.");
            if(variables.Count(v=>v.Codigo==code&&v.Origen=="ENTRADA"&&v.Tipo=="NUMERO"&&v.Unidad=="NUMERO")!=1)
                throw new RhNomError("VARIABLE_CONTROL_INVALIDA","La decision requiere una entrada numerica de control, no una formula monetaria.");
            var source=sources.SingleOrDefault(s=>s.CodigoVariable==code);
            if(source?.Proveedor!=RhNomControles.Proveedor||source.Control?.CodigoConcepto!=a.CodigoConcepto)
                throw new RhNomError("FUENTE_CONTROL_DISTINTA","El control debe capturarse para el mismo concepto objetivo con su politica versionada.");
            result.Add(new(key,concept,concept.Huella,code,a.TipoMovimiento,a.ValorRetiro));
        }
        return result.OrderBy(a=>a.Clave,StringComparer.Ordinal).ToList();
    }
    public static List<RhNomDecisionControl> Proyectar(RhNomSnapshot snapshot,IReadOnlyList<RhNomValorEntrada> inputs)
    {
        var stored=snapshot.AccionesControl??[];
        if(stored.Count==0)return [];
        if(snapshot.VersionAccionesControl!=Version)throw new RhNomError("VERSION_ACCIONES_INVALIDA","Version de decisiones de control incompatible.");
        var actions=Validar(stored.Select(Solicitud).ToList(),snapshot.Variables,snapshot.FuentesActuales??[],
            stored.Select(a=>a.Concepto).Distinct().ToList(),snapshot.CodigoEmpresa,snapshot.CodigoTipoNomina,snapshot.ReglasConcepto??[]);
        var values=inputs.ToDictionary(i=>RhNomNumeros.Codigo(i.Codigo),i=>i.Valor,StringComparer.Ordinal);
        return actions.Select(a=>
        {
            if(!values.TryGetValue(a.CodigoVariableControl,out var value))
                throw new RhNomError("VALOR_ACCION_CONTROL_INVALIDO","Falta la entrada de control de la decision.");
            value=RhNomNumeros.Texto(RhNomNumeros.Leer(value));
            if(value is not ("0" or "1"))
                throw new RhNomError("VALOR_ACCION_CONTROL_INVALIDO","La decision requiere un control capturado de valor 0 o 1.");
            return new RhNomDecisionControl(a.Clave,a.Concepto.CodigoConcepto,a.Concepto.FrecuenciaId!.Value,a.TipoMovimiento,
                a.CodigoVariableControl,value,value==a.ValorRetiro?"RETIRAR_GENERADO":"CONSERVAR_EXISTENTE");
        }).ToList();
    }
}
