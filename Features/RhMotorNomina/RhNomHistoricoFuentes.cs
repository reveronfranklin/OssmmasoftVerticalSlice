using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public sealed class RhNomHistoricoFuentesHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomContextoFuente>> Context(RhNomContextoQuery value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(async () =>
        {
            var date=RhNomFuentesHistoricas.Validar(value,company);
            using var cn=await RhNomCfgDb.Abrir(db,ct);
            return new ResultDto<RhNomContextoFuente>(await Leer(cn,value,company,date,ct)) {IsValid=true,Message="Contexto cerrado y diagnostico agregado recuperados. No certifica paridad."};
        });
    public Task<ResultDto<RhNomPreparacionHistorica>> Prepare(RhNomHistoricoPreparar value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(async () =>
        {
            if(value.ModeloId<=0 || value.Version<=0 || value.CodigoPeriodo<=0 || value.Fuentes is null || value.Fuentes.Count>RhNomModelos.MaxEntradas)
                throw new RhNomError("CONTEXTO_INVALIDO","Indique modelo/revision/periodo positivos y hasta 200 fuentes registradas.");
            var date=RhNomFuentesHistoricas.Fecha(value.FechaNomina);
            using var cn=await RhNomCfgDb.Abrir(db,ct);
            var model=await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
            var query=new RhNomContextoQuery(model.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina);
            var source=await Leer(cn,query,company,date,ct);
            var result=RhNomFuentesHistoricas.Preparar(model,source,value.Fuentes,ct);
            return new ResultDto<RhNomPreparacionHistorica>(result) {IsValid=true,
                Message="Diagnostico de fuentes completado. Revise pendientes y bloqueos; no se calculo ni certifico nomina."};
        });
    internal static async Task<RhNomContextoFuente> Leer(OracleConnection cn,RhNomContextoQuery query,int company,DateTime date,CancellationToken ct)
    {
        using var cmd=new OracleCommand("RH.SP_RH_NOM_CTX_HIST",cn)
            {BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=20};
        RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);
        RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Int32,query.CodigoTipoNomina);
        RhNomCfgDb.Add(cmd,"p_PERIODO",OracleDbType.Int32,query.CodigoPeriodo);
        RhNomCfgDb.Add(cmd,"p_FECHA",OracleDbType.Date,date);
        cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var reader=await cmd.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))throw new RhNomError("CONTEXTO_NO_ENCONTRADO","No existe ese periodo/fecha exactos en la empresa y tipo solicitados.");
        DateTime Date(string name)=>DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(name)),DateTimeKind.Unspecified);
        if(reader.IsDBNull(reader.GetOrdinal("FECHA_CIERRE")))throw new RhNomError("PERIODO_NO_CERRADO","La referencia historica requiere un periodo cerrado.");
        var context=new RhNomContextoHistorico(RhNomCfgDb.Int(reader,"CODIGO_EMPRESA"),RhNomCfgDb.Int(reader,"CODIGO_TIPO_NOMINA"),
            RhNomCfgDb.Int(reader,"CODIGO_PERIODO"),Date("FECHA_NOMINA"));
        if(context!=new RhNomContextoHistorico(company,query.CodigoTipoNomina,query.CodigoPeriodo,date))
            throw new RhNomError("CONTEXTO_DISTINTO","El contexto Oracle no coincide exactamente con la solicitud.");
        var result=new RhNomContextoFuente(context,Date("FECHA_CIERRE"),reader.IsDBNull(reader.GetOrdinal("CODIGO_PRESUPUESTO"))?null:RhNomCfgDb.Int(reader,"CODIGO_PRESUPUESTO"),
            RhNomCfgDb.Int(reader,"PARTIDAS"),RhNomCfgDb.Int(reader,"PERSONAS"),RhNomCfgDb.Int(reader,"MONTOS_NULOS"),
            RhNomCfgDb.Int(reader,"CLAVES_INVALIDAS"),RhNomCfgDb.Int(reader,"GRUPOS_DUPLICADOS"));
        if(await reader.ReadAsync(ct))throw new RhNomError("CONTEXTO_DUPLICADO","Hay mas de un periodo con la misma clave y fecha; no se elige uno arbitrariamente.");
        return result;
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("fuentesCatalogo")]
    public Task<IActionResult> HistoricalProviders() => Run("rh.nomina.historico.leer",_ =>
        new ResultDto<IReadOnlyList<RhNomProveedor>>(RhNomFuentesHistoricas.Catalogo) {IsValid=true,Message="Fuentes registradas. El sueldo de snapshot requiere cargo explicito y permiso de datos."});
    [HttpPost("historicoContexto")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> HistoricalContext(RhNomContextoQuery value) =>
        RunAsync("rh.nomina.historico.leer",(company,_,ct)=>new RhNomHistoricoFuentesHandler(db).Context(value,company,ct));
    [HttpPost("historicoPreparar")]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> HistoricalPrepare(RhNomHistoricoPreparar value) =>
        RunAsync("rh.nomina.historico.leer",(company,_,ct)=>new RhNomHistoricoFuentesHandler(db).Prepare(value,company,ct));
}
