-- =============================================================================
-- MFO - Alta o modificacion de una condicion.
--
-- Es el unico procedimiento que escribe una referencia polimorfica
-- (DESTINO_TIPO + DESTINO_ID), y por eso es el que tiene que verificar a mano lo
-- que en cualquier otra tabla verificaria una FK: que el destino existe y que
-- pertenece a ESTA version. Sin esta comprobacion, la unica barrera restante
-- seria SP_MFO_VER_VALIDAR, es decir el momento de publicar, mucho despues de
-- que el usuario se equivocara.
--
-- El destino y el origen se reciben por CLAVE y no por ID: es lo que maneja el
-- diseñador, y resolverlo aqui evita que el frontend tenga que conocer los IDs
-- internos de la version.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_COND_UPSERT (
    p_CondicionId  IN  NUMBER,
    p_VersionId    IN  NUMBER,
    p_Accion       IN  VARCHAR2,
    p_DestinoTipo  IN  VARCHAR2,
    p_ClaveDestino IN  VARCHAR2,
    p_ClaveOrigen  IN  VARCHAR2,
    p_Operador     IN  VARCHAR2,
    p_ValorCompara IN  VARCHAR2,
    p_Grupo        IN  NUMBER,
    p_Conector     IN  VARCHAR2,
    p_Orden        IN  NUMBER,
    p_Usuario      IN  VARCHAR2,
    p_OutId        OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_destino_id NUMBER;
    v_origen_id  NUMBER;
    v_id         NUMBER;
BEGIN
    p_OutId := 0;

    IF p_DestinoTipo NOT IN ('CAMPO', 'SECCION') THEN
        p_Message := 'El tipo de destino debe ser CAMPO o SECCION.';
        RETURN;
    END IF;

    -- Resolucion del destino dentro de la version.
    BEGIN
        IF p_DestinoTipo = 'CAMPO' THEN
            SELECT CAMPO_ID INTO v_destino_id
              FROM MFO_CAMPO WHERE VERSION_ID = p_VersionId AND CLAVE = UPPER(TRIM(p_ClaveDestino));
        ELSE
            SELECT SECCION_ID INTO v_destino_id
              FROM MFO_SECCION WHERE VERSION_ID = p_VersionId AND CLAVE = UPPER(TRIM(p_ClaveDestino));
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El destino ' || p_ClaveDestino || ' no existe en esta version.';
            RETURN;
    END;

    BEGIN
        SELECT CAMPO_ID INTO v_origen_id
          FROM MFO_CAMPO WHERE VERSION_ID = p_VersionId AND CLAVE = UPPER(TRIM(p_ClaveOrigen));
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El campo origen ' || p_ClaveOrigen || ' no existe en esta version.';
            RETURN;
    END;

    -- Una condicion que se evalua sobre si misma es un ciclo de longitud 1: no
    -- converge nunca. Se atrapa aqui porque es el caso trivial y el mas comun;
    -- los ciclos largos los detecta SP_MFO_VER_VALIDAR.
    IF p_DestinoTipo = 'CAMPO' AND v_destino_id = v_origen_id THEN
        p_Message := 'Un campo no puede depender de si mismo.';
        RETURN;
    END IF;

    -- Los operadores que no comparan contra nada no llevan valor, y los que si
    -- comparan lo necesitan.
    IF p_Operador IN ('VACIO', 'NO_VACIO') THEN
        NULL;
    ELSIF p_ValorCompara IS NULL THEN
        p_Message := 'El operador ' || p_Operador || ' necesita un valor de comparacion.';
        RETURN;
    END IF;

    BEGIN
        SELECT CONDICION_ID INTO v_id
          FROM MFO_CONDICION WHERE CONDICION_ID = p_CondicionId AND VERSION_ID = p_VersionId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_CONDICION.NEXTVAL INTO v_id FROM DUAL;
        INSERT INTO MFO_CONDICION (
            CONDICION_ID, VERSION_ID, ACCION, DESTINO_TIPO, DESTINO_ID,
            CAMPO_ORIGEN_ID, OPERADOR, VALOR_COMPARA, GRUPO, CONECTOR, ORDEN
        ) VALUES (
            v_id, p_VersionId, p_Accion, p_DestinoTipo, v_destino_id,
            v_origen_id, p_Operador, p_ValorCompara, NVL(p_Grupo, 1),
            NVL(p_Conector, 'Y'), NVL(p_Orden, 10)
        );
    ELSE
        UPDATE MFO_CONDICION
           SET ACCION          = p_Accion,
               DESTINO_TIPO    = p_DestinoTipo,
               DESTINO_ID      = v_destino_id,
               CAMPO_ORIGEN_ID = v_origen_id,
               OPERADOR        = p_Operador,
               VALOR_COMPARA   = p_ValorCompara,
               GRUPO           = NVL(p_Grupo, GRUPO),
               CONECTOR        = NVL(p_Conector, CONECTOR),
               ORDEN           = NVL(p_Orden, ORDEN)
         WHERE CONDICION_ID = v_id;
    END IF;

    COMMIT;
    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        IF SQLCODE = -20505 THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
