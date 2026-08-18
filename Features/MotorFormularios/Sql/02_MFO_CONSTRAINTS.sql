-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - PK, UK, CHECK y FK del nucleo
-- Requerimiento 16.
--
-- Orden: primero PK (las FK las necesitan), luego UK y CHECK, y al final las FK.
-- FK_MFO_FORM_VER_PUBL va de ultimo por la referencia circular entre
-- MFO_FORMULARIO y MFO_VERSION. En tiempo de ejecucion no hay problema de orden:
-- un formulario siempre nace con VERSION_PUBL_ID nulo.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Claves primarias
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT PK_MFO_TIPO_CAMPO PRIMARY KEY (TIPO_CAMPO_ID);
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT PK_MFO_FORMULARIO PRIMARY KEY (FORMULARIO_ID);
ALTER TABLE MFO_VERSION    ADD CONSTRAINT PK_MFO_VERSION    PRIMARY KEY (VERSION_ID);
ALTER TABLE MFO_SECCION    ADD CONSTRAINT PK_MFO_SECCION    PRIMARY KEY (SECCION_ID);
ALTER TABLE MFO_CAMPO      ADD CONSTRAINT PK_MFO_CAMPO      PRIMARY KEY (CAMPO_ID);
ALTER TABLE MFO_OPCION     ADD CONSTRAINT PK_MFO_OPCION     PRIMARY KEY (OPCION_ID);
ALTER TABLE MFO_REGLA      ADD CONSTRAINT PK_MFO_REGLA      PRIMARY KEY (REGLA_ID);
ALTER TABLE MFO_CONDICION  ADD CONSTRAINT PK_MFO_CONDICION  PRIMARY KEY (CONDICION_ID);
ALTER TABLE MFO_RESPUESTA  ADD CONSTRAINT PK_MFO_RESPUESTA  PRIMARY KEY (RESPUESTA_ID);
ALTER TABLE MFO_VALOR      ADD CONSTRAINT PK_MFO_VALOR      PRIMARY KEY (VALOR_ID);
ALTER TABLE MFO_ADJUNTO    ADD CONSTRAINT PK_MFO_ADJUNTO    PRIMARY KEY (ADJUNTO_ID);
ALTER TABLE MFO_PERMISO    ADD CONSTRAINT PK_MFO_PERMISO    PRIMARY KEY (PERMISO_ID);
ALTER TABLE MFO_AUDITORIA  ADD CONSTRAINT PK_MFO_AUDITORIA  PRIMARY KEY (AUDITORIA_ID);

-- -----------------------------------------------------------------------------
-- Claves unicas
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT UK_MFO_TIPO_CODIGO  UNIQUE (CODIGO);
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT UK_MFO_FORM_ALIAS   UNIQUE (ALIAS);
ALTER TABLE MFO_VERSION    ADD CONSTRAINT UK_MFO_VER_NUM      UNIQUE (FORMULARIO_ID, NUMERO);
ALTER TABLE MFO_SECCION    ADD CONSTRAINT UK_MFO_SEC_CLAVE    UNIQUE (VERSION_ID, CLAVE);
ALTER TABLE MFO_CAMPO      ADD CONSTRAINT UK_MFO_CAMPO_CLAVE  UNIQUE (VERSION_ID, CLAVE);
ALTER TABLE MFO_OPCION     ADD CONSTRAINT UK_MFO_OPCION_VALOR UNIQUE (CAMPO_ID, VALOR);
ALTER TABLE MFO_VALOR      ADD CONSTRAINT UK_MFO_VALOR_UBIC   UNIQUE (RESPUESTA_ID, CAMPO_ID, FILA, ORDEN_VAL);
ALTER TABLE MFO_RESPUESTA  ADD CONSTRAINT UK_MFO_RESP_IDEM    UNIQUE (CLAVE_IDEM);
ALTER TABLE MFO_PERMISO    ADD CONSTRAINT UK_MFO_PERMISO      UNIQUE (FORMULARIO_ID, ROL_CODIGO, ACCION);

-- -----------------------------------------------------------------------------
-- CHECK - MFO_TIPO_CAMPO
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT CK_MFO_TIPO_COL_VAL
    CHECK (COLUMNA_VALOR IN ('TXT', 'NUM', 'FEC', 'CLB', 'NUL'));
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT CK_MFO_TIPO_OPCIONES
    CHECK (ADMITE_OPCIONES IN ('S', 'N'));
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT CK_MFO_TIPO_MULTIPLE
    CHECK (ADMITE_MULTIPLE IN ('S', 'N'));
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT CK_MFO_TIPO_PRESENT
    CHECK (ES_PRESENTACION IN ('S', 'N'));
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT CK_MFO_TIPO_ARCHIVO
    CHECK (ADMITE_ARCHIVO IN ('S', 'N'));
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT CK_MFO_TIPO_ACTIVO
    CHECK (ACTIVO IN ('S', 'N'));

-- Un tipo de presentacion no guarda valor, y uno que guarda valor necesita
-- columna donde guardarlo. Es la coherencia que sostiene el mapeo de valores.
ALTER TABLE MFO_TIPO_CAMPO ADD CONSTRAINT CK_MFO_TIPO_PRES_COL
    CHECK ((ES_PRESENTACION = 'S' AND COLUMNA_VALOR = 'NUL')
        OR (ES_PRESENTACION = 'N' AND COLUMNA_VALOR <> 'NUL'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_FORMULARIO
-- ALIAS limitado a A-Z0-9_ para que MFO_V_<ALIAS> de la fase de proyeccion sea
-- un identificador Oracle valido y no haya que sanear en tiempo de ejecucion.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT CK_MFO_FORM_ALIAS
    CHECK (REGEXP_LIKE(ALIAS, '^[A-Z][A-Z0-9_]*$'));
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT CK_MFO_FORM_ESTADO
    CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'));
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT CK_MFO_FORM_BORRADOR
    CHECK (PERMITE_BORRADOR IN ('S', 'N'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_VERSION
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_VERSION ADD CONSTRAINT CK_MFO_VER_ESTADO
    CHECK (ESTADO IN ('BORRADOR', 'PUBLICADA', 'ARCHIVADA'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_SECCION
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_SECCION ADD CONSTRAINT CK_MFO_SEC_PASO
    CHECK (ES_PASO IN ('S', 'N'));
ALTER TABLE MFO_SECCION ADD CONSTRAINT CK_MFO_SEC_REPETIBLE
    CHECK (REPETIBLE IN ('S', 'N'));
ALTER TABLE MFO_SECCION ADD CONSTRAINT CK_MFO_SEC_COLAPSABLE
    CHECK (COLAPSABLE IN ('S', 'N'));
ALTER TABLE MFO_SECCION ADD CONSTRAINT CK_MFO_SEC_COLUMNAS
    CHECK (COLUMNAS BETWEEN 1 AND 4);

-- MIN_FILAS/MAX_FILAS solo tienen sentido en secciones repetibles, y el rango
-- debe ser coherente. Lo verifica tambien SP_MFO_VER_VALIDAR, pero aqui queda
-- cerrado a nivel de dato.
ALTER TABLE MFO_SECCION ADD CONSTRAINT CK_MFO_SEC_FILAS
    CHECK ((REPETIBLE = 'N' AND MIN_FILAS IS NULL AND MAX_FILAS IS NULL)
        OR (REPETIBLE = 'S' AND NVL(MIN_FILAS, 0) <= NVL(MAX_FILAS, 999999)));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_CAMPO
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_CAMPO ADD CONSTRAINT CK_MFO_CAMPO_REQUERIDO
    CHECK (REQUERIDO IN ('S', 'N'));
ALTER TABLE MFO_CAMPO ADD CONSTRAINT CK_MFO_CAMPO_SOLO_LEC
    CHECK (SOLO_LECTURA IN ('S', 'N'));
ALTER TABLE MFO_CAMPO ADD CONSTRAINT CK_MFO_CAMPO_ANCHO
    CHECK (ANCHO BETWEEN 1 AND 12);

-- CATALOGO_CLAVE es requerida si el origen es CATALOGO y no debe existir si no.
-- Esa clave la resuelve el backend contra una lista blanca; nunca se concatena
-- en SQL.
ALTER TABLE MFO_CAMPO ADD CONSTRAINT CK_MFO_CAMPO_ORIGEN_OPC
    CHECK ((ORIGEN_OPCIONES IS NULL AND CATALOGO_CLAVE IS NULL)
        OR (ORIGEN_OPCIONES = 'ESTATICA' AND CATALOGO_CLAVE IS NULL)
        OR (ORIGEN_OPCIONES = 'CATALOGO' AND CATALOGO_CLAVE IS NOT NULL));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_OPCION
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_OPCION ADD CONSTRAINT CK_MFO_OPCION_DEFECTO
    CHECK (ES_DEFECTO IN ('S', 'N'));
ALTER TABLE MFO_OPCION ADD CONSTRAINT CK_MFO_OPCION_ACTIVO
    CHECK (ACTIVO IN ('S', 'N'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_REGLA
-- Dominio cerrado de los 14 tipos de regla de v1.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_REGLA ADD CONSTRAINT CK_MFO_REGLA_TIPO
    CHECK (TIPO_REGLA IN ('REQUERIDO', 'LONG_MIN', 'LONG_MAX', 'PATRON',
                          'MIN', 'MAX', 'DECIMALES', 'FEC_MIN', 'FEC_MAX',
                          'SEL_MIN', 'SEL_MAX', 'ARCH_MAX_MB', 'ARCH_EXT',
                          'UNICO'));
ALTER TABLE MFO_REGLA ADD CONSTRAINT CK_MFO_REGLA_ACTIVO
    CHECK (ACTIVO IN ('S', 'N'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_CONDICION
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_CONDICION ADD CONSTRAINT CK_MFO_COND_ACCION
    CHECK (ACCION IN ('MOSTRAR', 'OCULTAR', 'EXIGIR', 'BLOQUEAR'));
ALTER TABLE MFO_CONDICION ADD CONSTRAINT CK_MFO_COND_DESTINO
    CHECK (DESTINO_TIPO IN ('CAMPO', 'SECCION'));
ALTER TABLE MFO_CONDICION ADD CONSTRAINT CK_MFO_COND_OPERADOR
    CHECK (OPERADOR IN ('IGUAL', 'DISTINTO', 'CONTIENE', 'VACIO', 'NO_VACIO',
                        'MAYOR', 'MENOR', 'EN_LISTA'));
ALTER TABLE MFO_CONDICION ADD CONSTRAINT CK_MFO_COND_CONECTOR
    CHECK (CONECTOR IN ('Y', 'O'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_RESPUESTA
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_RESPUESTA ADD CONSTRAINT CK_MFO_RESP_ESTADO
    CHECK (ESTADO IN ('BORRADOR', 'ENVIADA', 'ANULADA'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_VALOR
-- FILA 0 = seccion no repetible; ORDEN_VAL 0 = campo simple. Que el 0 sea valido
-- y no nulo es lo que hace utilizable UK_MFO_VALOR_UBIC: en Oracle una columna
-- nula no participa de una clave unica.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_VALOR ADD CONSTRAINT CK_MFO_VALOR_FILA
    CHECK (FILA >= 0);
ALTER TABLE MFO_VALOR ADD CONSTRAINT CK_MFO_VALOR_ORDEN
    CHECK (ORDEN_VAL >= 0);

-- -----------------------------------------------------------------------------
-- CHECK - MFO_ADJUNTO
-- Decision 2 de la Fase 0: filesystem. La columna CONTENIDO queda creada para no
-- migrar el modelo si alguna vez se cambia de criterio, pero el CHECK exige
-- exactamente uno de los dos destinos, asi que no pueden convivir por descuido.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_ADJUNTO ADD CONSTRAINT CK_MFO_ADJUNTO_DEST
    CHECK ((RUTA IS NOT NULL AND CONTENIDO IS NULL)
        OR (RUTA IS NULL AND CONTENIDO IS NOT NULL));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_PERMISO
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_PERMISO ADD CONSTRAINT CK_MFO_PERMISO_ACCION
    CHECK (ACCION IN ('DISENAR', 'LLENAR', 'VER', 'EXPORTAR', 'ANULAR'));

-- -----------------------------------------------------------------------------
-- Claves foraneas
--
-- IMPORTANTE - por que el arbol de DEFINICION no lleva ON DELETE CASCADE:
--
-- Los triggers de inmutabilidad de 05_MFO_TRIGGERS.sql son triggers de fila que
-- necesitan consultar el estado de la version dueña. En Oracle, un trigger de
-- fila no puede consultar una tabla que la sentencia esta modificando (ORA-04091,
-- tabla mutante), y el borrado en cascada cuenta como parte de esa sentencia.
--
-- Con ON DELETE CASCADE, un `DELETE FROM MFO_VERSION` haria disparar
-- TRG_MFO_SEC_LOCK sobre cada seccion hija, y ese trigger consulta MFO_VERSION,
-- que en ese momento esta mutando: el borrado de un BORRADOR fallaria con
-- ORA-04091 en vez de funcionar. Lo mismo pasa entre MFO_CAMPO y sus opciones y
-- reglas.
--
-- Por eso el borrado del arbol de definicion es explicito y de abajo hacia
-- arriba, dentro de los procedimientos (SP_MFO_CAMPO_DELETE, SP_MFO_SEC_DELETE):
-- opciones y reglas -> condiciones -> campos -> secciones -> version. Cada
-- sentencia toca una sola tabla y los triggers ven un estado estable.
--
-- El arbol de DATOS (respuesta -> valor -> adjunto) si lleva cascada: no tiene
-- triggers de inmutabilidad, asi que no hay tabla mutante que consultar.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_VERSION ADD CONSTRAINT FK_MFO_VER_FORM
    FOREIGN KEY (FORMULARIO_ID) REFERENCES MFO_FORMULARIO (FORMULARIO_ID);

ALTER TABLE MFO_SECCION ADD CONSTRAINT FK_MFO_SEC_VER
    FOREIGN KEY (VERSION_ID) REFERENCES MFO_VERSION (VERSION_ID);

ALTER TABLE MFO_CAMPO ADD CONSTRAINT FK_MFO_CAMPO_SEC
    FOREIGN KEY (SECCION_ID) REFERENCES MFO_SECCION (SECCION_ID);
ALTER TABLE MFO_CAMPO ADD CONSTRAINT FK_MFO_CAMPO_VER
    FOREIGN KEY (VERSION_ID) REFERENCES MFO_VERSION (VERSION_ID);
ALTER TABLE MFO_CAMPO ADD CONSTRAINT FK_MFO_CAMPO_TIPO
    FOREIGN KEY (TIPO_CAMPO_ID) REFERENCES MFO_TIPO_CAMPO (TIPO_CAMPO_ID);

ALTER TABLE MFO_OPCION ADD CONSTRAINT FK_MFO_OPCION_CAMPO
    FOREIGN KEY (CAMPO_ID) REFERENCES MFO_CAMPO (CAMPO_ID);

ALTER TABLE MFO_REGLA ADD CONSTRAINT FK_MFO_REGLA_CAMPO
    FOREIGN KEY (CAMPO_ID) REFERENCES MFO_CAMPO (CAMPO_ID);

ALTER TABLE MFO_CONDICION ADD CONSTRAINT FK_MFO_COND_VER
    FOREIGN KEY (VERSION_ID) REFERENCES MFO_VERSION (VERSION_ID);

-- Sin cascada tambien por una razon funcional: borrar un campo que es origen de
-- una condicion debe fallar y obligar al diseñador a resolverlo, no llevarse la
-- condicion en silencio y dejar el formulario con una rama menos sin aviso.
ALTER TABLE MFO_CONDICION ADD CONSTRAINT FK_MFO_COND_ORIGEN
    FOREIGN KEY (CAMPO_ORIGEN_ID) REFERENCES MFO_CAMPO (CAMPO_ID);

ALTER TABLE MFO_RESPUESTA ADD CONSTRAINT FK_MFO_RESP_VER
    FOREIGN KEY (VERSION_ID) REFERENCES MFO_VERSION (VERSION_ID);
ALTER TABLE MFO_RESPUESTA ADD CONSTRAINT FK_MFO_RESP_FORM
    FOREIGN KEY (FORMULARIO_ID) REFERENCES MFO_FORMULARIO (FORMULARIO_ID);

ALTER TABLE MFO_VALOR ADD CONSTRAINT FK_MFO_VALOR_RESP
    FOREIGN KEY (RESPUESTA_ID) REFERENCES MFO_RESPUESTA (RESPUESTA_ID) ON DELETE CASCADE;
ALTER TABLE MFO_VALOR ADD CONSTRAINT FK_MFO_VALOR_CAMPO
    FOREIGN KEY (CAMPO_ID) REFERENCES MFO_CAMPO (CAMPO_ID);

ALTER TABLE MFO_ADJUNTO ADD CONSTRAINT FK_MFO_ADJUNTO_VALOR
    FOREIGN KEY (VALOR_ID) REFERENCES MFO_VALOR (VALOR_ID) ON DELETE CASCADE;

ALTER TABLE MFO_PERMISO ADD CONSTRAINT FK_MFO_PERMISO_FORM
    FOREIGN KEY (FORMULARIO_ID) REFERENCES MFO_FORMULARIO (FORMULARIO_ID) ON DELETE CASCADE;

-- Circular: se agrega de ultimo. Un formulario nace con VERSION_PUBL_ID nulo.
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT FK_MFO_FORM_VER_PUBL
    FOREIGN KEY (VERSION_PUBL_ID) REFERENCES MFO_VERSION (VERSION_ID);
