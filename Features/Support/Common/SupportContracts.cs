using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Support;

public record SupportTicketCreateCommand(int UsuarioSolicitanteId, int TipoSolicitudId, int ModuloId, string Asunto, string Descripcion, int PrioridadId, int CreatedBy);
public record SupportTicketGetAllQuery(int UsuarioId, int PageSize = 10, int PageNumber = 1, string SearchText = "", int? EstadoId = null, int? PrioridadId = null, int? TipoSolicitudId = null, int? ModuloId = null, int? ResponsableId = null, int? SolicitanteId = null, DateTime? FechaDesde = null, DateTime? FechaHasta = null);
public record SupportTicketGetByIdQuery(int TicketId, int UsuarioId);
public record SupportTicketAssignCommand(int TicketId, int UsuarioResponsableId, int UpdatedBy, string? Comentario = null);
public record SupportTicketStatusCommand(int TicketId, int EstadoId, int UpdatedBy, string? Comentario = null);
public record SupportTicketCloseCommand(int TicketId, int UpdatedBy, string ObservacionResolucion);
public record SupportCommentCreateCommand(int TicketId, int UsuarioId, string Comentario, bool EsInterno);
public record SupportTicketChildQuery(int TicketId, int UsuarioId = 0);
public record SupportAttachmentCreateCommand(int TicketId, string NombreOriginal, string? IdentificadorArchivo, string? RutaArchivo, string? MimeType, long TamanoBytes, int UsuarioCargaId);
public record SupportCatalogGetAllQuery(string Catalogo);
public record SupportNotificationGetByUserQuery(int UsuarioId, int PageSize = 10, int PageNumber = 1);
public record SupportNotificationMarkReadCommand(int NotifId, int UsuarioId);
public record SupportDashboardSummaryQuery(int UsuarioId, DateTime? FechaDesde = null, DateTime? FechaHasta = null);
public record SupportPermissionsQuery(int UsuarioId);
public record SupportSlaNotifyCommand(int UsuarioId);
