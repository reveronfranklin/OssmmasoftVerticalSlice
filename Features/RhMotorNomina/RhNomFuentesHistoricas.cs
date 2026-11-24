using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomContextoQuery(int CodigoTipoNomina, int CodigoPeriodo, string FechaNomina);
public record RhNomContextoFuente(RhNomContextoHistorico Contexto, DateTime FechaCierre, int? CodigoPresupuesto,
    int Partidas, int Personas, int MontosNulos, int ClavesInvalidas, int GruposDuplicados);
public record RhNomProveedor(string Codigo, string Unidad, string Descripcion, bool RequiereDiaIso = false, string Version = "CALENDARIO_ISO_V1", bool RequiereCargo = false);
public record RhNomFuenteBinding(string CodigoVariable, string Proveedor, int? DiaSemanaIso = null);
public record RhNomFuenteResuelta(string CodigoVariable, string Valor, string Unidad, string Proveedor,
    int? DiaSemanaIso, string Evidencia, DateTime FechaNomina, string VersionProveedor = "CALENDARIO_ISO_V1", int? CodigoPersona = null, int? CodigoRelacionCargo = null);
public record RhNomHistoricoPreparar(int ModeloId, int Version, int CodigoPeriodo, string FechaNomina,
    List<RhNomFuenteBinding> Fuentes);
public record RhNomPreparacionHistorica(int ModeloId, int Version, string Hash, RhNomContextoFuente Fuente,
    List<RhNomFuenteResuelta> Entradas, List<string> EntradasPendientes, List<RhNomParametroResuelto> Parametros,
    List<RhNomDiagnostico> Bloqueos)
{
    public string Estado => "DIAGNOSTICO_DE_ENTRADAS";
    public bool EntradasResueltas => EntradasPendientes.Count == 0 && Bloqueos.Count == 0;
    public bool ParidadCertificada => false;
}

public static class RhNomFuentesHistoricas
{
    // Registered code only. No configured SQL, Oracle function or provider inference by variable name.
    public static readonly IReadOnlyList<RhNomProveedor> Catalogo = Array.AsReadOnly(new RhNomProveedor[] {
        new("FRECUENCIA_CARGO_SNAPSHOT","NUMERO","Frecuencia conservada del snapshot de cargo",Version:"CARGO_SNAPSHOT_V2",RequiereCargo:true),
        new("FECHA_INGRESO_CARGO_SNAPSHOT","FECHA","Fecha de ingreso conservada; no equivale a FECHA_INI de relacion actual",Version:"CARGO_SNAPSHOT_V2",RequiereCargo:true),
        new("FECHA_NOMINA_CONTEXTO","FECHA","Fecha calendario de nomina",Version:"FECHA_CONTEXTO_V1"),
        new("ANIO_FECHA_NOMINA","NUMERO","Anio de FECHA_NOMINA del periodo cerrado"),
        new("MES_FECHA_NOMINA","NUMERO","Mes de FECHA_NOMINA del periodo cerrado"),
        new("DIAS_MES_NOMINA","DIAS","Dias calendario del mes de FECHA_NOMINA"),
        new("DIA_ISO_MES_NOMINA","CANTIDAD","Ocurrencias de un dia ISO (1=lunes, 7=domingo) en el mes",true),
        new("SUELDO_CARGO_SNAPSHOT","DINERO","Sueldo capturado en snapshot del periodo (no prueba entrada original)",false,"CARGO_SNAPSHOT_V1",true)
    });
    public static DateTime Fecha(string? text)
    {
        if (!DateTime.TryParseExact(text,"yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)
            || date.Year<1900)
            throw new RhNomError("FECHA_HISTORICA_INVALIDA","FechaNomina debe ser yyyy-MM-ddTHH:mm:ss sin zona ni fracciones, conservando la hora Oracle.");
        return DateTime.SpecifyKind(date,DateTimeKind.Unspecified);
    }
    public static DateTime Validar(RhNomContextoQuery value,int company)
    {
        if(company<=0 || value.CodigoTipoNomina<=0 || value.CodigoPeriodo<=0)
            throw new RhNomError("CONTEXTO_INVALIDO","Empresa, tipo de nomina y periodo deben ser positivos y explicitos.");
        return Fecha(value.FechaNomina);
    }
    public static List<RhNomFuenteResuelta> Resolver(RhNomSnapshot snapshot,RhNomContextoFuente source,
        IReadOnlyList<RhNomFuenteBinding>? bindings,CancellationToken ct=default,RhNomCargoHistorico? cargo=null)
    {
        if(source.Contexto.CodigoEmpresa!=snapshot.CodigoEmpresa || source.Contexto.CodigoTipoNomina!=snapshot.CodigoTipoNomina
            || source.Contexto.CodigoPeriodo is null or <=0 || source.Contexto.FechaNomina.Year<1900 || source.FechaCierre==default
            || source.Contexto.FechaNomina.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("CONTEXTO_DISTINTO","El periodo cerrado debe corresponder a la empresa/tipo del modelo.");
        if(source.Partidas<0 || source.Personas<0 || source.Personas>source.Partidas || source.MontosNulos<0 || source.MontosNulos>source.Partidas
            || source.ClavesInvalidas<0 || source.ClavesInvalidas>source.Partidas || source.GruposDuplicados<0 || source.GruposDuplicados>source.Partidas)
            throw new RhNomError("DIAGNOSTICO_INVALIDO","Contadores historicos inconsistentes.");
        if(bindings is null || bindings.Count>RhNomModelos.MaxEntradas)
            throw new RhNomError("FUENTES_INVALIDAS","Indique hasta 200 fuentes registradas, sin duplicados.");
        var catalog=snapshot.Variables.ToDictionary(v=>v.Codigo,StringComparer.Ordinal);
        var codes=new HashSet<string>(StringComparer.Ordinal);var results=new List<RhNomFuenteResuelta>();
        foreach(var binding in bindings)
        {
            ct.ThrowIfCancellationRequested();
            if(binding is null)throw new RhNomError("FUENTE_INVALIDA","Fuente nula.");
            var code=RhNomNumeros.Codigo(binding.CodigoVariable);
            if(!codes.Add(code))throw new RhNomError("FUENTE_DUPLICADA","Cada variable admite una unica fuente.");
            if(!catalog.TryGetValue(code,out var variable) || variable.Origen!="ENTRADA")
                throw new RhNomError("FUENTE_NO_DECLARADA","La fuente solo puede resolver una variable ENTRADA declarada en la revision.");
            var provider=Catalogo.SingleOrDefault(p=>p.Codigo==binding.Proveedor)
                ??throw new RhNomError("PROVEEDOR_NO_REGISTRADO","Proveedor no registrado. No se admiten SQL ni nombres de funciones.");
            if(variable.Unidad!=provider.Unidad)
                throw new RhNomError("UNIDAD_FUENTE_INCOMPATIBLE",$"[{code}] requiere unidad {provider.Unidad} para esta fuente.");
            if(provider.RequiereDiaIso ? binding.DiaSemanaIso is null or <1 or >7 : binding.DiaSemanaIso is not null)
                throw new RhNomError("ARGUMENTO_FUENTE_INVALIDO","DiaSemanaIso 1..7 es obligatorio solo para DIA_ISO_MES_NOMINA.");
            var date=source.Contexto.FechaNomina;
            if(provider.RequiereCargo)
            {
                if(cargo is null)throw new RhNomError("FUENTE_REQUIERE_CARGO","Esta fuente requiere persona y relacion de cargo historicas explicitas.");
                cargo=RhNomCargoSnapshot.Validar(new(snapshot.CodigoTipoNomina,source.Contexto.CodigoPeriodo!.Value,
                    date.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture),cargo.CodigoPersona,cargo.CodigoRelacionCargo),snapshot.CodigoEmpresa,[cargo]);
                var cargoValue=provider.Codigo switch{
                    "SUELDO_CARGO_SNAPSHOT"=>cargo.Sueldo,
                    "FRECUENCIA_CARGO_SNAPSHOT"=>cargo.FrecuenciaPagoId is {} frequency && frequency>0?RhNomNumeros.Texto(frequency):throw new RhNomError("FRECUENCIA_SNAPSHOT_NULA","El snapshot carece de frecuencia, sin usar la actual."),
                    "FECHA_INGRESO_CARGO_SNAPSHOT"=>FechaIngreso(cargo),
                    _=>throw new RhNomError("PROVEEDOR_NO_REGISTRADO","Proveedor de cargo no registrado.")};
                var field=provider.Codigo switch{"FRECUENCIA_CARGO_SNAPSHOT"=>"FRECUENCIA_PAGO_ID","FECHA_INGRESO_CARGO_SNAPSHOT"=>"FECHA_INGRESO",_=>"SUELDO"};
                results.Add(new(code,RhNomValores.Normalizar(variable.Tipo,cargoValue),variable.Unidad,provider.Codigo,null,"RH.RH_HISTORICO_PERSONAL_CARGO."+field,date,provider.Version,cargo.CodigoPersona,cargo.CodigoRelacionCargo));
                continue;
            }
            if(provider.Codigo=="FECHA_NOMINA_CONTEXTO")
            {results.Add(new(code,RhNomValores.Normalizar(variable.Tipo,date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)),variable.Unidad,provider.Codigo,null,"RH.RH_PERIODOS.FECHA_NOMINA",date,provider.Version));continue;}
            var days=DateTime.DaysInMonth(date.Year,date.Month);
            var value=provider.Codigo switch {
                "ANIO_FECHA_NOMINA"=>date.Year,
                "MES_FECHA_NOMINA"=>date.Month,
                "DIAS_MES_NOMINA"=>days,
                "DIA_ISO_MES_NOMINA"=>Enumerable.Range(1,days).Count(day=>((int)new DateTime(date.Year,date.Month,day).DayOfWeek+6)%7+1==binding.DiaSemanaIso),
                _=>throw new RhNomError("PROVEEDOR_NO_REGISTRADO","Proveedor no registrado.")
            };
            results.Add(new(code,value.ToString(CultureInfo.InvariantCulture),variable.Unidad,provider.Codigo,binding.DiaSemanaIso,
                "RH.RH_PERIODOS.FECHA_NOMINA",date,provider.Version));
        }
        return results.OrderBy(r=>r.CodigoVariable,StringComparer.Ordinal).ToList();
    }
    private static string FechaIngreso(RhNomCargoHistorico cargo)
    {
        if(cargo.FechaIngreso is not {} date)throw new RhNomError("INGRESO_SNAPSHOT_NULO","El snapshot carece de fecha de ingreso; no se infiere FECHA_INI.");
        if(date.Kind!=DateTimeKind.Unspecified)throw new RhNomError("INGRESO_SNAPSHOT_ZONA","La fecha Oracle debe conservarse sin conversion de zona.");
        return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    }
    public static RhNomPreparacionHistorica Preparar(RhNomModeloVersion model,RhNomContextoFuente source,
        IReadOnlyList<RhNomFuenteBinding> bindings,CancellationToken ct=default,RhNomCargoHistorico? cargo=null)
    {
        var inputs=Resolver(model.Configuracion,source,bindings,ct,cargo);
        var resolved=inputs.Select(i=>i.CodigoVariable).ToHashSet(StringComparer.Ordinal);
        var pending=model.Configuracion.Variables.Where(v=>v.Origen=="ENTRADA"&&!resolved.Contains(v.Codigo)).Select(v=>v.Codigo).Order(StringComparer.Ordinal).ToList();
        var errors=new List<RhNomDiagnostico>();
        if(inputs.Any(i=>i.Proveedor=="SUELDO_CARGO_SNAPSHOT"))errors.Add(new("ORIGEN_SNAPSHOT_PENDIENTE","El sueldo esta capturado en el snapshot; falta verificar que fue la entrada original del calculo. No certifica completitud historica."));
        if(source.Partidas<=0)errors.Add(new("SIN_HISTORICO","El contexto no tiene partidas historicas."));
        if(source.MontosNulos>0)errors.Add(new("MONTOS_HISTORICOS_NULOS","Hay importes nulos en el historico; no se sustituyen por cero."));
        if(source.ClavesInvalidas>0)errors.Add(new("CLAVES_HISTORICAS_INVALIDAS","Hay claves incompletas o tipos no soportados para preparar el caso historico."));
        if(source.GruposDuplicados>0)errors.Add(new("PARTIDAS_HISTORICAS_DUPLICADAS","Hay claves repetidas; no se consolidan por SUM."));
        var parameters=new List<RhNomParametroResuelto>();
        try { parameters=RhNomParametros.Resolver(model.Configuracion,DateOnly.FromDateTime(source.Contexto.FechaNomina),ct,inputs.Where(i=>model.Configuracion.Variables.Single(v=>v.Codigo==i.CodigoVariable).Tipo=="NUMERO").ToDictionary(i=>i.CodigoVariable,i=>RhNomNumeros.Leer(i.Valor),StringComparer.Ordinal)); }
        catch(RhNomError ex){errors.Add(ex.Diagnostico);}
        return new(model.ModeloId,model.Version,model.Hash,source,inputs,pending,parameters,errors);
    }
}
