using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.RhTitularesBeneficiarios;

public record RhTipoNomina(int CodigoTipoNomina);
public record RhTitBenFilter(List<RhTipoNomina>? CodigoTipoNomina);
public record RhTitBenDireccion
{
    public int CodigoDireccion { get; init; }
    public bool Principal { get; init; }
    public string? TipoDireccion { get; init; }
    public string? Pais { get; init; }
    public string? Estado { get; init; }
    public string? Municipio { get; init; }
    public string? Ciudad { get; init; }
    public string? Parroquia { get; init; }
    public string? Sector { get; init; }
    public string? Urbanizacion { get; init; }
    public string? TipoVivienda { get; init; }
    public string? Vialidad { get; init; }
    public string? Vivienda { get; init; }
    public string? TipoNivel { get; init; }
    public string? Nivel { get; init; }
    public string? NroVivienda { get; init; }
    public string? Tenencia { get; init; }
    public string? ComplementoDir { get; init; }
    public string? CodigoPostal { get; init; }
    public string Resumen => string.Join(" | ", new[] {
        TipoDireccion, Vialidad, ComplementoDir, Pais, Estado, Municipio, Ciudad,
        Parroquia, Sector, Urbanizacion, TipoVivienda, Vivienda, TipoNivel, Nivel,
        NroVivienda, Tenencia, CodigoPostal }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public record RhTitBenResponse
{
    public int Id { get; set; }
    public string? CedulaTitular { get; set; }
    public string? CedulaBeneficiario { get; set; }
    public string? ApellidosTituBene { get; set; }
    public string? NombreTituBene { get; set; }
    public string? Sexo { get; set; }
    public DateTime? FechaNacimientoFamiliar { get; set; }
    public string? FechaNacimientoFamiliarString { get; set; }
    public string? EstadoCivil { get; set; }
    public string? CdLocalidad { get; set; }
    public string? CdGrupo { get; set; }
    public string? CdBanco { get; set; }
    public string? NuCuenta { get; set; }
    public string? TpCuenta { get; set; }
    public string? DeEmail { get; set; }
    public string? NroArea { get; set; }
    public string? NroTelefono { get; set; }
    public DateTime? FechaEgreso { get; set; }
    public int? CodigoIcp { get; set; }
    public string? PeriodosVacappDisfru { get; set; }
    public string? TiempoServicio { get; set; }
    public string? Parentesco { get; set; }
    public string? Vinculo { get; set; }
    public string? TipoNomina { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public string? FechaIngresoString { get; set; }
    public string? UnidadDescripcion { get; set; }
    public string? CargoNominal { get; set; }
    public string? AntiguedadCmc { get; set; }
    public string? AntiguedadOtros { get; set; }
    public string? Edad { get; set; }
    public decimal? SueldoBasico { get; set; }
    public decimal? AnosAntiguedadVaca { get; set; }
    public decimal? DiasAntiguedadVaca { get; set; }
    public string? PeriodosVaca { get; set; }
    public int CodigoPersona { get; set; }
    public int? CodigoFamiliar { get; set; }
    public string? EmailFamiliar { get; set; }
    public string? TelefonoFamiliar { get; set; }
    public string EstadoDireccion { get; set; } = "sin informacion";
    public string? DireccionTrabajador { get; set; }
    public RhTitBenDireccion? DireccionSeleccionada { get; set; }
    public List<RhTitBenDireccion> DireccionesTrabajador { get; set; } = [];
    public string SearchText => string.Join(" ", new[] {
        CedulaTitular, CedulaBeneficiario, NombreTituBene, ApellidosTituBene,
        UnidadDescripcion, CargoNominal, TipoNomina, EmailFamiliar, TelefonoFamiliar,
        string.Join(" ", DireccionesTrabajador.Select(d => d.Resumen)) });
}

internal record RhTitBenFamiliar(int Persona, int Familiar, string? Email, string? Telefono);

internal static class RhTitBenDb
{
    internal static string? Text(IDataRecord r, string name)
    {
        var value = r.IsDBNull(r.GetOrdinal(name)) ? null : Convert.ToString(r[name], CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
    internal static int Number(IDataRecord r, string name) => Convert.ToInt32(r[name]);
    internal static DateTime? Date(IDataRecord r, string name)
        => r.IsDBNull(r.GetOrdinal(name)) ? null : Convert.ToDateTime(r[name]);
    internal static string? Duration(DateTime? date)
    {
        if (date is null) return null;
        var start = date.Value.Date;
        var today = DateTime.Today;
        if (start > today) return "";
        var years = today.Year - start.Year;
        if (start.AddYears(years) > today) years--;
        var anchor = start.AddYears(years);
        var months = 0;
        while (months < 11 && anchor.AddMonths(months + 1) <= today) months++;
        var days = (today - anchor.AddMonths(months)).Days;
        return $"{years} Años {months} meses {days} dias";
    }
    internal static RhTitBenResponse Map(IDataRecord r, int id)
    {
        return new()
        {
            Id = id,
            CedulaTitular = Text(r, "CEDULA_TITULAR"),
            CedulaBeneficiario = Text(r, "CEDULA_BENEFICIARIO"),
            ApellidosTituBene = Text(r, "APELLIDOS_TITU_BENE"),
            NombreTituBene = Text(r, "NOMBRES_TITU_BENE"),
            Sexo = Text(r, "SEXO"),
            FechaNacimientoFamiliar = Date(r, "FECHA_NACIMIENTO_FAMILIAR"),
            FechaNacimientoFamiliarString = Date(r, "FECHA_NACIMIENTO_FAMILIAR")?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            EstadoCivil = Text(r, "ESTADO_CIVIL"),
            CdLocalidad = Text(r, "CD_LOCALIDAD"),
            CdGrupo = Text(r, "CD_GRUPO"),
            CdBanco = Text(r, "CD_BANCO"),
            NuCuenta = Text(r, "NU_CUENTA")?.Replace("'", ""),
            TpCuenta = Text(r, "TP_CUENTA"),
            DeEmail = Text(r, "DE_EMAIL"),
            NroArea = Text(r, "NRO_AREA"),
            NroTelefono = Text(r, "NRO_TELEFONO"),
            FechaEgreso = Date(r, "FECHA_EGRESO"),
            CodigoIcp = r.IsDBNull(r.GetOrdinal("CODIGO_ICP")) ? null : Number(r, "CODIGO_ICP"),
            PeriodosVacappDisfru = Text(r, "PERIODOS_VACA_PP_DISFRU"),
            TiempoServicio = Duration(Date(r, "FECHA_INGRESO")),
            Parentesco = Text(r, "PARENTESCO"),
            Vinculo = Text(r, "VINCULO"),
            TipoNomina = Text(r, "TIPO_NOMINA"),
            FechaIngreso = Date(r, "FECHA_INGRESO"),
            FechaIngresoString = Date(r, "FECHA_INGRESO")?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            UnidadDescripcion = Text(r, "UNIDAD_ADSCRIPCION"),
            CargoNominal = Text(r, "CARGO_NOMINAL"),
            AntiguedadCmc = Text(r, "ANTIGUEDAD_CMC"),
            AntiguedadOtros = Text(r, "ANTIGUEDAD_OTROS"),
            Edad = Duration(Date(r, "FECHA_NACIMIENTO_FAMILIAR")),
            SueldoBasico = r.IsDBNull(r.GetOrdinal("SUELDO_BASICO")) ? null : Convert.ToDecimal(r["SUELDO_BASICO"]),
            AnosAntiguedadVaca = r.IsDBNull(r.GetOrdinal("ANOS_ANTIGUEDAD_VACA")) ? null : Convert.ToDecimal(r["ANOS_ANTIGUEDAD_VACA"]),
            DiasAntiguedadVaca = r.IsDBNull(r.GetOrdinal("DIAS_ANTIGUEDAD_VACA")) ? null : Convert.ToDecimal(r["DIAS_ANTIGUEDAD_VACA"]),
            PeriodosVaca = Text(r, "PERIODOS_VACA"),
            CodigoPersona = Number(r, "TIT_PERSONA"),
            CodigoFamiliar = r.IsDBNull(r.GetOrdinal("CODIGO_FAMILIAR")) ? null : Number(r, "CODIGO_FAMILIAR")
        };
    }
    internal static RhTitBenDireccion MapDireccion(IDataRecord r) => new()
    {
        CodigoDireccion = Number(r, "CODIGO_DIRECCION"),
        Principal = Text(r, "PRINCIPAL") == "1",
        TipoDireccion = Text(r, "TIPO_DIRECCION"),
        Pais = Text(r, "PAIS"),
        Estado = Text(r, "ESTADO"),
        Municipio = Text(r, "MUNICIPIO"),
        Ciudad = Text(r, "CIUDAD"),
        Parroquia = Text(r, "PARROQUIA"),
        Sector = Text(r, "SECTOR"),
        Urbanizacion = Text(r, "URBANIZACION"),
        TipoVivienda = Text(r, "TIPO_VIVIENDA"),
        Vialidad = Text(r, "VIALIDAD"),
        Vivienda = Text(r, "VIVIENDA"),
        TipoNivel = Text(r, "TIPO_NIVEL"),
        Nivel = Text(r, "NIVEL"),
        NroVivienda = Text(r, "NRO_VIVIENDA"),
        Tenencia = Text(r, "TENENCIA"),
        ComplementoDir = Text(r, "COMPLEMENTO_DIR"),
        CodigoPostal = Text(r, "CODIGO_POSTAL"),
    };
    internal static void Enrich(List<RhTitBenResponse> rows, List<RhTitBenFamiliar> familiares,
        Dictionary<int, List<RhTitBenDireccion>> direcciones)
    {
        var lookup = familiares.ToLookup(f => (f.Persona, f.Familiar));
        foreach (var row in rows)
        {
            row.EmailFamiliar = null;
            row.TelefonoFamiliar = null;
            // The titular has no family key. Both source keys must match.
            if (row.CodigoFamiliar is > 0)
            {
                var matches = lookup[(row.CodigoPersona, row.CodigoFamiliar.Value)].Take(2).ToArray();
                if (matches.Length == 1)
                {
                    row.EmailFamiliar = matches[0].Email;
                    row.TelefonoFamiliar = matches[0].Telefono;
                }
            }
            row.DireccionesTrabajador = direcciones.GetValueOrDefault(row.CodigoPersona) ?? [];
            var principales = row.DireccionesTrabajador.Where(d => d.Principal).ToArray();
            row.DireccionSeleccionada = principales.Length == 1 ? principales[0]
                : principales.Length == 0 && row.DireccionesTrabajador.Count == 1 ? row.DireccionesTrabajador[0] : null;
            row.EstadoDireccion = row.DireccionSeleccionada is not null ? "disponible"
                : row.DireccionesTrabajador.Count == 0 ? "sin informacion" : "por definir";
            row.DireccionTrabajador = row.DireccionSeleccionada?.Resumen;
        }
    }
}
