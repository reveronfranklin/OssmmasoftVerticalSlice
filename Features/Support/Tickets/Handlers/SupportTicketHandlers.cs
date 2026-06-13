using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class CreateSupportTicketHandler(ConnectionDB connectionDB, IConfiguration config, SupportTicketHandlerSupport support)
{
    public async Task<ResultDto<int>> HandleAsync(SupportTicketCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.CreatedBy);
        if (!userValidation.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
        }

        var permission = await support.CheckPermissionAsync(value.CreatedBy, SupportSecurity.TicketCreate);
        if (!permission.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = permission.Message };
        }

        return await support.ExecuteScalarAsync("SIS.SP_SOP_TKT_INS", cmd =>
        {
            cmd.Parameters.Add("p_USUARIO_SOLICITANTE_ID", OracleDbType.Int32).Value = value.UsuarioSolicitanteId;
            cmd.Parameters.Add("p_TIPO_SOLICITUD_ID", OracleDbType.Int32).Value = value.TipoSolicitudId;
            cmd.Parameters.Add("p_MODULO_ID", OracleDbType.Int32).Value = value.ModuloId;
            cmd.Parameters.Add("p_ASUNTO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Asunto);
            cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Descripcion);
            cmd.Parameters.Add("p_PRIORIDAD_ID", OracleDbType.Int32).Value = value.PrioridadId;
            cmd.Parameters.Add("p_CREATED_BY", OracleDbType.Int32).Value = value.CreatedBy;
        }, "p_TICKET_ID_OUT");
    }
}

public class GetSupportTicketsHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<SupportTicketResponse>>> HandleAsync(SupportTicketGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<List<SupportTicketResponse>>(null!) { IsValid = false, Message = userValidation.Message };
            }

            if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<SupportTicketResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            if (value.UsuarioId <= 0)
            {
                return new ResultDto<List<SupportTicketResponse>>(null!) { IsValid = false, Message = "UsuarioId es requerido para consultar tickets." };
            }

            int pageSize = value.PageSize <= 0 ? 10 : value.PageSize;
            int pageNumber = value.PageNumber <= 0 ? 1 : value.PageNumber;

            using var cn = connectionDB.GetSisConnection();
            await cn.OpenAsync();

            var permissions = await SupportSecurity.GetPermissionsAsync(cn, value.UsuarioId, empresa);
            var canViewAll = SupportSecurity.HasAny(permissions, SupportSecurity.TicketViewAll);
            var canViewAssigned = SupportSecurity.HasAny(permissions, SupportSecurity.TicketViewAssigned);
            var canViewOwn = SupportSecurity.HasAny(permissions, SupportSecurity.TicketViewOwn);

            if (!canViewAll && !canViewAssigned && !canViewOwn)
            {
                return SupportSecurity.Forbidden<List<SupportTicketResponse>>(SupportSecurity.TicketViewOwn);
            }

            var responsableId = value.ResponsableId;
            var solicitanteId = value.SolicitanteId;

            if (!canViewAll && canViewAssigned)
            {
                responsableId = value.UsuarioId;
            }

            if (!canViewAll && !canViewAssigned && canViewOwn)
            {
                solicitanteId = value.UsuarioId;
            }

            using var cmd = new OracleCommand("SIS.SP_SOP_TKT_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = pageSize;
            cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = pageNumber;
            cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_ESTADO_ID", OracleDbType.Int32).Value = SupportDb.DbValue(value.EstadoId);
            cmd.Parameters.Add("p_PRIORIDAD_ID", OracleDbType.Int32).Value = SupportDb.DbValue(value.PrioridadId);
            cmd.Parameters.Add("p_TIPO_SOLICITUD_ID", OracleDbType.Int32).Value = SupportDb.DbValue(value.TipoSolicitudId);
            cmd.Parameters.Add("p_MODULO_ID", OracleDbType.Int32).Value = SupportDb.DbValue(value.ModuloId);
            cmd.Parameters.Add("p_RESPONSABLE_ID", OracleDbType.Int32).Value = SupportDb.DbValue(responsableId);
            cmd.Parameters.Add("p_SOLICITANTE_ID", OracleDbType.Int32).Value = SupportDb.DbValue(solicitanteId);
            cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = SupportDb.DbValue(value.FechaDesde);
            cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = SupportDb.DbValue(value.FechaHasta);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
            var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

            var list = new List<SupportTicketResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(SupportDb.MapTicket(reader));
                }
            }

            var message = SupportDb.GetMessage(pMessage);
            var isSuccess = SupportDb.IsSuccessMessage(message);
            return new ResultDto<List<SupportTicketResponse>>(list)
            {
                Data = isSuccess ? list : null,
                IsValid = isSuccess,
                Message = message,
                Page = pageNumber,
                TotalPage = SupportDb.GetIntOutput(pTotalPages),
                CantidadRegistros = SupportDb.GetIntOutput(pTotalRecords)
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<SupportTicketResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetSupportTicketByIdHandler(ConnectionDB connectionDB, IConfiguration config, SupportTicketHandlerSupport support)
{
    public async Task<ResultDto<SupportTicketResponse>> HandleAsync(SupportTicketGetByIdQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<SupportTicketResponse>(null!) { IsValid = false, Message = userValidation.Message };
        }

        var result = await support.ExecuteReaderSingleAsync("SIS.SP_SOP_TKT_GET_ID", cmd =>
        {
            cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = value.TicketId;
        }, SupportDb.MapTicket);

        if (!result.IsValid || result.Data is null)
        {
            return result;
        }

        var permission = await support.CanViewTicketAsync(value.UsuarioId, result.Data);
        return permission.IsValid
            ? result
            : new ResultDto<SupportTicketResponse>(null!) { Data = null, IsValid = false, Message = permission.Message };
    }
}

public class AssignSupportTicketHandler(ConnectionDB connectionDB, IConfiguration config, SupportTicketHandlerSupport support)
{
    public async Task<ResultDto<string>> HandleAsync(SupportTicketAssignCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UpdatedBy);
        if (!userValidation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = userValidation.Message };
        }

        var permission = await support.CheckPermissionAsync(value.UpdatedBy, SupportSecurity.TicketAssign);
        if (!permission.IsValid)
        {
            return permission;
        }

        var analystValidation = await support.ValidateSupportAnalystAsync(value.UsuarioResponsableId);
        if (!analystValidation.IsValid)
        {
            return analystValidation;
        }

        return await support.ExecuteMessageAsync("SIS.SP_SOP_TKT_ASSIGN", cmd =>
        {
            cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = value.TicketId;
            cmd.Parameters.Add("p_USUARIO_RESPONSABLE_ID", OracleDbType.Int32).Value = value.UsuarioResponsableId;
            cmd.Parameters.Add("p_UPDATED_BY", OracleDbType.Int32).Value = value.UpdatedBy;
            cmd.Parameters.Add("p_COMENTARIO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Comentario);
        });
    }
}

public class ChangeSupportTicketStatusHandler(ConnectionDB connectionDB, IConfiguration config, SupportTicketHandlerSupport support)
{
    public async Task<ResultDto<string>> HandleAsync(SupportTicketStatusCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UpdatedBy);
        if (!userValidation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = userValidation.Message };
        }

        var permission = await support.CheckPermissionAsync(value.UpdatedBy, SupportSecurity.TicketStatus);
        if (!permission.IsValid)
        {
            return permission;
        }

        return await support.ExecuteMessageAsync("SIS.SP_SOP_TKT_STATUS_UPD", cmd =>
        {
            cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = value.TicketId;
            cmd.Parameters.Add("p_ESTADO_ID", OracleDbType.Int32).Value = value.EstadoId;
            cmd.Parameters.Add("p_UPDATED_BY", OracleDbType.Int32).Value = value.UpdatedBy;
            cmd.Parameters.Add("p_COMENTARIO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Comentario);
        });
    }
}

public class CloseSupportTicketHandler(ConnectionDB connectionDB, IConfiguration config, SupportTicketHandlerSupport support)
{
    public async Task<ResultDto<string>> HandleAsync(SupportTicketCloseCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UpdatedBy);
        if (!userValidation.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = userValidation.Message };
        }

        var permission = await support.CheckPermissionAsync(value.UpdatedBy, SupportSecurity.TicketClose);
        if (!permission.IsValid)
        {
            return permission;
        }

        return await support.ExecuteMessageAsync("SIS.SP_SOP_TKT_CLOSE", cmd =>
        {
            cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = value.TicketId;
            cmd.Parameters.Add("p_UPDATED_BY", OracleDbType.Int32).Value = value.UpdatedBy;
            cmd.Parameters.Add("p_OBSERVACION_RESOLUCION", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.ObservacionResolucion);
        });
    }
}
