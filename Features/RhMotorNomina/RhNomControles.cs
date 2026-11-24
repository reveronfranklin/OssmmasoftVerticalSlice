using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomControlBinding(int CodigoConcepto,string Alcance,string Ausencia,string ValorNulo);
public record RhNomFilaControl(int? Id,int? CodigoEmpresa,int CodigoPersona,int CodigoConcepto,int? CodigoTipoNomina,int? ModuloId,
    string? ControlAplica,DateTime? FechaIns,DateTime? FechaUpd);
public record RhNomControlCapturado(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPeriodo,DateTime FechaNomina,int CodigoPersona,
    RhNomControlBinding Definicion,int? ModuloObjetivo,List<RhNomFilaControl> Filas,bool Omitir,string Hash)
{
    public string Origen=>"RH_PERSONAS_MOV_CONTROL_ACTUAL";
    public string VersionMetodo=>RhNomControles.Version;
    public bool EstadoHistoricoVerificado=>false;
}
public static class RhNomControles
{
    public const string Version="CONTROLES_OMISION_V1";
    public const string Proveedor="CONTROL_OMITIR_CONCEPTO";
    public const int MaxFilas=200,MaxDefiniciones=20,MaxFilasGlobal=1000;
    public static RhNomControlBinding Validar(RhNomControlBinding b)
    {
        if(b.CodigoConcepto<=0||b.Alcance is not ("CONCEPTO" or "MODULO_TIPO" or "MODULO_EMPRESA")||b.Ausencia is not ("ERROR" or "NO_OMITIR")||b.ValorNulo is not ("ERROR" or "NO_OMITIR"))
            throw new RhNomError("CONTROL_INVALIDO","Concepto positivo, alcance CONCEPTO/MODULO_TIPO/MODULO_EMPRESA y politicas ERROR/NO_OMITIR requeridos.");
        return b;
    }
    public static RhNomControlCapturado Capturar(RhNomControlBinding definition,RhNomCargoActual context,int? module,IReadOnlyList<RhNomFilaControl> rows,CancellationToken ct=default)
    {
        var b=Validar(definition);
        if(context.CodigoEmpresa<=0||context.CodigoTipoNomina<=0||context.CodigoPeriodo<=0||context.CodigoPersona<=0||context.FechaNomina.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("CONTEXTO_CONTROL_INVALIDO","Contexto de control positivo y fecha Oracle sin zona requeridos.");
        if(b.Alcance!="CONCEPTO"&&module is null or <=0)throw new RhNomError("MODULO_CONTROL_AUSENTE","El concepto objetivo no tiene modulo valido para el alcance elegido.");
        if(rows.Count>MaxFilas)throw new RhNomError("LIMITE_CONTROLES","La lectura excede 200 controles; no se trunca.");
        if(rows.Count==0&&b.Ausencia=="ERROR")throw new RhNomError("CONTROL_AUSENTE","No hay controles y la politica de ausencia es ERROR.");
        var ids=new HashSet<int>();var omit=false;
        foreach(var row in rows.OrderBy(r=>r?.Id))
        {
            ct.ThrowIfCancellationRequested();
            if(row is null||row.Id is null or <=0||!ids.Add(row.Id.Value)||row.CodigoPersona!=context.CodigoPersona||row.CodigoConcepto<=0)
                throw new RhNomError("FILA_CONTROL_INVALIDA","Control sin id, duplicado o ajeno a la persona.");
            if(row.CodigoEmpresa is null)throw new RhNomError("EMPRESA_CONTROL_AUSENTE","Un control candidato no tiene empresa; no se interpreta como ausencia ni se asigna por defecto.");
            if(row.CodigoEmpresa!=context.CodigoEmpresa)throw new RhNomError("CONTROL_EMPRESA_DISTINTA","Control de otra empresa.");
            if(b.Alcance=="CONCEPTO"&&(row.CodigoConcepto!=b.CodigoConcepto||row.CodigoTipoNomina!=context.CodigoTipoNomina)
               ||b.Alcance=="MODULO_TIPO"&&(row.ModuloId!=module||row.CodigoTipoNomina!=context.CodigoTipoNomina)
               ||b.Alcance=="MODULO_EMPRESA"&&row.ModuloId!=module)
                throw new RhNomError("CONTROL_FUERA_ALCANCE","Concepto/modulo/tipo del control no coincide con el alcance declarado.");
            if(row.ControlAplica is null){if(b.ValorNulo=="ERROR")throw new RhNomError("CONTROL_NULO","CONTROL_APLICA nulo; no se reemplaza implicitamente.");continue;}
            var flag=RhNomNumeros.Leer(row.ControlAplica);
            if(flag is not (0m or 1m))throw new RhNomError("VALOR_CONTROL_INVALIDO","CONTROL_APLICA debe ser 0 o 1; otros valores no se interpretan.");
            if(flag==1m)omit=true;
        }
        var result=new RhNomControlCapturado(context.CodigoEmpresa,context.CodigoTipoNomina,context.CodigoPeriodo,context.FechaNomina,context.CodigoPersona,b,module,rows.OrderBy(r=>r.Id).ToList(),omit,"");
        return result with{Hash=Huella(result)};
    }
    public static string Huella(RhNomControlCapturado c)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{c.CodigoEmpresa,c.CodigoTipoNomina,c.CodigoPeriodo,c.FechaNomina,c.CodigoPersona,c.Definicion,c.ModuloObjetivo,c.Filas,c.Omitir,c.Origen,c.VersionMetodo},new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    public static void Verificar(RhNomControlCapturado c,RhNomCargoActual context)
    {
        var restored=Capturar(c.Definicion,context,c.ModuloObjetivo,c.Filas);
        if(c.Hash!=Huella(c)||c.Hash!=restored.Hash)throw new RhNomError("CONTROL_ALTERADO","Contexto, controles o decision de omision no coinciden con su huella.");
    }
    public static async Task<RhNomControlCapturado> Leer(OracleConnection cn,RhNomCargoActual context,RhNomControlBinding definition,CancellationToken ct)
    {
        var b=Validar(definition);using var c=new OracleCommand("RH.SP_RH_NOM_CTL_ACT",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,context.CodigoEmpresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,context.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,context.CodigoPeriodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,context.FechaNomina);RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,context.CodigoPersona);RhNomCfgDb.Add(c,"p_CONCEPTO",OracleDbType.Int32,b.CodigoConcepto);RhNomCfgDb.Add(c,"p_ALCANCE",OracleDbType.Varchar2,b.Alcance);
        var module=c.Parameters.Add("p_MODULO",OracleDbType.Int32,ParameterDirection.Output);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);var rows=new List<RhNomFilaControl>();
        using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))
        {
            if(rows.Count>=MaxFilas)throw new RhNomError("LIMITE_CONTROLES","Mas de 200 controles; no se entrega una decision truncada.");
            int? N(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Int(r,n);DateTime? D(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(n)),DateTimeKind.Unspecified);
            rows.Add(new(N("CODIGO_PERSONA_MOV_CTRL"),N("CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_PERSONA"),RhNomCfgDb.Int(r,"CODIGO_CONCEPTO"),N("CODIGO_TIPO_NOMINA"),N("MODULO_ID"),r.IsDBNull(r.GetOrdinal("CONTROL_TEXTO"))?null:RhNomReferencias.NormalizarMontoOracle(RhNomCfgDb.Text(r,"CONTROL_TEXTO")),D("FECHA_INS"),D("FECHA_UPD")));
        }
        var value=module.Value;int? moduleId=value is null or DBNull||value is OracleDecimal {IsNull:true}?null:int.Parse(value.ToString()!,System.Globalization.CultureInfo.InvariantCulture);
        return Capturar(b,context,moduleId,rows,ct);
    }
}
