using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomReferenciaQuery(int CodigoTipoNomina,int CodigoPeriodo,string FechaNomina,int CodigoPersona);
public record RhNomPartidaReferencia(int? CodigoHistorico,int? CodigoConcepto,int? FrecuenciaId,
    string? TipoMovimiento,string? EstadoRegistro,string? Monto);
public record RhNomDocumentoReferencia(int Formato,RhNomContextoHistorico Contexto,int CodigoPersona,List<RhNomPartidaReferencia> Partidas);
public record RhNomReferenciaCapturada(RhNomDocumentoReferencia Documento,string Hash,List<RhNomDiagnostico> Observaciones)
{
    public string Fuente => "RH.RH_HISTORICO_NOMINA";
    public bool IntegridadEstructural => Observaciones.Count==0;
    public bool EntradasCompletas => false;
    public bool ClasificacionConceptosVerificada => false;
    public bool CargoIdentificadoEnPartidas => false;
    public bool ParidadCertificada => false;
}
public static class RhNomReferencias
{
    public const int MaxPartidas=1000;
    // Oracle TM9 can omit the zero before the decimal point. Normalize transport only.
    public static string? NormalizarMontoOracle(string? text)=>text switch
    {
        null=>null,
        _ when text.StartsWith("-.",StringComparison.Ordinal)=>"-0"+text[1..],
        _ when text.StartsWith(".",StringComparison.Ordinal)=>"0"+text,
        _=>text
    };
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static DateTime Validar(RhNomReferenciaQuery value,int company)
    {
        var date=RhNomFuentesHistoricas.Validar(new(value.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina),company);
        if(value.CodigoPersona<=0)throw new RhNomError("PERSONA_REQUERIDA","Indique una persona explicita y positiva para la referencia.");
        return date;
    }
    public static RhNomReferenciaCapturada Capturar(RhNomReferenciaQuery value,int company,IReadOnlyList<RhNomPartidaReferencia> rows,CancellationToken ct=default)
    {
        var date=Validar(value,company);
        if(rows is null || rows.Count is <1 or >MaxPartidas)
            throw new RhNomError("REFERENCIA_INVALIDA","La referencia requiere 1..1000 partidas. No se entrega una captura vacia ni truncada.");
        var errors=new List<RhNomDiagnostico>();var ids=new HashSet<int>();
        var keys=new HashSet<(int?,int?,string?)>();var all=new List<RhNomPartidaReferencia>();
        foreach(var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if(row is null)throw new RhNomError("PARTIDA_INVALIDA","Partida nula en la referencia.");
            if(row.CodigoHistorico is null or <=0 || row.CodigoConcepto is null or <=0 || row.FrecuenciaId is null or <=0 || row.TipoMovimiento is not ("F" or "V" or "E"))
                errors.Add(new("CLAVE_REFERENCIA_INVALIDA","Hay una partida con identificador/clave incompletos o tipo no soportado."));
            if(row.CodigoHistorico is { } id && !ids.Add(id))errors.Add(new("ID_HISTORICO_DUPLICADO","El identificador historico aparece mas de una vez."));
            if(!keys.Add((row.CodigoConcepto,row.FrecuenciaId,row.TipoMovimiento)))
                errors.Add(new("CLAVE_REFERENCIA_DUPLICADA","Hay varias partidas con la misma clave de concepto/frecuencia/tipo. Se preservan todas, sin SUM."));
            try{RhNomNumeros.Leer(row.Monto);}
            catch(RhNomError ex){errors.Add(new(ex.Diagnostico.Codigo,"Importe historico nulo o fuera del contrato decimal; no se transforma en cero."));}
            all.Add(row);
        }
        var ordered=all.OrderBy(r=>r.CodigoHistorico).ThenBy(r=>r.CodigoConcepto).ThenBy(r=>r.FrecuenciaId)
            .ThenBy(r=>r.TipoMovimiento,StringComparer.Ordinal).ThenBy(r=>r.EstadoRegistro,StringComparer.Ordinal).ThenBy(r=>r.Monto,StringComparer.Ordinal).ToList();
        var document=new RhNomDocumentoReferencia(1,new(company,value.CodigoTipoNomina,value.CodigoPeriodo,date),value.CodigoPersona,ordered);
        return new(document,Hash(document),errors);
    }
    public static string Hash(RhNomDocumentoReferencia document)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document,Json)));
    public static bool Verificar(RhNomReferenciaCapturada reference)=>reference.Hash==Hash(reference.Documento);
}
public sealed class RhNomReferenciaHistoricaHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomReferenciaCapturada>> Get(RhNomReferenciaQuery value,int company,CancellationToken ct)=>
        RhNomCfgDb.Resultado(async()=>
        {
            var date=RhNomReferencias.Validar(value,company);
            using var cn=await RhNomCfgDb.Abrir(db,ct);
            await RhNomHistoricoFuentesHandler.Leer(cn,new(value.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina),company,date,ct);
            using var cmd=new OracleCommand("RH.SP_RH_NOM_REF_HIST",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
            RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Int32,value.CodigoTipoNomina);
            RhNomCfgDb.Add(cmd,"p_PERIODO",OracleDbType.Int32,value.CodigoPeriodo);RhNomCfgDb.Add(cmd,"p_FECHA",OracleDbType.Date,date);
            RhNomCfgDb.Add(cmd,"p_PERSONA",OracleDbType.Int32,value.CodigoPersona);cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            var rows=new List<RhNomPartidaReferencia>();using(var reader=await cmd.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))
            {
                if(rows.Count>=RhNomReferencias.MaxPartidas)throw new RhNomError("LIMITE_REFERENCIA","La referencia supera 1000 partidas; no se entrega truncada.");
                int? NullableInt(string name)=>reader.IsDBNull(reader.GetOrdinal(name))?null:RhNomCfgDb.Int(reader,name);
                rows.Add(new(NullableInt("CODIGO_HISTORICO_NOMINA"),NullableInt("CODIGO_CONCEPTO"),NullableInt("FRECUENCIA_ID"),
                    reader.IsDBNull(reader.GetOrdinal("TIPO_MOVIMIENTO"))?null:RhNomCfgDb.Text(reader,"TIPO_MOVIMIENTO"),
                    reader.IsDBNull(reader.GetOrdinal("ESTADO_REGISTRO"))?null:RhNomCfgDb.Text(reader,"ESTADO_REGISTRO"),RhNomReferencias.NormalizarMontoOracle(reader.IsDBNull(reader.GetOrdinal("MONTO_TEXTO"))?null:RhNomCfgDb.Text(reader,"MONTO_TEXTO"))));
            }
            var result=RhNomReferencias.Capturar(value,company,rows,ct);
            return new ResultDto<RhNomReferenciaCapturada>(result){IsValid=true,Message="Referencia de resultados almacenados capturada. No resuelve entradas ni certifica paridad."};
        });
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoReferenciaGet")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> HistoricalReference(RhNomReferenciaQuery value)=>
        RunAsync("rh.nomina.historico.datos",(company,_,ct)=>new RhNomReferenciaHistoricaHandler(db).Get(value,company,ct));
}
