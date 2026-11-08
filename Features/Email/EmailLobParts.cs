using System.Data;
using System.Globalization;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace OssmmasoftVerticalSlice.Features.Email;

// Both receipt packages authorize each fragment by company, item, state and claim token.
internal static class EmailLobParts
{
    internal static async Task<string> Text(Func<OracleCommand> command,CancellationToken ct)
        => (await Read(command,false,2*1024*1024,ct)).Text;
    internal static async Task<byte[]?> Pdf(Func<OracleCommand> command,CancellationToken ct)
    {
        var value=await Read(command,true,5*1024*1024,ct);
        return value.Bytes.Length==0?null:value.Bytes;
    }
    private static async Task<(string Text,byte[] Bytes)> Read(Func<OracleCommand> command,bool binary,int max,CancellationToken ct)
    {
        var text=new StringBuilder();using var bytes=new MemoryStream();int offset=1;int? length=null;
        while(true)
        {
            ct.ThrowIfCancellationRequested();using var cmd=command();
            cmd.Parameters.Add("p_TIPO",OracleDbType.Varchar2).Value=binary?"B":"T";
            cmd.Parameters.Add("p_POS",OracleDbType.Int32).Value=offset;
            var part=cmd.Parameters.Add("p_TEXTO",OracleDbType.Varchar2,4000);part.Direction=ParameterDirection.Output;
            var raw=cmd.Parameters.Add("p_BIN",OracleDbType.Raw,16000);raw.Direction=ParameterDirection.Output;
            var total=cmd.Parameters.Add("p_TOTAL",OracleDbType.Int32,ParameterDirection.Output);
            var next=cmd.Parameters.Add("p_NEXT",OracleDbType.Int32,ParameterDirection.Output);
            await cmd.ExecuteNonQueryAsync(ct);
            var size=int.Parse(total.Value.ToString()!,CultureInfo.InvariantCulture);
            if(size<0 || size>max)throw new InvalidOperationException("Contenido de correo excede el limite permitido.");
            if(length is not null && length!=size)throw new InvalidOperationException("El contenido cambio durante su lectura.");
            length=size;if(size==0)break;
            var following=int.Parse(next.Value.ToString()!,CultureInfo.InvariantCulture);
            if(following<=offset || following>size+1)throw new InvalidOperationException("Lectura incompleta del correo.");
            if(binary)
            {
                var data=raw.Value is OracleBinary ob?(ob.IsNull?[]:ob.Value):raw.Value as byte[]??[];
                if(data.Length!=following-offset)throw new InvalidOperationException("Fragmento PDF incompleto.");
                bytes.Write(data);
            }
            else
            {
                var chunk=part.Value is OracleString os?(os.IsNull?"":os.Value):part.Value?.ToString()??"";
                if(chunk.Length!=following-offset)throw new InvalidOperationException("Fragmento de texto incompleto.");
                text.Append(chunk);
            }
            offset=following;if(offset>size)break;
        }
        return(text.ToString(),bytes.ToArray());
    }
}
