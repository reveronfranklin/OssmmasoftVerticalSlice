using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Support;

public class SupportChildRowsReader(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<T>>> ReadAsync<T>(string procedure, int ticketId, Func<IDataReader, T> map)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<List<T>>(null!) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = ticketId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var list = new List<T>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(map(reader));
            }
        }

        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<List<T>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
    }
}
