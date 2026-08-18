-- =============================================================================
-- MFO - Alta o modificacion de un campo.
--
-- VERSION_ID se deriva de la seccion y no se recibe como parametro: esta
-- denormalizado en MFO_CAMPO, y dejar que el cliente lo mande abriria la puerta
-- a un campo cuya VERSION_ID no coincide con la de su seccion. Ese estado
-- ninguna FK lo detecta -las dos son validas por separado- y romperia los
-- triggers de inmutabilidad y la carga de la definicion.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_CAMPO_UPSERT (
    p_CampoId        IN  NUMBER,
    p_SeccionId      IN  NUMBER,
    p_TipoCodigo     IN  VARCHAR2,
    p_Clave          IN  VARCHAR2,
    p_Etiqueta       IN  VARCHAR2,
    p_Ayuda          IN  VARCHAR2,
    p_Placeholder    IN  VARCHAR2,
    p_Orden          IN  NUMBER,
    p_Ancho          IN  NUMBER,
    p_Requerido      IN  CHAR,
    p_SoloLectura    IN  CHAR,
    p_ValorDefecto   IN  VARCHAR2,
    p_OrigenOpciones IN  VARCHAR2,
    p_CatalogoClave  IN  VARCHAR2,
    p_Mascara        IN  VARCHAR2,
    p_Unidad         IN  VARCHAR2,
    p_Usuario        IN  VARCHAR2,
    p_OutId          OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_clave       VARCHAR2(30) := UPPER(TRIM(p_Clave));
    v_version_id  NUMBER;
    v_tipo_id     NUMBER;
    v_presenta    CHAR(1);
    v_origen      VARCHAR2(12) := p_OrigenOpciones;
    v_catalogo    VARCHAR2(40) := p_CatalogoClave;
    v_id          NUMBER;
BEGIN
    p_OutId := 0;

    IF v_clave IS NULL OR p_Etiqueta IS NULL THEN
        p_Message := 'La clave y la etiqueta del campo son obligatorias.';
        RETURN;
    END IF;

    BEGIN
        SELECT VERSION_ID INTO v_version_id FROM MFO_SECCION WHERE SECCION_ID = p_SeccionId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La seccion indicada no existe.';
            RETURN;
    END;

    BEGIN
        SELECT TIPO_CAMPO_ID, ES_PRESENTACION INTO v_tipo_id, v_presenta
          FROM MFO_TIPO_CAMPO WHERE CODIGO = p_TipoCodigo AND ACTIVO = 'S';
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El tipo de campo ' || p_TipoCodigo || ' no existe o esta inactivo.';
            RETURN;
    END;

    -- Coherencia de origen de opciones. Se normaliza aqui para que
    -- CK_MFO_CAMPO_ORIGEN_OPC no salte con un ORA-02290 que el usuario no puede
    -- interpretar.
    IF v_origen = 'CATALOGO' AND v_catalogo IS NULL THEN
        p_Message := 'Un campo con origen CATALOGO necesita la clave del catalogo.';
        RETURN;
    END IF;

    IF NVL(v_origen, 'ESTATICA') <> 'CATALOGO' THEN
        v_catalogo := NULL;
    END IF;

    -- Un elemento de presentacion no captura valor: no puede ser obligatorio.
    -- Se corrige en silencio porque es un efecto de cambiar el tipo de un campo
    -- que ya estaba marcado, no una intencion del usuario.
    BEGIN
        SELECT CAMPO_ID INTO v_id
          FROM MFO_CAMPO
         WHERE VERSION_ID = v_version_id
           AND (CAMPO_ID = p_CampoId OR CLAVE = v_clave);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
        WHEN TOO_MANY_ROWS THEN
            p_Message := 'La clave ' || v_clave || ' ya la usa otro campo de esta version.';
            RETURN;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_id FROM DUAL;
        INSERT INTO MFO_CAMPO (
            CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
            PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
            ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
        ) VALUES (
            v_id, v_version_id, p_SeccionId, v_tipo_id, v_clave, p_Etiqueta, p_Ayuda,
            p_Placeholder, NVL(p_Orden, 10), NVL(p_Ancho, 12),
            CASE WHEN v_presenta = 'S' THEN 'N' ELSE NVL(p_Requerido, 'N') END,
            NVL(p_SoloLectura, 'N'), p_ValorDefecto,
            v_origen, v_catalogo, p_Mascara, p_Unidad, NULL
        );
    ELSE
        UPDATE MFO_CAMPO
           SET SECCION_ID      = p_SeccionId,
               TIPO_CAMPO_ID   = v_tipo_id,
               CLAVE           = v_clave,
               ETIQUETA        = p_Etiqueta,
               AYUDA           = p_Ayuda,
               PLACEHOLDER     = p_Placeholder,
               ORDEN           = NVL(p_Orden, ORDEN),
               ANCHO           = NVL(p_Ancho, ANCHO),
               REQUERIDO       = CASE WHEN v_presenta = 'S' THEN 'N' ELSE NVL(p_Requerido, REQUERIDO) END,
               SOLO_LECTURA    = NVL(p_SoloLectura, SOLO_LECTURA),
               VALOR_DEFECTO   = p_ValorDefecto,
               ORIGEN_OPCIONES = v_origen,
               CATALOGO_CLAVE  = v_catalogo,
               MASCARA         = p_Mascara,
               UNIDAD          = p_Unidad
         WHERE CAMPO_ID = v_id;
    END IF;

    COMMIT;
    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'La clave ' || v_clave || ' ya la usa otro campo de esta version.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        IF SQLCODE = -20502 THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
