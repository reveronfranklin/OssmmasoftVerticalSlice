using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

public record FacturacionElectronicaEmisorCreateCommand(
    string Rif,
    string RazonSocial,
    string DomicilioFiscal,
    string Correo,
    string UsuarioIns,

    // D-31. Por defecto 'ordinario', que es el conjunto MAS ESTRICTO: el Art. 13
    // tiene dieciseis numerales y el 15 es el del no ordinario. Un emisor sin
    // declarar queda sobre-validado, no sub-validado.
    string TipoContribuyente = "ordinario",

    // TM.4, D-54. Cupo de documentos con el que nace el emisor. null o 0 = sin
    // cupo, es decir sin limite, que es como funcionaban todos hasta ahora.
    int? CupoInicial = null);

public class FacturacionElectronicaEmisorCreateHandler(ConnectionDB _connectionDB)
{
    private static readonly string[] TiposContribuyenteValidos = ["ordinario", "formal", "no_sujeto"];

    public async Task<ResultDto<int>> HandleAsync(FacturacionElectronicaEmisorCreateCommand command)
    {
        // Nivel 1 - validacion previa. Sin TryGetEmpresa: el modulo no toca datos
        // de empresa. Un emisor es un cliente externo identificado por su RIF, no
        // el settings:EmpresaConfig de Ossmmasoft.
        //
        // Cada campo contra su columna real (VARCHAR(n) o el CHECK de
        // TIPO_CONTRIBUYENTE): sin esto, un valor vacio en USUARIO_INS o mas
        // largo que su columna llegaba intacto al INSERT, y el SQLSTATE de
        // Postgres se filtraba crudo por el catch generico (hallazgo de la
        // revision de seguridad previa al tramite del SENIAT).
        string? error =
            FacturacionElectronicaDb.ValidarTexto(command.Rif, "El RIF del emisor", 20)
            ?? FacturacionElectronicaDb.ValidarTexto(command.RazonSocial, "La razón social", 200)
            ?? FacturacionElectronicaDb.ValidarTexto(command.DomicilioFiscal, "El domicilio fiscal", 300)
            ?? FacturacionElectronicaDb.ValidarTexto(command.Correo, "El correo", 150, obligatorio: false)
            ?? FacturacionElectronicaDb.ValidarTexto(command.UsuarioIns, "El usuario", 50);

        if (error is not null)
        {
            return Falla(error);
        }

        // Fase E: el patron del RIF tambien en el backend, no solo en la pantalla.
        string rif = EmisorValidador.NormalizarRif(command.Rif);
        string? errorRif = EmisorValidador.ValidarRif(rif);

        if (errorRif is not null)
        {
            return Falla(errorRif);
        }

        string tipoContribuyente = string.IsNullOrWhiteSpace(command.TipoContribuyente)
            ? "ordinario"
            : command.TipoContribuyente.Trim();

        if (!TiposContribuyenteValidos.Contains(tipoContribuyente))
        {
            return Falla("El tipo de contribuyente debe ser ordinario, formal o no_sujeto.");
        }

        int cupoInicial = command.CupoInicial ?? 0;

        if (cupoInicial is < 0 or > FacturacionElectronicaDb.SecuencialMaximo)
        {
            return Falla($"El cupo inicial debe estar entre 1 y {EmisorCupoDb.CantidadMaximaTexto}, o vacío para no limitar.");
        }

        using var cn = _connectionDB.GetFedConnection();

        // Nivel 2 - apertura de conexion.
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        // Nivel 3 - ejecucion.
        try
        {
            // El emisor y su primer cupo van juntos o no va ninguno: un emisor
            // que nace sin el cupo pedido quedaria sin limite sin que nadie lo
            // note.
            using var tx = await cn.BeginTransactionAsync();

            object? id;

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlEmisorCreate, cn, tx))
            {
                cmd.Parameters.AddWithValue("rif", rif);
                cmd.Parameters.AddWithValue("razon_social", command.RazonSocial.Trim());
                cmd.Parameters.AddWithValue("domicilio_fiscal", command.DomicilioFiscal.Trim());
                cmd.Parameters.AddWithValue("correo", FacturacionElectronicaDb.DbValue(command.Correo));
                cmd.Parameters.AddWithValue("tipo_contribuyente", tipoContribuyente);
                cmd.Parameters.AddWithValue("estado", "activo");
                cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

                id = await cmd.ExecuteScalarAsync();
            }

            if (cupoInicial > 0)
            {
                // Consumido 0: el emisor recien creado todavia no recibio ningun
                // numero, y nadie mas lo ve hasta el commit.
                using var cmdCupo = new NpgsqlCommand(EmisorCupoDb.SqlInsertar, cn, tx);
                cmdCupo.Parameters.AddWithValue("emisor_id", Convert.ToInt64(id));
                cmdCupo.Parameters.AddWithValue("cantidad", cupoInicial);
                cmdCupo.Parameters.AddWithValue("consumido", 0L);
                cmdCupo.Parameters.AddWithValue("usuario_ins", command.UsuarioIns.Trim());

                await cmdCupo.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            return new ResultDto<int>(Convert.ToInt32(id))
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito
            };
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsRifDuplicado(ex))
        {
            // La defensa es el UNIQUE de la tabla, no un SELECT previo: entre la
            // consulta y el insert cabe otra peticion.
            return Falla($"Ya existe un emisor registrado con el RIF {rif}.");
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<int> Falla(string mensaje) =>
        new(0) { IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaEmisorCreateController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> Create(FacturacionElectronicaEmisorCreateCommand value)
    {
        var handler = new FacturacionElectronicaEmisorCreateHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
