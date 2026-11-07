using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;

namespace OssmmasoftVerticalSlice.Features.Email;

public class EmailQueueWorker(IServiceScopeFactory scopes, IConfiguration config, IEmailManualSender sender,
    ILogger<EmailQueueWorker> logger) : BackgroundService
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
                    // Exclusively manual R52 requests. Never consume the old support/backlog queue.
                    var email = await EmailManualDb.Claim(cn,empresa,stoppingToken);
                    if (email is not null)
                    {
                        var outcome = await sender.SendAsync(settings,email,stoppingToken);
                        await EmailManualDb.Finish(cn,email,outcome,stoppingToken);
                        logger.LogInformation("Correo manual {Id}: {State}.",email.EmailId,outcome.Estado);
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
