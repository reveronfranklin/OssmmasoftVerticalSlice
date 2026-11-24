using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomParametroConceptoBinding(int CodigoConcepto,string PoliticaVigencia);
public record RhNomDatosParametroConcepto(int Id,int CodigoEmpresa,int CodigoTipoNomina,DateTime FechaNomina,int CodigoConcepto,
    string PoliticaVigencia,DateTime? FechaDesde,DateTime? FechaHasta,string? Porcentaje,string? MontoTope,string? PorcentajePatronal,
    string? Extra2,string? Extra3,string? TipoSueldo)
{
    public string Origen=>"RH_FORMULA_CONCEPTOS";
    public string VersionFuente {get;init;}="PARAMETROS_CONCEPTO_V1";
    public bool EstadoHistoricoVerificado=>false;
}
public record RhNomParametroConceptoCapturado(RhNomDatosParametroConcepto Datos,string Hash);
public static class RhNomParametrosConcepto
{
    public static readonly IReadOnlyDictionary<string,string> Unidades=new Dictionary<string,string>(StringComparer.Ordinal)
    {{"PARAM_CON_PORCENTAJE","PORCENTAJE"},{"PARAM_CON_TOPE","DINERO"},{"PARAM_CON_PATRONAL","PORCENTAJE"},{"PARAM_CON_EXTRA2","NUMERO"},{"PARAM_CON_EXTRA3","NUMERO"}};
    public static RhNomParametroConceptoBinding Validar(RhNomParametroConceptoBinding b)
    {
        if(b.CodigoConcepto<=0||b.PoliticaVigencia is not ("FECHA_CONTEXTO" or "HASTA_NULA"))throw new RhNomError("PARAMETRO_CONCEPTO_INVALIDO","Concepto positivo y politica FECHA_CONTEXTO/HASTA_NULA requeridos.");
        return b;
    }
    public static string Hash(RhNomDatosParametroConcepto d)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(d,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    public static string Valor(string provider,RhNomParametroConceptoBinding binding,RhNomParametroConceptoCapturado captured,RhNomCargoActual context) =>
        Valor(provider,binding,captured,new RhNomContextoHistorico(context.CodigoEmpresa,context.CodigoTipoNomina,context.CodigoPeriodo,context.FechaNomina));
    public static string Valor(string provider,RhNomParametroConceptoBinding binding,RhNomParametroConceptoCapturado captured,RhNomContextoHistorico context)
    {
        Validar(binding);var d=captured.Datos;
        if(d.Id<=0||d.CodigoEmpresa!=context.CodigoEmpresa||d.CodigoTipoNomina!=context.CodigoTipoNomina||d.FechaNomina!=context.FechaNomina||d.CodigoConcepto!=binding.CodigoConcepto||d.PoliticaVigencia!=binding.PoliticaVigencia||d.VersionFuente!="PARAMETROS_CONCEPTO_V1"||captured.Hash!=Hash(d))throw new RhNomError("PARAMETRO_CONCEPTO_ALTERADO","El parametro no coincide con contexto, definicion o huella.");
        if(binding.PoliticaVigencia=="HASTA_NULA"&&d.FechaHasta is not null||binding.PoliticaVigencia=="FECHA_CONTEXTO"&&(d.FechaDesde is {} start&&start>d.FechaNomina||d.FechaHasta is {} end&&end<d.FechaNomina))throw new RhNomError("PARAMETRO_CONCEPTO_NO_VIGENTE","La vigencia del registro no cumple la politica elegida.");
        if(!Unidades.ContainsKey(provider))throw new RhNomError("PROVEEDOR_PARAMETRO_INVALIDO","Campo de parametro no registrado.");
        var text=provider switch{"PARAM_CON_PORCENTAJE"=>d.Porcentaje,"PARAM_CON_TOPE"=>d.MontoTope,"PARAM_CON_PATRONAL"=>d.PorcentajePatronal,"PARAM_CON_EXTRA2"=>d.Extra2,"PARAM_CON_EXTRA3"=>d.Extra3,_=>null};
        if(text is null)throw new RhNomError("PARAMETRO_CONCEPTO_NULO","El campo seleccionado es nulo; no se reemplaza por cero.");
        return RhNomNumeros.Texto(RhNomNumeros.Leer(RhNomReferencias.NormalizarMontoOracle(text)));
    }
    public static Task<RhNomParametroConceptoCapturado> Leer(OracleConnection cn,RhNomCargoActual source,RhNomParametroConceptoBinding definition,CancellationToken ct) =>
        Leer(cn,new RhNomContextoHistorico(source.CodigoEmpresa,source.CodigoTipoNomina,source.CodigoPeriodo,source.FechaNomina),definition,ct);
    public static async Task<RhNomParametroConceptoCapturado> Leer(OracleConnection cn,RhNomContextoHistorico source,RhNomParametroConceptoBinding definition,CancellationToken ct)
    {
        var b=Validar(definition);using var c=new OracleCommand("RH.SP_RH_NOM_PAR_CON",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,source.CodigoEmpresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,source.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,source.FechaNomina);RhNomCfgDb.Add(c,"p_CONCEPTO",OracleDbType.Int32,b.CodigoConcepto);RhNomCfgDb.Add(c,"p_POLITICA",OracleDbType.Varchar2,b.PoliticaVigencia);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new RhNomError("PARAMETRO_CONCEPTO_AUSENTE","No hay configuracion del concepto en empresa/tipo/vigencia.");
        string? S(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Text(r,n);DateTime? D(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(n)),DateTimeKind.Unspecified);
        var d=new RhNomDatosParametroConcepto(RhNomCfgDb.Int(r,"CODIGO_FORMULA_CONCEPTO"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),source.CodigoTipoNomina,source.FechaNomina,RhNomCfgDb.Int(r,"CODIGO_CONCEPTO"),b.PoliticaVigencia,D("FECHA_DESDE"),D("FECHA_HASTA"),S("PORCENTAJE_TEXTO"),S("MONTO_TOPE_TEXTO"),S("PATRONAL_TEXTO"),S("EXTRA2"),S("EXTRA3"),S("TIPO_SUELDO"));
        if(await r.ReadAsync(ct))throw new RhNomError("PARAMETRO_CONCEPTO_DUPLICADO","Mas de una configuracion elegible; no se elige la ultima ni se suma.");return new(d,Hash(d));
    }
}
