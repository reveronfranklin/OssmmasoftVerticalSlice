using System.Threading.Channels;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

public record BmReplicaTabla(string Tabla, string Estado, int Total, int Copiados);
public record BmReplicaEstadoDto(string? EjecucionId, bool EnCurso, string Mensaje,
    DateTime? Inicio, DateTime? Fin, List<BmReplicaTabla> Tablas);

// Estado compartido por la pantalla, la ejecucion manual y el worker programado.
public class BmReplicaEstado
{
    private readonly object gate = new();
    private BmReplicaEstadoDto estado = new(null, false, "Sin ejecuciones", null, null, new());
    public Channel<bool> Solicitudes { get; } = Channel.CreateBounded<bool>(1);
    private static readonly string[] Tablas = ["BMC.BM_ARTICULOS", "BMC.BM_BIENES",
        "BMC.BM_MOV_BIENES", "BMC.BM_DIR_BIEN", "BMC.BM_CLASIFICACION_BIENES",
        "BMC.BM_DESCRIPTIVAS", "RHC.RH_PERSONAS"];

    public BmReplicaEstadoDto Leer()
    {
        lock (gate) return estado with { Tablas = estado.Tablas.ToList() };
    }

    public bool Iniciar()
    {
        lock (gate)
        {
            if (estado.EnCurso) return false;
            estado = new(Guid.NewGuid().ToString(), true, "Preparando replica", DateTime.UtcNow,
                null, Tablas.Select(t => new BmReplicaTabla(t, "Pendiente", 0, 0)).ToList());
            return true;
        }
    }

    public void Tabla(string nombre, string fase, int? total = null, int? copiados = null)
    {
        lock (gate)
        {
            estado = estado with { Mensaje = $"{fase}: {nombre}", Tablas = estado.Tablas.Select(t =>
                t.Tabla == nombre ? t with { Estado = fase, Total = total ?? t.Total,
                    Copiados = copiados ?? t.Copiados } : t).ToList() };
        }
    }

    public void Terminar(bool correcto, string mensaje)
    {
        lock (gate)
        {
            estado = estado with { EnCurso = false, Fin = DateTime.UtcNow, Mensaje = mensaje,
                Tablas = estado.Tablas.Select(t => t.Estado == "Confirmada" ? t :
                    t with { Estado = correcto ? "Confirmada" : "No confirmada" }).ToList() };
        }
    }

    public ResultDto<BmReplicaEstadoDto> Resultado(bool valido = true, string? mensaje = null) =>
        new(Leer()) { IsValid = valido, Message = mensaje ?? Leer().Mensaje };
}

public class BmReplicaManualWorker(IServiceScopeFactory scopes, BmReplicaEstado estado) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var solicitud in estado.Solicitudes.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BmReplicaConteoService>().ReplicarAsync(true);
            }
            catch (Exception ex)
            {
                estado.Terminar(false, $"Error tecnico: {ex.Message}");
            }
        }
    }
}
