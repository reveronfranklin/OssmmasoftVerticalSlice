namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// This comparator never queries Oracle or calculates expected amounts from new results.
public record RhNomContextoHistorico(int CodigoEmpresa, int CodigoTipoNomina, int? CodigoPeriodo, DateTime FechaNomina);
public record RhNomClavePartida(int CodigoPersona, int CodigoConcepto, int? FrecuenciaId, string TipoMovimiento);
public record RhNomPartidaHistorica(RhNomClavePartida Clave, string TipoConcepto, string? Monto);
public record RhNomBaseline(string BaselineId, RhNomContextoHistorico Contexto, bool EntradasCompletas, List<RhNomPartidaHistorica> Partidas);
public record RhNomResultadoCalculado(RhNomContextoHistorico Contexto, List<RhNomPartidaHistorica> Partidas);
public record RhNomDiferencia(RhNomClavePartida? Clave, string Clase, string? Esperado, string? Obtenido, string? Diferencia);
public record RhNomTotales(string Asignaciones, string DeduccionesFirmadas, string Neto);
public record RhNomComparacion(string BaselineId, RhNomContextoHistorico Contexto, string Estado,
    int PartidasEsperadas, int PartidasObtenidas, int Coincidentes, RhNomTotales? TotalesEsperados,
    RhNomTotales? TotalesObtenidos, List<RhNomDiferencia> Diferencias, List<RhNomDiagnostico> Errores);
public record RhNomAmbitoTipo(int CodigoEmpresa, int CodigoTipoNomina);
public record RhNomResumenTipo(RhNomAmbitoTipo Tipo, string Estado, int Casos, int PeriodosDistintos, int Aprobados);

public static class RhNomComparadorHistorico
{
    public const int MaxPartidas = 100000;
    public static RhNomComparacion Comparar(RhNomBaseline baseline, RhNomResultadoCalculado calculated, CancellationToken ct = default)
    {
        RhNomComparacion Fail(string state, string code, string message) =>
            new(baseline.BaselineId, baseline.Contexto, state, baseline.Partidas?.Count ?? 0, calculated.Partidas?.Count ?? 0,
                0, null, null, [], [new(code, message)]);
        if (string.IsNullOrWhiteSpace(baseline.BaselineId) || baseline.BaselineId.Length > 100
            || baseline.Contexto.CodigoEmpresa <= 0 || baseline.Contexto.CodigoTipoNomina <= 0 || baseline.Contexto.FechaNomina == default)
            return Fail("NO_VALIDADO", "CONTEXTO_INVALIDO", "Baseline sin identificacion/contexto verificable.");
        if (baseline.Contexto != calculated.Contexto)
            return Fail("NO_VALIDADO", "CONTEXTO_DISTINTO", "Empresa, tipo, periodo y fecha deben coincidir exactamente.");
        if (!baseline.EntradasCompletas)
            return Fail("DATOS_HISTORICOS_INCOMPLETOS", "ENTRADAS_INCOMPLETAS", "No se puede certificar paridad sin reconstruir las entradas historicas.");
        if (baseline.Partidas is null || calculated.Partidas is null || baseline.Partidas.Count == 0
            || baseline.Partidas.Count > MaxPartidas || calculated.Partidas.Count > MaxPartidas)
            return Fail("NO_VALIDADO", "PARTIDAS_INVALIDAS", "Se requiere una referencia no vacia dentro del limite de partidas.");
        try
        {
            Dictionary<RhNomClavePartida, (string Tipo, decimal Monto)> Index(List<RhNomPartidaHistorica> rows)
            {
                var index = new Dictionary<RhNomClavePartida, (string, decimal)>();
                foreach (var row in rows)
                {
                    ct.ThrowIfCancellationRequested();
                    if (row is null || row.Clave is null || row.Clave.CodigoPersona <= 0 || row.Clave.CodigoConcepto <= 0
                        || row.Clave.FrecuenciaId is <= 0 || row.Clave.TipoMovimiento is not ("F" or "V") || row.TipoConcepto is not ("A" or "D"))
                        throw new RhNomError("PARTIDA_INVALIDA", "Clave, tipo de movimiento o tipo de concepto no soportados en el prototipo.");
                    if (!index.TryAdd(row.Clave, (row.TipoConcepto, RhNomNumeros.Leer(row.Monto))))
                        throw new RhNomError("PARTIDA_DUPLICADA", "Hay partidas repetidas: no se consolidan por SUM para ocultar diferencias.");
                }
                return index;
            }
            var expected = Index(baseline.Partidas); var actual = Index(calculated.Partidas);
            var differences = new List<RhNomDiferencia>(); var matches = 0;
            foreach (var (key, wanted) in expected)
            {
                ct.ThrowIfCancellationRequested();
                if (!actual.TryGetValue(key, out var got))
                    differences.Add(new(key, "FALTANTE", RhNomNumeros.Texto(wanted.Monto), null, null));
                else if (wanted.Tipo != got.Tipo)
                    differences.Add(new(key, "TIPO_CONCEPTO_DISTINTO", wanted.Tipo, got.Tipo, null));
                else if (wanted.Monto != got.Monto)
                    differences.Add(new(key, Math.Sign(wanted.Monto) != Math.Sign(got.Monto) && wanted.Monto != 0 && got.Monto != 0 ? "SIGNO_DISTINTO" : "MONTO_DISTINTO",
                        RhNomNumeros.Texto(wanted.Monto), RhNomNumeros.Texto(got.Monto), RhNomNumeros.Texto(checked(got.Monto - wanted.Monto))));
                else matches++;
            }
            foreach (var (key, got) in actual)
                if (!expected.ContainsKey(key)) differences.Add(new(key, "ADICIONAL", null, RhNomNumeros.Texto(got.Monto), null));
            RhNomTotales Total(Dictionary<RhNomClavePartida, (string Tipo, decimal Monto)> rows)
            {
                decimal asignaciones = 0, deducciones = 0;
                foreach (var row in rows.Values)
                {
                    ct.ThrowIfCancellationRequested();
                    if (row.Tipo == "A") asignaciones = checked(asignaciones + row.Monto);
                    else deducciones = checked(deducciones + row.Monto);
                }
                return new(RhNomNumeros.Texto(asignaciones), RhNomNumeros.Texto(deducciones), RhNomNumeros.Texto(checked(asignaciones + deducciones)));
            }
            var expectedTotals = Total(expected); var actualTotals = Total(actual);
            return new(baseline.BaselineId, baseline.Contexto, differences.Count == 0 ? "APROBADO" : "DIFERENCIAS",
                expected.Count, actual.Count, matches, expectedTotals, actualTotals, differences, []);
        }
        catch (RhNomError ex) { return Fail("NO_VALIDADO", ex.Diagnostico.Codigo, ex.Diagnostico.Mensaje); }
        catch (OverflowException) { return Fail("NO_VALIDADO", "DESBORDAMIENTO", "Importe o suma fuera del rango decimal admitido."); }
    }
    public static List<RhNomResumenTipo> Resumir(IReadOnlyList<RhNomAmbitoTipo> scope, IReadOnlyList<RhNomComparacion> cases)
    {
        return scope.Distinct().Select(type =>
        {
            var rows = cases.Where(c => c.Contexto.CodigoEmpresa == type.CodigoEmpresa && c.Contexto.CodigoTipoNomina == type.CodigoTipoNomina).ToList();
            var periods = rows.Select(c => c.Contexto.CodigoPeriodo is not null ? "P:" + c.Contexto.CodigoPeriodo : "F:" + c.Contexto.FechaNomina.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).Distinct().Count();
            var approved = rows.Count(c => c.Estado == "APROBADO" && c.Errores.Count == 0 && c.Diferencias.Count == 0
                && c.PartidasEsperadas > 0 && c.PartidasEsperadas == c.Coincidentes && c.PartidasObtenidas == c.Coincidentes
                && c.TotalesEsperados is not null && c.TotalesEsperados == c.TotalesObtenidos);
            var duplicates = rows.Select(c => c.BaselineId).Distinct(StringComparer.Ordinal).Count() != rows.Count;
            // Two periods are a minimum gate, not proof of complete functional coverage.
            return new RhNomResumenTipo(type, rows.Count >= 2 && periods >= 2 && approved == rows.Count && !duplicates ? "PARIDAD_CASOS_APROBADA" : "NO_VALIDADO", rows.Count, periods, approved);
        }).ToList();
    }
}
