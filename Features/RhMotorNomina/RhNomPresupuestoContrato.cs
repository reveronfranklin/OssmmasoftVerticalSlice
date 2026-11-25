using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomContratoPresupuesto(string Version,string Origen,string Referencia,string HashFuente);
public record RhNomPresupuestoCapturado(int Efectivo,int? Periodo,string Origen,string Estado,string Referencia);
public static class RhNomPresupuestos
{
    public const string PoliticaContrato="CONTRATO_TIPO_ACTUAL";
    public const string Version="PRESUPUESTO_TIPO_ACTUAL_V1";
    public static RhNomContratoPresupuesto Contrato()=>new(Version,"RH_TIPOS_NOMINA.CODIGO_PRESUPUESTO_ACTUAL","R57_FORMS_UTIL_GET_TIPO_NOMINA","B4F0ACA285B478E42DFBAC2D6B3404E493D45666DC53A49754CDC6513574587F");
    public static bool PoliticaValida(string? policy)=>policy is "COINCIDIR_PERIODO" or "TIPO_ACTUAL_EXPLICITO" or PoliticaContrato;
    public static void Habilitada(IConfiguration config,string policy)
    {
        if(policy==PoliticaContrato&&!config.GetValue<bool>("RhMotorNomina:ContratoPresupuestoTipoActualEnabled"))throw new RhNomError("CONTRATO_PRESUPUESTO_DESHABILITADO","El contrato de presupuesto actual no esta habilitado en este ambiente.");
    }
    public static bool SinConflicto(int? period,int current)=>current>0&&(period is null or 0||period==current);
    public static void Validar(int? period,int current,string policy,string? reason)
    {
        if(!PoliticaValida(policy)||current<=0)throw new RhNomError("POLITICA_PRESUPUESTO_REQUERIDA","Politica de presupuesto y presupuesto efectivo positivo requeridos.");
        if(policy==PoliticaContrato)
        {
            if(!SinConflicto(period,current))throw new RhNomError("CONTRATO_PRESUPUESTO_CONFLICTO","El periodo tiene otro presupuesto informado; el contrato no permite reemplazarlo por el actual.");
            return;
        }
        if(period==current)return;
        if(policy!="TIPO_ACTUAL_EXPLICITO")throw new RhNomError("PRESUPUESTO_DIFIERE","El presupuesto actual del tipo/relacion difiere del registrado en el periodo.");
        if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length<10||reason.Length>500)throw new RhNomError("MOTIVO_PRESUPUESTO_REQUERIDO","Documente el uso explicito del presupuesto actual cuando difiere del periodo (10..500 caracteres).");
    }
    public static bool ContratoVerificado(RhNomPeriodoCapturado? p,int current)=>p?.PoliticaPresupuesto==PoliticaContrato&&p.ContratoPresupuesto==Contrato()&&SinConflicto(p.PresupuestoPeriodo,current);
}
