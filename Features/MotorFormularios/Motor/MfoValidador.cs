using System.Globalization;
using System.Text.RegularExpressions;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Interprete C# de <c>MFO_REGLA</c> y de las validaciones estructurales.
/// **Es la autoridad**: el interprete TypeScript de la Fase 6 existe para la
/// experiencia de usuario, pero el endpoint de respuestas es generico -acepta
/// pares clave/valor arbitrarios- y un cliente manipulado puede enviar
/// cualquier cosa. Si esta clase se equivoca, el motor es una puerta abierta.
///
/// No toca base de datos: recibe la definicion (de la cache) y el payload, y
/// devuelve errores. Esa pureza es deliberada: es lo que permite probarlo
/// exhaustivamente sin Oracle, contra la misma tabla de casos que usara el
/// interprete TypeScript (Casos-Validacion.md).
///
/// La regla UNICO NO se evalua aqui: es la unica que necesita consultar la base
/// y vive en SP_MFO_RESP_SUBMIT.
///
/// Dos momentos distintos:
///   Guardado de borrador  -> solo validacion estructural. Exigir REQUERIDO en
///                            un borrador haria imposible guardar a medias, que
///                            es justamente para lo que existe un borrador.
///   Envio                 -> estructural + reglas + condiciones.
/// </summary>
public static class MfoValidador
{
    /// <summary>
    /// Tope de una regex de patron. Las escribe el diseñador del formulario, y
    /// una regex catastrofica en el servidor es una denegacion de servicio
    /// barata de provocar. El timeout acota el dano sin prohibir el uso.
    /// </summary>
    private static readonly TimeSpan TimeoutPatron = TimeSpan.FromMilliseconds(100);

    private const int MaxLongitudPatron = 500;

    public static List<MfoErrorValidacion> ValidarBorrador(
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> valores)
    {
        return ValidarEstructura(definicion, valores);
    }

    public static List<MfoErrorValidacion> ValidarEnvio(
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> valores)
    {
        var errores = ValidarEstructura(definicion, valores);

        // Si la estructura no cuadra, aplicar reglas encima solo produciria
        // ruido sobre datos que ya se van a rechazar.
        if (errores.Count > 0)
        {
            return errores;
        }

        var estado = MfoEvaluadorCondiciones.Evaluar(definicion, valores);
        errores.AddRange(ValidarReglas(definicion, valores, estado));
        errores.AddRange(ValidarFilas(definicion, valores));

        return errores;
    }

    // ------------------------------------------------------------------------
    // Estructura
    // ------------------------------------------------------------------------

    private static List<MfoErrorValidacion> ValidarEstructura(
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> valores)
    {
        var errores = new List<MfoErrorValidacion>();
        var indice = Indexar(definicion);

        foreach (var v in valores)
        {
            if (!indice.TryGetValue(v.Clave ?? string.Empty, out var ctx))
            {
                errores.Add(new MfoErrorValidacion(v.Clave ?? string.Empty, v.Fila, v.Orden,
                    "CLAVE_DESCONOCIDA",
                    $"El campo {v.Clave} no existe en esta version del formulario."));
                continue;
            }

            var campo = ctx.Campo;

            if (campo.EsPresentacion)
            {
                errores.Add(new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden,
                    "NO_CAPTURA",
                    $"{campo.Etiqueta} es un elemento de presentacion y no captura valor."));
                continue;
            }

            // Un campo de solo lectura puede mostrar un valor calculado por el
            // servidor, pero nunca acepta el que mande el cliente.
            if (campo.SoloLectura)
            {
                errores.Add(new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden,
                    "SOLO_LECTURA",
                    $"{campo.Etiqueta} es de solo lectura."));
                continue;
            }

            if (v.Orden > 0 && !campo.AdmiteMultiple)
            {
                errores.Add(new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden,
                    "NO_MULTIVALOR",
                    $"{campo.Etiqueta} no admite multiples valores."));
                continue;
            }

            if (v.Fila > 0 && !ctx.Seccion.Repetible)
            {
                errores.Add(new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden,
                    "NO_REPETIBLE",
                    $"{campo.Etiqueta} no pertenece a una seccion repetible."));
                continue;
            }

            if (v.Fila < 0 || v.Orden < 0)
            {
                errores.Add(new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden,
                    "UBICACION_INVALIDA",
                    $"La ubicacion del valor de {campo.Etiqueta} no es valida."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(v.Valor))
            {
                // Un valor vacio es legitimo: significa "sin dato". La
                // obligatoriedad la deciden las reglas, no la estructura.
                continue;
            }

            var errorTipo = ValidarTipo(campo, v);
            if (errorTipo is not null)
            {
                errores.Add(errorTipo);
                continue;
            }

            var errorOpcion = ValidarOpcion(ctx, v);
            if (errorOpcion is not null)
            {
                errores.Add(errorOpcion);
            }
        }

        return errores;
    }

    private static MfoErrorValidacion? ValidarTipo(MfoCampoResponse campo, MfoValorRequest v)
    {
        switch (campo.ColumnaValor?.ToUpperInvariant())
        {
            case "NUM":
                if (!decimal.TryParse(v.Valor, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    return new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden, "TIPO_NUMERO",
                        $"{campo.Etiqueta} debe ser un numero.");
                }
                break;

            case "FEC":
                if (!TryParseFecha(v.Valor, out _))
                {
                    return new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden, "TIPO_FECHA",
                        $"{campo.Etiqueta} debe ser una fecha valida.");
                }
                break;

            case "TXT":
                // VALOR_TXT es VARCHAR2(4000). Un texto mas largo no se trunca en
                // silencio: se rechaza. Truncar seria perder dato del usuario sin
                // que nadie se entere. Los tipos que admiten texto largo declaran
                // COLUMNA_VALOR='CLB' y no pasan por aqui.
                if (v.Valor!.Length > 4000)
                {
                    return new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden, "LARGO_EXCEDIDO",
                        $"{campo.Etiqueta} excede los 4000 caracteres que admite su tipo.");
                }
                break;
        }

        return null;
    }

    private static MfoErrorValidacion? ValidarOpcion(CampoContexto ctx, MfoValorRequest v)
    {
        var campo = ctx.Campo;

        if (!campo.AdmiteOpciones)
        {
            return null;
        }

        // Los campos de catalogo se resuelven contra datos vivos del ERP: sus
        // opciones no estan en MFO_OPCION y no se pueden comprobar aqui. Los
        // valida el resolutor de catalogos en su propio endpoint.
        if (string.Equals(campo.OrigenOpciones, "CATALOGO", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var permitido = ctx.Opciones.Any(o =>
            o.Activo && string.Equals(o.Valor, v.Valor, StringComparison.Ordinal));

        return permitido
            ? null
            : new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden, "OPCION_INVALIDA",
                $"El valor indicado no es una opcion valida de {campo.Etiqueta}.");
    }

    /// <summary>
    /// MIN_FILAS / MAX_FILAS de las secciones repetibles. Se comprueba solo al
    /// enviar: un borrador a medio llenar puede tener menos filas del minimo.
    /// </summary>
    private static List<MfoErrorValidacion> ValidarFilas(
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> valores)
    {
        var errores = new List<MfoErrorValidacion>();
        var clavesPorSeccion = definicion.Secciones.ToDictionary(
            s => s.Seccion.Clave,
            s => s.Campos.Select(c => c.Campo.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var s in definicion.Secciones.Where(x => x.Seccion.Repetible))
        {
            var claves = clavesPorSeccion[s.Seccion.Clave];

            var filas = valores
                .Where(v => claves.Contains(v.Clave) && !string.IsNullOrWhiteSpace(v.Valor))
                .Select(v => v.Fila)
                .Distinct()
                .Count();

            if (s.Seccion.MinFilas is int min && filas < min)
            {
                errores.Add(new MfoErrorValidacion(s.Seccion.Clave, 0, 0, "MIN_FILAS",
                    $"{s.Seccion.Titulo ?? s.Seccion.Clave} requiere al menos {min} fila(s)."));
            }

            if (s.Seccion.MaxFilas is int max && filas > max)
            {
                errores.Add(new MfoErrorValidacion(s.Seccion.Clave, 0, 0, "MAX_FILAS",
                    $"{s.Seccion.Titulo ?? s.Seccion.Clave} admite como maximo {max} fila(s)."));
            }
        }

        return errores;
    }

    // ------------------------------------------------------------------------
    // Reglas
    // ------------------------------------------------------------------------

    private static List<MfoErrorValidacion> ValidarReglas(
        MfoDefinicionResponse definicion,
        IReadOnlyList<MfoValorRequest> valores,
        MfoEstadoCondiciones estado)
    {
        var errores = new List<MfoErrorValidacion>();

        foreach (var seccion in definicion.Secciones)
        {
            // Las filas presentes de una seccion repetible. Las reglas se aplican
            // por fila: en una grilla de tres lineas, un campo obligatorio lo es
            // en las tres.
            var filas = FilasPresentes(seccion, valores);

            foreach (var campoDef in seccion.Campos)
            {
                var campo = campoDef.Campo;

                if (campo.EsPresentacion)
                {
                    continue;
                }

                // Un campo oculto por condicion no se valida. Es la interaccion
                // que hace usable la logica condicional: sin esto, un formulario
                // con ramas queda imposible de enviar.
                if (!estado.EsVisible(campo.Clave))
                {
                    continue;
                }

                var exigidoPorCondicion = estado.EsExigido(campo.Clave);

                foreach (var fila in filas)
                {
                    var delCampo = valores
                        .Where(v => string.Equals(v.Clave, campo.Clave, StringComparison.OrdinalIgnoreCase)
                                    && v.Fila == fila
                                    && !string.IsNullOrWhiteSpace(v.Valor))
                        .OrderBy(v => v.Orden)
                        .ToList();

                    errores.AddRange(ValidarCampo(campoDef, fila, delCampo, exigidoPorCondicion));
                }
            }
        }

        return errores;
    }

    private static List<int> FilasPresentes(MfoSeccionDefinicion seccion, IReadOnlyList<MfoValorRequest> valores)
    {
        if (!seccion.Seccion.Repetible)
        {
            return [0];
        }

        var claves = seccion.Campos.Select(c => c.Campo.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filas = valores
            .Where(v => claves.Contains(v.Clave))
            .Select(v => v.Fila)
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        // Una seccion repetible sin ningun valor sigue teniendo que validar sus
        // obligatorios si declara un minimo de filas.
        return filas.Count > 0 ? filas : (seccion.Seccion.MinFilas is > 0 ? [1] : []);
    }

    private static List<MfoErrorValidacion> ValidarCampo(
        MfoCampoDefinicion campoDef,
        int fila,
        List<MfoValorRequest> valores,
        bool exigidoPorCondicion)
    {
        var errores = new List<MfoErrorValidacion>();
        var campo = campoDef.Campo;
        var vacio = valores.Count == 0;

        var requerido = campo.Requerido
            || exigidoPorCondicion
            || campoDef.Reglas.Any(r => r.Activo && EsRegla(r, "REQUERIDO"));

        if (requerido && vacio)
        {
            var mensaje = campoDef.Reglas.FirstOrDefault(r => r.Activo && EsRegla(r, "REQUERIDO"))?.Mensaje;
            errores.Add(new MfoErrorValidacion(campo.Clave, fila, 0, "REQUERIDO",
                string.IsNullOrWhiteSpace(mensaje) ? $"{campo.Etiqueta} es obligatorio." : mensaje));
            return errores;
        }

        if (vacio)
        {
            // Las demas reglas no aplican a un campo sin valor: un LONG_MIN sobre
            // un opcional vacio lo convertiria en obligatorio por la puerta de
            // atras.
            return errores;
        }

        foreach (var regla in campoDef.Reglas.Where(r => r.Activo).OrderBy(r => r.Orden))
        {
            var tipo = (regla.TipoRegla ?? string.Empty).ToUpperInvariant();

            // Reglas de conjunto: se evaluan una vez sobre todas las ocurrencias.
            if (tipo is "SEL_MIN" or "SEL_MAX")
            {
                var error = ValidarSeleccion(campo, regla, tipo, fila, valores.Count);
                if (error is not null) errores.Add(error);
                continue;
            }

            if (tipo is "REQUERIDO" or "UNICO")
            {
                // REQUERIDO ya se resolvio arriba. UNICO necesita la base y lo
                // evalua SP_MFO_RESP_SUBMIT.
                continue;
            }

            foreach (var v in valores)
            {
                var error = ValidarValor(campo, regla, tipo, v);
                if (error is not null) errores.Add(error);
            }
        }

        return errores;
    }

    private static MfoErrorValidacion? ValidarSeleccion(
        MfoCampoResponse campo, MfoReglaResponse regla, string tipo, int fila, int cantidad)
    {
        if (!int.TryParse(regla.Param1, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limite))
        {
            return null;
        }

        var falla = tipo == "SEL_MIN" ? cantidad < limite : cantidad > limite;

        return falla
            ? new MfoErrorValidacion(campo.Clave, fila, 0, tipo, Mensaje(regla,
                tipo == "SEL_MIN"
                    ? $"Seleccione al menos {limite} opcion(es) en {campo.Etiqueta}."
                    : $"Seleccione como maximo {limite} opcion(es) en {campo.Etiqueta}."))
            : null;
    }

    private static MfoErrorValidacion? ValidarValor(
        MfoCampoResponse campo, MfoReglaResponse regla, string tipo, MfoValorRequest v)
    {
        var valor = v.Valor ?? string.Empty;

        switch (tipo)
        {
            case "LONG_MIN":
                if (int.TryParse(regla.Param1, out var lmin) && valor.Length < lmin)
                {
                    return Error(campo, v, tipo, regla, $"{campo.Etiqueta} debe tener al menos {lmin} caracteres.");
                }
                break;

            case "LONG_MAX":
                if (int.TryParse(regla.Param1, out var lmax) && valor.Length > lmax)
                {
                    return Error(campo, v, tipo, regla, $"{campo.Etiqueta} admite como maximo {lmax} caracteres.");
                }
                break;

            case "PATRON":
                if (!CumplePatron(regla.Param1, valor))
                {
                    return Error(campo, v, tipo, regla, $"{campo.Etiqueta} no tiene el formato esperado.");
                }
                break;

            case "MIN":
                if (decimal.TryParse(regla.Param1, NumberStyles.Any, CultureInfo.InvariantCulture, out var nmin)
                    && decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var num1)
                    && num1 < nmin)
                {
                    return Error(campo, v, tipo, regla, $"{campo.Etiqueta} no puede ser menor que {regla.Param1}.");
                }
                break;

            case "MAX":
                if (decimal.TryParse(regla.Param1, NumberStyles.Any, CultureInfo.InvariantCulture, out var nmax)
                    && decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var num2)
                    && num2 > nmax)
                {
                    return Error(campo, v, tipo, regla, $"{campo.Etiqueta} no puede ser mayor que {regla.Param1}.");
                }
                break;

            case "DECIMALES":
                if (int.TryParse(regla.Param1, out var escala) && Decimales(valor) > escala)
                {
                    return Error(campo, v, tipo, regla,
                        $"{campo.Etiqueta} admite como maximo {escala} decimal(es).");
                }
                break;

            case "FEC_MIN":
                if (TryParseLimiteFecha(regla.Param1, out var fmin)
                    && TryParseFecha(valor, out var f1)
                    && f1.Date < fmin.Date)
                {
                    return Error(campo, v, tipo, regla,
                        $"{campo.Etiqueta} no puede ser anterior a {fmin:dd/MM/yyyy}.");
                }
                break;

            case "FEC_MAX":
                if (TryParseLimiteFecha(regla.Param1, out var fmax)
                    && TryParseFecha(valor, out var f2)
                    && f2.Date > fmax.Date)
                {
                    return Error(campo, v, tipo, regla,
                        $"{campo.Etiqueta} no puede ser posterior a {fmax:dd/MM/yyyy}.");
                }
                break;

            case "ARCH_MAX_MB":
            case "ARCH_EXT":
                // Los adjuntos no viajan en el payload de valores: se suben por
                // su propio endpoint, que es donde se comprueban tamaño y
                // extension. Aqui el valor es solo el nombre del archivo.
                break;
        }

        return null;
    }

    // ------------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------------

    private static bool EsRegla(MfoReglaResponse r, string tipo)
    {
        return string.Equals(r.TipoRegla, tipo, StringComparison.OrdinalIgnoreCase);
    }

    private static string Mensaje(MfoReglaResponse regla, string porDefecto)
    {
        return string.IsNullOrWhiteSpace(regla.Mensaje) ? porDefecto : regla.Mensaje;
    }

    private static MfoErrorValidacion Error(
        MfoCampoResponse campo, MfoValorRequest v, string codigo, MfoReglaResponse regla, string porDefecto)
    {
        return new MfoErrorValidacion(campo.Clave, v.Fila, v.Orden, codigo, Mensaje(regla, porDefecto));
    }

    /// <summary>
    /// Evalua una regex del diseñador con timeout y tope de longitud. Si el
    /// patron es demasiado largo o la evaluacion se pasa de tiempo, la regla se
    /// considera **no cumplida**: es preferible rechazar un valor de mas que
    /// dejar pasar uno que la regla queria bloquear, y ademas asi el problema se
    /// nota en vez de degradarse en silencio.
    /// </summary>
    private static bool CumplePatron(string? patron, string valor)
    {
        if (string.IsNullOrWhiteSpace(patron))
        {
            return true;
        }

        if (patron.Length > MaxLongitudPatron)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(valor, patron, RegexOptions.None, TimeoutPatron);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            // Patron mal formado. No deberia llegar aqui -el diseñador tendria
            // que haberlo probado- pero un patron invalido no puede tumbar el
            // envio con una excepcion.
            return false;
        }
    }

    private static int Decimales(string valor)
    {
        var punto = valor.IndexOf('.');
        return punto < 0 ? 0 : valor.Length - punto - 1;
    }

    private static bool TryParseFecha(string? valor, out DateTime fecha)
    {
        // ISO primero: es lo que manda el frontend. El resto de formatos se
        // aceptan por tolerancia, con cultura invariante para que el mismo
        // payload se interprete igual en cualquier servidor.
        return DateTime.TryParseExact(valor, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"],
                   CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
            || DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }

    /// <summary>
    /// El limite de FEC_MIN / FEC_MAX puede ser una fecha o el token HOY.
    /// </summary>
    private static bool TryParseLimiteFecha(string? param, out DateTime fecha)
    {
        if (string.Equals(param?.Trim(), "HOY", StringComparison.OrdinalIgnoreCase))
        {
            fecha = DateTime.Today;
            return true;
        }

        return TryParseFecha(param, out fecha);
    }

    private sealed record CampoContexto(
        MfoCampoResponse Campo,
        MfoSeccionResponse Seccion,
        List<MfoOpcionResponse> Opciones);

    private static Dictionary<string, CampoContexto> Indexar(MfoDefinicionResponse definicion)
    {
        var indice = new Dictionary<string, CampoContexto>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in definicion.Secciones)
        {
            foreach (var c in s.Campos)
            {
                indice[c.Campo.Clave] = new CampoContexto(c.Campo, s.Seccion, c.Opciones);
            }
        }

        return indice;
    }
}
