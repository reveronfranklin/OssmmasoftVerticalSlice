using Oracle.ManagedDataAccess.Types;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomCambiosFinancieros
{
    public static RhNomCambioAplicado? Aplicar(RhNomPasoFinanciero p,RhNomConsolidacion c,int id,int actor,DateTime clock)
    {
        if(actor<=0||clock.Kind!=DateTimeKind.Unspecified||p.Bloqueo is not null)throw new RhNomError("CAMBIO_FINANCIERO_BLOQUEADO","Actor/reloj/destino financiero invalido.");
        return p.Accion switch
        {
            "CONSERVAR" or "SIN_MOVIMIENTO"=>null,
            // Explicit initial adapter policy from the existing movement contract:
            // active A, no complement/extras, F/V supplied by the frozen concept rule.
            "PROPONER_CREACION" when id>0&&p.Antes is null&&p.MontoNuevo is not null=>new("CREAR",null,new(id,c.CodigoEmpresa,c.CodigoTipoNomina,c.CodigoPersona,p.CodigoConcepto,p.FrecuenciaId,p.Tipo,p.MontoNuevo,"A",null,null,null,null,actor,clock,null,null)),
            "PROPONER_ACTUALIZACION_PROPIA" when p.Antes is not null&&p.MontoNuevo is not null=>new("ACTUALIZAR",p.Antes,p.Antes with{Monto=p.MontoNuevo,UsuarioUpd=actor,FechaUpd=clock}),
            "PROPONER_RETIRO_PROPIO" when p.Antes is not null=>new("RETIRAR",p.Antes,null),
            _=>throw new RhNomError("CAMBIO_FINANCIERO_INVALIDO","Accion/imagenes financieras invalidas.")
        };
    }
    public static RhNomCambioAplicado Revertir(RhNomCambioAplicado old,RhNomImagenMovimiento? current,int actor,DateTime clock)
    {
        if(actor<=0||clock.Kind!=DateTimeKind.Unspecified)throw new RhNomError("AUDITORIA_REVERSION_INVALIDA","Actor/reloj requeridos.");
        return old.Accion switch
        {
            "CREAR" when current is not null=>new("RETIRAR",current,null),
            "ACTUALIZAR" when current is not null&&old.Antes is not null=>new("ACTUALIZAR",current,old.Antes with{UsuarioIns=current.UsuarioIns,FechaIns=current.FechaIns,UsuarioUpd=actor,FechaUpd=clock}),
            "RETIRAR" when current is null&&old.Antes is not null=>new("CREAR",null,old.Antes with{UsuarioIns=actor,FechaIns=clock,UsuarioUpd=null,FechaUpd=null}),
            _=>throw new RhNomError("CAMBIO_REVERSION_INVALIDO","No se pudo construir una reversion completa.")
        };
    }
    public static void VerificarResultado(IReadOnlyList<RhNomImagenMovimiento> before,IReadOnlyList<RhNomCambioAplicado> changes,RhNomCapturaMovimientos actual)
    {
        if(actual.HayAmbitoAjeno||actual.Imagenes.Select(r=>r.Id).Distinct().Count()!=actual.Imagenes.Count||changes.Select(c=>(c.Despues??c.Antes)!.Id).Distinct().Count()!=changes.Count)
            throw new RhNomError("RESULTADO_TX_AMBIGUO","Imagenes/ambito financiero ambiguos.");
        var expected=before.ToDictionary(r=>r.Id);
        foreach(var c in changes){var id=(c.Despues??c.Antes)!.Id;if(c.Antes is not null&&(!expected.TryGetValue(id,out var previous)||previous!=c.Antes))throw new RhNomError("ANTES_TX_DISTINTO","Imagen anterior distinta.");if(c.Antes is null&&expected.ContainsKey(id))throw new RhNomError("ID_TX_OCUPADO","Identidad creada ocupada.");if(c.Despues is null)expected.Remove(id);else expected[id]=c.Despues;}
        if(expected.Count!=actual.Imagenes.Count)throw new RhNomError("CONTEO_TX_DISTINTO","Cantidad de movimientos posterior inconsistente.");
        var changed=changes.Where(c=>c.Despues is not null).Select(c=>c.Despues!.Id).ToHashSet();
        foreach(var image in actual.Imagenes)
        {
            if(!expected.TryGetValue(image.Id,out var proposed))throw new RhNomError("MOVIMIENTO_TX_NO_ESPERADO","Movimiento posterior inesperado.");
            var matches=changed.Contains(image.Id)?image==proposed with{Monto=image.Monto}&&image.Monto is not null&&proposed.Monto is not null&&RhNomTransaccion.Numero(image.Monto).Equals(RhNomTransaccion.Numero(proposed.Monto)):image==proposed;
            if(!matches)throw new RhNomError("IMAGEN_TX_DISTINTA","La imagen posterior difiere de lo previsto o se altero un movimiento protegido.");
        }
    }
}
