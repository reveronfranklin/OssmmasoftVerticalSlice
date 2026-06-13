using Microsoft.AspNetCore.Http;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class GetSupportHistoryByTicketHandler(ConnectionDB connectionDB, IConfiguration config, SupportChildRowsReader childRowsReader)
{
    public async Task<ResultDto<List<SupportHistoryResponse>>> HandleAsync(
        SupportTicketChildQuery value,
        ClaimsPrincipal user,
        HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<List<SupportHistoryResponse>>(null!) { IsValid = false, Message = userValidation.Message };
        }

        var ticketAccess = await SupportSecurity.CanViewTicketAsync(connectionDB, config, value.UsuarioId, value.TicketId);
        if (!ticketAccess.IsValid)
        {
            return new ResultDto<List<SupportHistoryResponse>>(null!) { IsValid = false, Message = ticketAccess.Message };
        }

        return await childRowsReader.ReadAsync("SIS.SP_SOP_HIST_GET_TKT", value.TicketId, reader => new SupportHistoryResponse(
            reader.SafeGetInt32("HISTORIAL_ID"),
            reader.SafeGetInt32("TICKET_ID"),
            reader.SafeGetString("TIPO_CAMBIO"),
            reader.SafeGetString("CAMPO"),
            reader.SafeGetString("VALOR_ANTERIOR"),
            reader.SafeGetString("VALOR_NUEVO"),
            reader.SafeGetString("COMENTARIO"),
            reader.SafeGetInt32("USUARIO_ID"),
            Convert.ToDateTime(reader.GetValue(reader.GetOrdinal("FECHA_CAMBIO")))
        ));
    }
}
