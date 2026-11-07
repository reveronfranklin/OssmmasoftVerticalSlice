using System.Text.Json;

namespace OssmmasoftVerticalSlice.Features.DatabaseDeployment;

public sealed class DeploymentStore(IConfiguration config,IWebHostEnvironment env)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly DateTime started=DateTime.UtcNow;
    private string DirectoryPath()
    {
        var value=DeploymentOptions.Read(config).StoragePath;
        if(string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) throw new InvalidOperationException("Configure DatabaseDeployment:StoragePath con una ruta privada absoluta fuera de la aplicacion.");
        var path=ResolveLinks(value);var content=ResolveLinks(env.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!string.IsNullOrWhiteSpace(env.WebRootPath))
        {
            var web=ResolveLinks(env.WebRootPath).TrimEnd(Path.DirectorySeparatorChar);
            if(path.Equals(web,StringComparison.OrdinalIgnoreCase) || path.StartsWith(web+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("El historial no puede almacenarse en archivos publicos.");
        }
        if(path.StartsWith(content,StringComparison.OrdinalIgnoreCase) || path.Equals(content.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El historial debe almacenarse fuera de la aplicacion y de sus archivos publicos.");
        Directory.CreateDirectory(path);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        return path;
    }
    private static string ResolveLinks(string value,int depth=0)
    {
        if(depth>32)throw new InvalidOperationException("Ruta privada con demasiados enlaces simbolicos.");
        var full=Path.GetFullPath(value);var root=Path.GetPathRoot(full)!;var current=root;
        foreach(var segment in full[root.Length..].Split(Path.DirectorySeparatorChar,StringSplitOptions.RemoveEmptyEntries))
        {
            current=Path.Combine(current,segment);
            if(Directory.Exists(current) && new DirectoryInfo(current).ResolveLinkTarget(true) is { } target)current=ResolveLinks(target.FullName,depth+1);
        }
        return current;
    }
    private string FilePath(string id)
    {
        if(!DeploymentRules.ValidId(id))throw new InvalidOperationException("Identificador invalido.");
        return Path.Combine(DirectoryPath(),id+".json");
    }
    public async Task<DeploymentRecord?> Read(string id,CancellationToken ct=default)
    {
        var path=FilePath(id);if(!File.Exists(path))return null;
        using var stream=File.OpenRead(path);return await JsonSerializer.DeserializeAsync<DeploymentRecord>(stream,cancellationToken:ct);
    }
    private async Task Write(DeploymentRecord record,CancellationToken ct)
    {
        var path=FilePath(record.Id);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            var settings=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write};
            if(!OperatingSystem.IsWindows())settings.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            await using(var stream=new FileStream(temp,settings)) await JsonSerializer.SerializeAsync(stream,record,cancellationToken:ct);
            File.Move(temp,path,true);
        }
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
    public async Task<(DeploymentRecord Record,bool Created)> Create(DeploymentRecord record,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var existing=await Read(record.Id,ct);
            if(existing is not null)
            {
                if(existing.Actor!=record.Actor || existing.RequestHash!=record.RequestHash)throw new InvalidOperationException("La solicitud ya existe con otro contenido o propietario.");
                return(existing,false);
            }
            var active=(await List(record.Actor,ct)).Count(x=>x.Status is "EN_COLA" or "COMPARANDO");
            if(active>=2)throw new InvalidOperationException("Espere a que finalicen sus comparaciones activas.");
            await Write(record,ct);return(record,true);
        }
        finally{gate.Release();}
    }
    public async Task<DeploymentPlan> Prepare(string id,int actor,List<string> selected,string version)
    {
        await gate.WaitAsync();
        try
        {
            var record=await Read(id)??throw new InvalidOperationException("Comparacion no encontrada.");
            if(record.Actor!=actor)throw new InvalidOperationException("Comparacion no autorizada.");
            var plan=DeploymentPlanBuilder.Build(record,selected,version);
            var existing=record.Plans.FirstOrDefault(x=>x.Hash==plan.Hash);
            if(existing is not null)return existing;
            if(record.Plans.Count>=10)throw new InvalidOperationException("Limite de diez planes por comparacion. Inicie otra comparacion.");
            record.Plans.Add(plan);await Write(record,CancellationToken.None);return plan;
        }
        finally{gate.Release();}
    }
    public async Task Save(DeploymentRecord record,CancellationToken ct=default)
    { await gate.WaitAsync(ct);try{await Write(record,ct);}finally{gate.Release();} }
    public async Task<List<DeploymentRecord>> List(int actor,CancellationToken ct=default)
    {
        var result=new List<DeploymentRecord>();
        foreach(var path in Directory.EnumerateFiles(DirectoryPath(),"*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(500))
        {
            ct.ThrowIfCancellationRequested();using var file=File.OpenRead(path);
            var value=await JsonSerializer.DeserializeAsync<DeploymentRecord>(file,cancellationToken:ct);
            if(value?.Actor==actor)result.Add(value);
        }
        return result.OrderByDescending(x=>x.CreatedAt).Take(30).ToList();
    }
    public async Task Recover(CancellationToken ct)
    {
        // Do not create storage or connect anywhere when the module is disabled.
        if(!DeploymentOptions.Read(config).Enabled)return;
        foreach(var path in Directory.EnumerateFiles(DirectoryPath(),"*.json"))
        {
            DeploymentRecord? value;
            using(var file=File.OpenRead(path))value=await JsonSerializer.DeserializeAsync<DeploymentRecord>(file,cancellationToken:ct);
            if(value is not null && value.CreatedAt < started && value.Status is ("EN_COLA" or "COMPARANDO")) {value.Status="INTERRUMPIDO";value.Message="Proceso interrumpido. Inicie otra comparacion; no se ejecutaron cambios.";await Save(value,ct);}
        }
    }
}
