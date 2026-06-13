using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Security.Claims;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.Support;

public record SupportTicketResponse(
    int TicketId,
    string TicketNumero,
    int UsuarioSolicitanteId,
    string UsuarioSolicitante,
    int TipoSolicitudId,
    string TipoSolicitud,
    int ModuloId,
    string Modulo,
    string Asunto,
    string Descripcion,
    int PrioridadId,
    string Prioridad,
    int EstadoId,
    string Estado,
    int? UsuarioResponsableId,
    string UsuarioResponsable,
    DateTime FechaCreacion,
    DateTime? FechaAsignacion,
    DateTime? FechaPrimeraRespuesta,
    DateTime? FechaResolucion,
    DateTime? FechaCierre,
    string ObservacionResolucion,
    DateTime? FechaVencimientoSla,
    int CodigoEmpresa
);

public record SupportCatalogResponse(int Id, string Nombre, string Descripcion, int Orden, bool Activo);
public record SupportCommentResponse(int ComentarioId, int TicketId, int UsuarioId, string Usuario, string Comentario, bool EsInterno, DateTime FechaComentario);
public record SupportHistoryResponse(int HistorialId, int TicketId, string TipoCambio, string Campo, string ValorAnterior, string ValorNuevo, string Comentario, int UsuarioId, DateTime FechaCambio);
public record SupportAttachmentResponse(int AdjuntoId, int TicketId, string NombreOriginal, string IdentificadorArchivo, string RutaArchivo, string MimeType, long TamanoBytes, int UsuarioCargaId, DateTime FechaCarga, bool Activo);
public record SupportNotificationResponse(int NotifId, int TicketId, int UsuarioDestinoId, string Evento, string Titulo, string Mensaje, string Canal, bool Leida, DateTime FechaCreacion);
public record SupportDashboardSummaryResponse(int TotalTickets, int TicketsAbiertos, int TicketsCerrados, int TicketsCriticos, int TicketsVencidos, int TicketsSinAsignar, decimal TiempoPromedioResolucion);
public record SupportPermissionsResponse(int UsuarioId, string Perfil, List<string> Permissions);

internal static class SupportDb
{
    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        empresa = 0;
        errorMessage = string.Empty;
        var empresaString = config["settings:EmpresaConfig"];

        if (string.IsNullOrWhiteSpace(empresaString))
        {
            errorMessage = "Configuración 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(empresaString, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un número válido.";
            return false;
        }

        return true;
    }

    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "ok", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static object DbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }

    public static object StringDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : ToInt32(parameter.Value);
    }

    public static DateTime? SafeGetNullableDate(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    public static DateTime SafeGetDate(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    public static int? SafeGetNullableInt(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : ToInt32(reader.GetValue(ordinal));
    }

    public static bool SafeGetFlag(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return !reader.IsDBNull(ordinal) && ToInt32(reader.GetValue(ordinal)) == 1;
    }

    public static int ToInt32(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(ToDecimal(value));
    }

    public static decimal ToDecimal(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return 0m;
        }

        if (value is OracleDecimal oracleDecimal)
        {
            return oracleDecimal.IsNull
                ? 0m
                : decimal.Parse(oracleDecimal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    public static SupportTicketResponse MapTicket(IDataReader reader)
    {
        return new SupportTicketResponse(
            reader.SafeGetInt32("TICKET_ID"),
            reader.SafeGetString("TICKET_NUMERO"),
            reader.SafeGetInt32("USUARIO_SOLICITANTE_ID"),
            reader.SafeGetString("USUARIO_SOLICITANTE"),
            reader.SafeGetInt32("TIPO_SOLICITUD_ID"),
            reader.SafeGetString("TIPO_SOLICITUD"),
            reader.SafeGetInt32("MODULO_ID"),
            reader.SafeGetString("MODULO"),
            reader.SafeGetString("ASUNTO"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetInt32("PRIORIDAD_ID"),
            reader.SafeGetString("PRIORIDAD"),
            reader.SafeGetInt32("ESTADO_ID"),
            reader.SafeGetString("ESTADO"),
            SafeGetNullableInt(reader, "USUARIO_RESPONSABLE_ID"),
            reader.SafeGetString("USUARIO_RESPONSABLE"),
            SafeGetDate(reader, "FECHA_CREACION"),
            SafeGetNullableDate(reader, "FECHA_ASIGNACION"),
            SafeGetNullableDate(reader, "FECHA_PRIMERA_RESPUESTA"),
            SafeGetNullableDate(reader, "FECHA_RESOLUCION"),
            SafeGetNullableDate(reader, "FECHA_CIERRE"),
            reader.SafeGetString("OBSERVACION_RESOLUCION"),
            SafeGetNullableDate(reader, "FECHA_VENCIMIENTO_SLA"),
            reader.SafeGetInt32("CODIGO_EMPRESA")
        );
    }
}

internal static class SupportSecurity
{
    public const string TicketCreate = "soporte.tickets.crear";
    public const string TicketViewOwn = "soporte.tickets.ver_propios";
    public const string TicketViewAssigned = "soporte.tickets.ver_asignados";
    public const string TicketViewUnassigned = "soporte.tickets.ver_sin_asignar";
    public const string TicketViewAll = "soporte.tickets.ver_todos";
    public const string TicketAssign = "soporte.tickets.asignar";
    public const string TicketStatus = "soporte.tickets.cambiar_estado";
    public const string TicketClose = "soporte.tickets.cerrar";
    public const string CommentCreate = "soporte.comentarios.crear";
    public const string CommentInternal = "soporte.comentarios.internos";
    public const string CatalogAdmin = "soporte.catalogos.admin";
    public const string DashboardView = "soporte.dashboard.ver";
    public const string SupportUsersConfig = "soporte.usuarios.configurar";

    public static string GetAuthenticatedLogin(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("unique_name")
            ?? user.FindFirstValue("name")
            ?? string.Empty;
    }

    public static async Task<int> GetAuthenticatedUserIdAsync(OracleConnection connection, ClaimsPrincipal user, int empresa, string? refreshToken)
    {
        var login = GetAuthenticatedLogin(user);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            using var refreshCmd = new OracleCommand(@"
                SELECT CODIGO_USUARIO
                  FROM SIS.SIS_USUARIOS
                 WHERE REFRESHTOKEN = :p_REFRESH_TOKEN
                   AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                   AND NVL(STATUS, '1') IN ('1', 'A')
                   AND TOKENEXPIRES IS NOT NULL
                   AND TOKENEXPIRES >= SYSDATE
                   AND ROWNUM = 1", connection)
            {
                BindByName = true
            };

            refreshCmd.Parameters.Add("p_REFRESH_TOKEN", OracleDbType.Varchar2).Value = refreshToken.Trim();
            refreshCmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

            var refreshResult = await refreshCmd.ExecuteScalarAsync();
            return SupportDb.ToInt32(refreshResult);
        }

        if (string.IsNullOrWhiteSpace(login))
        {
            return 0;
        }

        using var cmd = new OracleCommand(@"
            SELECT CODIGO_USUARIO
              FROM SIS.SIS_USUARIOS
             WHERE UPPER(LOGIN) = UPPER(:p_LOGIN)
               AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA
               AND NVL(STATUS, '1') IN ('1', 'A')
               AND TOKENEXPIRES IS NOT NULL
               AND TOKENEXPIRES >= SYSDATE
               AND ROWNUM = 1", connection)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_LOGIN", OracleDbType.Varchar2).Value = login.Trim();
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var result = await cmd.ExecuteScalarAsync();
        return SupportDb.ToInt32(result);
    }

    public static ResultDto<T> Unauthorized<T>()
    {
        return new ResultDto<T>(default!)
        {
            Data = default,
            IsValid = false,
            Message = "Usuario autenticado no valido."
        };
    }

    public static ResultDto<T> UserMismatch<T>()
    {
        return new ResultDto<T>(default!)
        {
            Data = default,
            IsValid = false,
            Message = "El usuario enviado no coincide con el usuario autenticado."
        };
    }

    public static async Task<ResultDto<int>> ResolveAuthenticatedUserAsync(ConnectionDB connectionDB, IConfiguration config, ClaimsPrincipal user, HttpRequest request)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var authenticatedUserId = await GetAuthenticatedUserIdAsync(cn, user, empresa, GetRefreshToken(request));
        return authenticatedUserId > 0
            ? new ResultDto<int>(authenticatedUserId) { IsValid = true, Message = "Success" }
            : Unauthorized<int>();
    }

    public static async Task<ResultDto<int>> ResolveAuthenticatedUserAsync(OracleConnection connection, int empresa, ClaimsPrincipal user, HttpRequest request)
    {
        var authenticatedUserId = await GetAuthenticatedUserIdAsync(connection, user, empresa, GetRefreshToken(request));

        return authenticatedUserId > 0
            ? new ResultDto<int>(authenticatedUserId) { IsValid = true, Message = "Success" }
            : Unauthorized<int>();
    }

    public static async Task<ResultDto<string>> ValidateRequestUserAsync(ConnectionDB connectionDB, IConfiguration config, ClaimsPrincipal user, HttpRequest request, int requestUsuarioId)
    {
        var session = await ResolveAuthenticatedUserAsync(connectionDB, config, user, request);
        if (!session.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = session.Message };
        }

        return session.Data == requestUsuarioId
            ? new ResultDto<string>("OK") { IsValid = true, Message = "Success" }
            : UserMismatch<string>();
    }

    public static async Task<ResultDto<string>> ValidateRequestUserAsync(OracleConnection connection, int empresa, ClaimsPrincipal user, HttpRequest request, int requestUsuarioId)
    {
        var session = await ResolveAuthenticatedUserAsync(connection, empresa, user, request);
        if (!session.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = session.Message };
        }

        return session.Data == requestUsuarioId
            ? new ResultDto<string>("OK") { IsValid = true, Message = "Success" }
            : UserMismatch<string>();
    }

    private static string? GetRefreshToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Refresh-Token", out var headerToken) && !string.IsNullOrWhiteSpace(headerToken))
        {
            return headerToken.ToString();
        }

        if (request.Headers.TryGetValue("RefreshToken", out var refreshHeader) && !string.IsNullOrWhiteSpace(refreshHeader))
        {
            return refreshHeader.ToString();
        }

        return request.Cookies["X-Refresh-Token"];
    }

    public static async Task<ResultDto<string>> CanViewTicketAsync(ConnectionDB connectionDB, IConfiguration config, int usuarioId, int ticketId)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var permissions = await GetPermissionsAsync(cn, usuarioId, empresa);

        using var cmd = new OracleCommand(@"
            SELECT USUARIO_SOLICITANTE_ID, USUARIO_RESPONSABLE_ID
              FROM SOP_TICKET
             WHERE TICKET_ID = :p_TICKET_ID
               AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = ticketId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        int solicitanteId = 0;
        int? responsableId = null;
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "No se encontró el ticket indicado." };
            }

            solicitanteId = reader.SafeGetInt32("USUARIO_SOLICITANTE_ID");
            responsableId = SupportDb.SafeGetNullableInt(reader, "USUARIO_RESPONSABLE_ID");
        }

        if (HasAny(permissions, TicketViewAll)
            || (HasAny(permissions, TicketViewAssigned) && responsableId == usuarioId)
            || (HasAny(permissions, TicketViewOwn) && solicitanteId == usuarioId))
        {
            return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
        }

        return Forbidden<string>(TicketViewOwn);
    }

    public static async Task<SupportPermissionsResponse> GetPermissionsAsync(OracleConnection connection, int usuarioId, int empresa)
    {
        using var cmd = new OracleCommand(@"
            SELECT r.JSON_MENU
             FROM SIS.OSS_USUARIO_ROL r
             JOIN SIS.SIS_USUARIOS u ON u.CODIGO_USUARIO = r.CODIGO_USUARIO
             WHERE r.CODIGO_USUARIO = :p_USUARIO_ID
               AND u.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
               AND NVL(u.STATUS, '1') IN ('1', 'A')", connection)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = usuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                ExtractPermissions(ReadJsonMenu(reader, "JSON_MENU"), permissions);
            }
        }

        var list = permissions.OrderBy(x => x).ToList();
        return new SupportPermissionsResponse(usuarioId, ResolvePerfil(list), list);
    }

    public static bool HasAny(SupportPermissionsResponse response, params string[] permissions)
    {
        return permissions.Any(permission => response.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase));
    }

    public static ResultDto<T> Forbidden<T>(string permission)
    {
        return new ResultDto<T>(default!)
        {
            Data = default,
            IsValid = false,
            Message = $"El usuario no tiene el permiso requerido: {permission}."
        };
    }

    private static string ResolvePerfil(IReadOnlyCollection<string> permissions)
    {
        if (permissions.Contains(TicketViewAll, StringComparer.OrdinalIgnoreCase)
            || permissions.Contains(CatalogAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return "SOPORTE_ADMIN";
        }

        if (permissions.Contains(TicketViewAssigned, StringComparer.OrdinalIgnoreCase)
            || permissions.Contains(TicketAssign, StringComparer.OrdinalIgnoreCase))
        {
            return "SOPORTE_AGENTE";
        }

        if (permissions.Contains(TicketViewOwn, StringComparer.OrdinalIgnoreCase)
            || permissions.Contains(TicketCreate, StringComparer.OrdinalIgnoreCase))
        {
            return "SOPORTE_USUARIO";
        }

        return "SIN_PERMISOS_SOPORTE";
    }

    private static void ExtractPermissions(string jsonMenu, ISet<string> permissions)
    {
        if (string.IsNullOrWhiteSpace(jsonMenu))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonMenu);
            ExtractPermissions(document.RootElement, permissions);
        }
        catch (JsonException)
        {
        }
    }

    private static void ExtractPermissions(JsonElement element, ISet<string> permissions)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ExtractPermissions(item, permissions);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("permissions", out var permissionsNode)
            && permissionsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var permission in permissionsNode.EnumerateArray())
            {
                if (permission.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(permission.GetString()))
                {
                    permissions.Add(permission.GetString()!);
                }
            }
        }

        if (element.TryGetProperty("children", out var children))
        {
            ExtractPermissions(children, permissions);
        }
    }

    private static string ReadJsonMenu(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        if (reader is OracleDataReader oracleReader)
        {
            using OracleClob clob = oracleReader.GetOracleClob(ordinal);
            return clob.IsNull ? string.Empty : clob.Value;
        }

        return reader.GetValue(ordinal).ToString() ?? string.Empty;
    }
}
