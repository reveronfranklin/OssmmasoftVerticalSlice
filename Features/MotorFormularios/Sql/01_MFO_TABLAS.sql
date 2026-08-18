-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Tablas del nucleo (13)
-- Requerimiento 16. Modelo columna por columna en Modelo-Datos.md.
--
-- Este script crea SOLO las tablas y las columnas. Las PK, FK, UK y CHECK van en
-- 02_MFO_CONSTRAINTS.sql: MFO_FORMULARIO y MFO_VERSION se referencian mutuamente
-- (VERSION_PUBL_ID <-> FORMULARIO_ID) y esa circularidad obliga a separar la
-- creacion de las tablas de la de sus restricciones.
--
-- Convenciones (Modelo-Datos.md):
--   - Todos los IDs son NUMBER alimentados por secuencia explicita (10g no tiene
--     IDENTITY).
--   - Los flags son CHAR(1) con dominio 'S'/'N', nunca NUMBER(1).
--   - "auditoria estandar" = USUARIO_INS, FECHA_INS, USUARIO_UPD, FECHA_UPD, y
--     solo la llevan las tablas que el modelo la declara.
--   - CODIGO_EMPRESA lo escribe el backend desde settings:EmpresaConfig; no
--     viaja en los DTOs del frontend.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Catalogo semilla de tipos de campo. Punto de extensibilidad del motor: agregar
-- un tipo es una fila aqui mas un componente React registrado.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_TIPO_CAMPO (
    TIPO_CAMPO_ID    NUMBER          NOT NULL,
    CODIGO           VARCHAR2(30)    NOT NULL,
    NOMBRE           VARCHAR2(60)    NOT NULL,
    COLUMNA_VALOR    VARCHAR2(3)     NOT NULL,
    ADMITE_OPCIONES  CHAR(1)         NOT NULL,
    ADMITE_MULTIPLE  CHAR(1)         NOT NULL,
    ES_PRESENTACION  CHAR(1)         NOT NULL,
    ADMITE_ARCHIVO   CHAR(1)         NOT NULL,
    COMPONENTE       VARCHAR2(60)    NOT NULL,
    ORDEN            NUMBER          NOT NULL,
    ICONO            VARCHAR2(60),
    ACTIVO           CHAR(1)         NOT NULL
);

-- -----------------------------------------------------------------------------
-- Identidad estable del formulario a traves del tiempo. No contiene definicion.
-- MODO_USO y REGISTRA_EJEC se agregan en 09_MFO_REP_TABLAS.sql.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_FORMULARIO (
    FORMULARIO_ID     NUMBER          NOT NULL,
    ALIAS             VARCHAR2(24)    NOT NULL,
    NOMBRE            VARCHAR2(150)   NOT NULL,
    DESCRIPCION       VARCHAR2(1000),
    CATEGORIA         VARCHAR2(60),
    CODIGO_EMPRESA    NUMBER          NOT NULL,
    ESTADO            VARCHAR2(12)    NOT NULL,
    VERSION_PUBL_ID   NUMBER,
    ENTIDAD_DESTINO   VARCHAR2(30),
    MAX_RESP_USUARIO  NUMBER,
    PERMITE_BORRADOR  CHAR(1)         NOT NULL,
    USUARIO_INS       VARCHAR2(60),
    FECHA_INS         DATE,
    USUARIO_UPD       VARCHAR2(60),
    FECHA_UPD         DATE
);

-- -----------------------------------------------------------------------------
-- Una version de la definicion. Inmutable en cuanto sale de BORRADOR.
-- Como maximo una PUBLICADA por formulario: lo refuerza IDX_MFO_VER_PUBL_UNQ en
-- 04_MFO_INDICES.sql, no una validacion de aplicacion.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_VERSION (
    VERSION_ID         NUMBER          NOT NULL,
    FORMULARIO_ID      NUMBER          NOT NULL,
    NUMERO             NUMBER          NOT NULL,
    ESTADO             VARCHAR2(12)    NOT NULL,
    NOTAS              VARCHAR2(1000),
    HASH_DEF           VARCHAR2(64),
    VERSION_ORIGEN_ID  NUMBER,
    FECHA_PUBL         DATE,
    USUARIO_PUBL       VARCHAR2(60),
    FECHA_ARCH         DATE,
    USUARIO_INS        VARCHAR2(60),
    FECHA_INS          DATE,
    USUARIO_UPD        VARCHAR2(60),
    FECHA_UPD          DATE
);

-- -----------------------------------------------------------------------------
-- Agrupacion visual y unidad de repeticion. Separa el layout del modelo de
-- campos, lo que permite wizards y grillas sin tocar MFO_CAMPO.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_SECCION (
    SECCION_ID   NUMBER          NOT NULL,
    VERSION_ID   NUMBER          NOT NULL,
    CLAVE        VARCHAR2(30)    NOT NULL,
    TITULO       VARCHAR2(150),
    DESCRIPCION  VARCHAR2(1000),
    ORDEN        NUMBER          NOT NULL,
    COLUMNAS     NUMBER          NOT NULL,
    ES_PASO      CHAR(1)         NOT NULL,
    REPETIBLE    CHAR(1)         NOT NULL,
    MIN_FILAS    NUMBER,
    MAX_FILAS    NUMBER,
    COLAPSABLE   CHAR(1)         NOT NULL
);

-- -----------------------------------------------------------------------------
-- VERSION_ID esta denormalizado desde la seccion a proposito: lo usan los
-- triggers de inmutabilidad y la carga completa de una definicion, y sin el cada
-- lectura pagaria un join.
-- CLAVE es estable entre versiones; es lo que permite comparar respuestas
-- llenadas con definiciones distintas.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_CAMPO (
    CAMPO_ID          NUMBER          NOT NULL,
    VERSION_ID        NUMBER          NOT NULL,
    SECCION_ID        NUMBER          NOT NULL,
    TIPO_CAMPO_ID     NUMBER          NOT NULL,
    CLAVE             VARCHAR2(30)    NOT NULL,
    ETIQUETA          VARCHAR2(200)   NOT NULL,
    AYUDA             VARCHAR2(500),
    PLACEHOLDER       VARCHAR2(100),
    ORDEN             NUMBER          NOT NULL,
    ANCHO             NUMBER          NOT NULL,
    REQUERIDO         CHAR(1)         NOT NULL,
    SOLO_LECTURA      CHAR(1)         NOT NULL,
    VALOR_DEFECTO     VARCHAR2(500),
    ORIGEN_OPCIONES   VARCHAR2(12),
    CATALOGO_CLAVE    VARCHAR2(40),
    MASCARA           VARCHAR2(60),
    UNIDAD            VARCHAR2(20),
    EXPRESION         VARCHAR2(1000)
);

COMMENT ON COLUMN MFO_CAMPO.EXPRESION IS
    'Reservado para campos CALCULADO. Sin uso en v1.';

CREATE TABLE MFO_OPCION (
    OPCION_ID    NUMBER          NOT NULL,
    CAMPO_ID     NUMBER          NOT NULL,
    VALOR        VARCHAR2(100)   NOT NULL,
    ETIQUETA     VARCHAR2(200)   NOT NULL,
    ORDEN        NUMBER          NOT NULL,
    GRUPO        VARCHAR2(60),
    ES_DEFECTO   CHAR(1)         NOT NULL,
    ACTIVO       CHAR(1)         NOT NULL
);

-- -----------------------------------------------------------------------------
-- Validaciones como dato. Las ejecuta el interprete C# (autoridad) y el
-- interprete TypeScript (UX). UNICO es la unica que no puede evaluarse en
-- memoria: se resuelve en SP_MFO_RESP_SUBMIT.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_REGLA (
    REGLA_ID     NUMBER          NOT NULL,
    CAMPO_ID     NUMBER          NOT NULL,
    TIPO_REGLA   VARCHAR2(20)    NOT NULL,
    PARAM_1      VARCHAR2(500),
    PARAM_2      VARCHAR2(500),
    MENSAJE      VARCHAR2(300)   NOT NULL,
    ORDEN        NUMBER          NOT NULL,
    ACTIVO       CHAR(1)         NOT NULL
);

-- -----------------------------------------------------------------------------
-- Logica de mostrar/ocultar/exigir declarativa.
-- DESTINO_ID es polimorfico (CAMPO o SECCION) y por eso NO lleva FK: se gana una
-- sola tabla de condiciones y se pierde integridad referencial. La validacion de
-- que el destino existe y pertenece a la misma version corre en
-- SP_MFO_VER_VALIDAR, que es prerequisito de publicar.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_CONDICION (
    CONDICION_ID     NUMBER          NOT NULL,
    VERSION_ID       NUMBER          NOT NULL,
    ACCION           VARCHAR2(12)    NOT NULL,
    DESTINO_TIPO     VARCHAR2(8)     NOT NULL,
    DESTINO_ID       NUMBER          NOT NULL,
    CAMPO_ORIGEN_ID  NUMBER          NOT NULL,
    OPERADOR         VARCHAR2(12)    NOT NULL,
    VALOR_COMPARA    VARCHAR2(500),
    GRUPO            NUMBER          NOT NULL,
    CONECTOR         VARCHAR2(1)     NOT NULL,
    ORDEN            NUMBER          NOT NULL
);

-- -----------------------------------------------------------------------------
-- El sobre de un envio. Apunta a la VERSION, no al formulario: es lo que permite
-- re-renderizar una respuesta historica exactamente como fue llenada.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_RESPUESTA (
    RESPUESTA_ID    NUMBER          NOT NULL,
    VERSION_ID      NUMBER          NOT NULL,
    FORMULARIO_ID   NUMBER          NOT NULL,
    CODIGO_EMPRESA  NUMBER          NOT NULL,
    ESTADO          VARCHAR2(12)    NOT NULL,
    CLAVE_IDEM      VARCHAR2(36),
    ENTIDAD_REF     VARCHAR2(30),
    CLAVE_REF       VARCHAR2(60),
    USUARIO_LLENA   VARCHAR2(60),
    FECHA_INICIO    DATE            NOT NULL,
    FECHA_ENVIO     DATE,
    IP_ORIGEN       VARCHAR2(45),
    SNAPSHOT_CLB    CLOB,
    MOTIVO_ANULA    VARCHAR2(300),
    USUARIO_INS     VARCHAR2(60),
    FECHA_INS       DATE,
    USUARIO_UPD     VARCHAR2(60),
    FECHA_UPD       DATE
);

COMMENT ON COLUMN MFO_RESPUESTA.SNAPSHOT_CLB IS
    'Payload crudo tal como llego. Escribir y no leer: es evidencia, no fuente.';

-- -----------------------------------------------------------------------------
-- Una fila por (respuesta, campo, fila de seccion repetible, indice multivalor).
-- CLAVE_CAMPO esta denormalizado desde MFO_CAMPO.CLAVE a proposito: lo usan
-- todos los reportes que cruzan versiones.
--
-- Cual columna de valor se llena lo dicta MFO_TIPO_CAMPO.COLUMNA_VALOR. No hay
-- CHECK que exija "exactamente una columna no nula" porque la correlacion es con
-- el tipo del campo y no con la fila aislada; en 10g eso seria un trigger. Se
-- valida en SP_MFO_RESP_VAL_SAVE.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_VALOR (
    VALOR_ID      NUMBER          NOT NULL,
    RESPUESTA_ID  NUMBER          NOT NULL,
    CAMPO_ID      NUMBER          NOT NULL,
    CLAVE_CAMPO   VARCHAR2(30)    NOT NULL,
    FILA          NUMBER          NOT NULL,
    ORDEN_VAL     NUMBER          NOT NULL,
    VALOR_TXT     VARCHAR2(4000),
    VALOR_NUM     NUMBER,
    VALOR_FEC     DATE,
    VALOR_CLB     CLOB,
    ETIQUETA_VAL  VARCHAR2(200)
);

CREATE TABLE MFO_ADJUNTO (
    ADJUNTO_ID       NUMBER          NOT NULL,
    VALOR_ID         NUMBER          NOT NULL,
    NOMBRE_ARCHIVO   VARCHAR2(260)   NOT NULL,
    MIME             VARCHAR2(120),
    TAMANO_BYTES     NUMBER          NOT NULL,
    HASH_SHA256      VARCHAR2(64),
    RUTA             VARCHAR2(500),
    CONTENIDO        BLOB,
    USUARIO_INS      VARCHAR2(60),
    FECHA_INS        DATE,
    USUARIO_UPD      VARCHAR2(60),
    FECHA_UPD        DATE
);

-- -----------------------------------------------------------------------------
-- Decision 4 de la Fase 0: ROL_CODIGO guarda el codigo de rol de
-- SIS.OSS_USUARIO_ROL. El motor no conoce esa tabla ni la consulta -no puede,
-- esta en otro schema-: guarda el codigo y el backend lo compara con el rol del
-- usuario autenticado que ya resuelve la aplicacion.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_PERMISO (
    PERMISO_ID     NUMBER          NOT NULL,
    FORMULARIO_ID  NUMBER          NOT NULL,
    ROL_CODIGO     VARCHAR2(30)    NOT NULL,
    ACCION         VARCHAR2(12)    NOT NULL
);

-- -----------------------------------------------------------------------------
-- Bitacora transversal. Sin FK a proposito: debe sobrevivir al borrado de lo
-- auditado.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_AUDITORIA (
    AUDITORIA_ID  NUMBER          NOT NULL,
    ENTIDAD       VARCHAR2(20)    NOT NULL,
    ENTIDAD_ID    NUMBER,
    ACCION        VARCHAR2(20)    NOT NULL,
    USUARIO       VARCHAR2(60),
    FECHA         DATE            NOT NULL,
    DETALLE_CLB   CLOB
);
