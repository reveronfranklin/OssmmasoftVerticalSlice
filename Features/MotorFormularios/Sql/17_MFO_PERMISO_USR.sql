-- =============================================================================
-- Motor de Formularios (MFO) - Permisos por usuario y por reporte
-- Requerimiento 16, extension posterior al cierre de la Fase 9.
--
-- Sustituye el modelo por rol por uno por usuario, y agrega un segundo eje que
-- el modelo original no tenia: que reportes de un formulario puede ejecutar
-- cada persona. Esto ultimo estaba anotado como limite conocido en
-- Revision-Seguridad.md ("el permiso de reporte es por formulario, no por
-- reporte; distinguirlos necesitaria una tabla propia"). Esta es esa tabla.
--
-- Tres decisiones que gobiernan el modelo:
--
--   1. **Solo por usuario.** MFO_PERMISO (por rol) deja de aplicar. Se conserva
--      la tabla para no perder lo configurado, pero ningun slice la consulta.
--      Un modelo con dos ejes obliga a mirar dos sitios para explicar por que
--      alguien entra, y esa pregunta se hace justo cuando hay un problema.
--
--   2. **Se asignan acciones**, el mismo dominio de siempre: DISENAR, LLENAR,
--      VER, EXPORTAR, ANULAR.
--
--   3. **Los reportes se heredan salvo que se acoten.** Con el formulario
--      asignado y sin ninguna fila en MFO_PERMISO_REP, el usuario ejecuta todos
--      sus reportes. En cuanto se le asigna uno, ejecuta solo los asignados. Es
--      la misma politica que ya rige MFO_PERMISO -sin configurar es abierto- y
--      evita que asignar un formulario nuevo sean siempre dos pasos.
--
-- Se ejecuta conectado como MFO.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Acciones concedidas a una persona sobre un formulario.
--
-- USUARIO guarda el login tal como llega en la cabecera X-Usuario. No hay FK a
-- SIS: esta en otro schema y una FK entre schemas ataria el motor al ciclo de
-- vida de la tabla de usuarios del ERP.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_PERMISO_USR (
    PERM_USR_ID    NUMBER          NOT NULL,
    FORMULARIO_ID  NUMBER          NOT NULL,
    USUARIO        VARCHAR2(60)    NOT NULL,
    ACCION         VARCHAR2(12)    NOT NULL,
    USUARIO_INS    VARCHAR2(60),
    FECHA_INS      DATE
);

COMMENT ON COLUMN MFO_PERMISO_USR.USUARIO IS
    'Login del ERP, el mismo que viaja en la cabecera X-Usuario. Sin FK a SIS: no se cruzan schemas.';

-- -----------------------------------------------------------------------------
-- Reportes concretos que una persona puede ejecutar.
--
-- La ausencia de filas significa "todos los del formulario". Es informacion en
-- la ausencia, asi que conviene decirlo donde se lea: sin esta convencion,
-- asignar un formulario obligaria a enumerar siempre sus reportes.
-- -----------------------------------------------------------------------------
CREATE TABLE MFO_PERMISO_REP (
    PERM_REP_ID    NUMBER          NOT NULL,
    REPORTE_ID     NUMBER          NOT NULL,
    USUARIO        VARCHAR2(60)    NOT NULL,
    USUARIO_INS    VARCHAR2(60),
    FECHA_INS      DATE
);

-- -----------------------------------------------------------------------------
-- Secuencias
-- -----------------------------------------------------------------------------
CREATE SEQUENCE SEQ_MFO_PERM_USR START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_PERM_REP START WITH 1 INCREMENT BY 1 NOCYCLE;

-- -----------------------------------------------------------------------------
-- Restricciones
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_PERMISO_USR ADD CONSTRAINT PK_MFO_PERM_USR PRIMARY KEY (PERM_USR_ID);
ALTER TABLE MFO_PERMISO_REP ADD CONSTRAINT PK_MFO_PERM_REP PRIMARY KEY (PERM_REP_ID);

ALTER TABLE MFO_PERMISO_USR ADD CONSTRAINT UK_MFO_PERM_USR
    UNIQUE (FORMULARIO_ID, USUARIO, ACCION);
ALTER TABLE MFO_PERMISO_REP ADD CONSTRAINT UK_MFO_PERM_REP
    UNIQUE (REPORTE_ID, USUARIO);

ALTER TABLE MFO_PERMISO_USR ADD CONSTRAINT FK_MFO_PERM_USR_FORM
    FOREIGN KEY (FORMULARIO_ID) REFERENCES MFO_FORMULARIO (FORMULARIO_ID);
ALTER TABLE MFO_PERMISO_REP ADD CONSTRAINT FK_MFO_PERM_REP_REP
    FOREIGN KEY (REPORTE_ID) REFERENCES MFO_REPORTE (REPORTE_ID);

-- Mismo dominio que CK_MFO_PERMISO_ACCION: los dos modelos hablan de lo mismo.
ALTER TABLE MFO_PERMISO_USR ADD CONSTRAINT CK_MFO_PERM_USR_ACC
    CHECK (ACCION IN ('DISENAR', 'LLENAR', 'VER', 'EXPORTAR', 'ANULAR'));

-- -----------------------------------------------------------------------------
-- Indices. La consulta caliente es "que puede este usuario en este formulario",
-- que se ejecuta en cada peticion del motor.
-- -----------------------------------------------------------------------------
CREATE INDEX IDX_MFO_PERM_USR_USR ON MFO_PERMISO_USR (USUARIO, FORMULARIO_ID);
CREATE INDEX IDX_MFO_PERM_REP_USR ON MFO_PERMISO_REP (USUARIO);

PROMPT === Verificacion
SELECT TABLE_NAME FROM USER_TABLES WHERE TABLE_NAME LIKE 'MFO_PERMISO%' ORDER BY TABLE_NAME;
