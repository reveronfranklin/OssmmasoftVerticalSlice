using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.BienesMunicipales;
using OssmmasoftVerticalSlice.Features.ReporteBm1;
using OssmmasoftVerticalSlice.Features.ReporteChequesPeriodo;
using OssmmasoftVerticalSlice.Features.ReporteRelacionCompromiso;
using OssmmasoftVerticalSlice.Features.ReporteRelacionRetencionIva;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Todo lo que un delegado de ejecucion necesita. Se le entrega ya resuelto:
/// el delegado no lee el request, no consulta MFO_REP_PARAM y no decide
/// permisos. Recibe parametros validados y devuelve un documento.
/// </summary>
public sealed record MfoContextoEjecucion(
    ConnectionDB Conexiones,
    IConfiguration Config,
    IWebHostEnvironment Ambiente,
    MfoReporteResponse Reporte,
    IReadOnlyDictionary<string, MfoParametroResuelto> Parametros,
    CancellationToken Cancelacion)
{
    public MfoParametroResuelto? Param(string nombre) =>
        Parametros.TryGetValue(nombre, out var p) ? p : null;
}

/// <summary>
/// Lo que produjo una ejecucion.
///
/// <c>Resultado</c> usa el dominio de <c>MFO_REP_EJEC.RESULTADO</c>:
/// OK / VACIO / TRUNCADO / ERROR. VACIO y TRUNCADO no son errores tecnicos y
/// merecen un mensaje propio al usuario -"no hubo datos con esos parametros" y
/// "se alcanzo el limite de N filas"-, porque un error generico ahi hace que la
/// gente reintente lo mismo esperando otro resultado.
/// </summary>
public sealed record MfoResultadoEjecucion(
    byte[]? Documento,
    string NombreArchivo,
    int Filas,
    string Resultado,
    string Mensaje)
{
    public static MfoResultadoEjecucion Vacio(string mensaje) =>
        new(null, string.Empty, 0, "VACIO", mensaje);

    public static MfoResultadoEjecucion Error(string mensaje) =>
        new(null, string.Empty, 0, "ERROR", mensaje);
}

/// <summary>
/// **La lista blanca de reportes ejecutables.** Es el unico punto del sistema
/// autorizado a decidir que se ejecuta.
///
/// El riesgo que esta clase existe para contener es el mas severo de todo el
/// requerimiento: un motor que ejecuta lo que dice una fila de base de datos es
/// ejecucion arbitraria configurable. <c>MFO_REPORTE.CLAVE_REGISTRO</c> no es
/// una ruta, ni un nombre de procedimiento, ni nada que se invoque: es **una
/// clave de busqueda en este diccionario**. Si no esta registrada, la ejecucion
/// falla con <c>IsValid = false</c> y no se invoca absolutamente nada.
///
/// De ahi salen las dos reglas que no se negocian:
///   1. Nunca construir el delegado a partir del texto que viene de la base
///      (nada de reflexion por nombre, nada de invocar un procedimiento cuyo
///      nombre venga de una fila).
///   2. Nunca hacer que una clave desconocida caiga en un camino "por defecto"
///      que intente adivinar. Desconocida = rechazada.
///
/// Agregar un reporte **requiere despliegue**. Es deliberado y conviene decirlo
/// en voz alta cuando se presente el modulo: lo que el motor elimina es el
/// trabajo repetido de construir la pantalla de parametros, no el control sobre
/// que codigo corre en el servidor.
/// </summary>
public static class MfoRegistroReportes
{
    private delegate Task<MfoResultadoEjecucion> Ejecutor(MfoContextoEjecucion contexto);

    private static readonly Dictionary<string, Ejecutor> Registro =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Decision 8 de la Fase 0: el primer caso va por ENDPOINT contra un
            // reporte que ya existe, para validar el mapeo de parametros de punta
            // a punta antes de construir el generador tabular generico.
            ["REPORTE_BM1_PDF"] = EjecutarReporteBm1Pdf,

            // Placas de bienes. Comparte los parametros de REPORTE_BM1_PDF -las
            // mismas fechas y las mismas unidades- y por eso cuelga del mismo
            // formulario: MFO_REPORTE admite N reportes por formulario
            // precisamente para este caso.
            ["REPORTE_BM1_PLACAS_PDF"] = EjecutarReporteBm1PlacasPdf,

            // Formulario oficial BM-1 (requerimiento 27). Comparte el query de
            // negocio con REPORTE_BM1_PDF pero expone filtros propios, asi que
            // cuelga de su propio formulario de parametros.
            ["REPORTE_BM1_ESP_PDF"] = EjecutarReporteBm1EspPdf,

            // Relacion de Retenciones de IVA por periodos (requerimiento 22).
            // Primer reporte del dominio ADM que entra por el motor: su pantalla
            // de parametros -rango de fechas y estatus- es exactamente el caso
            // para el que existe el modo PARAMETROS, asi que no se codifico a
            // mano en el frontend.
            ["REPORTE_RET_IVA_PER_PDF"] = EjecutarReporteRetIvaPerPdf,

            // Relacion de cheques emitidos por periodos, con motivo
            // (requerimiento 23). Seis filtros, dos de ellos contra catalogos de
            // bancos y cuentas: es el tipo de pantalla que el motor construye
            // sola y que antes habia que codificar entera.
            ["REPORTE_CHEQ_MOTIVO_PDF"] = EjecutarReporteChequesMotivoPdf,

            // La misma relacion sin el bloque de motivo (requerimiento 24).
            // Comparte handler, SP y generador con la anterior -el .rdf del 24 es
            // un subconjunto del del 23-, pero cuelga de su propio formulario
            // porque expone menos filtros: el reporte legado del 24 no tiene
            // status ni proveedor.
            ["REPORTE_CHEQ_PERIODO_PDF"] = EjecutarReporteChequesPeriodoPdf,

            // Relacion de Compromisos (requerimiento 25). Es el primer reporte
            // del motor que exige elegir un presupuesto: su formulario lo pide
            // contra el catalogo PRE_PRESUPUESTO, porque no existe -ni se
            // invento- un "presupuesto activo" en configuracion.
            ["REPORTE_REL_COMPROMISO_PDF"] = EjecutarReporteRelCompromisoPdf
        };

    public static bool EstaRegistrado(string? clave)
    {
        return !string.IsNullOrWhiteSpace(clave) && Registro.ContainsKey(clave);
    }

    public static IEnumerable<string> ClavesRegistradas() => Registro.Keys.OrderBy(k => k);

    /// <summary>
    /// Punto unico de ejecucion. Comprueba la lista blanca antes de nada.
    /// </summary>
    public static async Task<MfoResultadoEjecucion> EjecutarAsync(MfoContextoEjecucion contexto)
    {
        var clave = contexto.Reporte.ClaveRegistro;

        if (!EstaRegistrado(clave))
        {
            // Mensaje deliberadamente concreto: este fallo casi siempre significa
            // "se configuro un reporte que nadie registro en el codigo", y quien
            // lo lea tiene que entender que falta un despliegue, no un permiso.
            return MfoResultadoEjecucion.Error(
                $"El reporte '{clave}' no esta registrado en el backend. " +
                "Habilitarlo requiere registrarlo en MfoRegistroReportes.cs y desplegar.");
        }

        return await Registro[clave](contexto);
    }

    // ------------------------------------------------------------------------
    // Ejecutores registrados
    // ------------------------------------------------------------------------

    /// <summary>
    /// Reporte BM1 en PDF. Reusa el handler y el generador que ya existen en
    /// <c>Features/ReporteBm1</c>: el modo reporte no reimplementa el reporte,
    /// solo sustituye el dialogo de parametros que hoy esta codificado a mano en
    /// la pantalla.
    ///
    /// <c>CodigoEmpresa</c> esta mapeado como parametro de origen SISTEMA y por
    /// eso aparece en PARAMS_CLB, pero este reporte no lo recibe por argumento:
    /// <c>ReporteBm1GetAllHandler</c> lo resuelve el mismo desde
    /// settings:EmpresaConfig. El mapeo se mantiene igual porque es lo que hace
    /// auditable con que empresa corrio, y porque demuestra el punto que importa:
    /// un CodigoEmpresa enviado por el cliente no llega a la consulta por ninguna
    /// via.
    /// </summary>
    private static async Task<MfoResultadoEjecucion> EjecutarReporteBm1Pdf(MfoContextoEjecucion contexto)
    {
        var desde = contexto.Param("FechaDesde")?.Fecha;
        var hasta = contexto.Param("FechaHasta")?.Fecha;
        var icps = contexto.Param("CodigosIcp")?.Enteros ?? [];

        var query = new ReporteBm1GetAllQuery(desde, hasta, icps.Count > 0 ? icps : null);

        var handler = new ReporteBm1GetAllHandler(contexto.Conexiones, contexto.Config);
        var datos = await handler.HandleAsync(query);

        if (!datos.IsValid || datos.Data is null)
        {
            return MfoResultadoEjecucion.Error(datos.Message);
        }

        if (datos.Data.Count == 0)
        {
            return MfoResultadoEjecucion.Vacio(
                "El reporte no devolvio datos con esos parametros.");
        }

        var items = datos.Data;
        var resultado = "OK";
        var mensaje = string.Empty;

        // MAX_FILAS es un corte de seguridad, no un filtro: el usuario tiene que
        // enterarse de que esta viendo un resultado incompleto, porque si no
        // tomara decisiones sobre datos truncados creyendolos completos.
        if (contexto.Reporte.MaxFilas is int max && max > 0 && items.Count > max)
        {
            items = items.Take(max).ToList();
            resultado = "TRUNCADO";
            mensaje = $"Se alcanzo el limite de {max} filas. Refine los parametros.";
        }

        var bytes = ReporteBm1PdfGenerator.Generate(items, query, contexto.Ambiente);

        return new MfoResultadoEjecucion(
            bytes,
            $"ReporteBm1_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            items.Count,
            resultado,
            mensaje);
    }

    /// <summary>
    /// Placas de bienes en PDF. Reusa la lectura y el generador que ya existen en
    /// <c>Features/BienesMunicipales/Bm1</c>, igual que el reporte anterior: el
    /// modo reporte sustituye la pantalla de parametros, no reimplementa el
    /// reporte.
    ///
    /// El filtro de este reporte espera los ICP como objetos, no como numeros
    /// sueltos, y solo usa el codigo. Se construyen aqui con la descripcion
    /// vacia porque la lectura no la mira: rellenarla exigiria una consulta al
    /// catalogo que no cambia el resultado.
    /// </summary>
    private static async Task<MfoResultadoEjecucion> EjecutarReporteBm1PlacasPdf(MfoContextoEjecucion contexto)
    {
        var desde = contexto.Param("FechaDesde")?.Fecha;
        var hasta = contexto.Param("FechaHasta")?.Fecha;
        var icps = contexto.Param("CodigosIcp")?.Enteros ?? [];
        var busqueda = contexto.Param("SearchValue")?.Texto;

        var filtro = new Bm1FilterRequest(
            icps.Select(codigo => new BmIcpResponse(codigo, string.Empty)).ToList(),
            desde,
            hasta,
            busqueda);

        var datos = await Bm1PlacasLector.LeerAsync(contexto.Conexiones, contexto.Config, filtro);

        if (!datos.IsValid || datos.Data is null)
        {
            return MfoResultadoEjecucion.Error(datos.Message);
        }

        if (datos.Data.Count == 0)
        {
            return MfoResultadoEjecucion.Vacio(
                "No hay bienes para generar placas con esos parametros.");
        }

        var items = datos.Data;
        var resultado = "OK";
        var mensaje = string.Empty;

        if (contexto.Reporte.MaxFilas is int max && max > 0 && items.Count > max)
        {
            items = items.Take(max).ToList();
            resultado = "TRUNCADO";
            mensaje = $"Se alcanzo el limite de {max} placas. Refine los parametros.";
        }

        var imagenes = await Bm1PlacasImagenesLoader.LoadAsync(
            contexto.Conexiones, contexto.Config, contexto.Ambiente);

        var bytes = Bm1PlacasPdfGenerator.Generate(items, imagenes);

        return new MfoResultadoEjecucion(
            bytes,
            $"PlacasBm1_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            items.Count,
            resultado,
            mensaje);
    }

    /// <summary>
    /// Formulario oficial BM-1. Requerimiento 27.
    ///
    /// Todos los filtros son opcionales: sin ninguno imprime el inventario
    /// completo de la empresa, que es el comportamiento del reporte legado que
    /// sustituye. Por eso lleva MAX_FILAS: pedirlo sin filtros en una empresa
    /// grande produce un documento de cientos de paginas.
    /// </summary>
    private static async Task<MfoResultadoEjecucion> EjecutarReporteBm1EspPdf(MfoContextoEjecucion contexto)
    {
        var query = new ReporteBm1EspQuery(
            CodigoDirBien: (int?)contexto.Param("CodigoDirBien")?.Numero,
            PlacaDesde: contexto.Param("PlacaDesde")?.Texto,
            PlacaHasta: contexto.Param("PlacaHasta")?.Texto,
            CodigoArticulo: (int?)contexto.Param("CodigoArticulo")?.Numero,
            FechaDesde: contexto.Param("FechaDesde")?.Fecha,
            FechaHasta: contexto.Param("FechaHasta")?.Fecha,
            Responsable: contexto.Param("Responsable")?.Texto);

        var handler = new ReporteBm1EspHandler(contexto.Conexiones, contexto.Config);
        var datos = await handler.HandleAsync(query);

        if (!datos.IsValid || datos.Data is null)
        {
            return MfoResultadoEjecucion.Error(datos.Message);
        }

        if (datos.Data.Count == 0)
        {
            return MfoResultadoEjecucion.Vacio(
                "No hay bienes para generar el formulario BM-1 con esos parametros.");
        }

        var unidades = datos.Data;
        var filas = unidades.Sum(u => u.Items.Count);
        var resultado = "OK";
        var mensaje = string.Empty;

        // El corte se aplica por unidad completa, no por fila: partir una unidad
        // a la mitad dejaria un subtotal que no cuadra con lo impreso, y un
        // formulario legal con un total mal es peor que uno truncado.
        if (contexto.Reporte.MaxFilas is int max && max > 0 && filas > max)
        {
            var acumulado = 0;
            var recortadas = new List<ReporteBm1EspUnidad>();

            foreach (var unidad in unidades)
            {
                if (acumulado > 0 && acumulado + unidad.Items.Count > max) break;

                recortadas.Add(unidad);
                acumulado += unidad.Items.Count;
            }

            unidades = recortadas;
            filas = acumulado;
            resultado = "TRUNCADO";
            mensaje = $"Se alcanzo el limite de {max} bienes. Se incluyeron {unidades.Count} unidad(es) completas.";
        }

        var entidad = await handler.ObtenerEntidadAsync();
        var bytes = ReporteBm1EspPdfGenerator.Generate(unidades, entidad, query);

        return new MfoResultadoEjecucion(
            bytes,
            $"BM1Especial_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            filas,
            resultado,
            mensaje);
    }

    /// <summary>
    /// Relacion de Retenciones de IVA por periodos. Requerimiento 22.
    ///
    /// Las dos fechas son obligatorias y el motor ya las exige por
    /// <c>MFO_REP_PARAM.OBLIGATORIO</c>; el handler las vuelve a validar porque
    /// el endpoint <c>api/ReporteRelacionRetIva/pdf</c> tambien es publico y no
    /// pasa por aqui.
    ///
    /// <c>Usuario</c> se mapea como parametro de origen SISTEMA y **si** se usa:
    /// es lo que imprime el pie de auditoria del requerimiento 17. Un reporte
    /// fiscal sin constancia de quien lo emitio es justo lo que ese
    /// requerimiento vino a cerrar, asi que sin usuario identificado no se
    /// genera.
    /// </summary>
    private static async Task<MfoResultadoEjecucion> EjecutarReporteRetIvaPerPdf(MfoContextoEjecucion contexto)
    {
        var usuario = contexto.Param("Usuario")?.Texto;

        if (string.IsNullOrWhiteSpace(usuario))
        {
            return MfoResultadoEjecucion.Error(
                "No se pudo determinar el usuario conectado, requerido para el pie de auditoria del reporte.");
        }

        var query = new ReporteRelacionRetIvaQuery(
            FechaDesde: contexto.Param("FechaDesde")?.Fecha,
            FechaHasta: contexto.Param("FechaHasta")?.Fecha,
            Estatus: contexto.Param("Estatus")?.Texto,
            Usuario: usuario);

        var handler = new ReporteRelacionRetIvaHandler(contexto.Conexiones, contexto.Config);
        var datos = await handler.HandleAsync(query);

        if (!datos.IsValid || datos.Data is null)
        {
            return MfoResultadoEjecucion.Error(datos.Message);
        }

        if (datos.Data.Count == 0)
        {
            return MfoResultadoEjecucion.Vacio(
                "No hay retenciones de IVA en el periodo seleccionado.");
        }

        var comprobantes = datos.Data;
        var filas = comprobantes.Sum(c => c.Documentos.Count);
        var resultado = "OK";
        var mensaje = string.Empty;

        // El corte se aplica por comprobante completo, igual que el BM-1 corta
        // por unidad: partir un comprobante a la mitad deja un TOTAL que no
        // cuadra con las filas impresas, y en un reporte fiscal un total mal es
        // peor que un reporte truncado.
        if (contexto.Reporte.MaxFilas is int max && max > 0 && filas > max)
        {
            var acumulado = 0;
            var recortados = new List<ReporteRelacionRetIvaComprobante>();

            foreach (var comprobante in comprobantes)
            {
                if (acumulado > 0 && acumulado + comprobante.Documentos.Count > max) break;

                recortados.Add(comprobante);
                acumulado += comprobante.Documentos.Count;
            }

            comprobantes = recortados;
            filas = acumulado;
            resultado = "TRUNCADO";
            mensaje = $"Se alcanzo el limite de {max} documentos. Se incluyeron {comprobantes.Count} comprobante(s) completos. Refine el periodo.";
        }

        var printContext = ReportPrintContext.Create(usuario);
        var bytes = ReporteRelacionRetIvaPdfGenerator.Generate(comprobantes, query, printContext);

        return new MfoResultadoEjecucion(
            bytes,
            $"RelacionRetencionIva_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            filas,
            resultado,
            mensaje);
    }

    /// <summary>
    /// Relacion de Cheques Emitidos Por Periodos, **con** motivo.
    /// Requerimiento 23.
    ///
    /// Seis filtros: rango de fechas obligatorio y banco, cuenta, status y
    /// proveedor opcionales. Los dos primeros opcionales llegan resueltos desde
    /// los catalogos SIS_BANCO_NOMBRE y SIS_CUENTA_BANCO, asi que su valor es el
    /// nombre y el numero de cuenta reales y no hay que traducir nada aqui.
    /// </summary>
    private static Task<MfoResultadoEjecucion> EjecutarReporteChequesMotivoPdf(
        MfoContextoEjecucion contexto) => EjecutarChequesAsync(contexto, conMotivo: true);

    /// <summary>
    /// La misma relacion **sin** el bloque de motivo. Requerimiento 24.
    ///
    /// Su formulario solo expone el rango de fechas, el banco y la cuenta, porque
    /// su .rdf no define status ni proveedor. Los dos filtros que no expone
    /// quedan nulos, que es "todos".
    /// </summary>
    private static Task<MfoResultadoEjecucion> EjecutarReporteChequesPeriodoPdf(
        MfoContextoEjecucion contexto) => EjecutarChequesAsync(contexto, conMotivo: false);

    /// <summary>
    /// Ejecucion compartida por las dos variantes de la relacion de cheques.
    ///
    /// Lo unico que cambia entre ellas es <paramref name="conMotivo"/>, que decide
    /// si el reporte trae el motivo, la orden de pago y las partidas -y por tanto
    /// si el SP llama a ADM_F_GET_PARTIDAS_CHEQUE, que es lo caro-. Todo lo demas
    /// -filtros, validacion, agrupamiento, corte por MAX_FILAS, pie de
    /// auditoria- es identico, asi que tener dos copias solo garantizaria que un
    /// dia se corrija una.
    ///
    /// El corte por MAX_FILAS se aplica por banco/cuenta completo, no por fila:
    /// cada grupo imprime su subtotal, y un grupo partido a la mitad dejaria un
    /// subtotal que no cuadra con los cheques listados debajo.
    /// </summary>
    private static async Task<MfoResultadoEjecucion> EjecutarChequesAsync(
        MfoContextoEjecucion contexto, bool conMotivo)
    {
        var usuario = contexto.Param("Usuario")?.Texto;

        if (string.IsNullOrWhiteSpace(usuario))
        {
            return MfoResultadoEjecucion.Error(
                "No se pudo determinar el usuario conectado, requerido para el pie de auditoria del reporte.");
        }

        var query = new ReporteChequesPeriodoQuery(
            FechaDesde: contexto.Param("FechaDesde")?.Fecha,
            FechaHasta: contexto.Param("FechaHasta")?.Fecha,
            NombreBanco: contexto.Param("NombreBanco")?.Texto,
            NumeroCuenta: contexto.Param("NumeroCuenta")?.Texto,
            Status: contexto.Param("Status")?.Texto,
            CodigoProveedor: (int?)contexto.Param("CodigoProveedor")?.Numero,
            Usuario: usuario,
            ConMotivo: conMotivo);

        var handler = new ReporteChequesPeriodoHandler(contexto.Conexiones, contexto.Config);
        var datos = await handler.HandleAsync(query);

        if (!datos.IsValid || datos.Data is null)
        {
            return MfoResultadoEjecucion.Error(datos.Message);
        }

        if (datos.Data.Count == 0)
        {
            return MfoResultadoEjecucion.Vacio(
                "No hay cheques emitidos en el periodo seleccionado.");
        }

        var grupos = datos.Data;
        var filas = grupos.Sum(g => g.Items.Count);
        var resultado = "OK";
        var mensaje = string.Empty;

        if (contexto.Reporte.MaxFilas is int max && max > 0 && filas > max)
        {
            var acumulado = 0;
            var recortados = new List<ReporteChequesPeriodoGrupo>();

            foreach (var grupo in grupos)
            {
                if (acumulado > 0 && acumulado + grupo.Items.Count > max) break;

                recortados.Add(grupo);
                acumulado += grupo.Items.Count;
            }

            grupos = recortados;
            filas = acumulado;
            resultado = "TRUNCADO";
            mensaje = $"Se alcanzo el limite de {max} cheques. Se incluyeron {grupos.Count} cuenta(s) completas. Refine el periodo.";
        }

        var printContext = ReportPrintContext.Create(usuario);
        var bytes = ReporteChequesPeriodoPdfGenerator.Generate(grupos, query, printContext);
        var prefijo = conMotivo ? "RelacionChequesMotivo" : "RelacionCheques";

        return new MfoResultadoEjecucion(
            bytes,
            $"{prefijo}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            filas,
            resultado,
            mensaje);
    }

    /// <summary>
    /// Relacion de Compromisos. Requerimiento 25.
    ///
    /// <c>CodigoPresupuesto</c> es el unico parametro obligatorio de origen CAMPO
    /// de todo el registro: el reporte no tiene sentido sin un presupuesto, y a
    /// diferencia de la empresa **no se puede resolver en el servidor** -no existe
    /// un settings:PresupuestoConfig y el ERP siempre lo hace elegir-. Llega
    /// resuelto desde el catalogo PRE_PRESUPUESTO.
    ///
    /// Las dos fechas son opcionales e independientes, como en el reporte legado:
    /// sin ninguna lista el presupuesto completo. Por eso lleva MAX_FILAS.
    /// </summary>
    private static async Task<MfoResultadoEjecucion> EjecutarReporteRelCompromisoPdf(
        MfoContextoEjecucion contexto)
    {
        var usuario = contexto.Param("Usuario")?.Texto;

        if (string.IsNullOrWhiteSpace(usuario))
        {
            return MfoResultadoEjecucion.Error(
                "No se pudo determinar el usuario conectado, requerido para el pie de auditoria del reporte.");
        }

        var query = new ReporteRelacionCompromisoQuery(
            CodigoPresupuesto: (int?)contexto.Param("CodigoPresupuesto")?.Numero ?? 0,
            FechaDesde: contexto.Param("FechaDesde")?.Fecha,
            FechaHasta: contexto.Param("FechaHasta")?.Fecha,
            CodigoProveedor: (int?)contexto.Param("CodigoProveedor")?.Numero,
            Usuario: usuario);

        var handler = new ReporteRelacionCompromisoHandler(contexto.Conexiones, contexto.Config);
        var datos = await handler.HandleAsync(query);

        if (!datos.IsValid || datos.Data is null)
        {
            return MfoResultadoEjecucion.Error(datos.Message);
        }

        if (datos.Data.Count == 0)
        {
            return MfoResultadoEjecucion.Vacio(
                "No hay compromisos con los parametros seleccionados.");
        }

        var items = datos.Data;
        var resultado = "OK";
        var mensaje = string.Empty;

        // Aqui el corte si puede ser por fila: este reporte no tiene grupos ni
        // subtotales intermedios, asi que recortar no descuadra ningun parcial.
        // Lo que si cambia es el total de cierre, y de eso avisa el mensaje.
        if (contexto.Reporte.MaxFilas is int max && max > 0 && items.Count > max)
        {
            items = items.Take(max).ToList();
            resultado = "TRUNCADO";
            mensaje = $"Se alcanzo el limite de {max} compromisos. El total de cierre corresponde solo a los incluidos. Refine el periodo.";
        }

        var entidad = await handler.ObtenerEntidadAsync(query.CodigoPresupuesto);
        var printContext = ReportPrintContext.Create(usuario);
        var bytes = ReporteRelacionCompromisoPdfGenerator.Generate(items, query, entidad, printContext);

        return new MfoResultadoEjecucion(
            bytes,
            $"RelacionCompromisos_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            items.Count,
            resultado,
            mensaje);
    }
}
