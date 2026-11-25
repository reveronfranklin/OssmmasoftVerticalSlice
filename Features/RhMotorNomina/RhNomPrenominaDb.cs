using System.Data;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomPrenominaDb
{
    public static OracleCommand Cmd(OracleConnection cn,string proc)=>new(proc,cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
    public static void Scope(OracleCommand c,int company,int actor){RhNomArchivoCapsulas.Alcance(company,actor);RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_ACTOR",OracleDbType.Int32,actor);}
    public static async Task<RhNomContextoPrenomina> Contexto(OracleConnection cn,int company,int type,int period,CancellationToken ct)
    {
        int I(IDataReader r,string n)=>RhNomCfgDb.Int(r,n);
        int? N(IDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:I(r,n);
        string? S(IDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Text(r,n);
        DateTime? D(IDataReader r,string n)=>r.IsDBNull(r.GetOrdinal(n))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(n)),DateTimeKind.Unspecified);
        async Task<List<T>> Read<T>(string origin,Func<IDataReader,T> map){
            using var c=Cmd(cn,"RH.SP_RH_NOM_PRE_FUE");RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,type);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,period);RhNomCfgDb.Add(c,"p_ORIGEN",OracleDbType.Varchar2,origin);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            using var r=await c.ExecuteReaderAsync(ct);var rows=new List<T>();while(await r.ReadAsync(ct)){if(rows.Count>=5000)throw new RhNomError("PRENOMINA_LIMITE","La fuente excede 5000 filas; no se trunca.");rows.Add(map(r));}return rows;
        }
        var periods=await Read("PERIODO",r=>new RhNomPeriodoPrenomina(I(r,"CODIGO_PERIODO"),I(r,"CODIGO_TIPO_NOMINA"),D(r,"FECHA_NOMINA")!.Value,I(r,"PERIODO"),S(r,"TIPO_NOMINA")!,I(r,"CODIGO_PRESUPUESTO_ACTUAL"),N(r,"CODIGO_PRESUPUESTO"),D(r,"FECHA_PRECIERRE"),D(r,"FECHA_CIERRE")));
        if(periods.Count!=1)throw new RhNomError("PRENOMINA_PERIODO","Periodo unico requerido.");
        var people=await Read("PERSONAS",r=>new RhNomPersonaPrenomina(N(r,"CODIGO_PERSONA"),N(r,"CODIGO_RELACION_CARGO"),S(r,"NOMBRE"),S(r,"CEDULA"),S(r,"STATUS"),I(r,"VIGENTE")==1,I(r,"COINCIDENCIAS")));
        var concepts=await Read("CONCEPTOS",r=>new RhNomConceptoPrenomina(new(I(r,"CODIGO_EMPRESA"),I(r,"CODIGO_TIPO_NOMINA"),I(r,"CODIGO_CONCEPTO"),S(r,"CODIGO")!,S(r,"DENOMINACION")!,S(r,"TIPO_CONCEPTO")!,N(r,"FRECUENCIA_ID"),S(r,"STATUS")!,N(r,"AUTOMATICO")),S(r,"EXTRA1"),S(r,"MODULO"),S(r,"PADRE")));
        return new(periods[0],people,concepts,await Read("FRECUENCIAS",r=>new RhNomFrecuenciaSeleccion(I(r,"DESCRIPCION_ID"),S(r,"CODIGO"))));
    }
    public static async Task<RhNomLotePrenomina?> Leer(OracleConnection cn,int company,int actor,int? id,string? key,CancellationToken ct)
    {
        using var c=Cmd(cn,"RH.RH_PKG_NOM_PRE.LEER");Scope(c,company,actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,id);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,key);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;using var clob=r.GetOracleClob(r.GetOrdinal("DOCUMENTO"));var text=clob.Value;
        if(System.Text.Encoding.UTF8.GetByteCount(text)>2097152||RhNomCorridas.Hash(text)!=RhNomCfgDb.Text(r,"HASH_DOC"))throw new RhNomError("PRENOMINA_DOCUMENTO","Documento de lote alterado.");
        var doc=JsonSerializer.Deserialize<RhNomDocumentoPrenomina>(text,RhNomCorridas.Json)??throw new RhNomError("PRENOMINA_DOCUMENTO","Documento vacio.");
        if(doc.Empresa!=company||doc.Actor!=actor)throw new RhNomError("PRENOMINA_AMBITO","Lote ajeno.");return new(RhNomCfgDb.Int(r,"ID_LOTE"),RhNomCfgDb.Int(r,"VERSION"),RhNomCfgDb.Text(r,"HASH_SOL"),doc);
    }
    public static async Task<int> Crear(OracleConnection cn,int company,int actor,string hash,RhNomDocumentoPrenomina doc,CancellationToken ct)=>await RhNomCfgDb.Guardar(cn,async tx=>{
        using var c=Cmd(cn,"RH.RH_PKG_NOM_PRE.CREAR");c.Transaction=tx;Scope(c,company,actor);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,doc.Solicitud.ClaveSolicitud);RhNomCfgDb.Add(c,"p_HASH_SOL",OracleDbType.Varchar2,hash);Document(c,doc);var id=RhNomCfgDb.Out(c,"p_ID");await c.ExecuteNonQueryAsync(ct);return int.Parse(id.Value.ToString()!);},ct);
    static void Document(OracleCommand c,RhNomDocumentoPrenomina doc){RhNomCfgDb.Add(c,"p_ESTADO",OracleDbType.Varchar2,doc.Estado);var text=RhNomCorridas.Serializar(doc,2097152);RhNomCfgDb.Add(c,"p_DOC",OracleDbType.Clob,text);RhNomCfgDb.Add(c,"p_HASH_DOC",OracleDbType.Varchar2,RhNomCorridas.Hash(text));}
    public static async Task Reservar(OracleConnection cn,OracleTransaction tx,RhNomLotePrenomina lote,CancellationToken ct){using var c=Cmd(cn,"RH.RH_PKG_NOM_PRE.RESERVAR");c.Transaction=tx;Scope(c,lote.Documento.Empresa,lote.Documento.Actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,lote.IdLote);RhNomCfgDb.Add(c,"p_VERSION",OracleDbType.Int32,lote.Version);await c.ExecuteNonQueryAsync(ct);}
    public static async Task Guardar(OracleConnection cn,RhNomLotePrenomina lote,RhNomDocumentoPrenomina doc,CancellationToken ct,OracleTransaction? existing=null){
        async Task<bool> Write(OracleTransaction tx){using var c=Cmd(cn,"RH.RH_PKG_NOM_PRE.GUARDAR");c.Transaction=tx;Scope(c,doc.Empresa,doc.Actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,lote.IdLote);RhNomCfgDb.Add(c,"p_VERSION",OracleDbType.Int32,lote.Version);Document(c,doc);await c.ExecuteNonQueryAsync(ct);return true;}
        if(existing is not null)await Write(existing);else await RhNomCfgDb.Guardar(cn,Write,ct);
    }
}
