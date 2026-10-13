using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class GetCntComprobantesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntComprobanteResponse>>> HandleAsync(CntComprobanteGetAllQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<List<CntComprobanteResponse>>(null!) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteView);
            if (!permission.IsValid)
            {
                return new ResultDto<List<CntComprobanteResponse>>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntComprobanteResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            int pageSize = value.PageSize <= 0 ? 10 : value.PageSize;
            int pageNumber = value.PageNumber <= 0 ? 1 : value.PageNumber;

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();

            if (value.CodigoPeriodo.HasValue && (value.FechaDesde.HasValue || value.FechaHasta.HasValue))
            {
                using var periodCmd = new OracleCommand(@"
                    SELECT FECHA_DESDE,
                           FECHA_HASTA
                      FROM CNT.CNT_PERIODOS
                     WHERE CODIGO_PERIODO = :p_CODIGO_PERIODO
                       AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn);
                periodCmd.BindByName = true;
                periodCmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo.Value;
                periodCmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

                using var periodReader = await periodCmd.ExecuteReaderAsync();
                if (!await periodReader.ReadAsync())
                {
                    return new ResultDto<List<CntComprobanteResponse>>(null!) { Data = null, IsValid = false, Message = "El periodo seleccionado no existe." };
                }

                var periodoDesde = CntDb.SafeGetDate(periodReader, "FECHA_DESDE").Date;
                var periodoHasta = CntDb.SafeGetDate(periodReader, "FECHA_HASTA").Date;

                if ((value.FechaDesde.HasValue && value.FechaDesde.Value.Date < periodoDesde)
                    || (value.FechaHasta.HasValue && value.FechaHasta.Value.Date > periodoHasta)
                    || (value.FechaDesde.HasValue && value.FechaHasta.HasValue && value.FechaDesde.Value.Date > value.FechaHasta.Value.Date))
                {
                    return new ResultDto<List<CntComprobanteResponse>>(null!) { Data = null, IsValid = false, Message = "Las fechas deben estar dentro del periodo seleccionado." };
                }
            }

            using var cmd = new OracleCommand("CNT.SP_CNT_CMP_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = pageSize;
            cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = pageNumber;
            cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoPeriodo);
            cmd.Parameters.Add("p_ORIGEN_ID", OracleDbType.Int32).Value = CntDb.DbValue(value.OrigenId);
            cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = CntDb.DbValue(value.FechaDesde);
            cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = CntDb.DbValue(value.FechaHasta);
            cmd.Parameters.Add("p_ESTADO_CMP", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.EstadoContable);
            cmd.Parameters.Add("p_ES_AUTOMATICO", OracleDbType.Int32).Value = value.EsAutomatico.HasValue ? (value.EsAutomatico.Value ? 1 : 0) : DBNull.Value;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
            var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

            var list = new List<CntComprobanteResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntDb.MapComprobante(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntComprobanteResponse>>(list)
            {
                Data = isSuccess ? list : null,
                IsValid = isSuccess,
                Message = message,
                Page = pageNumber,
                TotalPage = CntDb.GetIntOutput(pTotalPages),
                CantidadRegistros = CntDb.GetIntOutput(pTotalRecords)
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntComprobanteResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntComprobanteByIdHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntComprobanteResponse>> HandleAsync(CntComprobanteGetByIdQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<CntComprobanteResponse>(null!) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteView);
            if (!permission.IsValid)
            {
                return new ResultDto<CntComprobanteResponse>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<CntComprobanteResponse>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CMP_GET_ID", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = value.CodigoComprobante;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            CntComprobanteResponse? item = null;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    item = CntDb.MapComprobante(reader);
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message) && item is not null;
            return new ResultDto<CntComprobanteResponse>(item!) { Data = isSuccess ? item : null, IsValid = isSuccess, Message = isSuccess ? message : "Comprobante no encontrado." };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntComprobanteResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntComprobantePrintHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntComprobantePrintResponse>> HandleAsync(CntComprobantePrintQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<CntComprobantePrintResponse>(null!) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteView);
            if (!permission.IsValid)
            {
                return new ResultDto<CntComprobantePrintResponse>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<CntComprobantePrintResponse>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();

            CntComprobanteResponse? encabezado = null;
            using (var cmd = new OracleCommand("CNT.SP_CNT_CMP_GET_ID", cn) { CommandType = CommandType.StoredProcedure, BindByName = true })
            {
                cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = value.CodigoComprobante;
                cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
                cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
                var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        encabezado = CntDb.MapComprobante(reader);
                    }
                }

                var message = CntDb.GetMessage(pMessage);
                if (!CntDb.IsSuccessMessage(message))
                {
                    return new ResultDto<CntComprobantePrintResponse>(null!) { Data = null, IsValid = false, Message = message };
                }
            }

            if (encabezado is null)
            {
                return new ResultDto<CntComprobantePrintResponse>(null!) { Data = null, IsValid = false, Message = "Comprobante no encontrado." };
            }

            var detalles = new List<CntDetalleResponse>();
            using (var cmd = new OracleCommand("CNT.SP_CNT_DET_GET_CMP", cn) { CommandType = CommandType.StoredProcedure, BindByName = true })
            {
                cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = value.CodigoComprobante;
                cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
                cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
                var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        detalles.Add(CntDb.MapDetalle(reader));
                    }
                }

                var message = CntDb.GetMessage(pMessage);
                if (!CntDb.IsSuccessMessage(message))
                {
                    return new ResultDto<CntComprobantePrintResponse>(null!) { Data = null, IsValid = false, Message = message };
                }
            }

            var response = new CntComprobantePrintResponse(encabezado, detalles);
            return new ResultDto<CntComprobantePrintResponse>(response) { Data = response, IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntComprobantePrintResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GenerateCntComprobanteNumberHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntComprobanteNumberResponse>> HandleAsync(CntComprobanteNumberQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<CntComprobanteNumberResponse>(null!) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteCreate);
            if (!permission.IsValid)
            {
                return new ResultDto<CntComprobanteNumberResponse>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<CntComprobanteNumberResponse>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CMP_NUM_GEN", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo;
            cmd.Parameters.Add("p_TIPO_COMPROBANTE_ID", OracleDbType.Int32).Value = value.TipoComprobanteId;
            cmd.Parameters.Add("p_FECHA_COMPROBANTE", OracleDbType.Date).Value = value.FechaComprobante;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            var pNumero = cmd.Parameters.Add("p_NUMERO_OUT", OracleDbType.Varchar2, 20, null, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var response = new CntComprobanteNumberResponse(pNumero.Value?.ToString() ?? string.Empty);
            return new ResultDto<CntComprobanteNumberResponse>(response) { Data = isSuccess ? response : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntComprobanteNumberResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class GetCntDetallesByComprobanteHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CntDetalleResponse>>> HandleAsync(CntDetalleGetByComprobanteQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<List<CntDetalleResponse>>(null!) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteView);
            if (!permission.IsValid)
            {
                return new ResultDto<List<CntDetalleResponse>>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<List<CntDetalleResponse>>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_DET_GET_CMP", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = value.CodigoComprobante;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            var list = new List<CntDetalleResponse>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(CntDb.MapDetalle(reader));
                }
            }

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<List<CntDetalleResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<CntDetalleResponse>>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class CreateCntComprobanteHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntComprobanteCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
        }

        var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteCreate);
        if (!permission.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = permission.Message };
        }

        return await SaveAsync(connectionDB, config, value.UsuarioId, null, value.CodigoPeriodo, value.TipoComprobanteId, value.FechaComprobante, value.OrigenId, value.Observacion, value.Detalles);
    }

    internal static async Task<ResultDto<int>> SaveAsync(ConnectionDB connectionDB, IConfiguration config, int usuarioId, int? codigoComprobante, int codigoPeriodo, int tipoComprobanteId, DateTime fechaComprobante, int? origenId, string? observacion, List<CntDetalleCreateCommand> detalles, bool permitirAutomatico = false, int? version = null)
    {
        try
        {
            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
            }

            if (detalles.Count == 0)
            {
                return new ResultDto<int>(0) { IsValid = false, Message = "El comprobante debe tener al menos un detalle." };
            }

            detalles = detalles.Select(x => x with { Monto = CntMoney.Normalize(x.Monto) }).ToList();
            if (detalles.Any(x => x.Monto == 0 || x.CodigoMayor <= 0 || x.CodigoAuxiliar <= 0))
                return new ResultDto<int>(0) { IsValid = false, Message = "Cada detalle requiere cuentas e importe valido." };

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();

            try
            {
                await CntPendienteDb.LockAsync(cn, tx);
                await CntPendienteDb.ValidatePeriodAsync(cn, tx, codigoPeriodo, empresa, fechaComprobante);
                bool pendiente = false;
                int? periodoAnterior = null;
                if (codigoComprobante.HasValue)
                {
                    await CntPendienteDb.CheckEditAsync(cn, tx, codigoComprobante.Value, empresa, version, usuarioId, permitirAutomatico, "EDITAR");
                    using var header = new OracleCommand(@"SELECT CODIGO_PERIODO, TIPO_COMPROBANTE_ID, FECHA_COMPROBANTE, ORIGEN_ID, EXTRA1, ESTADO_CMP
                        FROM CNT.CNT_COMPROBANTES WHERE CODIGO_COMPROBANTE = :id AND CODIGO_EMPRESA = :empresa", cn) { BindByName = true, Transaction = tx };
                    header.Parameters.Add("id", OracleDbType.Int32).Value = codigoComprobante.Value;
                    header.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;
                    using var reader = await header.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) throw new InvalidOperationException("Comprobante no encontrado.");
                    periodoAnterior = reader.SafeGetInt32("CODIGO_PERIODO");
                    bool automatico = reader.SafeGetString("EXTRA1") == "AUTOMATICO";
                    pendiente = automatico && reader.SafeGetString("ESTADO_CMP") == "PENDIENTE";
                    if (automatico && (periodoAnterior != codigoPeriodo || reader.SafeGetInt32("TIPO_COMPROBANTE_ID") != tipoComprobanteId
                        || CntDb.SafeGetDate(reader, "FECHA_COMPROBANTE").Date != fechaComprobante.Date
                        || CntDb.SafeGetNullableInt(reader, "ORIGEN_ID") != origenId))
                        throw new InvalidOperationException("No puede cambiar la cabecera de un comprobante automatico.");
                    if (automatico) fechaComprobante = CntDb.SafeGetDate(reader, "FECHA_COMPROBANTE");
                }
                if (!pendiente && !CntMoney.IsBalanced(detalles.Select(x => x.Monto)))
                    return new ResultDto<int>(0) { IsValid = false, Message = "El comprobante no esta cuadrado." };

                int comprobanteId;
                var procedure = codigoComprobante.HasValue ? "CNT.SP_CNT_CMP_UPD" : "CNT.SP_CNT_CMP_INS";
                using (var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx })
                {
                    if (codigoComprobante.HasValue)
                    {
                        cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = codigoComprobante.Value;
                    }

                    cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = codigoPeriodo;
                    cmd.Parameters.Add("p_TIPO_COMPROBANTE_ID", OracleDbType.Int32).Value = tipoComprobanteId;
                    cmd.Parameters.Add("p_FECHA_COMPROBANTE", OracleDbType.Date).Value = fechaComprobante;
                    cmd.Parameters.Add("p_ORIGEN_ID", OracleDbType.Int32).Value = CntDb.DbValue(origenId);
                    cmd.Parameters.Add("p_OBSERVACION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(observacion);
                    cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = usuarioId;
                    cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
                    var pComprobanteId = cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
                    var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                    if (codigoComprobante.HasValue)
                    {
                        cmd.Parameters.Add("p_PERMITIR_AUTOMATICO", OracleDbType.Int32).Value = permitirAutomatico ? 1 : 0;
                    }
                    await cmd.ExecuteNonQueryAsync();

                    var message = CntDb.GetMessage(pMessage);
                    if (!CntDb.IsSuccessMessage(message))
                    {
                        tx.Rollback();
                        return new ResultDto<int>(0) { IsValid = false, Message = message };
                    }

                    comprobanteId = CntDb.GetIntOutput(pComprobanteId);
                }

                if (codigoComprobante.HasValue)
                {
                    using var delCmd = new OracleCommand("CNT.SP_CNT_DET_DEL_CMP", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
                    delCmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = comprobanteId;
                    delCmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
                    delCmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = usuarioId;
                    var deleteMessage = delCmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                    delCmd.Parameters.Add("p_PERMITIR_AUTOMATICO", OracleDbType.Int32).Value = permitirAutomatico ? 1 : 0;
                    await delCmd.ExecuteNonQueryAsync();
                    if (!CntDb.IsSuccessMessage(CntDb.GetMessage(deleteMessage)))
                        throw new InvalidOperationException(CntDb.GetMessage(deleteMessage));
                }

                foreach (var detalle in detalles)
                {
                    using var detCmd = new OracleCommand("CNT.SP_CNT_DET_INS", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
                    detCmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = comprobanteId;
                    detCmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = detalle.CodigoMayor;
                    detCmd.Parameters.Add("p_CODIGO_AUXILIAR", OracleDbType.Int32).Value = detalle.CodigoAuxiliar;
                    detCmd.Parameters.Add("p_REFERENCIA1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Referencia1);
                    detCmd.Parameters.Add("p_REFERENCIA2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Referencia2);
                    detCmd.Parameters.Add("p_REFERENCIA3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Referencia3);
                    detCmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Descripcion);
                    detCmd.Parameters.Add("p_MONTO", OracleDbType.Decimal).Value = detalle.Monto;
                    detCmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = usuarioId;
                    detCmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
                    detCmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
                    var pMessage = detCmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                    detCmd.Parameters.Add("p_PERMITIR_AUTOMATICO", OracleDbType.Int32).Value = permitirAutomatico ? 1 : 0;
                    await detCmd.ExecuteNonQueryAsync();

                    var detailMessage = CntDb.GetMessage(pMessage);
                    if (!CntDb.IsSuccessMessage(detailMessage))
                    {
                        tx.Rollback();
                        return new ResultDto<int>(0) { IsValid = false, Message = detailMessage };
                    }
                }

                if (codigoComprobante.HasValue)
                    await CntPendienteDb.FinishAsync(cn, tx, comprobanteId, empresa, usuarioId);
                else
                    await CntPendienteDb.AuditAsync(cn, tx, comprobanteId, empresa, usuarioId, "CREADO");
                await CntPendienteDb.InvalidatePrecloseAsync(cn, tx, codigoPeriodo, empresa);
                if (periodoAnterior.HasValue && periodoAnterior != codigoPeriodo)
                    await CntPendienteDb.InvalidatePrecloseAsync(cn, tx, periodoAnterior.Value, empresa);
                tx.Commit();
                return new ResultDto<int>(comprobanteId) { IsValid = true, Message = pendiente ? "Cambios guardados. El comprobante permanece pendiente de finalizacion." : "Success" };
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }
}

public class UpdateCntComprobanteHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntComprobanteUpdateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
        }

        var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteEdit);
        if (!permission.IsValid)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = permission.Message };
        }

        var automaticPermission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteEditAutomatic);

        return await CreateCntComprobanteHandler.SaveAsync(connectionDB, config, value.UsuarioId, value.CodigoComprobante, value.CodigoPeriodo, value.TipoComprobanteId, value.FechaComprobante, value.OrigenId, value.Observacion, value.Detalles, automaticPermission.IsValid, value.Version);
    }
}

public class DeleteCntComprobanteHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntComprobanteDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<string>(string.Empty) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteDelete);
            if (!permission.IsValid)
            {
                return permission;
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<string>(string.Empty) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var automaticPermission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteEditAutomatic);
            await CntPendienteDb.CheckEditAsync(cn, tx, value.CodigoComprobante, empresa, value.Version, value.UsuarioId, automaticPermission.IsValid, "ELIMINAR");
            using (var period = new OracleCommand("SELECT CODIGO_PERIODO FROM CNT.CNT_COMPROBANTES WHERE CODIGO_COMPROBANTE = :id AND CODIGO_EMPRESA = :empresa", cn) { BindByName = true, Transaction = tx })
            {
                period.Parameters.Add("id", OracleDbType.Int32).Value = value.CodigoComprobante;
                period.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;
                await CntPendienteDb.InvalidatePrecloseAsync(cn, tx, Convert.ToInt32(await period.ExecuteScalarAsync()), empresa);
            }
            using var cmd = new OracleCommand("CNT.SP_CNT_CMP_DEL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
            cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = value.CodigoComprobante;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            cmd.Parameters.Add("p_PERMITIR_AUTOMATICO", OracleDbType.Int32).Value = automaticPermission.IsValid ? 1 : 0;
            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            if (isSuccess) tx.Commit();
            return new ResultDto<string>(message) { Data = isSuccess ? message : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class ReorderCntComprobantesHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CntComprobanteReorderResponse>> HandleAsync(CntComprobanteReorderCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<CntComprobanteReorderResponse>(null!) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, CntSecurity.ComprobanteReorder);
            if (!permission.IsValid)
            {
                return new ResultDto<CntComprobanteReorderResponse>(null!) { Data = null, IsValid = false, Message = permission.Message };
            }

            if (value.CodigoPeriodo <= 0 || value.TipoComprobanteId <= 0)
            {
                return new ResultDto<CntComprobanteReorderResponse>(null!) { Data = null, IsValid = false, Message = "El periodo y el tipo de comprobante son requeridos." };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<CntComprobanteReorderResponse>(null!) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand("CNT.SP_CNT_CMP_NUM_ORD", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo;
            cmd.Parameters.Add("p_TIPO_COMPROBANTE_ID", OracleDbType.Int32).Value = value.TipoComprobanteId;
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            var pCantidad = cmd.Parameters.Add("p_CANTIDAD", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            var response = new CntComprobanteReorderResponse(CntDb.GetIntOutput(pCantidad));
            return new ResultDto<CntComprobanteReorderResponse>(response) { Data = isSuccess ? response : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<CntComprobanteReorderResponse>(null!) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}

public class AddCntDetalleHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntDetalleAddCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var detail = new CntDetalleCreateCommand(value.CodigoMayor, value.CodigoAuxiliar, value.Referencia1, value.Referencia2, value.Referencia3, value.Descripcion, value.Monto);
        return await CntDetalleWriteSupport.ExecuteDetailAsync(
            connectionDB,
            config,
            user,
            request,
            value.UsuarioId,
            CntSecurity.ComprobanteEdit,
            "CNT.SP_CNT_DET_INS",
            value.CodigoComprobante, null, value.Version,
            cmd =>
            {
                cmd.Parameters.Add("p_CODIGO_COMPROBANTE", OracleDbType.Int32).Value = value.CodigoComprobante;
                CntDetalleWriteSupport.AddDetailParams(cmd, detail);
            });
    }
}

public class UpdateCntDetalleHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(CntDetalleUpdateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var detail = new CntDetalleCreateCommand(value.CodigoMayor, value.CodigoAuxiliar, value.Referencia1, value.Referencia2, value.Referencia3, value.Descripcion, value.Monto);
        return await CntDetalleWriteSupport.ExecuteDetailAsync(
            connectionDB,
            config,
            user,
            request,
            value.UsuarioId,
            CntSecurity.ComprobanteEdit,
            "CNT.SP_CNT_DET_UPD",
            null, value.CodigoDetalleComprobante, value.Version,
            cmd =>
            {
                cmd.Parameters.Add("p_CODIGO_DETALLE", OracleDbType.Int32).Value = value.CodigoDetalleComprobante;
                CntDetalleWriteSupport.AddDetailParams(cmd, detail);
            });
    }
}

public class DeleteCntDetalleHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> HandleAsync(CntDetalleDeleteCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var result = await CntDetalleWriteSupport.ExecuteDetailAsync(connectionDB, config, user, request,
            value.UsuarioId, CntSecurity.ComprobanteEdit, "CNT.SP_CNT_DET_DEL", null, value.CodigoDetalleComprobante, value.Version,
            cmd => cmd.Parameters.Add("p_CODIGO_DETALLE", OracleDbType.Int32).Value = value.CodigoDetalleComprobante);
        return new ResultDto<string>(result.IsValid ? "OK" : "") { IsValid = result.IsValid, Message = result.Message };
    }
}

internal static class CntDetalleWriteSupport
{
    public static async Task<ResultDto<int>> ExecuteDetailAsync(
        ConnectionDB connectionDB,
        IConfiguration config,
        ClaimsPrincipal user,
        HttpRequest request,
        int usuarioId,
        string permissionName,
        string procedure,
        int? comprobante, int? detalle, int? version,
        Action<OracleCommand> configure)
    {
        try
        {
            var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, usuarioId);
            if (!userValidation.IsValid)
            {
                return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
            }

            var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, usuarioId, permissionName);
            if (!permission.IsValid)
            {
                return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = permission.Message };
            }

            if (!CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
            {
                return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
            }

            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            await CntPendienteDb.LockAsync(cn, tx);
            int id = comprobante ?? await CntPendienteDb.ResolveDetailAsync(cn, tx, detalle!.Value, empresa);
            var automaticPermission = await CntSecurity.CheckPermissionAsync(connectionDB, config, usuarioId, CntSecurity.ComprobanteEditAutomatic);
            await CntPendienteDb.CheckEditAsync(cn, tx, id, empresa, version, usuarioId, automaticPermission.IsValid, "DETALLE");
            using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
            configure(cmd);
            cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = usuarioId;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            var pCodigoOut = procedure == "CNT.SP_CNT_DET_DEL" ? null : cmd.Parameters.Add("p_CODIGO_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
            cmd.Parameters.Add("p_PERMITIR_AUTOMATICO", OracleDbType.Int32).Value = automaticPermission.IsValid ? 1 : 0;
            await cmd.ExecuteNonQueryAsync();

            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            if (isSuccess)
            {
                await CntPendienteDb.FinishAsync(cn, tx, id, empresa, usuarioId);
                tx.Commit();
            }
            return new ResultDto<int>((pCodigoOut is null ? id : CntDb.GetIntOutput(pCodigoOut))) { Data = isSuccess ? (pCodigoOut is null ? id : CntDb.GetIntOutput(pCodigoOut)) : 0, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0) { Data = 0, IsValid = false, Message = ex.Message };
        }
    }

    public static void AddDetailParams(OracleCommand cmd, CntDetalleCreateCommand detalle)
    {
        cmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = detalle.CodigoMayor;
        cmd.Parameters.Add("p_CODIGO_AUXILIAR", OracleDbType.Int32).Value = detalle.CodigoAuxiliar;
        cmd.Parameters.Add("p_REFERENCIA1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Referencia1);
        cmd.Parameters.Add("p_REFERENCIA2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Referencia2);
        cmd.Parameters.Add("p_REFERENCIA3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Referencia3);
        cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(detalle.Descripcion);
        cmd.Parameters.Add("p_MONTO", OracleDbType.Decimal).Value = CntMoney.Normalize(detalle.Monto);
    }
}
