using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class CreateSupportCommentHandler(ConnectionDB connectionDB, IConfiguration config, SupportTicketEvents events)
{
    public async Task<ResultDto<int>> HandleAsync(SupportCommentCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
        }

        var result = await events.Execute(value.TicketId, value.UsuarioId, "COMENTARIO", value.Comentario, isInternal: value.EsInterno);
        return new ResultDto<int>(result.IsValid ? result.Data?.Id ?? 0 : 0) { IsValid = result.IsValid, Message = result.Message };
    }
}
