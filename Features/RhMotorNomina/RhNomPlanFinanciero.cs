namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Complete RH_MOV_NOMINA image. Audit/extras participate in change detection.
public record RhNomImagenMovimiento(int Id,int CodigoEmpresa,int CodigoTipoNomina,int CodigoPersona,int CodigoConcepto,
    int? FrecuenciaId,string? Tipo,string? Monto,string? Estado,string? Complemento,string? Extra1,string? Extra2,string? Extra3,
    int? UsuarioIns,DateTime? FechaIns,int? UsuarioUpd,DateTime? FechaUpd);
// Only a server-owned ledger may supply this record; never infer ownership from
// AUTOMATICO, amounts, audit usernames or a boolean supplied by the client.
public record RhNomPropiedadMovimiento(int IdAplicacion,RhNomImagenMovimiento UltimaImagen,string HashUltimaImagen);
public record RhNomPasoFinanciero(int CodigoConcepto,int FrecuenciaId,string Tipo,string Accion,
    RhNomImagenMovimiento? Antes,string? MontoNuevo,int? IdAplicacionPropietaria,string? Bloqueo);
public record RhNomPlanFinanciero(List<RhNomPasoFinanciero> Pasos)
{
    public string VersionMetodo=>RhNomPlanesFinancieros.Version;
    public bool TieneBloqueos=>Pasos.Any(p=>p.Bloqueo is not null);
    public bool AplicacionDisponible=>false;
    public bool MovimientosAplicados=>false;
    public bool DatosRevalidadosEnTransaccion=>false;
    public string PoliticaAuditoria=>"ACTOR_AUTENTICADO_Y_RELOJ_BD";
}
public record RhNomCambioAplicado(string Accion,RhNomImagenMovimiento? Antes,RhNomImagenMovimiento? Despues);
public record RhNomPasoReversion(string Accion,RhNomImagenMovimiento? Actual,RhNomImagenMovimiento? ReferenciaRestaurar,string? Bloqueo);
public record RhNomPlanReversion(List<RhNomPasoReversion> Pasos)
{
    public string VersionMetodo=>RhNomPlanesFinancieros.Version;
    public bool TieneBloqueos=>Pasos.Any(p=>p.Bloqueo is not null);
    public bool ReversionDisponible=>false;
    public bool MovimientosAplicados=>false;
    public string PoliticaAuditoria=>"ACTOR_AUTENTICADO_Y_RELOJ_BD";
}
// Pure preparation, not an authorization or persistence boundary. A future
// adapter must lock/revalidate context, inputs, publication and every before image
// in one transaction, and record the real after image with the database clock.
public static class RhNomPlanesFinancieros
{
    public const string Version="PLAN_FINANCIERO_PREVIO_V1";
    public static string Hash(RhNomImagenMovimiento image)=>RhNomRecalculosHistoricos.Hash(ValidarImagen(image));
    static RhNomImagenMovimiento ValidarImagen(RhNomImagenMovimiento r)
    {
        if(r.Id<=0||r.CodigoEmpresa<=0||r.CodigoTipoNomina<=0||r.CodigoPersona<=0||r.CodigoConcepto<=0
            ||r.FechaIns is {} a&&a.Kind!=DateTimeKind.Unspecified||r.FechaUpd is {} b&&b.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("IMAGEN_MOV_INVALIDA","Imagen de movimiento sin identidad/contexto positivo o con fechas convertidas de zona.");
        return r;
    }
    static (int,int?,string?) Clave(RhNomImagenMovimiento r)=>(r.CodigoConcepto,r.FrecuenciaId,r.Tipo);
    // RH_MOV_NOMINA_U2 intentionally does not include company or frequency.
    // Oracle treats an empty VARCHAR2 as NULL; the other key columns are non-null
    // in every image accepted here, so partially-null composite keys also collide.
    static bool MismaClaveUnica(RhNomImagenMovimiento a,RhNomImagenMovimiento b)=>
        a.CodigoTipoNomina==b.CodigoTipoNomina&&a.CodigoPersona==b.CodigoPersona
        &&a.CodigoConcepto==b.CodigoConcepto&&a.Tipo==b.Tipo
        &&(string.IsNullOrEmpty(a.Complemento)?null:a.Complemento)==(string.IsNullOrEmpty(b.Complemento)?null:b.Complemento);
    static bool MismoAmbito(RhNomImagenMovimiento a,RhNomImagenMovimiento b)=>a.CodigoEmpresa==b.CodigoEmpresa&&a.CodigoTipoNomina==b.CodigoTipoNomina&&a.CodigoPersona==b.CodigoPersona&&Clave(a)==Clave(b);
    public static RhNomPlanFinanciero Preparar(RhNomConsolidacion consolidated,IReadOnlyList<RhNomImagenMovimiento> actuales,
        IReadOnlyList<RhNomPropiedadMovimiento> propiedades,CancellationToken ct=default)
    {
        if(actuales.Any(r=>r is null)||propiedades.Any(p=>p is null||p.UltimaImagen is null))throw new RhNomError("IMAGEN_MOV_INVALIDA","Imagen o propiedad nula.");
        if(consolidated.CodigoEmpresa<=0||consolidated.CodigoTipoNomina<=0||consolidated.CodigoPersona<=0||consolidated.CodigoPeriodo<=0
            ||consolidated.CodigoPresupuesto<=0||consolidated.FechaNomina.Kind!=DateTimeKind.Unspecified||consolidated.FechaNomina.Year<1900||consolidated.Escenario is not ("ADJUNTAR_CARGO" or "RECALCULA_VARIACIONES")
            ||consolidated.Destinos.Count is <1 or >100||actuales.Count>2000||propiedades.Count>2000)
            throw new RhNomError("CONTEXTO_PLAN_INVALIDO","Contexto positivo, 1..100 destinos y hasta 2000 imagenes/propiedades requeridos.");
        if(actuales.Select(r=>r.Id).Distinct().Count()!=actuales.Count||propiedades.Select(p=>p.UltimaImagen.Id).Distinct().Count()!=propiedades.Count)
            throw new RhNomError("IMAGEN_DUPLICADA","Imagen o propiedad repetida; no se deduplica silenciosamente.");
        void Ambito(RhNomImagenMovimiento r){ValidarImagen(r);if(r.CodigoEmpresa!=consolidated.CodigoEmpresa||r.CodigoTipoNomina!=consolidated.CodigoTipoNomina||r.CodigoPersona!=consolidated.CodigoPersona)throw new RhNomError("MOVIMIENTO_AJENO","Imagen ajena a la empresa/tipo/persona del plan.");}
        foreach(var r in actuales)Ambito(r);
        foreach(var p in propiedades){Ambito(p.UltimaImagen);if(p.IdAplicacion<=0||p.HashUltimaImagen!=Hash(p.UltimaImagen))throw new RhNomError("PROPIEDAD_INVALIDA","La propiedad del ledger no coincide con su ultima imagen.");}
        if(consolidated.Destinos.Select(d=>(d.CodigoConcepto,d.FrecuenciaId,d.TipoMovimiento)).Distinct().Count()!=consolidated.Destinos.Count)
            throw new RhNomError("DESTINO_DUPLICADO","Destinos repetidos en la consolidacion.");
        RhNomConsolidaciones.VerificarDecisiones(consolidated);
        var multipleCargos=consolidated.Destinos.SelectMany(d=>d.Origenes).Select(o=>o.CodigoRelacionCargo).Distinct().Count()>1;
        var steps=new List<RhNomPasoFinanciero>();
        foreach(var d in consolidated.Destinos.OrderBy(d=>d.CodigoConcepto).ThenBy(d=>d.FrecuenciaId).ThenBy(d=>d.TipoMovimiento,StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if(d.CodigoConcepto<=0||d.FrecuenciaId<=0||d.TipoMovimiento is not ("F" or "V"))throw new RhNomError("DESTINO_PLAN_INVALIDO","Destino de concepto/frecuencia positivos y movimiento F/V requerido.");
            var candidates=actuales.Where(r=>Clave(r)==(d.CodigoConcepto,(int?)d.FrecuenciaId,d.TipoMovimiento)).ToList();
            var owners=propiedades.Where(p=>Clave(p.UltimaImagen)==(d.CodigoConcepto,(int?)d.FrecuenciaId,d.TipoMovimiento)).ToList();
            var before=candidates.Count==1?candidates[0]:null;var owner=owners.Count==1?owners[0]:null;
            string? block=multipleCargos?"CARGOS_SIN_SEGMENTACION":d.Estado!="DESTINO_UNICO"||d.Origenes.Count!=1?"CONSOLIDACION_EN_CONFLICTO":candidates.Count>1||owners.Count>1?"DESTINO_MULTIPLE":
                actuales.Any(r=>r.CodigoConcepto==d.CodigoConcepto&&r.Tipo is not ("E" or "F" or "V"))?"TIPO_MOVIMIENTO_AMBIGUO":
                actuales.Any(r=>r.CodigoConcepto==d.CodigoConcepto&&r.Tipo==d.TipoMovimiento&&r.FrecuenciaId!=d.FrecuenciaId)?"FRECUENCIA_AMBIGUA":
                owner is not null&&(before is null||before.Id!=owner.UltimaImagen.Id)?"MOVIMIENTO_PROPIO_AUSENTE_O_SUSTITUIDO":
                owner is not null&&Hash(before!)!=owner.HashUltimaImagen?"MOVIMIENTO_MODIFICADO_POSTERIORMENTE":null;
            if(block is not null){steps.Add(new(d.CodigoConcepto,d.FrecuenciaId,d.TipoMovimiento,"BLOQUEADO",before,null,owner?.IdAplicacion,block));continue;}
            var origin=d.Origenes.Single();var proposal=origin.Partida;
            string action;string? amount=null;
            switch(proposal?.Accion??origin.DecisionControl!.Accion)
            {
                case "CONSERVAR_EXISTENTE":case "OMITIR":action="CONSERVAR";break;
                case "REGISTRAR_RESULTADO":case "REGISTRAR_CERO":
                    if(proposal is null||proposal.MontoPropuesto is null||RhNomNumeros.Leer(proposal.MontoPropuesto)!=RhNomNumeros.Leer(proposal.ResultadoCalculado))throw new RhNomError("MONTO_PLAN_INVALIDO","Monto propuesto requerido e igual al resultado firmado.");
                    amount=RhNomNumeros.Texto(RhNomNumeros.Leer(proposal.MontoPropuesto));
                    if(proposal.Accion=="REGISTRAR_CERO"&&amount!="0")throw new RhNomError("MONTO_PLAN_INVALIDO","REGISTRAR_CERO requiere importe cero.");
                    if(before is null)action="PROPONER_CREACION";
                    else if(owner is null){action="BLOQUEADO";block="MOVIMIENTO_NO_PROPIO";amount=null;}
                    else action="PROPONER_ACTUALIZACION_PROPIA";
                    break;
                case "RETIRAR_GENERADO":
                    if(before is null)action="SIN_MOVIMIENTO";
                    else if(owner is null){action="BLOQUEADO";block="MOVIMIENTO_NO_PROPIO";}
                    else action="PROPONER_RETIRO_PROPIO";
                    break;
                default:throw new RhNomError("ACCION_PLAN_INVALIDA","Accion de proyeccion no admitida.");
            }
            steps.Add(new(d.CodigoConcepto,d.FrecuenciaId,d.TipoMovimiento,action,before,amount,owner?.IdAplicacion,block));
        }
        if(propiedades.Any(p=>!consolidated.Destinos.Any(d=>Clave(p.UltimaImagen)==(d.CodigoConcepto,(int?)d.FrecuenciaId,d.TipoMovimiento))))
            throw new RhNomError("PROPIEDAD_SIN_REGLA","Hay movimientos propios anteriores sin destino en esta revision; defina su tratamiento antes de aplicar.");
        return new(steps);
    }
    public static RhNomPlanReversion Revertir(IReadOnlyList<RhNomCambioAplicado> cambios,IReadOnlyList<RhNomImagenMovimiento> actuales,CancellationToken ct=default)
    {
        if(actuales.Any(r=>r is null)||cambios.Any(c=>c is null))throw new RhNomError("REVERSION_INVALIDA","Imagen o cambio nulo.");
        if(cambios.Count is <1 or >100||actuales.Count>2000||actuales.Select(r=>r.Id).Distinct().Count()!=actuales.Count)
            throw new RhNomError("REVERSION_INVALIDA","Cambios acotados e imagenes actuales distintas requeridos.");
        foreach(var r in actuales)ValidarImagen(r);
        RhNomImagenMovimiento? scope=null;
        var steps=new List<RhNomPasoReversion>();var ids=new HashSet<int>();
        var restorations=cambios.Where(c=>c.Antes is not null).Select(c=>c.Antes!).ToList();
        for(var i=0;i<restorations.Count;i++)
            if(restorations.Skip(i+1).Any(r=>MismaClaveUnica(restorations[i],r)))
                throw new RhNomError("CLAVE_REVERSION_DUPLICADA","La aplicacion contiene restauraciones que colisionan en el indice unico.");
        foreach(var change in cambios)
        {
            ct.ThrowIfCancellationRequested();var a=change.Antes;var b=change.Despues;
            if(change.Accion=="CREAR"?(a is not null||b is null):change.Accion=="ACTUALIZAR"?(a is null||b is null):change.Accion=="RETIRAR"?(a is null||b is not null):true)
                throw new RhNomError("CAMBIO_REVERSION_INVALIDO","Imagenes antes/despues incompatibles con la accion aplicada.");
            if(a is not null)ValidarImagen(a);if(b is not null)ValidarImagen(b);
            var reference=(b??a)!;scope??=reference;
            if(reference.CodigoEmpresa!=scope.CodigoEmpresa||reference.CodigoTipoNomina!=scope.CodigoTipoNomina||reference.CodigoPersona!=scope.CodigoPersona)throw new RhNomError("CONTEXTO_REVERSION_DISTINTO","Revierta una aplicacion de la misma empresa/tipo/persona.");
            var id=reference.Id;if(!ids.Add(id))throw new RhNomError("CAMBIO_REVERSION_DUPLICADO","Un movimiento no puede repetirse en la aplicacion.");
            if(a is not null&&b is not null&&(a.Id!=b.Id||!MismoAmbito(a,b)||!MismaClaveUnica(a,b)))throw new RhNomError("CAMBIO_REVERSION_AJENO","Una actualizacion no cambia identidad ni clave de negocio.");
            var current=actuales.SingleOrDefault(r=>r.Id==id);string? block=null;string action;
            if(current is not null&&!MismoAmbito(current,reference)){steps.Add(new("BLOQUEADO",null,a,"IDENTIDAD_AJENA"));continue;}
            if(b is not null){block=current is null?"MOVIMIENTO_AUSENTE":Hash(current)!=Hash(b)?"MOVIMIENTO_MODIFICADO_POSTERIORMENTE":null;action=a is null?"PROPONER_RETIRO_CREADO":"PROPONER_RESTAURACION";}
            else{block=current is not null?"IDENTIDAD_OCUPADA":null;action="PROPONER_REINSERCION";}
            // The future adapter must fetch these candidates globally, not filter
            // them by company/frequency, and revalidate under transaction locks.
            if(block is null&&a is not null&&actuales.Any(r=>r.Id!=id&&MismaClaveUnica(r,a)))
                block="CLAVE_UNICA_OCUPADA";
            steps.Add(new(block is null?action:"BLOQUEADO",current,a,block));
        }
        return new(steps);
    }
}
