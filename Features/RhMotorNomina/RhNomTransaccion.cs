using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Types;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomEjecutarPlan(int IdPlan,string HashPlanEsperado,string ClaveSolicitud,string Motivo,int? IdRevertido=null);
public record RhNomReciboFinanciero(int IdEvento,int IdPlan,string HashSolicitud,string HashEvento,DateTime FechaBd)
{
    public bool MovimientosAplicados=>true;
    public bool EventoConfirmado=>true;
}
public static class RhNomTransaccion
{
    public const string Version="TRANSACCION_FINANCIERA_V1";
    public static string Validar(RhNomEjecutarPlan q,int company,int actor)
    {
        if(company<=0||actor<=0||q.IdPlan<=0||q.HashPlanEsperado is null||!Regex.IsMatch(q.HashPlanEsperado,@"\A[A-F0-9]{64}\z")
            ||q.ClaveSolicitud is null||q.ClaveSolicitud.Length is <1 or >80||!Regex.IsMatch(q.ClaveSolicitud,@"\A[A-Za-z0-9:_-]+\z")||q.IdRevertido is <=0)
            throw new RhNomError("EJECUCION_INVALIDA","Plan, hash, clave y referencia de reversion invalidos.");
        var reason=RhNomValidaciones.Motivo(q.Motivo);
        return RhNomRecalculosHistoricos.Hash(new{Version,CodigoEmpresa=company,Actor=actor,q.IdPlan,q.HashPlanEsperado,q.IdRevertido,Motivo=reason});
    }
    public static void PerfilDeProveedores(bool reversal)
    {
        // A reversal restores confirmed ledger images; it does not evaluate current providers.
        // The application profile deliberately stays closed until its complete review.
        if(!reversal&&(!string.Equals(RhNomFuentesActuales.Version,"FUENTES_ACTUALES_V11",StringComparison.Ordinal)||!string.Equals(RhNomFuentesModelo.Version,"FUENTES_MODELO_V10",StringComparison.Ordinal)))
            throw new RhNomError("PERFIL_TX_NO_REVISADO","Los proveedores cambiaron; revisar bloqueos del adaptador antes de aplicar.");
    }
    public static void RevalidarCatalogo(RhNomSnapshot snapshot,IReadOnlyList<RhNomConceptoCfg> catalog,int company,int type)
    {
        if(snapshot.CodigoEmpresa!=company||snapshot.CodigoTipoNomina!=type)throw new RhNomError("CATALOGO_TX_AJENO","Revision ajena al contexto transaccional.");
        var rules=RhNomReglasConcepto.Validar((snapshot.ReglasConcepto??[]).Select(RhNomReglasConcepto.Solicitud).ToList(),snapshot.Variables,catalog,company,type);
        RhNomAccionesControl.Validar((snapshot.AccionesControl??[]).Select(RhNomAccionesControl.Solicitud).ToList(),snapshot.Variables,snapshot.FuentesActuales??[],catalog,company,type,rules);
    }
    public static (int Modelo,int Version,string Hash) ModeloUnico(RhNomConsolidacion c)
    {
        var origins=c.Destinos.SelectMany(d=>d.Origenes).ToList();
        if(origins.Select(o=>o.CodigoRelacionCargo).Distinct().Count()!=1)throw new RhNomError("SEGMENTOS_PENDIENTES","La aplicacion requiere una relacion de cargo; no se suman cargos.");
        var models=origins.Select(o=>(o.ModeloId,o.Version,o.HashModelo)).Distinct().ToList();
        if(models.Count!=1)throw new RhNomError("MODELOS_MULTIPLES","La primera version transaccional requiere una revision unica con todas las reglas.");
        return models.Single();
    }
    public static void Publicacion(RhNomEstadoRevision state,int model,int version,string hash,DateTime date)
    {
        if(state.ModeloId!=model||state.Version!=version||state.HashModelo!=hash||state.Estado!="PUBLICADO"||state.Evidencia is null
            ||state.Desde is null||state.Desde>DateOnly.FromDateTime(date)||state.Hasta is {} until&&until<DateOnly.FromDateTime(date))
            throw new RhNomError("PUBLICACION_FINANCIERA_REQUERIDA","Revision integral publicada y vigente requerida para aplicar.");
        RhNomValidaciones.Verificar(state.Evidencia,model,version,hash);
        if(!state.Evidencia.Publicable)throw new RhNomError("PARIDAD_FINANCIERA_PENDIENTE","No hay evidencia integral publicable para esta revision.");
    }

    public static void CapacidadLibro(IReadOnlyList<RhNomLibroEvento> previous,RhNomLibroDocumento next)
    {
        var nextBytes=System.Text.Encoding.UTF8.GetByteCount(RhNomCorridas.Serializar(next,RhNomLibroFinancieroDb.MaxDocumento));
        var bytes=previous.Sum(e=>(long)System.Text.Encoding.UTF8.GetByteCount(RhNomCorridas.Serializar(e.Documento,RhNomLibroFinancieroDb.MaxDocumento)))+nextBytes;
        var application=next.Accion=="APLICACION";
        // Reserve one complete inverse event; longer unwind chains need a checkpoint.
        if(previous.Count+1>(application?RhNomLibroFinanciero.MaxEventos-1:RhNomLibroFinanciero.MaxEventos)
            ||bytes>(application?RhNomLibroFinancieroDb.MaxCadena-RhNomLibroFinancieroDb.MaxDocumento:RhNomLibroFinancieroDb.MaxCadena)
            ||application&&nextBytes>RhNomLibroFinancieroDb.MaxDocumento-16384)
            throw new RhNomError("LIBRO_TX_SIN_CAPACIDAD","No hay capacidad para confirmar y conservar un inverso completo; requiere checkpoint validado.");
    }
    public static OracleDecimal Numero(string value)
    {
        // Synchronous scope only: retain NUMBER's 38 digits and exponent, never decimal/double.
        var original=CultureInfo.CurrentCulture;
        try
        {
            if(value is null||value.Length>64)throw new FormatException();
            var m=Regex.Match(value,@"\A([+-]?)([0-9]*)(?:\.([0-9]*))?(?:[Ee]([+-]?[0-9]+))?\z");
            if(!m.Success||m.Groups[2].Length+m.Groups[3].Length==0)throw new FormatException();
            var digits=m.Groups[2].Value+m.Groups[3].Value;
            if(digits.TrimStart('0').TrimEnd('0').Length>38)throw new FormatException();
            if(m.Groups[4].Success)
            {
                if(!int.TryParse(m.Groups[4].Value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var exponent)||exponent is <-300 or >300)throw new FormatException();
                var point=m.Groups[2].Length+exponent;
                value=m.Groups[1].Value+(point<=0?"0."+new string('0',-point)+digits:point>=digits.Length?digits+new string('0',point-digits.Length):digits.Insert(point,"."));
            }
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;var result=new OracleDecimal(value);
            if(result.Equals(new OracleDecimal(0))&&digits.Any(c=>c!='0'))throw new FormatException();
            return result;
        }
        catch(Exception e) when(e is ArgumentException or OverflowException or FormatException){throw new RhNomError("MONTO_ORACLE_INVALIDO","Monto no representable como NUMBER Oracle.");}
        finally{CultureInfo.CurrentCulture=original;}
    }
    public static void FuentesIguales(RhNomCorridaDocumento frozen,RhNomResultadoActual fresh)
    {
        if(frozen.FuenteActual is null||frozen.EntradasActuales is null||fresh.Simulacion.Hash!=frozen.Modelo.Hash||fresh.Simulacion.ModeloId!=frozen.Modelo.ModeloId||fresh.Simulacion.Version!=frozen.Modelo.Version
            ||RhNomFuentesActuales.Hash(frozen.FuenteActual)!=fresh.HashFuente
            ||RhNomRecalculosHistoricos.Hash(frozen.EntradasActuales)!=RhNomRecalculosHistoricos.Hash(fresh.Entradas))
            throw new RhNomError("FUENTES_CAMBIARON","Las fuentes actuales difieren de la corrida; recalcule y guarde un plan nuevo.");
    }
}
