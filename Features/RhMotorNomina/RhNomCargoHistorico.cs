using System.Data;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomCargoHistoricoQuery(int CodigoTipoNomina,int CodigoPeriodo,string FechaNomina,int CodigoPersona,int CodigoRelacionCargo);
public record RhNomCargoHistorico(RhNomContextoHistorico Contexto,int CodigoPersona,int CodigoRelacionCargo,int? CodigoCargo,string Sueldo,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? FrecuenciaPagoId=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] DateTime? FechaIngreso=null)
{
    public string Fuente => "RH.RH_HISTORICO_PERSONAL_CARGO.SUELDO";
    public string VersionFuente {get;init;} = "CARGO_SNAPSHOT_V1";
    public bool OrigenDelCalculoVerificado => false;
}
public record RhNomHistoricoPersona(int ModeloId,int Version,int CodigoPeriodo,string FechaNomina,
    int CodigoPersona,int CodigoRelacionCargo,List<RhNomFuenteBinding> Fuentes);
public record RhNomPreparacionPersona(RhNomCargoHistorico Cargo,RhNomPreparacionHistorica Preparacion);

public static class RhNomCargoSnapshot
{
    public static RhNomCargoHistorico Validar(RhNomCargoHistoricoQuery query,int company,IReadOnlyList<RhNomCargoHistorico> rows)
    {
        var date=RhNomFuentesHistoricas.Validar(new(query.CodigoTipoNomina,query.CodigoPeriodo,query.FechaNomina),company);
        if(query.CodigoPersona<=0 || query.CodigoRelacionCargo<=0)
            throw new RhNomError("CARGO_EXPLICITO_REQUERIDO","Indique persona y relacion de cargo positivas y explicitas.");
        if(rows.Count!=1)throw new RhNomError("SNAPSHOT_NO_UNICO","No existe un snapshot unico para la persona/cargo/contexto exactos. No se elige MAX ni el cargo actual.");
        var row=rows[0];
        if(row.Contexto!=new RhNomContextoHistorico(company,query.CodigoTipoNomina,query.CodigoPeriodo,date)
            || row.CodigoPersona!=query.CodigoPersona || row.CodigoRelacionCargo!=query.CodigoRelacionCargo
            || row.Contexto.FechaNomina.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("CONTEXTO_DISTINTO","El snapshot no corresponde a la persona/cargo/contexto solicitados.");

        return row with {Sueldo=RhNomNumeros.Texto(RhNomNumeros.Leer(row.Sueldo))};
    }
}
public sealed class RhNomCargoHistoricoHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomCargoHistorico>> Get(RhNomCargoHistoricoQuery value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(async () =>
        {
            var date=ValidarSolicitud(value,company);
            using var cn=await RhNomCfgDb.Abrir(db,ct);
            await RhNomHistoricoFuentesHandler.Leer(cn,new(value.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina),company,date,ct);
            var row=await Leer(cn,value,company,date,ct);
            return new ResultDto<RhNomCargoHistorico>(row){IsValid=true,Message="Sueldo del snapshot recuperado. Origen como entrada original del calculo pendiente de verificar."};
        });
    public Task<ResultDto<RhNomPreparacionPersona>> Prepare(RhNomHistoricoPersona value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(async () =>
        {
            if(value.ModeloId<=0 || value.Version<=0 || value.CodigoPersona<=0 || value.CodigoRelacionCargo<=0
                || value.CodigoPeriodo<=0 || value.Fuentes is null || value.Fuentes.Count>RhNomModelos.MaxEntradas)
                throw new RhNomError("CONTEXTO_INVALIDO","Indique modelo/revision/periodo/persona/cargo explicitos y hasta 200 fuentes.");
            var date=RhNomFuentesHistoricas.Fecha(value.FechaNomina);
            using var cn=await RhNomCfgDb.Abrir(db,ct);
            var model=await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
            var source=await RhNomHistoricoFuentesHandler.Leer(cn,new(model.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina),company,date,ct);
            var cargo=await Leer(cn,new(model.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina,value.CodigoPersona,value.CodigoRelacionCargo),company,date,ct);
            var prepared=RhNomFuentesHistoricas.Preparar(model,source,value.Fuentes,ct,cargo);
            return new ResultDto<RhNomPreparacionPersona>(new(cargo,prepared)){IsValid=true,Message="Diagnostico individual completado. No calcula ni certifica nomina."};
        });
    private static DateTime ValidarSolicitud(RhNomCargoHistoricoQuery value,int company)
    {
        var date=RhNomFuentesHistoricas.Validar(new(value.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina),company);
        if(value.CodigoPersona<=0 || value.CodigoRelacionCargo<=0)
            throw new RhNomError("CARGO_EXPLICITO_REQUERIDO","Indique persona y relacion de cargo positivas y explicitas.");
        return date;
    }
    internal static async Task<RhNomCargoHistorico> Leer(OracleConnection cn,RhNomCargoHistoricoQuery value,int company,DateTime date,CancellationToken ct)
    {
        using var cmd=new OracleCommand("RH.SP_RH_NOM_CAR_HIST",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
        RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Int32,value.CodigoTipoNomina);
        RhNomCfgDb.Add(cmd,"p_PERIODO",OracleDbType.Int32,value.CodigoPeriodo);RhNomCfgDb.Add(cmd,"p_FECHA",OracleDbType.Date,date);
        RhNomCfgDb.Add(cmd,"p_PERSONA",OracleDbType.Int32,value.CodigoPersona);RhNomCfgDb.Add(cmd,"p_RELACION",OracleDbType.Int32,value.CodigoRelacionCargo);
        cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        var rows=new List<RhNomCargoHistorico>();using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
        {
            if(rows.Count>0)throw new RhNomError("SNAPSHOT_NO_UNICO","Hay snapshots duplicados; no se consolidan ni se elige uno arbitrariamente.");
            var context=new RhNomContextoHistorico(RhNomCfgDb.Int(reader,"CODIGO_EMPRESA"),RhNomCfgDb.Int(reader,"CODIGO_TIPO_NOMINA"),
                RhNomCfgDb.Int(reader,"CODIGO_PERIODO"),DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("FECHA_NOMINA")),DateTimeKind.Unspecified));
            rows.Add(new(context,RhNomCfgDb.Int(reader,"CODIGO_PERSONA"),RhNomCfgDb.Int(reader,"CODIGO_RELACION_CARGO"),
                reader.IsDBNull(reader.GetOrdinal("CODIGO_CARGO"))?null:RhNomCfgDb.Int(reader,"CODIGO_CARGO"),RhNomReferencias.NormalizarMontoOracle(RhNomCfgDb.Text(reader,"SUELDO_TEXTO"))??"",
                reader.IsDBNull(reader.GetOrdinal("FRECUENCIA_PAGO_ID"))?null:RhNomCfgDb.Int(reader,"FRECUENCIA_PAGO_ID"),reader.IsDBNull(reader.GetOrdinal("FECHA_INGRESO"))?null:DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("FECHA_INGRESO")),DateTimeKind.Unspecified)){VersionFuente="CARGO_SNAPSHOT_V2"});
        }
        return RhNomCargoSnapshot.Validar(value,company,rows);
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoCargoGet")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> HistoricalCargo(RhNomCargoHistoricoQuery value) =>
        RunAsync("rh.nomina.historico.datos",(company,_,ct)=>new RhNomCargoHistoricoHandler(db).Get(value,company,ct));
    [HttpPost("historicoPrepararPersona")]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> HistoricalPersonPrepare(RhNomHistoricoPersona value) =>
        RunAsync("rh.nomina.historico.datos",(company,_,ct)=>new RhNomCargoHistoricoHandler(db).Prepare(value,company,ct));
}
