using System.Data;
using System.Globalization;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomPersonalContexto(int Empresa,int Tipo,int Periodo,DateTime Fecha,int Persona,int Relacion);
public record RhNomDatoPersonal(int Id,int? Empresa,DateTime? Fecha,int? Titulo,string? Estado);
public record RhNomPersonalDocumento(int Formato,RhNomPersonalContexto Contexto,string Proveedor,List<RhNomDatoPersonal> Filas);
public record RhNomPersonalCapturado(RhNomPersonalDocumento Documento,int ElegidoId,string Valor,string Hash)
{
    public string VersionMetodo=>RhNomDatosPersonales.Version;
    public bool EstadoHistoricoVerificado=>false;
}
public static class RhNomDatosPersonales
{
    public const string Version="DATOS_PERSONALES_V1";
    public const int MaxFilas=200;
    public const string UltimoOSinRegistro="TITULO_EDUC_ULTIMO_O_SIN_REG";
    public static readonly IReadOnlyDictionary<string,string> Proveedores=new Dictionary<string,string>{
        ["TITULO_EDUC_ULTIMO_REG"]="NUMERO",
        [UltimoOSinRegistro]="NUMERO",
        ["TITULO_EDUC_FECHA_NOMINA"]="NUMERO",
        ["FECHA_INGRESO_ADMIN_UNICO"]="FECHA"};
    public static RhNomPersonalContexto Contexto(RhNomCargoActual s)=>new(s.CodigoEmpresa,s.CodigoTipoNomina,s.CodigoPeriodo,s.FechaNomina,s.CodigoPersona,s.CodigoRelacionCargo);
    public static RhNomPersonalCapturado Capturar(RhNomPersonalDocumento doc)
    {
        if(doc is null||doc.Contexto is null)throw new RhNomError("CONTEXTO_PERSONAL_INVALIDO","Documento y contexto personal requeridos.");
        var c=doc.Contexto;
        if(doc.Formato!=1||!Proveedores.ContainsKey(doc.Proveedor)||c.Empresa<=0||c.Tipo<=0||c.Periodo<=0||c.Persona<=0||c.Relacion<=0||c.Fecha.Year<1900||c.Fecha.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("CONTEXTO_PERSONAL_INVALIDO","Proveedor y contexto personal explicitos requeridos.");
        if(doc.Filas is null||doc.Filas.Count>MaxFilas||doc.Filas.Any(r=>r is null||r.Id<=0)||doc.Filas.Select(r=>r.Id).Distinct().Count()!=doc.Filas.Count)
            throw new RhNomError("FILAS_PERSONALES_INVALIDAS","Hasta 200 registros personales con ids unicos; no se truncan.");
        if(doc.Filas.Any(r=>r.Empresa!=c.Empresa))throw new RhNomError("EMPRESA_PERSONAL_NO_EXPLICITA","Registro personal sin empresa explicita o ajeno a la empresa; no se asigna por inferencia.");
        if(doc.Filas.Any(r=>r.Fecha is {} d&&(d.Kind!=DateTimeKind.Unspecified||d.Year<1900)))throw new RhNomError("FECHA_PERSONAL_INVALIDA","Fechas Oracle desde 1900, sin conversion de zona.");
        var rows=doc.Filas;
        RhNomDatoPersonal chosen;
        if(doc.Proveedor=="FECHA_INGRESO_ADMIN_UNICO")
        {
            if(rows.Any(r=>r.Titulo is not null))throw new RhNomError("FILA_ADMIN_INVALIDA","Registro administrativo incompatible.");
            // Legacy administrative STATUS is not a verified active flag (QA has NULL).
            if(rows.Count!=1)throw new RhNomError("INGRESO_ADMIN_NO_UNICO","Se requiere un unico registro administrativo de la empresa; no se interpreta STATUS ni se elige primera/ultima fecha.");
            chosen=rows[0];
            if(chosen.Fecha is null||chosen.Fecha>c.Fecha)throw new RhNomError("INGRESO_ADMIN_INVALIDO","Ingreso administrativo requerido y no posterior al contexto.");
        }
        else
        {
            if(rows.Any(r=>r.Estado is not null))throw new RhNomError("FILA_EDUC_INVALIDA","Registro de educacion incompatible.");
            var eligible=rows.Where(r=>r.Fecha is not null&&(doc.Proveedor is "TITULO_EDUC_ULTIMO_REG" or UltimoOSinRegistro||r.Fecha<=c.Fecha)).ToList();
            if(eligible.Count==0&&doc.Proveedor==UltimoOSinRegistro)
                return Empaquetar(doc,0,"0"); // Explicit absence sentinel, never a real title id.
            if(eligible.Count==0)throw new RhNomError("EDUCACION_NO_ENCONTRADA","No hay educacion finalizada elegible; no se inventa titulo ni porcentaje.");
            var last=eligible.Max(r=>r.Fecha);var selected=eligible.Where(r=>r.Fecha==last).ToList();
            if(selected.Count!=1)throw new RhNomError("EDUCACION_EMPATE","Varias educaciones tienen la ultima fecha; no se elige id mayor ni porcentaje mas alto.");
            chosen=selected[0];if(chosen.Titulo is null or <=0)throw new RhNomError("TITULO_EDUC_NULO","La educacion elegida no tiene titulo valido.");
        }
        var value=doc.Proveedor=="FECHA_INGRESO_ADMIN_UNICO"?chosen.Fecha!.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):RhNomNumeros.Texto(chosen.Titulo!.Value);
        return Empaquetar(doc,chosen.Id,value);
    }
    private static RhNomPersonalCapturado Empaquetar(RhNomPersonalDocumento doc,int elegido,string value)
    {
        var sorted=doc with{Filas=doc.Filas.OrderBy(r=>r.Id).ToList()};
        var hash=RhNomCorridas.Hash(JsonSerializer.Serialize(new{Documento=sorted,ElegidoId=elegido,Valor=value},RhNomCorridas.Json));
        return new(sorted,elegido,value,hash);
    }
    public static void Verificar(RhNomPersonalCapturado captured,RhNomCargoActual source,string provider)
    {
        if(captured is null||captured.Documento is null)throw new RhNomError("DATOS_PERSONALES_NO_CAPTURADOS","Documento personal requerido.");
        if(captured.Documento.Contexto!=Contexto(source)||captured.Documento.Proveedor!=provider)throw new RhNomError("CONTEXTO_PERSONAL_DISTINTO","Datos personales ajenos al contexto/proveedor.");
        var expected=Capturar(captured.Documento);
        if(expected.Hash!=captured.Hash||expected.Valor!=captured.Valor||expected.ElegidoId!=captured.ElegidoId)throw new RhNomError("DATOS_PERSONALES_ALTERADOS","La evidencia personal no coincide con su valor/seleccion/huella.");
    }
    public static async Task<RhNomPersonalCapturado> Leer(OracleConnection cn,RhNomCargoActual source,string provider,CancellationToken ct)
    {
        if(!Proveedores.ContainsKey(provider))throw new RhNomError("PROVEEDOR_PERSONAL_INVALIDO","Proveedor personal no registrado.");
        using var cmd=new OracleCommand("RH.SP_RH_NOM_PER_FUE",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
        RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,source.CodigoEmpresa);RhNomCfgDb.Add(cmd,"p_PERSONA",OracleDbType.Int32,source.CodigoPersona);RhNomCfgDb.Add(cmd,"p_PROVEEDOR",OracleDbType.Varchar2,provider==UltimoOSinRegistro?"TITULO_EDUC_ULTIMO_REG":provider);cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        var rows=new List<RhNomDatoPersonal>();using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))
        {
            int? N(string name)=>r.IsDBNull(r.GetOrdinal(name))?null:RhNomCfgDb.Int(r,name);
            rows.Add(new(RhNomCfgDb.Int(r,"ID_FILA"),N("EMPRESA"),r.IsDBNull(r.GetOrdinal("FECHA"))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("FECHA")),DateTimeKind.Unspecified),N("TITULO"),r.IsDBNull(r.GetOrdinal("ESTADO"))?null:RhNomCfgDb.Text(r,"ESTADO")));
            if(rows.Count>MaxFilas)throw new RhNomError("LIMITE_DATOS_PERSONALES","Mas de 200 registros; lectura rechazada sin truncar.");
        }
        return Capturar(new(1,Contexto(source),provider,rows));
    }
}
