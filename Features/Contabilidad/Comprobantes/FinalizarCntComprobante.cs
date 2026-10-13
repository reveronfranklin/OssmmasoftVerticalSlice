using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class FinalizarCntComprobanteHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntComprobanteFinalizarResponse>> HandleAsync(CntComprobanteFinalizarCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var access = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!access.IsValid) return Failure(access.Message);
            foreach (var permission in new[] { CntSecurity.ComprobanteEdit, CntSecurity.ComprobanteEditAutomatic })
            {
                access = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, permission);
                if (!access.IsValid) return Failure(access.Message);
            }
            if (value.CodigoComprobante <= 0 || value.Version <= 0) return Failure("El comprobante y la version son requeridos.");
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string error)) return Failure(error);
            using var cn = connectionDB.GetCntConnection();
            try { await cn.OpenAsync(); }
            catch (Exception ex) { return Failure($"Error de conexion CNT: {ex.Message}"); }
            using var tx = cn.BeginTransaction();
            using var cmd = new OracleCommand("CNT.SP_CNT_CMP_FINALIZAR", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
            cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = value.CodigoComprobante;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_VERSION_CMP", OracleDbType.Int32).Value = value.Version;
            var version = cmd.Parameters.Add("p_VERSION_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var message = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            await cmd.ExecuteNonQueryAsync();
            if (!CntDb.IsSuccessMessage(CntDb.GetMessage(message))) return Failure(CntDb.GetMessage(message));
            tx.Commit();
            var data = new CntComprobanteFinalizarResponse(value.CodigoComprobante, "FINALIZADO", 0m, CntDb.GetIntOutput(version));
            return new ResultDto<CntComprobanteFinalizarResponse>(data) { IsValid = true, Message = "Comprobante finalizado correctamente." };
        }
        catch (Exception ex) { return Failure($"Error tecnico: {ex.Message}"); }
    }

    private static ResultDto<CntComprobanteFinalizarResponse> Failure(string message) => new(null!) { Data = null, IsValid = false, Message = message };
}
