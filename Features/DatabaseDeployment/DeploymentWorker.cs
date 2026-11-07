using System.Threading.Channels;
using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.DatabaseDeployment;

public sealed class DeploymentQueue
{
    private readonly Channel<string> channel=Channel.CreateBounded<string>(new BoundedChannelOptions(20){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
    public bool Enqueue(string id)=>channel.Writer.TryWrite(id);
    public IAsyncEnumerable<string> Read(CancellationToken ct)=>channel.Reader.ReadAllAsync(ct);
}
public class DeploymentWorker(DeploymentQueue queue,DeploymentStore store,DeploymentMetadata metadata,IConfiguration config,ILogger<DeploymentWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try{await store.Recover(stoppingToken);}catch(Exception ex){logger.LogWarning("No se pudo recuperar el historial de comparaciones ({Type}).",ex.GetType().Name);}
        await foreach(var id in queue.Read(stoppingToken))
        {
            DeploymentRecord? record=null;
            try
            {
                record=await store.Read(id,stoppingToken);if(record is null)continue;
                var options=DeploymentOptions.Read(config);
                if(!options.Enabled)throw new InvalidOperationException("Comparacion deshabilitada.");
                foreach(var schema in record.Schemas)
                {
                    if(string.IsNullOrWhiteSpace(options.Pruebas.Expected(schema)) || string.IsNullOrWhiteSpace(options.ProduccionLectura.Expected(schema)))
                        throw new InvalidOperationException("Compruebe las conexiones y configure los ExpectedDatabaseId antes de comparar.");
                    if(options.Pruebas.Expected(schema)==options.ProduccionLectura.Expected(schema))
                        throw new InvalidOperationException("Pruebas y produccion no pueden ser la misma base para el mismo esquema.");
                }
                record.Status="COMPARANDO";record.Message="Consultando exclusivamente metadatos.";await store.Save(record,stoppingToken);
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);deadline.CancelAfter(TimeSpan.FromMinutes(20));
                foreach(var schema in record.Schemas)
                {
                    var source=await metadata.Capture(schema,true,record.Types,deadline.Token);
                    var target=await metadata.Capture(schema,false,record.Types,deadline.Token);
                    if(source.Identity.DatabaseId==target.Identity.DatabaseId)throw new InvalidOperationException("La comparacion apunta a la misma base y esquema.");
                    record.SourceIdentities.Add(source.Identity);record.TargetIdentities.Add(target.Identity);
                    record.Source.AddRange(source.Objects);record.Target.AddRange(target.Objects);
                    if(record.Source.Sum(x=>x.Ddl.Length)+record.Target.Sum(x=>x.Ddl.Length)>64000000)throw new InvalidOperationException("La comparacion es demasiado grande. Seleccione menos tipos o esquemas.");
                }
                record.Differences=DeploymentRules.Compare(record.Source,record.Target);
                record.Status="COMPLETO";record.Message="Comparacion finalizada. No se ejecutaron cambios en las bases.";
                await store.Save(record,stoppingToken);
            }
            catch(Exception ex)
            {
                logger.LogWarning("Comparacion {Id} no completada ({Type}).",id,ex.GetType().Name);
                if(record is not null)
                {
                    record.Status=stoppingToken.IsCancellationRequested?"INTERRUMPIDO":"ERROR";
                    record.Message=ex switch {OracleException oracle=>$"No se pudieron consultar metadatos (ORA-{oracle.Number:D5}). Revise permisos y conectividad.",InvalidOperationException validation=>validation.Message,OperationCanceledException=>"Comparacion interrumpida o tiempo maximo agotado.",_=>"No se pudo guardar o completar la comparacion. Revise el almacenamiento privado y la configuracion."};
                    record.Differences=[];
                    try{await store.Save(record,CancellationToken.None);}catch{logger.LogWarning("No se pudo guardar el resultado de {Id}.",id);}
                }
            }
        }
    }
}
