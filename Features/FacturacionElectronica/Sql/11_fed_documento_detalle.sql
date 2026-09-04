-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 4
--
-- Tabla FED_DOCUMENTO_DETALLE: los renglones del documento. Articulo 7.8 y 7.9.
--
-- El 7.8 pide descripcion, codigo de la operacion y precio; cantidad cuando el
-- precio refiere a varios bienes o servicios iguales. Y una regla de formato que
-- parece menor y no lo es:
--
--   Si la operacion es exenta, exonerada o no gravada, junto a la descripcion o
--   al precio debe aparecer el caracter E separado por un espacio en blanco y
--   entre parentesis, con el formato (E).
--
-- La norma fija el literal, asi que aca se guarda el HECHO -este renglon es
-- exento- y la marca la pone la representacion. Guardar el "(E)" como texto seria
-- guardar la presentacion en vez del dato, y despues nadie puede filtrar por
-- exento sin buscar una cadena.
--
-- APPEND-ONLY, como el documento del que cuelga: un renglon emitido no se edita.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_DOCUMENTO_DETALLE (
    ID              BIGINT        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    DOCUMENTO_ID    BIGINT        NOT NULL,

    -- Orden en que se muestran. El documento impreso tiene que poder reproducirse
    -- igual que se emitio, y sin esto el orden lo decidiria el motor.
    ORDEN           INTEGER       NOT NULL,

    DESCRIPCION     VARCHAR(500)  NOT NULL,
    CODIGO          VARCHAR(60),

    -- 7.8: la cantidad se exige cuando el precio refiere a varios bienes o
    -- servicios iguales. Se guarda siempre, con 1 por defecto, porque un renglon
    -- sin cantidad es un renglon de cantidad uno.
    CANTIDAD        NUMERIC(18,4) NOT NULL DEFAULT 1,
    PRECIO          NUMERIC(18,2) NOT NULL,

    -- La alicuota aplicada, GUARDADA y no referenciada (D-22). Si manana cambia
    -- la tasa en el catalogo del ERP, este renglon conserva la que se uso.
    -- Cero cuando el renglon es exento, exonerado o no gravado.
    ALICUOTA        NUMERIC(5,2)  NOT NULL DEFAULT 0,

    -- El hecho, no la marca. El "(E)" del 7.8 lo pone la representacion grafica.
    EXENTO          BOOLEAN       NOT NULL DEFAULT FALSE,

    -- 7.9: si la prestacion de servicios comporta entrega de bienes, hay que
    -- describir esos bienes.
    BIENES_ENTREGADOS VARCHAR(500),

    -- 7.10: cargos, descuentos, bonificaciones y cualquier otro ajuste al precio,
    -- con descripcion y valor. Se guardan por renglon para poder reproducir el
    -- documento tal como se emitio.
    AJUSTE_DESCRIPCION VARCHAR(200),
    AJUSTE_VALOR       NUMERIC(18,2) NOT NULL DEFAULT 0,

    -- Lo que este renglon aporta al documento. Se guarda calculado y no se deriva
    -- al leer: el total impreso en un documento fiscal es un hecho historico, y
    -- recalcularlo despues con otra regla de redondeo daria otro numero.
    TOTAL_RENGLON   NUMERIC(18,2) NOT NULL,

    CONSTRAINT FED_DOC_DETALLE_DOC_FK FOREIGN KEY (DOCUMENTO_ID)
        REFERENCES FED.FED_DOCUMENTO (ID),

    CONSTRAINT FED_DOC_DETALLE_ORDEN_UK UNIQUE (DOCUMENTO_ID, ORDEN),

    CONSTRAINT FED_DOC_DETALLE_CANT_CK  CHECK (CANTIDAD > 0),
    CONSTRAINT FED_DOC_DETALLE_PREC_CK  CHECK (PRECIO >= 0),
    CONSTRAINT FED_DOC_DETALLE_ALIC_CK  CHECK (ALICUOTA >= 0 AND ALICUOTA <= 100),

    -- Coherencia entre el hecho y la tasa: un renglon exento no puede llevar
    -- alicuota, y uno con alicuota no puede estar marcado exento. El 7.11 obliga a
    -- discriminar la base por alicuota y a informar aparte el total exento; si un
    -- renglon fuera las dos cosas, esa discriminacion no cerraria.
    CONSTRAINT FED_DOC_DETALLE_EXENTO_CK
        CHECK ((EXENTO = TRUE AND ALICUOTA = 0) OR (EXENTO = FALSE))
);

CREATE INDEX IF NOT EXISTS FED_DOC_DETALLE_DOC_IX
    ON FED.FED_DOCUMENTO_DETALLE (DOCUMENTO_ID);

COMMENT ON TABLE  FED.FED_DOCUMENTO_DETALLE          IS 'Renglones del documento. Arts. 7.8, 7.9 y 7.10. APPEND-ONLY.';
COMMENT ON COLUMN FED.FED_DOCUMENTO_DETALLE.EXENTO   IS 'El hecho de que el renglon sea exento, exonerado o no gravado. La marca "(E)" del Art. 7.8 la pone la representacion grafica, no el dato.';
COMMENT ON COLUMN FED.FED_DOCUMENTO_DETALLE.ALICUOTA IS 'Tasa aplicada, guardada y no referenciada (D-22): un cambio futuro del catalogo no puede alterar un documento emitido.';
