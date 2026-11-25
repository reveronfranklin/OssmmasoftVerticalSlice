using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomVariableGuardada(int Id, int Version);
public sealed class RhNomVariablesHandler(ConnectionDB db)
{
    public Task<ResultDto<List<RhNomVariable>>> GetAll(RhNomListaQuery value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(()=>GetAllCore(value,company,ct));
    private async Task<ResultDto<List<RhNomVariable>>> GetAllCore(RhNomListaQuery value,int company,CancellationToken ct)
    {
        value = RhNomConfiguracion.Lista(value);
        using var cn = await RhNomCfgDb.Abrir(db,ct); using var cmd = RhNomCfgDb.Comando(cn,"VARS_GET");
        RhNomCfgDb.Paginacion(cmd,company,value); var total = RhNomCfgDb.Out(cmd,"p_TOTAL");
        var rows = new List<RhNomVariable>();
        using (var reader = await cmd.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) rows.Add(RhNomCfgDb.Variable(reader));
        return RhNomCfgDb.Pagina(rows,value,total);
    }
    public Task<ResultDto<RhNomVariableGuardada>> Save(RhNomVariableSave value,int company,int actor,CancellationToken ct) =>
        RhNomCfgDb.Resultado(()=>SaveCore(value,company,actor,ct));
    private async Task<ResultDto<RhNomVariableGuardada>> SaveCore(RhNomVariableSave value,int company,int actor,CancellationToken ct)
    {
        value = RhNomConfiguracion.Variable(value);
        using var cn = await RhNomCfgDb.Abrir(db,ct);
        var saved = await RhNomCfgDb.Guardar(cn,async tx =>
        {
            using var cmd = RhNomCfgDb.Comando(cn,"VAR_SAVE",tx);
            RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company); RhNomCfgDb.Add(cmd,"p_ACTOR",OracleDbType.Int32,actor);
            RhNomCfgDb.Add(cmd,"p_ID",OracleDbType.Int32,value.Id); RhNomCfgDb.Add(cmd,"p_VERSION",OracleDbType.Int32,value.VersionEsperada);
            RhNomCfgDb.Add(cmd,"p_CODIGO",OracleDbType.Varchar2,value.Codigo); RhNomCfgDb.Add(cmd,"p_DESCRIPCION",OracleDbType.Varchar2,value.Descripcion);
            RhNomCfgDb.Add(cmd,"p_TIPO",OracleDbType.Varchar2,value.Tipo); RhNomCfgDb.Add(cmd,"p_UNIDAD",OracleDbType.Varchar2,value.Unidad);
            RhNomCfgDb.Add(cmd,"p_ORIGEN",OracleDbType.Varchar2,value.Origen); RhNomCfgDb.Add(cmd,"p_ACTIVA",OracleDbType.Int32,value.Activa?1:0);
            var id = RhNomCfgDb.Out(cmd,"p_ID_OUT"); var version = RhNomCfgDb.Out(cmd,"p_VERSION_OUT");
            await cmd.ExecuteNonQueryAsync(ct);
            return new RhNomVariableGuardada(SupportDb.GetIntOutput(id),SupportDb.GetIntOutput(version));
        },ct);
        return new(saved) {IsValid=true,Message="Variable guardada. Las revisiones existentes conservan su snapshot."};
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("variablesGetAll")]
    public Task<IActionResult> VariablesGetAll(RhNomListaQuery value) =>
        RunAsync("rh.nomina.configuracion.leer",(company,_,ct)=>new RhNomVariablesHandler(db).GetAll(value,company,ct));
    [HttpPost("variablesSave")]
    [RequestSizeLimit(16384)]
    public Task<IActionResult> VariablesSave(RhNomVariableSave value) =>
        RunAsync("rh.nomina.configuracion.editar",(company,actor,ct)=>new RhNomVariablesHandler(db).Save(value,company,actor,ct));
}
