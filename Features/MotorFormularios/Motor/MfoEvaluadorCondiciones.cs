using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Estado calculado de visibilidad, exigencia y bloqueo de cada campo, dado un
/// payload concreto. Es de solo lectura para el consumidor.
/// </summary>
public sealed class MfoEstadoCondiciones
{
    private readonly HashSet<string> _ocultos;
    private readonly HashSet<string> _exigidos;
    private readonly HashSet<string> _bloqueados;

    internal MfoEstadoCondiciones(
        HashSet<string> ocultos, HashSet<string> exigidos, HashSet<string> bloqueados)
    {
        _ocultos = ocultos;
        _exigidos = exigidos;
        _bloqueados = bloqueados;
    }

    public bool EsVisible(string clave) => !_ocultos.Contains(clave);
    public bool EsExigido(string clave) => _exigidos.Contains(clave);
    public bool EsBloqueado(string clave) => _bloqueados.Contains(clave);
}

/// <summary>
/// Evalua <c>MFO_CONDICION</c> para decidir que campos estan visibles, exigidos
/// o bloqueados con el payload dado.
///
/// **El validador tiene que consultarlo antes de aplicar REQUERIDO.** Un campo
/// oculto por condicion no puede ser obligatorio: sin esto, cualquier formulario
/// con ramas condicionales queda imposible de enviar, porque el usuario nunca ve
/// el campo que le estan exigiendo.
///
/// Semantica de cada accion:
///   MOSTRAR  - el destino esta OCULTO por defecto y aparece si se cumple. Es la
///              forma natural de expresar "solo si aplica".
///   OCULTAR  - el destino esta visible por defecto y se oculta si se cumple.
///   EXIGIR   - el destino pasa a obligatorio si se cumple.
///   BLOQUEAR - el destino pasa a solo lectura si se cumple.
///
/// Dentro de un GRUPO, las condiciones se combinan con el CONECTOR de cada fila
/// ('Y' / 'O'). Grupos distintos sobre el mismo destino y accion se combinan con
/// O: cada grupo es una razon independiente para que la accion aplique.
///
/// Un destino de tipo SECCION propaga la accion a todos sus campos.
///
/// Este es el espejo C# de evaluadorCondiciones.ts (Fase 6). Los dos tienen que
/// dar el mismo veredicto sobre los casos de Casos-Validacion.md.
/// </summary>
public static class MfoEvaluadorCondiciones
{
    private sealed record Arista(string Destino, string Accion, int Grupo, MfoCondicionResponse Condicion);

    public static MfoEstadoCondiciones Evaluar(
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> valores)
    {
        var ocultos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exigidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bloqueados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (definicion.Condiciones.Count == 0)
        {
            return new MfoEstadoCondiciones(ocultos, exigidos, bloqueados);
        }

        var porClave = AgruparValores(valores);
        var camposPorSeccion = definicion.Secciones.ToDictionary(
            s => s.Seccion.Clave,
            s => s.Campos.Select(c => c.Campo.Clave).ToList(),
            StringComparer.OrdinalIgnoreCase);

        // Se aplana a (destino concreto, accion, grupo): un destino de tipo
        // SECCION se convierte aqui en una arista por cada campo suyo, y a
        // partir de ese punto el resto del algoritmo no tiene que volver a
        // distinguir entre campo y seccion.
        var aristas = new List<Arista>();
        foreach (var c in definicion.Condiciones)
        {
            var accion = (c.Accion ?? string.Empty).ToUpperInvariant();
            foreach (var destino in ResolverDestinos(c, camposPorSeccion))
            {
                aristas.Add(new Arista(destino, accion, c.Grupo, c));
            }
        }

        foreach (var porDestinoAccion in aristas.GroupBy(a => (a.Destino, a.Accion)))
        {
            // Cada GRUPO se evalua por separado; basta con que uno se cumpla.
            var algunGrupoCumple = porDestinoAccion
                .GroupBy(a => a.Grupo)
                .Any(g => EvaluarGrupo(g.Select(a => a.Condicion).OrderBy(c => c.Orden).ToList(), porClave));

            var destino = porDestinoAccion.Key.Destino;

            switch (porDestinoAccion.Key.Accion)
            {
                // MOSTRAR es la unica que invierte el valor por defecto: el
                // destino esta oculto salvo que alguna de sus condiciones se
                // cumpla.
                case "MOSTRAR":
                    if (!algunGrupoCumple) ocultos.Add(destino);
                    break;

                case "OCULTAR":
                    if (algunGrupoCumple) ocultos.Add(destino);
                    break;

                case "EXIGIR":
                    if (algunGrupoCumple) exigidos.Add(destino);
                    break;

                case "BLOQUEAR":
                    if (algunGrupoCumple) bloqueados.Add(destino);
                    break;
            }
        }

        return new MfoEstadoCondiciones(ocultos, exigidos, bloqueados);
    }

    /// <summary>
    /// Valores por clave de campo. Se ignora FILA a proposito: una condicion se
    /// evalua sobre el campo, no sobre una fila concreta de una seccion
    /// repetible. Soportar condiciones por fila exigiria que MFO_CONDICION
    /// supiera de filas, y el modelo no lo contempla.
    /// </summary>
    private static Dictionary<string, List<string>> AgruparValores(IReadOnlyList<MfoValorRequest> valores)
    {
        var porClave = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in valores)
        {
            if (!porClave.TryGetValue(v.Clave, out var lista))
            {
                lista = [];
                porClave[v.Clave] = lista;
            }

            if (!string.IsNullOrWhiteSpace(v.Valor))
            {
                lista.Add(v.Valor);
            }
        }

        return porClave;
    }

    private static List<string> ResolverDestinos(
        MfoCondicionResponse c,
        Dictionary<string, List<string>> camposPorSeccion)
    {
        if (string.Equals(c.DestinoTipo, "SECCION", StringComparison.OrdinalIgnoreCase))
        {
            return camposPorSeccion.TryGetValue(c.ClaveDestino ?? string.Empty, out var campos)
                ? campos
                : [];
        }

        return string.IsNullOrWhiteSpace(c.ClaveDestino) ? [] : [c.ClaveDestino];
    }

    /// <summary>
    /// Combina las condiciones de un grupo. El CONECTOR de cada fila indica como
    /// se une con la ANTERIOR; el de la primera se ignora.
    /// </summary>
    private static bool EvaluarGrupo(
        List<MfoCondicionResponse> condiciones,
        Dictionary<string, List<string>> porClave)
    {
        if (condiciones.Count == 0)
        {
            return false;
        }

        var acumulado = EvaluarUna(condiciones[0], porClave);

        for (var i = 1; i < condiciones.Count; i++)
        {
            var actual = EvaluarUna(condiciones[i], porClave);
            acumulado = string.Equals(condiciones[i].Conector, "O", StringComparison.OrdinalIgnoreCase)
                ? acumulado || actual
                : acumulado && actual;
        }

        return acumulado;
    }

    private static bool EvaluarUna(
        MfoCondicionResponse c,
        Dictionary<string, List<string>> porClave)
    {
        var valores = porClave.TryGetValue(c.ClaveOrigen ?? string.Empty, out var lista) ? lista : [];
        var comparar = c.ValorCompara ?? string.Empty;

        return (c.Operador ?? string.Empty).ToUpperInvariant() switch
        {
            "VACIO" => valores.Count == 0,
            "NO_VACIO" => valores.Count > 0,

            // En un campo multivalor basta con que UNO de los valores cumpla:
            // "si marco Otros" tiene que activarse aunque haya marcado tres cosas.
            "IGUAL" => valores.Any(v => string.Equals(v, comparar, StringComparison.OrdinalIgnoreCase)),
            "CONTIENE" => valores.Any(v => v.Contains(comparar, StringComparison.OrdinalIgnoreCase)),
            "EN_LISTA" => comparar.Split('|', StringSplitOptions.RemoveEmptyEntries)
                                  .Any(op => valores.Any(v => string.Equals(v, op.Trim(), StringComparison.OrdinalIgnoreCase))),

            // DISTINTO se cumple tambien cuando el campo esta vacio: "distinto de
            // A" es cierto si no hay nada. Lo contrario obligaria a escribir dos
            // condiciones para el caso mas comun.
            "DISTINTO" => !valores.Any(v => string.Equals(v, comparar, StringComparison.OrdinalIgnoreCase)),

            "MAYOR" => Comparar(valores, comparar, (a, b) => a > b),
            "MENOR" => Comparar(valores, comparar, (a, b) => a < b),

            _ => false
        };
    }

    /// <summary>
    /// MAYOR y MENOR comparan como numero cuando ambos lados lo son, y como
    /// fecha cuando ambos lo son. Si no se puede interpretar ninguno de los dos,
    /// la condicion no se cumple: es preferible que una rama no se active a que
    /// se active por una comparacion de texto que nadie pidio.
    /// </summary>
    private static bool Comparar(List<string> valores, string comparar, Func<decimal, decimal, bool> op)
    {
        foreach (var v in valores)
        {
            if (decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var izq)
                && decimal.TryParse(comparar, NumberStyles.Any, CultureInfo.InvariantCulture, out var der))
            {
                if (op(izq, der)) return true;
                continue;
            }

            if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fIzq)
                && DateTime.TryParse(comparar, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fDer))
            {
                if (op(fIzq.Ticks, fDer.Ticks)) return true;
            }
        }

        return false;
    }
}
