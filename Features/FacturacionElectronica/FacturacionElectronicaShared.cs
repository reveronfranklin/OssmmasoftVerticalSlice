using System.Data;
using Npgsql;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Respuesta de un emisor. Va aqui y no en el archivo de operacion porque la
// comparten GetAll, GetById y las que vengan despues.
public record EmisorResponse(
    long Id,
    string Rif,
    string RazonSocial,
    string DomicilioFiscal,
    string Correo,
    string Estado,
    string RifVerificadoEl,
    string RifVerificadoEstado,
    string UsuarioIns,
    string FechaIns,
    string UsuarioUpd,
    string FechaUpd);

// Numero de control asignado. Fase 2.
//
// YaExistia distingue una asignacion nueva de la devolucion de una ya hecha: es
// lo que hace idempotente al endpoint y la defensa directa contra INV-1.
public record NumeroControlResponse(
    long Id,
    long EmisorId,
    long DocumentoId,
    string Identificador,
    int Secuencial,
    string NumeroControl,
    string NumeroControlTexto,
    string TipoDocumento,
    string FechaAsignacion,
    bool YaExistia);

// Fila del listado de numeros de control asignados. Es un record distinto del de
// asignacion a proposito: el listado trae los datos del emisor por join, y la
// asignacion no los necesita. Un solo record obligaria a inventar campos vacios.
public record NumeroControlListaResponse(
    long Id,
    long EmisorId,
    string EmisorRif,
    string EmisorRazonSocial,
    long DocumentoId,
    string Identificador,
    int Secuencial,
    string NumeroControl,
    string TipoDocumento,
    string FechaAsignacion,
    long ReporteId,
    string UsuarioIns);

// Fila del registro del Articulo 32. Los siete numerales, en orden.
//
// El numeral 6 llega desdoblado -DocumentoId y FacturaServicio- porque la norma
// admite las dos lecturas: ver D-15. NumeracionFormato viaja vacio hasta la
// Fase 4, cuando exista FED_DOCUMENTO (D-17).
public record RegistroArt32Response(
    long NumControlId,
    string EmisorRif,
    string EmisorRazonSocial,
    string FechaAsignacion,
    string FechaAsignacion8d,
    string TipoDocumento,
    string NumeroControl,
    string NumeracionFormato,
    long DocumentoId,
    string FacturaServicio,
    string EstadoConciliacion,
    string DatosAdicionales,
    long ReporteId,
    string Periodo);

// Un periodo mensual del Art. 29.7. La fila existe desde antes de que haya algo
// que reportar: eso es lo que convierte un periodo omitido en algo visible.
public record ReporteMensualResponse(
    long Id,
    string Periodo,
    string FechaCierre,
    string FechaVence,
    string EnviadoEn,
    int CantidadReportada,
    string Estado,
    string UltimoIntentoEn,
    string UltimoError,
    bool Vencido);

// Fila unica de contadores del panel. Es un DTO propio y no los campos Total1..4
// del ResultDto: esos, en las seis operaciones del repo que los usan, siempre
// acompanan a una lista. Un tablero que devolviera cuatro decimales sin nombre
// obligaria al frontend a recordar cual es cual. Ver D-25.
//
// Cada campo sale de una consulta que lo cuenta. Ninguno se estima ni se deduce
// de una pagina: en un modulo fiscal una cifra de adorno es peor que un hueco,
// porque el hueco se ve y la cifra falsa se cree. Ver D-23.
public record PanelResumenResponse(
    string Periodo,
    int DocumentosPeriodo,
    int DocumentosPrueba,
    int NumerosAsignadosPeriodo,
    int EmisoresTotal,
    int EmisoresActivos,
    int EmisoresRifSinVerificar,
    string PeriodoEstado,
    string PeriodoVence,
    int PeriodoDiasRestantes,
    int PeriodoCantidadReportada,
    int PeriodosVencidosAnio);

// Piezas comunes del modulo de Facturacion Electronica (requerimiento 32).
//
// Unico modulo del proyecto sobre PostgreSQL: usa GetFedConnection y el schema
// FED. El resto del backend trabaja contra Oracle con stored procedures.
public static class FacturacionElectronicaDb
{
    // Mensaje de exito del proyecto. El typo es contrato con el frontend y no se
    // corrige: lo declara el estandar y lo consumen los modulos existentes.
    public const string MensajeExito = "suscces";

    // -----------------------------------------------------------------------
    // SQL del modulo. D-13: vive aqui, parametrizado y nunca interpolado. Los
    // archivos de Sql/ son solo DDL. Es la traduccion a PostgreSQL del
    // antipatron "SQL inline en el handler" que marca el estandar.
    //
    // Se usa LIMIT/OFFSET y no ROW_NUMBER(). El estandar prohibe OFFSET porque
    // Oracle 10g no lo soporta; en PostgreSQL es lo idiomatico y trasladar el
    // rodeo de Oracle seria copiar la solucion sin el problema.
    // -----------------------------------------------------------------------

    public const string SqlHealth =
        "SELECT current_database() || ' / ' || current_user || ' / schema ' || current_schema();";

    private const string ColumnasEmisor = @"
        ID, RIF, RAZON_SOCIAL, DOMICILIO_FISCAL, CORREO, ESTADO,
        RIF_VERIFICADO_EL, RIF_VERIFICADO_ESTADO,
        USUARIO_INS, FECHA_INS, USUARIO_UPD, FECHA_UPD";

    public const string SqlEmisorCreate = @"
        INSERT INTO FED.FED_EMISOR
            (RIF, RAZON_SOCIAL, DOMICILIO_FISCAL, CORREO, ESTADO, USUARIO_INS)
        VALUES
            (@rif, @razon_social, @domicilio_fiscal, @correo, @estado, @usuario_ins)
        RETURNING ID;";

    public static readonly string SqlEmisorGetAll = $@"
        SELECT {ColumnasEmisor}, COUNT(*) OVER() AS TOTAL_REGISTROS
        FROM FED.FED_EMISOR
        WHERE (@search = '' OR RIF ILIKE @like OR RAZON_SOCIAL ILIKE @like)
        ORDER BY RAZON_SOCIAL
        LIMIT @page_size OFFSET @row_offset;";

    public static readonly string SqlEmisorGetById = $@"
        SELECT {ColumnasEmisor}
        FROM FED.FED_EMISOR
        WHERE ID = @id;";

    // El RIF no se actualiza a proposito. El Articulo 30 ata la secuencia de
    // numero de control al RIF del emisor, asi que cambiarlo rompe la unicidad
    // por emisor de todo lo ya asignado. Si el negocio necesita corregirlo, se
    // desactiva el emisor y se da de alta el correcto.
    public const string SqlEmisorUpdate = @"
        UPDATE FED.FED_EMISOR SET
            RAZON_SOCIAL          = @razon_social,
            DOMICILIO_FISCAL      = @domicilio_fiscal,
            CORREO                = @correo,
            ESTADO                = @estado,
            RIF_VERIFICADO_EL     = @rif_verificado_el,
            RIF_VERIFICADO_ESTADO = @rif_verificado_estado,
            USUARIO_UPD           = @usuario_upd,
            FECHA_UPD             = now()
        WHERE ID = @id;";

    // -----------------------------------------------------------------------
    // Numero de control - Fase 2. Art. 30 y 32.
    //
    // El orden de estas cuatro sentencias no es decorativo: es lo que sostiene
    // INV-1. Se ejecutan todas dentro de una transaccion.
    // -----------------------------------------------------------------------

    public const string SqlEmisorEstado = @"
        SELECT ESTADO
        FROM FED.FED_EMISOR
        WHERE ID = @emisor_id;";

    // Idempotencia (T2.6): si el documento ya tiene numero, se devuelve ese. Es
    // la defensa directa contra INV-1, que prohibe dos numeros distintos para un
    // mismo documento (Art. 34.2).
    public const string SqlNumControlPorDocumento = @"
        SELECT ID, EMISOR_ID, DOCUMENTO_ID, IDENTIFICADOR, SECUENCIAL, TIPO_DOCUMENTO, FECHA_ASIGNACION
        FROM FED.FED_NUM_CONTROL
        WHERE DOCUMENTO_ID = @documento_id;";

    // Crea la fila del contador si el emisor todavia no tiene, y la BLOQUEA.
    //
    // Por que DO UPDATE y no DO NOTHING: con DO NOTHING, si otra transaccion
    // acaba de insertar la fila y aun no confirmo, esta no la ve -la fila es
    // invisible- y el RETURNING vuelve vacio. Con DO UPDATE, PostgreSQL toma el
    // bloqueo de esa fila y espera a que la otra confirme, que es exactamente la
    // serializacion que se busca. La asignacion es un no-op a proposito: no
    // cambia nada, solo existe para tomar el bloqueo.
    //
    // El bloqueo es por emisor: dos emisores distintos asignan en paralelo.
    public const string SqlContadorBloquear = @"
        INSERT INTO FED.FED_EMISOR_CONTADOR (EMISOR_ID)
        VALUES (@emisor_id)
        ON CONFLICT (EMISOR_ID) DO UPDATE
            SET FECHA_UPD = FED.FED_EMISOR_CONTADOR.FECHA_UPD
        RETURNING IDENTIFICADOR, SECUENCIAL;";

    public const string SqlContadorActualizar = @"
        UPDATE FED.FED_EMISOR_CONTADOR SET
            IDENTIFICADOR = @identificador,
            SECUENCIAL    = @secuencial,
            FECHA_UPD     = now()
        WHERE EMISOR_ID = @emisor_id;";

    public const string SqlNumControlInsert = @"
        INSERT INTO FED.FED_NUM_CONTROL
            (EMISOR_ID, DOCUMENTO_ID, IDENTIFICADOR, SECUENCIAL, TIPO_DOCUMENTO, USUARIO_INS)
        VALUES
            (@emisor_id, @documento_id, @identificador, @secuencial, @tipo_documento, @usuario_ins)
        RETURNING ID, FECHA_ASIGNACION;";

    // Listado del Art. 32: por emisor y por rango de fechas, que es como lo pide
    // el reporte mensual del Art. 29.7 y la vista de consulta de T2.9.
    //
    // Los parametros de fecha llevan cast explicito a date: sin el, Npgsql no
    // puede inferir el tipo de un parametro que solo aparece en un IS NULL.
    public const string SqlNumControlGetAll = @"
        SELECT
            nc.ID, nc.EMISOR_ID, nc.DOCUMENTO_ID, nc.IDENTIFICADOR, nc.SECUENCIAL,
            nc.TIPO_DOCUMENTO, nc.FECHA_ASIGNACION, nc.REPORTE_ID, nc.USUARIO_INS,
            e.RIF AS EMISOR_RIF, e.RAZON_SOCIAL AS EMISOR_RAZON_SOCIAL,
            COUNT(*) OVER() AS TOTAL_REGISTROS
        FROM FED.FED_NUM_CONTROL nc
        JOIN FED.FED_EMISOR e ON e.ID = nc.EMISOR_ID
        WHERE (@emisor_id = 0 OR nc.EMISOR_ID = @emisor_id)
          AND (@fecha_desde::date IS NULL OR nc.FECHA_ASIGNACION >= @fecha_desde::date)
          AND (@fecha_hasta::date IS NULL OR nc.FECHA_ASIGNACION < @fecha_hasta::date + INTERVAL '1 day')
        ORDER BY nc.EMISOR_ID, nc.IDENTIFICADOR, nc.SECUENCIAL
        LIMIT @page_size OFFSET @row_offset;";

    // -----------------------------------------------------------------------
    // Registro del Art. 32 y reporte mensual del Art. 29.7 - Fase 3
    // -----------------------------------------------------------------------

    public const string SqlRegistroArt32GetAll = @"
        SELECT
            NUM_CONTROL_ID, EMISOR_RIF, EMISOR_RAZON_SOCIAL, FECHA_ASIGNACION, FECHA_ASIGNACION_8D,
            TIPO_DOCUMENTO, NUMERO_CONTROL, NUMERACION_FORMATO, DOCUMENTO_ID, FACTURA_SERVICIO,
            ESTADO_CONCILIACION, DATOS_ADICIONALES, REPORTE_ID, PERIODO,
            COUNT(*) OVER() AS TOTAL_REGISTROS
        FROM FED.FED_V_REGISTRO_ART32
        WHERE (@emisor_rif = '' OR EMISOR_RIF = @emisor_rif)
          AND (@periodo = '' OR PERIODO = @periodo)
        ORDER BY EMISOR_RIF, IDENTIFICADOR, SECUENCIAL
        LIMIT @page_size OFFSET @row_offset;";

    // Crea por adelantado la fila de cada periodo, desde el mes de la primera
    // asignacion hasta el mes en curso. Reejecutable por el ON CONFLICT.
    //
    // Si no hay ninguna asignacion todavia, el COALESCE hace que igual se cree la
    // fila del mes actual: el Art. 29.7 obliga a reportar "con independencia de no
    // haber asignado ningun numero de control", asi que el periodo tiene que
    // existir aunque este vacio.
    public const string SqlReporteAsegurarPeriodos = @"
        INSERT INTO FED.FED_REPORTE_MENSUAL (PERIODO, FECHA_CIERRE, FECHA_VENCE)
        SELECT
            TO_CHAR(m, 'YYYYMM'),
            (m + INTERVAL '1 month - 1 day')::date,
            (m + INTERVAL '1 month - 1 day')::date + 10
        FROM generate_series(
            COALESCE(
                (SELECT date_trunc('month', MIN(FECHA_ASIGNACION)) FROM FED.FED_NUM_CONTROL),
                date_trunc('month', now())),
            date_trunc('month', now()),
            INTERVAL '1 month') AS m
        ON CONFLICT (PERIODO) DO NOTHING;";

    // Periodos cuyo mes ya cerro y todavia no tienen reporte generado.
    public const string SqlReportePendientesACerrar = @"
        SELECT ID, PERIODO
        FROM FED.FED_REPORTE_MENSUAL
        WHERE ESTADO = 'pendiente'
          AND FECHA_CIERRE < CURRENT_DATE
        ORDER BY PERIODO;";

    // Ata al reporte los numeros de su periodo que todavia no esten atados.
    public const string SqlReporteAtarNumeros = @"
        UPDATE FED.FED_NUM_CONTROL
           SET REPORTE_ID = @reporte_id
         WHERE TO_CHAR(FECHA_ASIGNACION, 'YYYYMM') = @periodo
           AND REPORTE_ID IS NULL;";

    // Cero es un resultado valido y esperado, no una anomalia.
    public const string SqlReporteContarPeriodo = @"
        SELECT COUNT(*) FROM FED.FED_NUM_CONTROL
        WHERE TO_CHAR(FECHA_ASIGNACION, 'YYYYMM') = @periodo;";

    // Se marca 'generado', no 'enviado': el reporte esta calculado pero no existe
    // canal para transmitirlo al SENIAT. Ver D-19.
    public const string SqlReporteMarcarGenerado = @"
        UPDATE FED.FED_REPORTE_MENSUAL SET
            ESTADO             = 'generado',
            CANTIDAD_REPORTADA = @cantidad,
            ULTIMO_INTENTO_EN  = now(),
            ULTIMO_ERROR       = NULL
        WHERE ID = @id;";

    // Vencimiento del Art. 29.7. Devuelve los periodos que ACABAN de vencer, y
    // solo esos: la alerta se dispara en la transicion y no en cada tick, para que
    // no se vuelva ruido que nadie mira.
    public const string SqlReporteMarcarVencidos = @"
        UPDATE FED.FED_REPORTE_MENSUAL SET
            ESTADO = 'vencido'
        WHERE ESTADO IN ('pendiente', 'generado')
          AND ENVIADO_EN IS NULL
          AND FECHA_VENCE < CURRENT_DATE
        RETURNING PERIODO, CANTIDAD_REPORTADA;";

    public const string SqlReporteRegistrarError = @"
        UPDATE FED.FED_REPORTE_MENSUAL SET
            ULTIMO_INTENTO_EN = now(),
            ULTIMO_ERROR      = @error
        WHERE ID = @id;";

    public const string SqlReporteGetAll = @"
        SELECT
            ID, PERIODO, FECHA_CIERRE, FECHA_VENCE, ENVIADO_EN, CANTIDAD_REPORTADA,
            ESTADO, ULTIMO_INTENTO_EN, ULTIMO_ERROR,
            COUNT(*) OVER() AS TOTAL_REGISTROS
        FROM FED.FED_REPORTE_MENSUAL
        WHERE (@estado = '' OR ESTADO = @estado)
        ORDER BY PERIODO DESC
        LIMIT @page_size OFFSET @row_offset;";

    // Panel del modulo: una sola consulta, una sola fila.
    //
    // Cada subconsulta es un COUNT real sobre su tabla. Se hace en un viaje y no
    // en siete porque los numeros se muestran juntos y tienen que ser coherentes
    // entre si: contarlos en momentos distintos permitiria que la pantalla
    // afirme que hay 30 documentos y 29 numeros asignados.
    //
    // PERIODOS_VENCIDOS_ANIO es el contador que importa de verdad: dos periodos
    // omitidos en un ano calendario revocan la autorizacion sin sancion previa
    // (Art. 34.3). Por eso se cuenta por ano, no por los ultimos doce meses.
    //
    // El LEFT JOIN sobre el periodo en curso no puede fallar por ausencia: las
    // filas de periodo se crean por adelantado (ver D-18). Aun asi se protege con
    // COALESCE, porque una base recien creada tiene la tabla vacia hasta el
    // primer tick del job.
    public const string SqlPanelResumen = @"
        SELECT
            (SELECT COUNT(*) FROM FED.FED_DOCUMENTO
              WHERE TO_CHAR(EMITIDO_EN, 'YYYYMM') = @periodo)                     AS DOCUMENTOS_PERIODO,
            (SELECT COUNT(*) FROM FED.FED_DOCUMENTO
              WHERE TO_CHAR(EMITIDO_EN, 'YYYYMM') = @periodo AND ES_PRUEBA)       AS DOCUMENTOS_PRUEBA,
            (SELECT COUNT(*) FROM FED.FED_NUM_CONTROL
              WHERE TO_CHAR(FECHA_ASIGNACION, 'YYYYMM') = @periodo)               AS NUMEROS_PERIODO,
            (SELECT COUNT(*) FROM FED.FED_EMISOR)                                 AS EMISORES_TOTAL,
            (SELECT COUNT(*) FROM FED.FED_EMISOR WHERE ESTADO = 'activo')         AS EMISORES_ACTIVOS,
            (SELECT COUNT(*) FROM FED.FED_EMISOR
              WHERE RIF_VERIFICADO_ESTADO IS NULL)                                AS EMISORES_RIF_SIN_VERIFICAR,
            (SELECT COUNT(*) FROM FED.FED_REPORTE_MENSUAL
              WHERE ESTADO = 'vencido' AND SUBSTR(PERIODO, 1, 4) = @anio)         AS PERIODOS_VENCIDOS_ANIO,
            COALESCE(r.ESTADO, '')                                                AS PERIODO_ESTADO,
            r.FECHA_VENCE                                                         AS PERIODO_VENCE,
            COALESCE(r.FECHA_VENCE - CURRENT_DATE, 0)                             AS PERIODO_DIAS_RESTANTES,
            COALESCE(r.CANTIDAD_REPORTADA, 0)                                     AS PERIODO_CANTIDAD_REPORTADA
        FROM (SELECT 1) AS unica
        LEFT JOIN FED.FED_REPORTE_MENSUAL r ON r.PERIODO = @periodo;";

    // -----------------------------------------------------------------------
    // Reglas del numero de control
    // -----------------------------------------------------------------------

    // Los cuatro documentos en alcance: DOC-1 a DOC-4. Coincide con el CHECK de
    // la tabla; se valida antes para devolver un mensaje en espanol en vez de un
    // error tecnico del motor.
    public static readonly string[] TiposDocumento = ["factura", "debito", "credito", "entrega"];

    public const int SecuencialMaximo = 99999999;
    public const int IdentificadorMaximo = 99;

    // Art. 30: identificador de dos digitos y secuencial de hasta ocho.
    //
    // El relleno con ceros y el guion separador son [Interpretacion]: el articulo
    // fija la cantidad de digitos, no como se escriben. Se elige el formato de uso
    // corriente en Venezuela, 00-00000001, y se expone tambien el numero crudo por
    // si hubiera que cambiarlo sin tocar a quien lo consuma.
    public static string FormatearNumeroControl(string identificador, int secuencial) =>
        $"{identificador}-{secuencial:D8}";

    // Art. 30: la frase "N de Control" precede al numero. Va con el grado, tal
    // como aparece en el texto de la Providencia.
    public static string FormatearNumeroControlTexto(string identificador, int secuencial) =>
        $"N° de Control {FormatearNumeroControl(identificador, secuencial)}";

    // Decision D-2: el identificador rota al agotarse el secuencial. Al pasar de
    // 99999999 se incrementa el identificador y el secuencial vuelve a 1.
    //
    // El Art. 30 fija el inicio -00 y 1- pero no dice cuando incrementa el
    // identificador; esta es la lectura que preserva la consecutividad por emisor.
    //
    // Devuelve false cuando la secuencia del emisor se agoto por completo: 99
    // identificadores por 99.999.999 secuenciales. No hay regla en la norma para
    // ese caso, asi que no se inventa una: se falla con mensaje claro en vez de
    // producir un identificador de tres digitos que la tabla rechazaria igual.
    public static bool CalcularSiguiente(
        string identificadorActual,
        int secuencialActual,
        out string identificador,
        out int secuencial)
    {
        if (secuencialActual < SecuencialMaximo)
        {
            identificador = identificadorActual;
            secuencial = secuencialActual + 1;

            return true;
        }

        int siguienteIdentificador = int.Parse(identificadorActual) + 1;

        if (siguienteIdentificador > IdentificadorMaximo)
        {
            identificador = identificadorActual;
            secuencial = secuencialActual;

            return false;
        }

        identificador = siguienteIdentificador.ToString("D2");
        secuencial = 1;

        return true;
    }

    // -----------------------------------------------------------------------
    // Mapeo y normalizacion
    // -----------------------------------------------------------------------

    // Las extensiones SafeGet* de helper/ sirven aqui: son sobre IDataReader y
    // NpgsqlDataReader lo implementa. Falta una para fechas, y como solo la usa
    // esta feature vive aqui y no en helper/, como manda el estandar.
    private static string SafeGetFecha(IDataReader reader, string columna, string formato)
    {
        int ordinal = reader.GetOrdinal(columna);

        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        // Npgsql mapea el tipo date de PostgreSQL a DateOnly, que NO implementa
        // IConvertible: Convert.ToDateTime lo rechaza con
        // "Unable to cast object of type 'System.DateOnly' to type 'System.IConvertible'".
        //
        // Se descubrio al leer FED_REPORTE_MENSUAL, pero el defecto ya existia
        // desde la Fase 1: EMISOR.RIF_VERIFICADO_EL tambien es date, y solo no
        // aparecio porque estuvo siempre en NULL y el mapeo salia antes por el
        // IsDBNull. Con un emisor de RIF verificado, el listado se habria caido.
        object valor = reader.GetValue(ordinal);

        return valor switch
        {
            DateOnly fecha       => fecha.ToDateTime(TimeOnly.MinValue).ToString(formato),
            DateTime fechaHora   => fechaHora.ToString(formato),
            DateTimeOffset fecha => fecha.LocalDateTime.ToString(formato),
            _                    => Convert.ToDateTime(valor).ToString(formato)
        };
    }

    public static EmisorResponse MapEmisor(IDataReader reader) => new(
        reader.SafeGetInt64("id"),
        reader.SafeGetString("rif"),
        reader.SafeGetString("razon_social"),
        reader.SafeGetString("domicilio_fiscal"),
        reader.SafeGetString("correo"),
        reader.SafeGetString("estado"),
        SafeGetFecha(reader, "rif_verificado_el", "dd/MM/yyyy"),
        reader.SafeGetString("rif_verificado_estado"),
        reader.SafeGetString("usuario_ins"),
        SafeGetFecha(reader, "fecha_ins", "dd/MM/yyyy HH:mm"),
        reader.SafeGetString("usuario_upd"),
        SafeGetFecha(reader, "fecha_upd", "dd/MM/yyyy HH:mm"));

    // DOCUMENTO_ID nulo llega como 0 por SafeGetInt64: 0 significa "asignado sin
    // documento todavia", que es un estado valido en la Fase 2.
    public static NumeroControlResponse MapNumeroControl(IDataReader reader, bool yaExistia)
    {
        string identificador = reader.SafeGetString("identificador");
        int secuencial = reader.SafeGetInt32("secuencial");

        return new NumeroControlResponse(
            reader.SafeGetInt64("id"),
            reader.SafeGetInt64("emisor_id"),
            reader.SafeGetInt64("documento_id"),
            identificador,
            secuencial,
            FormatearNumeroControl(identificador, secuencial),
            FormatearNumeroControlTexto(identificador, secuencial),
            reader.SafeGetString("tipo_documento"),
            SafeGetFecha(reader, "fecha_asignacion", "dd/MM/yyyy HH:mm:ss"),
            yaExistia);
    }

    public static NumeroControlListaResponse MapNumeroControlLista(IDataReader reader)
    {
        string identificador = reader.SafeGetString("identificador");
        int secuencial = reader.SafeGetInt32("secuencial");

        return new NumeroControlListaResponse(
            reader.SafeGetInt64("id"),
            reader.SafeGetInt64("emisor_id"),
            reader.SafeGetString("emisor_rif"),
            reader.SafeGetString("emisor_razon_social"),
            reader.SafeGetInt64("documento_id"),
            identificador,
            secuencial,
            FormatearNumeroControl(identificador, secuencial),
            reader.SafeGetString("tipo_documento"),
            SafeGetFecha(reader, "fecha_asignacion", "dd/MM/yyyy HH:mm:ss"),
            reader.SafeGetInt64("reporte_id"),
            reader.SafeGetString("usuario_ins"));
    }

    public static RegistroArt32Response MapRegistroArt32(IDataReader reader) => new(
        reader.SafeGetInt64("num_control_id"),
        reader.SafeGetString("emisor_rif"),
        reader.SafeGetString("emisor_razon_social"),
        SafeGetFecha(reader, "fecha_asignacion", "dd/MM/yyyy HH:mm:ss"),
        reader.SafeGetString("fecha_asignacion_8d"),
        reader.SafeGetString("tipo_documento"),
        reader.SafeGetString("numero_control"),
        reader.SafeGetString("numeracion_formato"),
        reader.SafeGetInt64("documento_id"),
        reader.SafeGetString("factura_servicio"),
        reader.SafeGetString("estado_conciliacion"),
        reader.SafeGetString("datos_adicionales"),
        reader.SafeGetInt64("reporte_id"),
        reader.SafeGetString("periodo"));

    // Vencido se calcula aqui y no en la pantalla: es la condicion que dispara
    // INV-2, y no puede depender de que cada consumidor la reimplemente igual.
    public static ReporteMensualResponse MapReporteMensual(IDataReader reader)
    {
        string estado = reader.SafeGetString("estado");

        return new ReporteMensualResponse(
            reader.SafeGetInt64("id"),
            reader.SafeGetString("periodo"),
            SafeGetFecha(reader, "fecha_cierre", "dd/MM/yyyy"),
            SafeGetFecha(reader, "fecha_vence", "dd/MM/yyyy"),
            SafeGetFecha(reader, "enviado_en", "dd/MM/yyyy HH:mm"),
            reader.SafeGetInt32("cantidad_reportada"),
            estado,
            SafeGetFecha(reader, "ultimo_intento_en", "dd/MM/yyyy HH:mm"),
            reader.SafeGetString("ultimo_error"),
            estado == "vencido");
    }

    // Los COUNT de PostgreSQL vuelven como bigint: se leen con SafeGetInt64 y se
    // truncan a int. Ningun contador de este modulo puede acercarse al limite de
    // int, y el frontend recibe numeros y no cadenas.
    public static PanelResumenResponse MapPanelResumen(IDataReader reader, string periodo) =>
        new(
            periodo,
            (int)reader.SafeGetInt64("documentos_periodo"),
            (int)reader.SafeGetInt64("documentos_prueba"),
            (int)reader.SafeGetInt64("numeros_periodo"),
            (int)reader.SafeGetInt64("emisores_total"),
            (int)reader.SafeGetInt64("emisores_activos"),
            (int)reader.SafeGetInt64("emisores_rif_sin_verificar"),
            reader.SafeGetString("periodo_estado"),
            SafeGetFecha(reader, "periodo_vence", "dd/MM/yyyy"),
            reader.SafeGetInt32("periodo_dias_restantes"),
            reader.SafeGetInt32("periodo_cantidad_reportada"),
            (int)reader.SafeGetInt64("periodos_vencidos_anio"));

    // Nulos via helper y no con "?? DBNull.Value" inline, como pide el estandar.
    public static object DbValue(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

    public static object DbValueFecha(DateTime? valor) =>
        valor.HasValue ? valor.Value.Date : (object)DBNull.Value;

    // 0 o negativo significa "sin documento": va NULL a la base, para que el
    // UNIQUE de DOCUMENTO_ID no considere iguales a dos asignaciones sin
    // documento. En PostgreSQL varios NULL no chocan entre si; varios 0 si.
    public static object DbValueId(long valor) =>
        valor > 0 ? valor : DBNull.Value;

    // Codigo SQLSTATE de violacion de unicidad en PostgreSQL. La defensa contra
    // el RIF duplicado es el UNIQUE de la tabla, no un SELECT previo: entre la
    // consulta y el insert cabe otra peticion.
    public const string SqlStateUnico = "23505";

    public static bool EsClaveDuplicada(NpgsqlException ex) =>
        ex is PostgresException pg && pg.SqlState == SqlStateUnico;

    public static bool EsRifDuplicado(NpgsqlException ex) => EsClaveDuplicada(ex);

    // Nombre de la restriccion que choco. Sirve para distinguir cual de las dos
    // mitades de INV-1 se violo sin adivinar por el texto del mensaje.
    public static string NombreRestriccion(NpgsqlException ex) =>
        ex is PostgresException pg ? pg.ConstraintName ?? string.Empty : string.Empty;

    // En minuscula: PostgreSQL pliega los identificadores sin comillas, asi que la
    // restriccion escrita FED_NUM_CONTROL_DOC_UK existe como fed_num_control_doc_uk.
    public const string RestriccionDocumentoUnico = "fed_num_control_doc_uk";
}
