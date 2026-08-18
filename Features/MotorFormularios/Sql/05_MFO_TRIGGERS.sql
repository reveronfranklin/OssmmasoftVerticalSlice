-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Triggers de inmutabilidad (6)
-- Requerimiento 16.
--
-- Estos triggers son lo que hace REAL la decision de versionado inmutable. Sin
-- ellos, "una version publicada no se modifica" seria una convencion del backend
-- que cualquier UPDATE manual o cualquier slice nuevo podria romper en silencio,
-- corrompiendo respuestas historicas que ya se renderizaron con esa definicion.
--
-- Reglas que imponen:
--   - Las tablas de definicion (seccion, campo, opcion, regla, condicion) solo
--     admiten INSERT, UPDATE y DELETE mientras su version dueña este en
--     BORRADOR.
--   - MFO_VERSION solo admite las transiciones de estado validas.
--
-- INSERT entra en el trigger, y no es un detalle menor: el modelo original solo
-- contemplaba UPDATE y DELETE, pero sin cubrir INSERT se podria AGREGAR un campo
-- a una version ya publicada. Eso cambia la definicion que las respuestas ya
-- guardadas dicen haber usado, que es exactamente lo que el versionado inmutable
-- existe para impedir. En INSERT se lee :NEW porque :OLD no existe.
--
-- Restriccion de Oracle que condiciona el diseño: un trigger de fila no puede
-- consultar una tabla que la sentencia esta modificando. Por eso el arbol de
-- definicion NO tiene ON DELETE CASCADE (ver la nota extensa en
-- 02_MFO_CONSTRAINTS.sql) y por eso cada trigger consulta hacia ARRIBA
-- (opcion -> campo -> version), nunca hacia la tabla que se esta tocando.
--
-- Rango de error: -20501 a -20599, reservado para MFO.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- MFO_SECCION
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TRG_MFO_SEC_LOCK
BEFORE INSERT OR UPDATE OR DELETE ON MFO_SECCION
FOR EACH ROW
DECLARE
    v_estado     MFO_VERSION.ESTADO%TYPE;
    v_version_id NUMBER;
BEGIN
    -- En INSERT no existe :OLD.
    IF INSERTING THEN
        v_version_id := :NEW.VERSION_ID;
    ELSE
        v_version_id := :OLD.VERSION_ID;
    END IF;

    SELECT ESTADO INTO v_estado
      FROM MFO_VERSION
     WHERE VERSION_ID = v_version_id;

    IF v_estado <> 'BORRADOR' THEN
        RAISE_APPLICATION_ERROR(-20501,
            'La seccion pertenece a una version ' || v_estado ||
            '. Cree una version nueva para modificar el formulario.');
    END IF;
END;
/

-- -----------------------------------------------------------------------------
-- MFO_CAMPO
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TRG_MFO_CAMPO_LOCK
BEFORE INSERT OR UPDATE OR DELETE ON MFO_CAMPO
FOR EACH ROW
DECLARE
    v_estado     MFO_VERSION.ESTADO%TYPE;
    v_version_id NUMBER;
BEGIN
    -- En INSERT no existe :OLD.
    IF INSERTING THEN
        v_version_id := :NEW.VERSION_ID;
    ELSE
        v_version_id := :OLD.VERSION_ID;
    END IF;

    SELECT ESTADO INTO v_estado
      FROM MFO_VERSION
     WHERE VERSION_ID = v_version_id;

    IF v_estado <> 'BORRADOR' THEN
        RAISE_APPLICATION_ERROR(-20502,
            'El campo pertenece a una version ' || v_estado ||
            '. Cree una version nueva para modificar el formulario.');
    END IF;
END;
/

-- -----------------------------------------------------------------------------
-- MFO_OPCION
-- Sube dos niveles porque MFO_OPCION no lleva VERSION_ID: el modelo solo
-- denormaliza VERSION_ID hasta MFO_CAMPO.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TRG_MFO_OPCION_LOCK
BEFORE INSERT OR UPDATE OR DELETE ON MFO_OPCION
FOR EACH ROW
DECLARE
    v_estado   MFO_VERSION.ESTADO%TYPE;
    v_campo_id NUMBER;
BEGIN
    -- En INSERT no existe :OLD.
    IF INSERTING THEN
        v_campo_id := :NEW.CAMPO_ID;
    ELSE
        v_campo_id := :OLD.CAMPO_ID;
    END IF;

    SELECT v.ESTADO INTO v_estado
      FROM MFO_VERSION v
      JOIN MFO_CAMPO c ON c.VERSION_ID = v.VERSION_ID
     WHERE c.CAMPO_ID = v_campo_id;

    IF v_estado <> 'BORRADOR' THEN
        RAISE_APPLICATION_ERROR(-20503,
            'La opcion pertenece a una version ' || v_estado ||
            '. Cree una version nueva para modificar el formulario.');
    END IF;
END;
/

-- -----------------------------------------------------------------------------
-- MFO_REGLA
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TRG_MFO_REGLA_LOCK
BEFORE INSERT OR UPDATE OR DELETE ON MFO_REGLA
FOR EACH ROW
DECLARE
    v_estado   MFO_VERSION.ESTADO%TYPE;
    v_campo_id NUMBER;
BEGIN
    -- En INSERT no existe :OLD.
    IF INSERTING THEN
        v_campo_id := :NEW.CAMPO_ID;
    ELSE
        v_campo_id := :OLD.CAMPO_ID;
    END IF;

    SELECT v.ESTADO INTO v_estado
      FROM MFO_VERSION v
      JOIN MFO_CAMPO c ON c.VERSION_ID = v.VERSION_ID
     WHERE c.CAMPO_ID = v_campo_id;

    IF v_estado <> 'BORRADOR' THEN
        RAISE_APPLICATION_ERROR(-20504,
            'La regla pertenece a una version ' || v_estado ||
            '. Cree una version nueva para modificar el formulario.');
    END IF;
END;
/

-- -----------------------------------------------------------------------------
-- MFO_CONDICION
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TRG_MFO_COND_LOCK
BEFORE INSERT OR UPDATE OR DELETE ON MFO_CONDICION
FOR EACH ROW
DECLARE
    v_estado     MFO_VERSION.ESTADO%TYPE;
    v_version_id NUMBER;
BEGIN
    -- En INSERT no existe :OLD.
    IF INSERTING THEN
        v_version_id := :NEW.VERSION_ID;
    ELSE
        v_version_id := :OLD.VERSION_ID;
    END IF;

    SELECT ESTADO INTO v_estado
      FROM MFO_VERSION
     WHERE VERSION_ID = v_version_id;

    IF v_estado <> 'BORRADOR' THEN
        RAISE_APPLICATION_ERROR(-20505,
            'La condicion pertenece a una version ' || v_estado ||
            '. Cree una version nueva para modificar el formulario.');
    END IF;
END;
/

-- -----------------------------------------------------------------------------
-- MFO_VERSION - transiciones de estado
--
-- No consulta ninguna tabla: solo compara :OLD y :NEW, asi que es inmune al
-- problema de tabla mutante.
--
-- Transiciones permitidas:
--   BORRADOR  -> BORRADOR   (edicion normal del borrador)
--   BORRADOR  -> PUBLICADA  (SP_MFO_VER_PUBLICAR)
--   PUBLICADA -> ARCHIVADA  (SP_MFO_VER_ARCHIVAR, o al publicar la siguiente)
--
-- Una version PUBLICADA solo puede cambiar de estado a ARCHIVADA: el resto de
-- sus columnas queda congelado, incluido HASH_DEF, que es la huella con la que
-- se comprueba que la definicion no cambio despues de publicarse.
-- Una version ARCHIVADA no vuelve. Republicar es clonar.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TRG_MFO_VER_LOCK
BEFORE UPDATE OR DELETE ON MFO_VERSION
FOR EACH ROW
BEGIN
    IF DELETING THEN
        IF :OLD.ESTADO <> 'BORRADOR' THEN
            RAISE_APPLICATION_ERROR(-20506,
                'Solo se puede eliminar una version en BORRADOR. Esta version esta '
                || :OLD.ESTADO || '.');
        END IF;
        RETURN;
    END IF;

    IF :OLD.ESTADO = 'BORRADOR' AND :NEW.ESTADO IN ('BORRADOR', 'PUBLICADA') THEN
        RETURN;
    END IF;

    IF :OLD.ESTADO = 'PUBLICADA' AND :NEW.ESTADO = 'ARCHIVADA' THEN
        RETURN;
    END IF;

    RAISE_APPLICATION_ERROR(-20507,
        'Transicion de estado no permitida: ' || :OLD.ESTADO || ' -> ' || :NEW.ESTADO
        || '. Para modificar un formulario publicado, cree una version nueva.');
END;
/
