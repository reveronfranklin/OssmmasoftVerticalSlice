using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.RhReciboPago;
using OssmmasoftVerticalSlice.Features.Support;

namespace OssmmasoftVerticalSlice.Features.Email;

public class EmailQueueWorker(IServiceScopeFactory scopes, IConfiguration config, IEmailManualSender sender,
    ILogger<EmailQueueWorker> logger, IWebHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = EmailSettings.Read(config);
                if (settings.Enabled && settings.Validate() is null && SupportDb.TryGetEmpresa(config,out var empresa,out _))
                {
                    using var scope = scopes.CreateScope();
                    using var cn = scope.ServiceProvider.GetRequiredService<ConnectionDB>().GetSisConnection();
                    await cn.OpenAsync(stoppingToken);
                    var receipts = config.GetValue("settings:RhReciboEmail:Enabled", false);
                    if (receipts)
                    {
                        try { await RhReciboEmailProcessor.Tick(scope.ServiceProvider.GetRequiredService<ConnectionDB>(),config,environment,empresa,settings,stoppingToken); }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("No se pudo preparar/reconciliar recibos ({Type}).",ex.GetType().Name); }
                    }
                    // R53 shares the limiter and transport with R52; other legacy origins remain excluded.
                    var email = receipts
                        ? await EmailReciboDb.Claim(cn,empresa,Math.Clamp(config.GetValue("settings:RhReciboEmail:MaxDailyMessages",200),1,10000),Math.Clamp(config.GetValue("settings:RhReciboEmail:IntervalSeconds",10),10,3600),stoppingToken)
                        : await EmailManualDb.Claim(cn,empresa,stoppingToken);
                    if (email is not null)
                    {
                        var outcome = await sender.SendAsync(settings,email,stoppingToken);
                        if (receipts) await EmailReciboDb.Finish(cn,email,outcome,stoppingToken);
                        else await EmailManualDb.Finish(cn,email,outcome,stoppingToken);
                        logger.LogInformation("Correo {Id}: {State}.",email.EmailId,outcome.Estado);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("No se pudo procesar la cola manual ({Type}).",ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
