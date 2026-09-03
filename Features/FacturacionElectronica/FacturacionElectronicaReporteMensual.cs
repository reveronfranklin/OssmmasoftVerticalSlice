using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Reporte mensual del Articulo 29.7 de la Providencia SNAT/2024/000102.
//
// AQUI SE SOSTIENE INV-2: nunca dejar de informar al SENIAT la totalidad de
// numeros de control asignados en un periodo mensual. Dos periodos omitidos en un
// ano calendario, consecutivos o no, bastan para la revocatoria, y el Art. 34.3
// no exige sancion previa.
//
// El plazo es de diez dias CONTINUOS siguientes al cierre de cada mes, y aplica
// "con independencia de no haber asignado ningun numero de control". Coincide con
// el Art. 12 de la Providencia SNAT/2018/0141, su norma supletoria.
//
// Estructura: servicio + worker en el mismo archivo, siguiendo el precedente de
// Features/BienesMunicipales/BmReplicaConteo.cs. El listado para la pantalla vive
// aparte, en su propio archivo de operacion, como manda el estandar.
public class FacturacionElectronicaReporteMensualService(ConnectionDB _connectionDB, IConfiguration _config, ILogger<FacturacionElectronicaReporteMensualService> _logger)
{
    // Modulo con el que se identifica la alerta en la cola de correo.
    private const string ModuloAlerta = "FED_REPORTE";

    public async Task<ResultDto<int>> EjecutarAsync()
    {
        using var cn = _connectionDB.GetFedConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        try
        {
            // 1. Las filas de periodo se crean POR ADELANTADO. Es lo que hace que
            //    un periodo omitido sea una fila visible y no una ausencia.
            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReporteAsegurarPeriodos, cn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Generar el reporte de cada periodo cuyo mes ya cerro.
            var periodos = new List<(long Id, string Periodo)>();

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReportePendientesACerrar, cn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    periodos.Add((reader.SafeGetInt64("id"), reader.SafeGetString("periodo")));
                }
            }

            int generados = 0;

            foreach (var (id, periodo) in periodos)
            {
                generados += await GenerarPeriodoAsync(cn, id, periodo) ? 1 : 0;
            }

            // 3. Vencer lo que paso el plazo sin transmitirse, y alertar. La
            //    consulta devuelve solo los que ACABAN de vencer, asi que la
            //    alerta suena una vez por periodo y no en cada tick.
            var vencidos = new List<string>();

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReporteMarcarVencidos, cn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    vencidos.Add(reader.SafeGetString("periodo"));
                }
            }

            foreach (string periodo in vencidos)
            {
                await AlertarVencimientoAsync(periodo);
            }

            return new ResultDto<int>(generados)
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito,
                CantidadRegistros = generados,
                Total1 = vencidos.Count
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    // Genera el reporte de un periodo: ata sus numeros, cuenta y marca.
    //
    // Se marca 'generado' y no 'enviado' a proposito (D-19). El Art. 29.7 dice que
    // la informacion se remite "en los terminos y condiciones que se establezca en
    // el Portal Fiscal", y ese canal todavia no existe de nuestro lado. Marcar
    // 'enviado' seria escribir una constancia falsa en la tabla con la que
    // precisamente se prueba que se reporto.
    private async Task<bool> GenerarPeriodoAsync(NpgsqlConnection cn, long id, string periodo)
    {
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReporteAtarNumeros, cn, tx))
            {
                cmd.Parameters.AddWithValue("reporte_id", id);
                cmd.Parameters.AddWithValue("periodo", periodo);
                await cmd.ExecuteNonQueryAsync();
            }

            int cantidad;

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReporteContarPeriodo, cn, tx))
            {
                cmd.Parameters.AddWithValue("periodo", periodo);
                cantidad = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReporteMarcarGenerado, cn, tx))
            {
                cmd.Parameters.AddWithValue("cantidad", cantidad);
                cmd.Parameters.AddWithValue("id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            // Cero no es una anomalia: es el reporte que el Art. 29.7 obliga a
            // hacer igual, y por eso se registra con el mismo enfasis.
            _logger.LogInformation(
                "Reporte del periodo {Periodo} generado con {Cantidad} numeros de control asignados.", periodo, cantidad);

            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Fallo la generacion del reporte del periodo {Periodo}.", periodo);

            // El fallo tiene que quedar en la fila, no solo en el log: un error
            // repetido que nadie ve es como no tener el dato.
            try
            {
                using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReporteRegistrarError, cn);
                cmd.Parameters.AddWithValue("error", ex.Message.Length > 500 ? ex.Message[..500] : ex.Message);
                cmd.Parameters.AddWithValue("id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception exRegistro)
            {
                _logger.LogError(exRegistro, "Tampoco se pudo registrar el error en el periodo {Periodo}.", periodo);
            }

            return false;
        }
    }

    // Alerta ante no ejecucion: la defensa contra INV-2.
    //
    // El canal principal NO es este: es el estado 'vencido', que la pantalla de
    // control muestra en rojo y que no depende de ninguna infraestructura. El
    // correo es el segundo canal, y puede fallar sin que la alerta se pierda.
    //
    // ADVERTENCIA CONOCIDA: settings:EmailWorkerEnabled esta en false en los dos
    // appsettings, y el worker de correo lee esa bandera una sola vez al arrancar.
    // Mientras siga asi, lo que se encole aqui queda en la cola sin salir.
    // Habilitarlo afecta a todo el ERP, no solo a este modulo.
    private async Task AlertarVencimientoAsync(string periodo)
    {
        string destino = _config["settings:FedAlertaCorreo"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(destino))
        {
            _logger.LogWarning(
                "Periodo {Periodo} VENCIDO sin reportar al SENIAT. No se envio correo porque settings:FedAlertaCorreo no esta configurado.", periodo);

            return;
        }

        try
        {
            using var cn = _connectionDB.GetSisConnection();
            await cn.OpenAsync();

            using var cmd = new OracleCommand("SIS.SP_EMAIL_Q_INS", cn)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            string asunto = $"FED: el periodo {periodo} vencio sin reportarse al SENIAT";
            string cuerpo =
                $"<p>El periodo <b>{periodo}</b> paso su plazo de diez dias continuos sin que el reporte del " +
                "Articulo 32 se transmitiera al SENIAT.</p>" +
                "<p>El Articulo 34.3 establece que <b>dos periodos omitidos en un ano calendario</b>, consecutivos " +
                "o no, son causal de revocatoria de la autorizacion como imprenta digital, sin que se requiera " +
                "sancion previa.</p>";

            cmd.Parameters.Add("p_MODULO_ORIGEN", OracleDbType.Varchar2).Value = ModuloAlerta;
            cmd.Parameters.Add("p_REFERENCIA_ID", OracleDbType.Int32).Value = DBNull.Value;
            cmd.Parameters.Add("p_TO_EMAIL", OracleDbType.Varchar2).Value = destino;
            cmd.Parameters.Add("p_TO_NAME", OracleDbType.Varchar2).Value = "Operador de la imprenta digital";
            cmd.Parameters.Add("p_SUBJECT", OracleDbType.Varchar2).Value = asunto;
            cmd.Parameters.Add("p_BODY_HTML", OracleDbType.Clob).Value = cuerpo;
            cmd.Parameters.Add("p_BODY_TEXT", OracleDbType.Clob).Value = DBNull.Value;
            cmd.Parameters.Add("p_FECHA_PROGRAMADA", OracleDbType.Date).Value = DBNull.Value;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = LeerEmpresa();
            cmd.Parameters.Add("p_EMAIL_ID_OUT", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            _logger.LogWarning("Periodo {Periodo} VENCIDO sin reportar. Alerta encolada para {Destino}.", periodo, destino);
        }
        catch (Exception ex)
        {
            // Nunca se deja caer el job por el canal secundario. El estado
            // 'vencido' ya quedo escrito, que es lo que de verdad sostiene INV-2.
            _logger.LogError(ex, "Periodo {Periodo} VENCIDO sin reportar. Ademas fallo el encolado del correo.", periodo);
        }
    }

    private int LeerEmpresa() =>
        int.TryParse(_config["settings:EmpresaConfig"], out int empresa) ? empresa : 0;

    private static ResultDto<int> Falla(string mensaje) =>
        new(0) { IsValid = false, Message = mensaje };
}

// Worker del reporte mensual.
//
// POR QUE TICKEA CADA HORA Y NO CADA MES (decision D-18). Un temporizador mensual
// dentro del proceso no sobrevive a un reciclado del app pool: al reiniciarse la
// cuenta vuelve a empezar y la ejecucion se pierde sin dejar rastro, que es
// exactamente lo que INV-2 prohibe. Tickeando seguido y preguntando "hay algun
// periodo vencido", el reinicio deja de importar.
//
// La idempotencia no la da el reloj: la da el UNIQUE (periodo) de la tabla y la
// fila creada por adelantado. Dos ticks seguidos no producen dos reportes.
//
// Sigue el patron de BmReplicaConteoWorker: PeriodicTimer, la bandera releida en
// cada tick -para poder apagarlo sin reiniciar el ERP- y try/catch por iteracion
// que loguea sin matar el worker.
public class FacturacionElectronicaReporteMensualWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<FacturacionElectronicaReporteMensualWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int minutos = Math.Max(1, config.GetValue<int?>("settings:FedReporteIntervaloMinutos") ?? 60);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutos));

        logger.LogInformation("Worker del reporte mensual FED iniciado. Intervalo: {Minutos} minutos.", minutos);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            string? habilitado = config["settings:FedReporteHabilitado"];

            if (!string.Equals(habilitado, "1", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Reporte mensual FED omitido: settings:FedReporteHabilitado esta deshabilitado.");

                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<FacturacionElectronicaReporteMensualService>();
                var resultado = await service.EjecutarAsync();

                if (resultado.IsValid)
                {
                    logger.LogInformation(
                        "Reporte mensual FED: {Generados} periodo(s) generado(s), {Vencidos} vencido(s).",
                        resultado.CantidadRegistros, resultado.Total1);
                }
                else
                {
                    logger.LogError("Reporte mensual FED fallo: {Message}", resultado.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error no controlado en el worker del reporte mensual FED.");
            }
        }
    }
}

// Ejecucion manual del ciclo, para el operador de la imprenta y para poder
// verificar la fase sin esperar a que el worker tickee.
[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaReporteMensualEjecutarController(
    ConnectionDB _connectionDB,
    IConfiguration _config,
    ILogger<FacturacionElectronicaReporteMensualService> _logger) : ControllerBase
{
    [HttpPost]
    [Route("reporteMensualEjecutar")]
    public async Task<IActionResult> Ejecutar()
    {
        var service = new FacturacionElectronicaReporteMensualService(_connectionDB, _config, _logger);
        var result = await service.EjecutarAsync();

        return Ok(result);
    }
}
