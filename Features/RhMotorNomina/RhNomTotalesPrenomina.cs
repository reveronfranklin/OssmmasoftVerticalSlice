using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomTotalPersona(int CodigoPersona,int FilasIncluidas,string Estado,string? Asignaciones,
    string? DeduccionesFirmadas,string? Neto,List<string> Motivos);
public record RhNomTotalesPrenominaResultado(string VersionMetodo,string Hash,RhNomSeleccionPrenominaResultado Seleccion,List<RhNomTotalPersona> Personas)
{
    public bool TieneBloqueos=>Personas.Any(p=>p.Estado=="BLOQUEADO");
    public bool MovimientosAplicados=>false;
    public bool TemporalGenerado=>false;
    public bool ParidadHistoricaCertificada=>false;
    public string PoliticaNeto=>"NO_NEGATIVO_CERO_PERMITIDO";
    public string PoliticaPrecision=>"EXACTA_SIN_REDONDEO_NUMBER38";
}
public static class RhNomTotalesPrenomina
{
    public const string Version="TOTALES_PRENOMINA_V1";
    // Rebuild selection from source images/metadata. Never accept client decisions or totals.
    public static RhNomTotalesPrenominaResultado Preparar(RhNomContextoSeleccion context,RhNomSeleccionDef definition,
        IReadOnlyList<RhNomImagenMovimiento> images,IReadOnlyList<RhNomPersonalSeleccion> people,
        IReadOnlyList<RhNomFrecuenciaSeleccion> frequencies,IReadOnlyList<RhNomConceptoSeleccion> concepts)
    {
        var selection=RhNomSeleccionPrenomina.Preparar(context,definition,images,people,frequencies,concepts);
        var byPerson=selection.Filas.ToLookup(f=>f.Imagen.CodigoPersona);var conceptIndex=concepts.ToDictionary(c=>c.CodigoConcepto);
        var results=new List<RhNomTotalPersona>();
        foreach(var person in people.Select(p=>p.CodigoPersona).Concat(images.Select(i=>i.CodigoPersona)).Distinct().Order())
        {
            var rows=byPerson[person].ToList();var included=rows.Where(r=>r.Resultado=="INCLUIDA").ToList();
            var reasons=rows.Where(r=>r.Resultado=="BLOQUEADA").Select(r=>r.Motivo).Distinct().Order(StringComparer.Ordinal).ToList();
            if(person<=0)reasons.Add("IDENTIDAD_PERSONA_INVALIDA");
            if(reasons.Count>0){results.Add(new(person,included.Count,"BLOQUEADO",null,null,null,reasons));continue;}
            if(included.Count==0){results.Add(new(person,0,"SIN_PARTIDAS",null,null,null,[]));continue;}
            var assignments=Exact.Zero;var deductions=Exact.Zero;
            foreach(var row in included)
            {
                var amount=Exact.Parse(row.Imagen.Monto!);
                if(conceptIndex[row.Imagen.CodigoConcepto].TipoConcepto=="A")assignments=assignments.Add(amount);
                else deductions=deductions.Add(amount);
            }
            var net=assignments.Add(deductions);
            if(net.Units.Sign<0)reasons.Add("NETO_NEGATIVO");
            if(!assignments.OracleRepresentable)reasons.Add("ASIGNACIONES_FUERA_NUMBER38");
            if(!deductions.OracleRepresentable)reasons.Add("DEDUCCIONES_FUERA_NUMBER38");
            if(!net.OracleRepresentable)reasons.Add("NETO_FUERA_NUMBER38");
            results.Add(new(person,included.Count,reasons.Count==0?"VALIDADO":"BLOQUEADO",assignments.Text(),deductions.Text(),net.Text(),reasons));
        }
        var hash=RhNomRecalculosHistoricos.Hash(new{Version,HashSeleccion=selection.Hash,Personas=results});
        return new(Version,hash,selection,results);
    }
    // Base-10 integer arithmetic retains every input digit and cancellation. Decimal,
    // double and Oracle SUM may round before the final validation of NUMBER's range.
    internal readonly record struct Exact(BigInteger Units,int Scale)
    {
        public static Exact Zero=>new(BigInteger.Zero,0);
        private static Exact Normalize(BigInteger units,int scale)
        {
            if(units.IsZero)return Zero;
            while(units%10==0){units/=10;scale--;}
            return new(units,scale);
        }
        public static Exact Parse(string value)
        {
            var m=Regex.Match(value,@"\A([+-]?)([0-9]*)(?:\.([0-9]*))?(?:[Ee]([+-]?[0-9]+))?\z");
            if(!m.Success||m.Groups[2].Length+m.Groups[3].Length==0)throw new RhNomError("IMPORTE_EXACTO_INVALIDO","La seleccion requiere importes Oracle validos.");
            var digits=m.Groups[2].Value+m.Groups[3].Value;var units=BigInteger.Parse(digits,CultureInfo.InvariantCulture);
            if(m.Groups[1].Value=="-")units=-units;
            var exponent=m.Groups[4].Success?int.Parse(m.Groups[4].Value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture):0;
            return Normalize(units,m.Groups[3].Length-exponent);
        }
        public Exact Add(Exact other)
        {
            var scale=Math.Max(Scale,other.Scale);
            return Normalize(Units*BigInteger.Pow(10,scale-Scale)+other.Units*BigInteger.Pow(10,scale-other.Scale),scale);
        }
        public bool OracleRepresentable
        {
            get
            {
                if(Units.IsZero)return true;
                var digits=BigInteger.Abs(Units).ToString(CultureInfo.InvariantCulture).Length;var exponent=digits-Scale-1;
                return digits<=38&&exponent is >=-130 and <=125;
            }
        }
        public string Text()
        {
            if(Units.IsZero)return "0";
            var digits=BigInteger.Abs(Units).ToString(CultureInfo.InvariantCulture);var sign=Units.Sign<0?"-":"";
            var value=Scale<=0?digits+new string('0',-Scale):Scale>=digits.Length?"0."+new string('0',Scale-digits.Length)+digits:digits.Insert(digits.Length-Scale,".");
            if(value.Length+sign.Length<=64)return sign+value;
            return sign+digits[..1]+(digits.Length>1?"."+digits[1..]:"")+"E"+(digits.Length-Scale-1).ToString(CultureInfo.InvariantCulture);
        }
    }
}
