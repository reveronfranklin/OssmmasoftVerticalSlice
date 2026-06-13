using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class SupportDashboardSummaryHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<SupportDashboardSummaryResponse>> HandleAsync(
        SupportDashboardSummaryQuery value,
        ClaimsPrincipal user,
        HttpRequest request)
    {
        try
        {
            if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<SupportDashboardSummaryResponse>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetSisConnection();
            await cn.OpenAsync();

            var userValidation = await SupportSecurity.ValidateRequestUserAsync(cn, empresa, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<SupportDashboardSummaryResponse>(null!) { IsValid = false, Message = userValidation.Message };
            }

            var permissions = await SupportSecurity.GetPermissionsAsync(cn, value.UsuarioId, empresa);

            if (!SupportSecurity.HasAny(permissions, SupportSecurity.DashboardView, SupportSecurity.TicketViewAll, SupportSecurity.TicketViewAssigned))
            {
                return SupportSecurity.Forbidden<SupportDashboardSummaryResponse>(SupportSecurity.DashboardView);
            }

            using var cmd = new OracleCommand("SIS.SP_SOP_DASH_SUMMARY", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = SupportDb.DbValue(value.FechaDesde);
            cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = SupportDb.DbValue(value.FechaHasta);
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            SupportDashboardSummaryResponse? summary = null;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    summary = new SupportDashboardSummaryResponse(
                        reader.SafeGetInt32("TOTAL_TICKETS"),
                        reader.SafeGetInt32("TICKETS_ABIERTOS"),
                        reader.SafeGetInt32("TICKETS_CERRADOS"),
                        reader.SafeGetInt32("TICKETS_CRITICOS"),
                        reader.SafeGetInt32("TICKETS_VENCIDOS"),
                        reader.SafeGetInt32("TICKETS_SIN_ASIGNAR"),
                        reader.SafeGetDecimal("TIEMPO_PROMEDIO_RESOLUCION")
                    );
                }
            }

            var message = SupportDb.GetMessage(pMessage);
            var isSuccess = SupportDb.IsSuccessMessage(message);
            return new ResultDto<SupportDashboardSummaryResponse>(summary!) { Data = isSuccess ? summary : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<SupportDashboardSummaryResponse>(null!) { IsValid = false, Message = $"{ex.GetType().Name}: {ex.Message}" };
        }
    }
}
