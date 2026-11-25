using System.Data;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomTransaccionDb
{
    public static OracleCommand Cmd(OracleConnection cn,string op)=>new("RH.RH_PKG_NOM_FIN_TX."+op,cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
    public static async Task<RhNomReciboFinanciero?> Solicitud(OracleConnection cn,int company,int actor,string key,string hash,int plan,CancellationToken ct)
    {
        using var c=Cmd(cn,"SOLICITUD");RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_ACTOR",OracleDbType.Int32,actor);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,key);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return null;
        if(RhNomCfgDb.Text(r,"HASH_SOL")!=hash||RhNomCfgDb.Int(r,"ID_PLAN")!=plan)throw new RhNomError("CLAVE_FINANCIERA_REUTILIZADA","La clave ya identifica otra operacion financiera.");
        return new(RhNomCfgDb.Int(r,"ID_EVT"),plan,hash,RhNomCfgDb.Text(r,"HASH_EVT"),DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("FECHA_BD")),DateTimeKind.Unspecified));
    }
    public static async Task<(int Id,DateTime Clock)> Iniciar(OracleConnection cn,RhNomPlanGuardado plan,int actor,bool reversal,CancellationToken ct)
    {
        var x=plan.Diagnostico.Consolidacion;var m=RhNomTransaccion.ModeloUnico(x);using var c=Cmd(cn,"INICIAR");
        foreach(var (name,value) in new[]{("p_EMPRESA",x.CodigoEmpresa),("p_TIPO",x.CodigoTipoNomina),("p_PERSONA",x.CodigoPersona),("p_PERIODO",x.CodigoPeriodo),("p_PRESUPUESTO",x.CodigoPresupuesto),("p_ACTOR",actor),("p_PLAN",plan.Cabecera.IdPlan),("p_MODELO",m.Modelo),("p_VERSION",m.Version)})RhNomCfgDb.Add(c,name,OracleDbType.Int32,value);
        RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,x.FechaNomina);RhNomCfgDb.Add(c,"p_HASH",OracleDbType.Varchar2,m.Hash);RhNomCfgDb.Add(c,"p_ESCENARIO",OracleDbType.Varchar2,x.Escenario);RhNomCfgDb.Add(c,"p_ACCION",OracleDbType.Varchar2,reversal?"REVERSION":"APLICACION");
        var id=RhNomCfgDb.Out(c,"p_EVT");var clock=c.Parameters.Add("p_RELOJ",OracleDbType.Date,ParameterDirection.Output);await c.ExecuteNonQueryAsync(ct);
        return(int.Parse(id.Value.ToString()!),DateTime.SpecifyKind(((Oracle.ManagedDataAccess.Types.OracleDate)clock.Value).Value,DateTimeKind.Unspecified));
    }
    public static async Task<int> NuevoId(OracleConnection cn,CancellationToken ct){using var c=Cmd(cn,"NUEVO_ID");var id=RhNomCfgDb.Out(c,"p_ID");await c.ExecuteNonQueryAsync(ct);return int.Parse(id.Value.ToString()!);}
    public static async Task Cambiar(OracleConnection cn,RhNomCambioAplicado change,CancellationToken ct)
    {
        if(change.Accion is not ("CREAR" or "ACTUALIZAR" or "RETIRAR"))throw new RhNomError("ACCION_DML_INVALIDA","Accion financiera no admitida.");
        using var c=Cmd(cn,change.Accion=="CREAR"?"CREAR_MOV":change.Accion=="ACTUALIZAR"?"ACTUALIZAR_MOV":"RETIRAR_MOV");var image=(change.Despues??change.Antes)!;
        RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,image.Id);
        if(change.Accion!="RETIRAR")RhNomCfgDb.Add(c,"p_MONTO",OracleDbType.Decimal,RhNomTransaccion.Numero(image.Monto!));
        if(change.Accion=="CREAR")
        {
            RhNomCfgDb.Add(c,"p_CONCEPTO",OracleDbType.Int32,image.CodigoConcepto);RhNomCfgDb.Add(c,"p_FRECUENCIA",OracleDbType.Int32,image.FrecuenciaId);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Varchar2,image.Tipo);
            foreach(var(n,v)in new[]{("p_ESTADO",image.Estado),("p_COMPLEMENTO",image.Complemento),("p_EXTRA1",image.Extra1),("p_EXTRA2",image.Extra2),("p_EXTRA3",image.Extra3)})RhNomCfgDb.Add(c,n,OracleDbType.Varchar2,v);
        }
        await c.ExecuteNonQueryAsync(ct);
    }
    public static async Task Registrar(OracleConnection cn,RhNomEjecutarPlan q,string requestHash,RhNomLibroEvento e,CancellationToken ct)
    {
        var text=RhNomCorridas.Serializar(e.Documento,RhNomLibroFinancieroDb.MaxDocumento);if(RhNomCorridas.Hash(text)!=e.HashDocumento)throw new RhNomError("EVENTO_NO_CANONICO","Documento financiero no canonico.");
        using var c=Cmd(cn,"REGISTRAR");foreach(var(n,v)in new[]{("p_CLAVE",q.ClaveSolicitud),("p_HASH_SOL",requestHash),("p_HASH_DOC",e.HashDocumento),("p_HASH_EVT",e.HashEvento),("p_HASH_ANT",e.HashAnterior)})RhNomCfgDb.Add(c,n,OracleDbType.Varchar2,v);
        RhNomCfgDb.Add(c,"p_DOC",OracleDbType.Clob,text);RhNomCfgDb.Add(c,"p_ANT",OracleDbType.Int32,e.IdAnterior);RhNomCfgDb.Add(c,"p_CAMBIOS",OracleDbType.Int32,e.Documento.Cambios.Count);await c.ExecuteNonQueryAsync(ct);
    }
}
