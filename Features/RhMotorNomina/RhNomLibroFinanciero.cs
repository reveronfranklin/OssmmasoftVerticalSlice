using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Immutable facts supplied by a server transaction adapter, never HTTP input.
// Validating this document does not prove that DML occurred in the same transaction.
public record RhNomLibroDocumento(string Version,int CodigoEmpresa,int CodigoTipoNomina,int CodigoPersona,
    int CodigoPeriodo,DateTime FechaNomina,int CodigoPresupuesto,int Actor,int IdPlan,string HashPlan,
    string Accion,int? IdRevertido,List<RhNomCambioAplicado> Cambios,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? Motivo=null);
public record RhNomLibroEvento(int IdEvento,int? IdAnterior,string? HashAnterior,DateTime FechaBd,
    string HashDocumento,string HashEvento,RhNomLibroDocumento Documento);
public record RhNomLibroEstado(List<RhNomPropiedadMovimiento> Propiedades,int? UltimoEvento,string? UltimoHash,int Eventos)
{
    public string VersionMetodo=>RhNomLibroFinanciero.Version;
    public bool DatosRevalidadosEnTransaccion=>false;
    public bool AplicacionDisponible=>false;
    public bool ParidadCertificada=>false;
}
public static class RhNomLibroFinanciero
{
    public const string Version="LIBRO_FINANCIERO_V1";
    public const int MaxEventos=200,MaxCambios=100;
    static bool Hex(string? s)=>s is not null&&Regex.IsMatch(s,@"\A[A-F0-9]{64}\z");
    static int Id(RhNomCambioAplicado c)=>(c.Despues??c.Antes)!.Id;
    static bool Scope(RhNomImagenMovimiento a,RhNomLibroDocumento d)=>a.CodigoEmpresa==d.CodigoEmpresa&&a.CodigoTipoNomina==d.CodigoTipoNomina&&a.CodigoPersona==d.CodigoPersona;
    static bool MismaClave(RhNomImagenMovimiento a,RhNomImagenMovimiento b)=>a.CodigoEmpresa==b.CodigoEmpresa&&a.CodigoTipoNomina==b.CodigoTipoNomina&&a.CodigoPersona==b.CodigoPersona&&a.CodigoConcepto==b.CodigoConcepto&&a.FrecuenciaId==b.FrecuenciaId&&a.Tipo==b.Tipo&&a.Complemento==b.Complemento;
    public static string Hash(RhNomLibroDocumento d)=>RhNomRecalculosHistoricos.Hash(d);
    public static string Huella(RhNomLibroEvento e)=>RhNomRecalculosHistoricos.Hash(new{e.IdEvento,e.IdAnterior,e.HashAnterior,e.FechaBd,e.HashDocumento});
    public static RhNomLibroEstado Reconstruir(IReadOnlyList<RhNomLibroEvento> events,int company,int type,int person,CancellationToken ct=default)
    {
        if(company<=0||type<=0||person<=0||events is null||events.Count>MaxEventos||events.Any(e=>e is null||e.Documento is null))
            throw new RhNomError("LIBRO_CONTEXTO_INVALIDO","Contexto positivo y hasta 200 eventos completos requeridos; no se trunca el libro.");
        var owners=new Dictionary<int,RhNomPropiedadMovimiento>();var seen=new Dictionary<int,RhNomLibroEvento>();var reverted=new HashSet<int>();var active=new Stack<int>();var identities=new HashSet<int>();
        int? previous=null;string? previousHash=null;
        foreach(var e in events)
        {
            ct.ThrowIfCancellationRequested();var d=e.Documento;
            if(e.IdEvento<=0||previous is not null&&e.IdEvento<=previous||!seen.TryAdd(e.IdEvento,e)||e.IdAnterior!=previous||e.HashAnterior!=previousHash||!Hex(e.HashDocumento)||Hash(d)!=e.HashDocumento||!Hex(e.HashEvento)||Huella(e)!=e.HashEvento)
                throw new RhNomError("LIBRO_CADENA_ALTERADA","Cadena financiera incompleta, bifurcada, desordenada o alterada.");
            if(d.Version!=Version||d.CodigoEmpresa!=company||d.CodigoTipoNomina!=type||d.CodigoPersona!=person||d.CodigoPeriodo<=0||d.CodigoPresupuesto<=0||d.Actor<=0||d.IdPlan<=0||!Hex(d.HashPlan)
                ||d.FechaNomina.Kind!=DateTimeKind.Unspecified||d.FechaNomina.Year<1900||e.FechaBd.Kind!=DateTimeKind.Unspecified||e.FechaBd.Year<1900
                ||d.Accion is not ("APLICACION" or "REVERSION")||d.Cambios is null||d.Cambios.Count is <1 or >MaxCambios||d.Cambios.Any(c=>c is null))
                throw new RhNomError("LIBRO_DOCUMENTO_INVALIDO","Evento fuera de alcance, version desconocida o cambios invalidos.");
            foreach(var c in d.Cambios)
            {
                if(c.Accion=="CREAR"?(c.Antes is not null||c.Despues is null):c.Accion=="ACTUALIZAR"?(c.Antes is null||c.Despues is null):c.Accion=="RETIRAR"?(c.Antes is null||c.Despues is not null):true)
                    throw new RhNomError("LIBRO_CAMBIO_INVALIDO","Imagenes incompatibles con la accion financiera.");
                foreach(var image in new[]{c.Antes,c.Despues}.OfType<RhNomImagenMovimiento>())
                    if(!Scope(image,d)||RhNomPlanesFinancieros.Hash(image).Length!=64)throw new RhNomError("LIBRO_IMAGEN_AJENA","Imagen ajena al contexto financiero.");
                if(c.Antes is {} a&&c.Despues is {} b&&(a.Id!=b.Id||!MismaClave(a,b)))throw new RhNomError("LIBRO_IDENTIDAD_CAMBIADA","Una actualizacion no cambia identidad ni clave de negocio.");
            }
            if(d.Cambios.Select(Id).Distinct().Count()!=d.Cambios.Count)throw new RhNomError("LIBRO_MOV_REPETIDO","Movimiento repetido en el evento financiero.");
            RhNomLibroEvento? original=null;
            if(d.Accion=="REVERSION")
            {
                if(d.IdRevertido is not {} target||(active.Count==0||target!=active.Peek())||!seen.TryGetValue(target,out original)||original.Documento.Accion!="APLICACION"||!reverted.Add(target)
                    ||d.IdPlan!=original.Documento.IdPlan||d.HashPlan!=original.Documento.HashPlan||d.CodigoPeriodo!=original.Documento.CodigoPeriodo||d.FechaNomina!=original.Documento.FechaNomina||d.CodigoPresupuesto!=original.Documento.CodigoPresupuesto)
                    throw new RhNomError("LIBRO_REVERSION_INVALIDA","Solo puede revertirse la ultima aplicacion activa del mismo plan/contexto; no se permite doble reversion.");
                if(d.Cambios.Count!=original.Documento.Cambios.Count)throw new RhNomError("LIBRO_REVERSION_PARCIAL","La reversion debe cubrir todos los cambios de la aplicacion.");
            }
            else if(d.IdRevertido is not null)throw new RhNomError("LIBRO_ORIGEN_INVALIDO","Una aplicacion no declara un evento revertido.");
            foreach(var c in d.Cambios)
            {
                var id=Id(c);owners.TryGetValue(id,out var owner);
                if(c.Antes is {} before&&(owner is null||owner.HashUltimaImagen!=RhNomPlanesFinancieros.Hash(before)))
                    throw new RhNomError("LIBRO_MOV_NO_PROPIO","No se adopta ni modifica un movimiento sin propiedad y ultima imagen coincidente.");
                if(c.Antes is null&&d.Accion=="APLICACION"&&!identities.Add(id))throw new RhNomError("LIBRO_ID_REUTILIZADO","No se reutiliza una identidad retirada o revertida para otra creacion.");
                if(c.Antes is null&&owner is not null)throw new RhNomError("LIBRO_IDENTIDAD_OCUPADA","Una creacion no reemplaza un movimiento propio existente.");
                if(original is not null)
                {
                    var old=original.Documento.Cambios.SingleOrDefault(x=>Id(x)==id)??throw new RhNomError("LIBRO_REVERSION_AJENA","La reversion incluye un movimiento ajeno a la aplicacion.");
                    if(old.Antes is null){if(c.Accion!="RETIRAR"||!DatosRestaurados(c.Antes!,old.Despues!))throw new RhNomError("LIBRO_INVERSO_INVALIDO","Inverso de creacion inconsistente.");}
                    else if(old.Despues is null){if(c.Accion!="CREAR"||!DatosRestaurados(c.Despues!,old.Antes))throw new RhNomError("LIBRO_INVERSO_INVALIDO","Reinsercion de imagen retirada inconsistente.");}
                    else if(c.Accion!="ACTUALIZAR"||!DatosRestaurados(c.Antes!,old.Despues!)||!DatosRestaurados(c.Despues!,old.Antes))throw new RhNomError("LIBRO_INVERSO_INVALIDO","Restauracion de imagen anterior inconsistente.");
                }
                if(d.Accion=="APLICACION"&&c.Antes is {} originalBefore&&c.Despues is {} updated
                    &&updated with{Monto=originalBefore.Monto,UsuarioUpd=originalBefore.UsuarioUpd,FechaUpd=originalBefore.FechaUpd}!=originalBefore)
                    throw new RhNomError("LIBRO_ACTUALIZACION_INVALIDA","Una aplicacion solo cambia monto y auditoria de actualizacion de un movimiento propio.");
                if(c.Despues is {} after)
                {
                    if(after.Tipo is not ("F" or "V")||after.FrecuenciaId is null or <=0)throw new RhNomError("LIBRO_DESTINO_INVALIDO","Solo movimientos automaticos F/V con frecuencia positiva pueden ser propios del motor.");
                    if(after.Monto is null||after.Monto.Length>64||!Regex.IsMatch(after.Monto,@"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[Ee][+-]?[0-9]+)?\z"))
                        throw new RhNomError("LIBRO_MONTO_INVALIDO","La imagen posterior requiere un NUMBER Oracle textual, sin conversion ni redondeo.");
                    // Reinsertions/restorations get the new authenticated actor/DB clock;
                    // old audit values are evidence, never blindly reset on reversal.
                    if(c.Accion=="CREAR"?(after.UsuarioIns!=d.Actor||after.FechaIns!=e.FechaBd||after.UsuarioUpd is not null||after.FechaUpd is not null)
                        :(after.UsuarioUpd!=d.Actor||after.FechaUpd!=e.FechaBd||after.UsuarioIns!=c.Antes!.UsuarioIns||after.FechaIns!=c.Antes.FechaIns))
                        throw new RhNomError("LIBRO_AUDITORIA_INVALIDA","La imagen posterior debe contener actor y reloj BD del evento.");
                    owners[id]=new(e.IdEvento,after,RhNomPlanesFinancieros.Hash(after));
                }
                else owners.Remove(id);
            }
            if(owners.Count>2000||owners.Values.GroupBy(p=>new{p.UltimaImagen.CodigoTipoNomina,p.UltimaImagen.CodigoPersona,p.UltimaImagen.CodigoConcepto,Complemento=string.IsNullOrEmpty(p.UltimaImagen.Complemento)?null:p.UltimaImagen.Complemento,p.UltimaImagen.Tipo}).Any(g=>g.Count()>1))
                throw new RhNomError("LIBRO_DESTINO_MULTIPLE","Propiedad fuera de limite o incompatible con el indice unico de movimientos.");
            if(d.Accion=="APLICACION")active.Push(e.IdEvento);else active.Pop();
            previous=e.IdEvento;previousHash=e.HashEvento;
        }
        return new(owners.Values.OrderBy(p=>p.UltimaImagen.Id).ToList(),previous,previousHash,events.Count);
    }
    static bool DatosRestaurados(RhNomImagenMovimiento a,RhNomImagenMovimiento b)=>a with{UsuarioIns=b.UsuarioIns,FechaIns=b.FechaIns,UsuarioUpd=b.UsuarioUpd,FechaUpd=b.FechaUpd}==b;
}
