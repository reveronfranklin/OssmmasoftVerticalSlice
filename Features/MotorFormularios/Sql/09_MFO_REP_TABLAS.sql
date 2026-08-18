-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Tablas del modo "parametros de reporte"
-- Requerimiento 16. Decision 7 de la Fase 0: Camino B, el modo entra en v1 y se
-- adelanta antes del diseñador.
--
-- Se instalan aunque el modo no se use: agregar tablas y dos columnas a un
-- schema ya poblado cuesta mas que instalarlas vacias ahora, y las columnas
-- nuevas tienen valor por defecto, asi que no afectan al nucleo.
--
-- Nota de seguridad que atraviesa todo este bloque: ninguna de estas tablas
-- guarda algo que se ejecute. CLAVE_REGISTRO es una CLAVE DE BUSQUEDA en una
-- lista blanca escrita en C# (MfoRegistroReportes.cs), nunca una ruta ni un
-- nombre de objeto ejecutable. No existe -ni debe existir- un
-- SP_MFO_REP_EJECUTAR generico que reciba el nombre de un procedimiento y lo
-- ejecute.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Dos columnas nuevas en MFO_FORMULARIO. Con DEFAULT para que las filas ya
-- sembradas por 08 queden en modo CAPTURA sin tocarlas.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_FORMULARIO ADD (
    MODO_USO       VARCHAR2(12) DEFAULT 'CAPTURA' NOT NULL,
    REGISTRA_EJEC  CHAR(1)      DEFAULT 'N'       NOT NULL
);

COMMENT ON COLUMN MFO_FORMULARIO.MODO_USO IS
    'CAPTURA / PARAMETROS / MIXTO. Decide si el formulario guarda respuestas, alimenta reportes, o ambas.';
COMMENT ON COLUMN MFO_FORMULARIO.REGISTRA_EJEC IS
    'Solo aplica si MODO_USO<>CAPTURA. S persiste cada ejecucion como MFO_RESPUESTA; N la ejecuta sin guardar.';

-- -----------------------------------------------------------------------------
-- Los reportes que un formulario puede ejecutar. N por formulario: el mismo
-- juego de parametros alimenta varios reportes y el usuario elige.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_REPORTE (
    REPORTE_ID      NUMBER          NOT NULL,
    FORMULARIO_ID   NUMBER          NOT NULL,
    CLAVE           VARCHAR2(40)    NOT NULL,
    NOMBRE          VARCHAR2(150)   NOT NULL,
    DESCRIPCION     VARCHAR2(500),
    TIPO_EJEC       VARCHAR2(12)    NOT NULL,
    CLAVE_REGISTRO  VARCHAR2(60)    NOT NULL,
    TITULO_REPORTE  VARCHAR2(200),
    ORIENTACION     VARCHAR2(10)    NOT NULL,
    MAX_FILAS       NUMBER,
    TIMEOUT_SEG     NUMBER,
    ORDEN           NUMBER          NOT NULL,
    ACTIVO          CHAR(1)         NOT NULL,
    USUARIO_INS     VARCHAR2(60),
    FECHA_INS       DATE,
    USUARIO_UPD     VARCHAR2(60),
    FECHA_UPD       DATE
);

COMMENT ON COLUMN MFO_REPORTE.CLAVE_REGISTRO IS
    'Clave de busqueda en la lista blanca de MfoRegistroReportes.cs. NO es una ruta ni un nombre de objeto ejecutable.';

-- -----------------------------------------------------------------------------
-- Mapeo explicito de entrada del formulario a parametro del reporte. No se
-- asume que MFO_CAMPO.CLAVE coincida con el nombre del parametro.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_REP_PARAM (
    REP_PARAM_ID    NUMBER          NOT NULL,
    REPORTE_ID      NUMBER          NOT NULL,
    NOMBRE_PARAM    VARCHAR2(30)    NOT NULL,
    ORIGEN          VARCHAR2(8)     NOT NULL,
    CAMPO_ID        NUMBER,
    VALOR_FIJO      VARCHAR2(500),
    CLAVE_SISTEMA   VARCHAR2(30),
    TIPO_DATO       VARCHAR2(10)    NOT NULL,
    FORMATO         VARCHAR2(30),
    OBLIGATORIO     CHAR(1)         NOT NULL,
    VALOR_DEFECTO   VARCHAR2(500),
    ORDEN           NUMBER          NOT NULL
);

COMMENT ON COLUMN MFO_REP_PARAM.CLAVE_SISTEMA IS
    'Dominio cerrado. Se resuelve SIEMPRE en el servidor: es lo que impide que un cliente manipulado pida el reporte de otra empresa.';

-- -----------------------------------------------------------------------------
-- Solo para TIPO_EJEC='SP_CURSOR': como renderizar las columnas que devuelve el
-- ref cursor. Es lo que permite agregar un listado sin escribir codigo de PDF.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_REP_COLUMNA (
    REP_COLUMNA_ID  NUMBER          NOT NULL,
    REPORTE_ID      NUMBER          NOT NULL,
    NOMBRE_COL      VARCHAR2(30)    NOT NULL,
    TITULO          VARCHAR2(100)   NOT NULL,
    ORDEN           NUMBER          NOT NULL,
    ANCHO_REL       NUMBER          NOT NULL,
    ALINEACION      VARCHAR2(8)     NOT NULL,
    FORMATO         VARCHAR2(30),
    TOTALIZAR       CHAR(1)         NOT NULL,
    AGRUPAR         CHAR(1)         NOT NULL,
    VISIBLE         CHAR(1)         NOT NULL
);

-- -----------------------------------------------------------------------------
-- Bitacora de ejecuciones, incluidas las fallidas. Es informacion que hoy no
-- existe en ninguna parte del ERP: quien ejecuto que reporte, con que
-- parametros y cuando.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_REP_EJEC (
    REP_EJEC_ID     NUMBER          NOT NULL,
    REPORTE_ID      NUMBER          NOT NULL,
    RESPUESTA_ID    NUMBER,
    CODIGO_EMPRESA  NUMBER          NOT NULL,
    USUARIO         VARCHAR2(60),
    FECHA_INICIO    DATE            NOT NULL,
    MILISEGUNDOS    NUMBER,
    FILAS           NUMBER,
    RESULTADO       VARCHAR2(10)    NOT NULL,
    MENSAJE         VARCHAR2(500),
    PARAMS_CLB      CLOB,
    IP_ORIGEN       VARCHAR2(45)
);

COMMENT ON COLUMN MFO_REP_EJEC.PARAMS_CLB IS
    'Parametros efectivamente bindeados, incluidos los de origen SISTEMA. Se escribe siempre, tambien en ejecucion transitoria: es lo que hace auditable una ejecucion que no guarda respuesta.';
