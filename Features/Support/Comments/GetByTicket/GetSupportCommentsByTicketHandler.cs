using Microsoft.AspNetCore.Http;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class GetSupportCommentsByTicketHandler(ConnectionDB connectionDB, IConfiguration config, SupportChildRowsReader childRowsReader)
{
    public async Task<ResultDto<List<SupportCommentResponse>>> HandleAsync(
        SupportTicketChildQuery value,
        ClaimsPrincipal user,
        HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<List<SupportCommentResponse>>(null!) { IsValid = false, Message = userValidation.Message };
        }

        var ticketAccess = await SupportSecurity.CanViewTicketAsync(connectionDB, config, value.UsuarioId, value.TicketId);
        if (!ticketAccess.IsValid)
        {
            return new ResultDto<List<SupportCommentResponse>>(null!) { IsValid = false, Message = ticketAccess.Message };
        }

        return await childRowsReader.ReadAsync("SIS.SP_SOP_COMM_GET_TKT", value.TicketId, reader => new SupportCommentResponse(
            reader.SafeGetInt32("COMENTARIO_ID"),
            reader.SafeGetInt32("TICKET_ID"),
            reader.SafeGetInt32("USUARIO_ID"),
            reader.SafeGetString("USUARIO"),
            reader.SafeGetString("COMENTARIO"),
            SupportDb.SafeGetFlag(reader, "ES_INTERNO"),
            Convert.ToDateTime(reader.GetValue(reader.GetOrdinal("FECHA_COMENTARIO")))
        ));
    }
}
