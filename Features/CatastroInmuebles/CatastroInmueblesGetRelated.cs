using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroInmuebles;

public record CatastroInmueblesGetRelatedQuery(long CodigoInmueble);

public class CatastroInmueblesGetRelatedHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<CatastroInmuebleRelacionadosResponse>> HandleAsync(CatastroInmueblesGetRelatedQuery query)
    {
        if (query.CodigoInmueble <= 0)
        {
            return Failure("El código del inmueble debe ser mayor que cero.");
        }

        if (!CatastroInmueblesDb.TryGetEmpresa(config, out var empresa, out var empresaError))
        {
            return Failure(empresaError);
        }

        using var cn = connectionDB.GetCatConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Failure($"Error técnico al abrir conexión CAT: {ex.Message}");
        }

        using var cmd = new OracleCommand("CAT.SP_CAT_INM_GET_REL", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };
        cmd.Parameters.Add("p_CodigoInmueble", OracleDbType.Int64).Value = query.CodigoInmueble;
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Caracteristicas", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Documentos", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Folios", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_OtrosDatos", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Roles", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Usos", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Multiusos", OracleDbType.RefCursor, ParameterDirection.Output);
        var message = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            var caracteristicas = new List<CatastroCaracteristicaResponse>();
            var documentos = new List<CatastroDocumentoLegalResponse>();
            var folios = new List<CatastroFolioRealResponse>();
            var otros = new List<CatastroOtroDatoResponse>();
            var roles = new List<CatastroRolResponse>();
            var usos = new List<CatastroUsoZonaResponse>();
            var multiusos = new List<CatastroMultiusoResponse>();

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    caracteristicas.Add(new(CatastroInmueblesDb.GetInt64(reader, "CODIGO_PADRE"), CatastroInmueblesDb.GetInt64(reader, "CODIGO_DESC"), CatastroInmueblesDb.GetString(reader, "CODIGO_CHE")));

                if (await reader.NextResultAsync())
                    while (await reader.ReadAsync())
                        documentos.Add(new(CatastroInmueblesDb.GetInt64(reader, "CODIGO_DOCUMENTOS_LEGALES"), CatastroInmueblesDb.GetInt64(reader, "DOCUMENTO_NUMERO"), CatastroInmueblesDb.GetInt64(reader, "FOLIO_NUMERO"), CatastroInmueblesDb.GetInt64(reader, "TOMO_NUMERO"), CatastroInmueblesDb.GetInt64(reader, "PROF_NUMERO"), CatastroInmueblesDb.GetDateTime(reader, "FECHA_REGISTRO"), CatastroInmueblesDb.GetDecimal(reader, "AREA_TERRENO"), CatastroInmueblesDb.GetDecimal(reader, "PRECIO_TERRENO")));

                if (await reader.NextResultAsync())
                    while (await reader.ReadAsync())
                        folios.Add(new(CatastroInmueblesDb.GetString(reader, "OFICINA_REG"), CatastroInmueblesDb.GetInt64(reader, "ESTADO"), CatastroInmueblesDb.GetInt64(reader, "MUNICIPIO"), CatastroInmueblesDb.GetInt64(reader, "PARROQUIA"), CatastroInmueblesDb.GetInt64(reader, "NUMERO_INSCRIPCION"), CatastroInmueblesDb.GetDateTime(reader, "FECHA"), CatastroInmueblesDb.GetDecimal(reader, "VALOR_ADQUISICION"), CatastroInmueblesDb.GetString(reader, "ASIENTOREGISTRAL")));

                if (await reader.NextResultAsync())
                    while (await reader.ReadAsync())
                        otros.Add(new(CatastroInmueblesDb.GetString(reader, "NUMERO_TRAMITE"), CatastroInmueblesDb.GetDateTime(reader, "FECHA_RECEPCION"), CatastroInmueblesDb.GetString(reader, "ELABORADO_POR"), CatastroInmueblesDb.GetDateTime(reader, "FECHA_ELABORACION"), CatastroInmueblesDb.GetInt64(reader, "TIPO_TRAMITE")));

                if (await reader.NextResultAsync())
                    while (await reader.ReadAsync())
                        roles.Add(new(Convert.ToInt64(reader["CODIGO_ROL"]), CatastroInmueblesDb.GetInt64(reader, "CODIGO_CONTACTO"), CatastroInmueblesDb.GetInt64(reader, "ROL_ID"), CatastroInmueblesDb.GetDateTime(reader, "FECHA_INI"), CatastroInmueblesDb.GetDateTime(reader, "FECHA_FIN"), CatastroInmueblesDb.GetInt64(reader, "CODIGO_CONTRIBUYENTE"), CatastroInmueblesDb.GetInt64(reader, "NACIONALIDAD_ID")));

                if (await reader.NextResultAsync())
                    while (await reader.ReadAsync())
                        usos.Add(new(CatastroInmueblesDb.GetInt64(reader, "CODIGO_USO"), CatastroInmueblesDb.GetString(reader, "ZONIFICACION"), string.Equals(CatastroInmueblesDb.GetString(reader, "PRINCIPAL"), "S", StringComparison.OrdinalIgnoreCase) || CatastroInmueblesDb.GetString(reader, "PRINCIPAL") == "1", CatastroInmueblesDb.GetDateTime(reader, "FECHA_INS")));

                if (await reader.NextResultAsync())
                    while (await reader.ReadAsync())
                        multiusos.Add(new(Convert.ToInt64(reader["CODIGO_DIRECCION"]), Convert.ToInt64(reader["TIPO"]), Convert.ToInt64(reader["TIPO_UNIDAD"]), Convert.ToDecimal(reader["METROS"])));
            }

            var dbMessage = CatastroInmueblesDb.GetMessage(message);
            var isValid = CatastroInmueblesDb.IsSuccessMessage(dbMessage);
            var data = new CatastroInmuebleRelacionadosResponse(caracteristicas, documentos, folios, otros, roles, usos, multiusos);
            return new ResultDto<CatastroInmuebleRelacionadosResponse>(isValid ? data : null!) { IsValid = isValid, Message = dbMessage };
        }
        catch (Exception ex)
        {
            return Failure($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<CatastroInmuebleRelacionadosResponse> Failure(string message) =>
        new(null!) { IsValid = false, Message = message };
}

[ApiController]
[Authorize]
[Route("api/CatastroInmuebles")]
public class CatastroInmueblesGetRelatedController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("getRelated")]
    public async Task<IActionResult> GetRelated(CatastroInmueblesGetRelatedQuery query)
    {
        var result = await new CatastroInmueblesGetRelatedHandler(connectionDB, config).HandleAsync(query);
        return Ok(result);
    }
}
