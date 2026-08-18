-- =============================================================================
-- MFO - Alta o modificacion del mapeo campo -> parametro de reporte.
--
-- El campo se indica por CLAVE, no por CAMPO_ID, y esa eleccion es deliberada:
-- un CAMPO_ID pertenece a una version concreta, y al publicar una version nueva
-- todos los CAMPO_ID cambian. La CLAVE la preserva el clonado. Aceptar la clave
-- aqui -y resolverla contra la version publicada vigente- es lo que permite
-- reconfigurar el mapeo sin conocer ids internos.
--
-- La coherencia origen/fuente la garantiza CK_MFO_REP_PARAM_COHER, pero se
-- comprueba antes para devolver un mensaje util en vez de una violacion de
-- constraint: el diseñador tiene que entender que le falta, no ver un ORA-02290.
--
-- CLAVE_SISTEMA no se puede inventar: su dominio esta cerrado por CHECK y su
-- valor lo resuelve el servidor en cada ejecucion. Es lo que impide que un
-- cliente manipulado pida el reporte de otra empresa.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_PARAM_UPSERT (
    p_RepParamId   IN  NUMBER,
    p_ReporteId    IN  NUMBER,
    p_NombreParam  IN  VARCHAR2,
    p_Origen       IN  VARCHAR2,
    p_ClaveCampo   IN  VARCHAR2,
    p_ValorFijo    IN  VARCHAR2,
    p_ClaveSistema IN  VARCHAR2,
    p_TipoDato     IN  VARCHAR2,
    p_Formato      IN  VARCHAR2,
    p_Obligatorio  IN  CHAR,
    p_ValorDefecto IN  VARCHAR2,
    p_Orden        IN  NUMBER,
    p_Usuario      IN  VARCHAR2,
    p_OutId        OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_nombre   VARCHAR2(30) := TRIM(p_NombreParam);
    v_origen   VARCHAR2(8)  := UPPER(TRIM(p_Origen));
    v_tipo     VARCHAR2(10) := UPPER(TRIM(NVL(p_TipoDato, 'TEXTO')));
    v_sistema  VARCHAR2(30) := UPPER(TRIM(p_ClaveSistema));
    v_clave    VARCHAR2(30) := UPPER(TRIM(p_ClaveCampo));
    v_form_id  NUMBER;
    v_ver_id   NUMBER;
    v_campo_id NUMBER := NULL;
    v_fijo     VARCHAR2(500) := p_ValorFijo;
    v_id       NUMBER;
    v_aud      NUMBER;
    v_msg      VARCHAR2(4000);
BEGIN
    p_OutId := 0;

    IF v_nombre IS NULL THEN
        p_Message := 'El nombre del parametro es obligatorio.';
        RETURN;
    END IF;

    IF v_origen NOT IN ('CAMPO', 'FIJO', 'SISTEMA') THEN
        p_Message := 'El origen debe ser CAMPO, FIJO o SISTEMA.';
        RETURN;
    END IF;

    IF v_tipo NOT IN ('TEXTO', 'NUMERO', 'FECHA') THEN
        p_Message := 'El tipo de dato debe ser TEXTO, NUMERO o FECHA.';
        RETURN;
    END IF;

    BEGIN
        SELECT R.FORMULARIO_ID, F.VERSION_PUBL_ID
          INTO v_form_id, v_ver_id
          FROM MFO_REPORTE R
          JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
         WHERE R.REPORTE_ID = p_ReporteId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El reporte indicado no existe.';
            RETURN;
    END;

    -- ------------------------------------------------------------------------
    -- Resolucion de la fuente segun el origen declarado. Exactamente una queda
    -- poblada; las otras dos se fuerzan a nulo para no depender del cuidado del
    -- llamador.
    -- ------------------------------------------------------------------------
    IF v_origen = 'CAMPO' THEN
        IF v_clave IS NULL THEN
            p_Message := 'Con origen CAMPO debe indicar la clave del campo.';
            RETURN;
        END IF;

        IF v_ver_id IS NULL THEN
            p_Message := 'El formulario no tiene una version publicada contra la cual resolver el campo.';
            RETURN;
        END IF;

        BEGIN
            SELECT CAMPO_ID INTO v_campo_id
              FROM MFO_CAMPO
             WHERE VERSION_ID = v_ver_id
               AND CLAVE = v_clave;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                p_Message := 'El campo ' || v_clave || ' no existe en la version publicada del formulario.';
                RETURN;
        END;

        v_fijo := NULL;
        v_sistema := NULL;

    ELSIF v_origen = 'FIJO' THEN
        IF v_fijo IS NULL THEN
            p_Message := 'Con origen FIJO debe indicar el valor.';
            RETURN;
        END IF;

        v_campo_id := NULL;
        v_sistema := NULL;

    ELSE
        IF v_sistema IS NULL THEN
            p_Message := 'Con origen SISTEMA debe indicar la clave de sistema.';
            RETURN;
        END IF;

        IF v_sistema NOT IN ('CODIGO_EMPRESA', 'USUARIO', 'FECHA_ACTUAL', 'IP_ORIGEN') THEN
            p_Message := 'Clave de sistema no valida: ' || v_sistema ||
                         '. Use CODIGO_EMPRESA, USUARIO, FECHA_ACTUAL o IP_ORIGEN.';
            RETURN;
        END IF;

        v_campo_id := NULL;
        v_fijo := NULL;
    END IF;

    BEGIN
        SELECT REP_PARAM_ID INTO v_id
          FROM MFO_REP_PARAM
         WHERE REPORTE_ID = p_ReporteId
           AND (REP_PARAM_ID = p_RepParamId OR NOMBRE_PARAM = v_nombre);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
        WHEN TOO_MANY_ROWS THEN
            p_Message := 'El parametro ' || v_nombre || ' ya existe en este reporte.';
            RETURN;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_REP_PARAM.NEXTVAL INTO v_id FROM DUAL;

        INSERT INTO MFO_REP_PARAM (
            REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO,
            CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN
        ) VALUES (
            v_id, p_ReporteId, v_nombre, v_origen, v_campo_id, v_fijo,
            v_sistema, v_tipo, p_Formato, NVL(p_Obligatorio, 'N'), p_ValorDefecto,
            NVL(p_Orden, 10)
        );
    ELSE
        UPDATE MFO_REP_PARAM
           SET NOMBRE_PARAM  = v_nombre,
               ORIGEN        = v_origen,
               CAMPO_ID      = v_campo_id,
               VALOR_FIJO    = v_fijo,
               CLAVE_SISTEMA = v_sistema,
               TIPO_DATO     = v_tipo,
               FORMATO       = p_Formato,
               OBLIGATORIO   = NVL(p_Obligatorio, OBLIGATORIO),
               VALOR_DEFECTO = p_ValorDefecto,
               ORDEN         = NVL(p_Orden, ORDEN)
         WHERE REP_PARAM_ID = v_id;
    END IF;

    COMMIT;

    SP_MFO_AUD_INS('REP_PARAM', v_id, CASE WHEN p_RepParamId IS NULL THEN 'CREAR' ELSE 'MODIFICAR' END,
                   p_Usuario, 'Parametro ' || v_nombre || ' origen ' || v_origen, v_aud, v_msg);

    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'El parametro ' || v_nombre || ' ya existe en este reporte.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
