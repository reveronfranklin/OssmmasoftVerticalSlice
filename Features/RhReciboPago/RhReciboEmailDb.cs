using System.Data;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Features.Support;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

internal record RrmParam(string Name, OracleDbType Type, object? Value);
internal static class RrmDb
{
    internal static RrmParam N(string n, object? v) => new(n,OracleDbType.Int32,v);
    internal static RrmParam S(string n, string? v) => new(n,OracleDbType.Varchar2,string.IsNullOrEmpty(v)?null:v);
    internal static RrmParam C(string n, string? v) => new(n,OracleDbType.Clob,v);
    internal static RrmParam B(string n, byte[] v) => new(n,OracleDbType.Blob,v);
    internal static OracleCommand Cmd(OracleConnection cn, string op, OracleTransaction? tx, params RrmParam[] args)
    {
        var cmd = new OracleCommand("RH.PKG_RH_REC_MAIL."+op,cn) { BindByName=true,CommandType=CommandType.StoredProcedure,Transaction=tx };
        foreach (var arg in args) cmd.Parameters.Add(arg.Name,arg.Type).Value=RhReciboPagoDb.DbValue(arg.Value);
        return cmd;
    }
    internal static async Task Run(OracleConnection cn,string op,OracleTransaction? tx,CancellationToken ct,params RrmParam[] args)
    { using var cmd=Cmd(cn,op,tx,args); await cmd.ExecuteNonQueryAsync(ct); }
    internal static async Task<ReciboEmailDestino> Contact(OracleConnection cn,int company,int persona,CancellationToken ct)
    {
        using var cmd=Cmd(cn,"CONTACTOS",null,N("p_EMPRESA",company),N("p_PERSONA",persona));
        cmd.Parameters.Add("p_ROWS",OracleDbType.RefCursor,ParameterDirection.Output);
        var rows=new List<ReciboEmailContacto>();
        using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) rows.Add(new(reader.SafeGetInt32("CODIGO_COMUNICACION"),reader.SafeGetString("CORREO"),reader.SafeGetInt32("PRINCIPAL")==1));
        return RhReciboEmailRules.ResolveContact(rows);
    }
    private static byte[] Blob(OracleDataReader reader,string name)
    {
        using var value=reader.GetOracleBlob(reader.GetOrdinal(name));
        if(value.Length>RhReciboEmailRules.MaxPdfBytes) throw new InvalidOperationException("PDF mayor al limite.");
        return value.Value;
    }
    internal static RrmItem Item(OracleDataReader r,bool blob=false) => new(r.SafeGetInt32("ITEM_ID"),r.SafeGetInt32("PERSONA"),r.SafeGetString("NOMBRE"),r.SafeGetString("CORREO"),r.SafeGetString("ESTADO"),r.SafeGetString("ERROR"),
        r.SafeGetString("FUENTE"),r.SafeGetString("HUELLA"),r.SafeGetString("LOGO_HASH"),r.SafeGetString("SOL_SMTP"),r.SafeGetInt32("INTENTOS"),
        blob && !r.IsDBNull(r.GetOrdinal("PDF")) ? Blob(r,"PDF") : null,r.SafeGetString("PDF_HASH"),r.SafeGetString("TOKEN"));
    internal static RrmLot Head(OracleDataReader r,List<RrmItem> items) => new(r.SafeGetString("LOTE_ID"),r.SafeGetInt32("ACTOR"),r.SafeGetString("REVISION"),r.SafeGetString("ESTADO"),r.SafeGetString("MOTIVO"),
        r.SafeGetInt32("NOMINA"),r.SafeGetInt32("PERIODO"),r.SafeGetInt32("ICP"),r.GetDateTime(r.GetOrdinal("CREADO")),r.GetDateTime(r.GetOrdinal("VENCE")),items);
    internal static async Task<RrmLot?> Read(OracleConnection cn,string id,int company,int actor,CancellationToken ct,OracleTransaction? tx=null)
    {
        using var cmd=Cmd(cn,"LEER",tx,S("p_ID",id),N("p_EMPRESA",company),N("p_ACTOR",actor));
        cmd.Parameters.Add("p_HEAD",OracleDbType.RefCursor,ParameterDirection.Output);cmd.Parameters.Add("p_ROWS",OracleDbType.RefCursor,ParameterDirection.Output);
        using var reader=await cmd.ExecuteReaderAsync(ct); RrmLot? lot=null;
        if(await reader.ReadAsync(ct)) lot=Head(reader,[]);
        if(lot is null) return null;
        if(await reader.NextResultAsync(ct)) while(await reader.ReadAsync(ct)) lot.Items.Add(Item(reader));
        return lot;
    }
    internal static ReciboEmailLote View(RrmLot lot,bool enabled,int page=1,int size=20) => new(lot.Id,lot.Revision,RhReciboEmailRules.LotState(lot.Estado,lot.Items.Select(x=>x.Estado)),lot.Motivo,
        lot.Nomina,lot.Periodo,lot.Icp,lot.Creado,lot.Vence,enabled,lot.Items.Count,lot.Items.GroupBy(x=>x.Estado).ToDictionary(x=>x.Key,x=>x.Count()),
        lot.Items.Skip((page-1)*size).Take(size).Select(x=>new ReciboEmailItem(x.Id,x.Persona,x.Nombre,x.Correo,x.Estado,x.Error,x.Intentos)).ToList(),page,size);
}
