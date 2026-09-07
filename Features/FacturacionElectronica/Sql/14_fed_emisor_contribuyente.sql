-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 5
--
-- Dos cambios aditivos que la Providencia SNAT/2011/00071 obliga a hacer, y que
-- no se podian anticipar antes de leerla (T5.1).
--
-- 1) TIPO DE CONTRIBUYENTE EN EL EMISOR (D-31). Art. 23: la nota cumple los
--    requisitos del Art. 13 -contribuyentes ordinarios del IVA- o del Art. 15
--    -no ordinarios-, "SEGUN SEA EL CASO". El caso es una propiedad del emisor y
--    no de la peticion: dejarlo viajar en el request permitiria que la misma
--    empresa emitiera notas bajo dos regimenes distintos.
--    El Art. 15.6 agrega una leyenda obligatoria que solo aplica a los no
--    ordinarios: "Contribuyente Formal" o "no sujeto al impuesto al valor
--    agregado".
--
-- 2) MONEDA EN EL DOCUMENTO (D-30). Art. 13.14: una operacion en moneda
--    extranjera debe expresar AMBOS MONTOS y el TIPO DE CAMBIO. El Art. 7 de la
--    102 -que rige la factura- NO lo pide; el Art. 23 lo hace aplicable a las
--    notas. La asimetria es de la norma, no del diseno: el dato se guarda para
--    cualquier documento, porque pertenece a donde vive el monto, y se EXIGE
--    solo donde la norma lo exige. Guardar sin exigir es lo unico que se puede
--    hacer sin inventar una obligacion para la factura ni negar la de la nota.
--
-- Los dos son ALTER aditivos con valor por defecto, asi que no cambian el
-- comportamiento de nada de lo ya construido.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) Tipo de contribuyente del emisor
-- -----------------------------------------------------------------------------

ALTER TABLE FED.FED_EMISOR
    ADD COLUMN IF NOT EXISTS TIPO_CONTRIBUYENTE VARCHAR(20) NOT NULL DEFAULT 'ordinario';

-- Se agrega por separado y con guarda: ADD CONSTRAINT no admite IF NOT EXISTS en
-- PostgreSQL, y el script tiene que poder correrse dos veces.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fed_emisor_tipo_contrib_ck'
    ) THEN
        ALTER TABLE FED.FED_EMISOR
            ADD CONSTRAINT FED_EMISOR_TIPO_CONTRIB_CK
            CHECK (TIPO_CONTRIBUYENTE IN ('ordinario', 'formal', 'no_sujeto'));
    END IF;
END $$;

COMMENT ON COLUMN FED.FED_EMISOR.TIPO_CONTRIBUYENTE IS
    'Decide si la nota valida contra el Art. 13 o el 15 de la 00071, y si lleva la leyenda del 15.6 (D-31).';

-- -----------------------------------------------------------------------------
-- 2) Moneda del documento
-- -----------------------------------------------------------------------------

ALTER TABLE FED.FED_DOCUMENTO
    ADD COLUMN IF NOT EXISTS MONEDA       CHAR(3)       NOT NULL DEFAULT 'VES';

ALTER TABLE FED.FED_DOCUMENTO
    ADD COLUMN IF NOT EXISTS TASA_CAMBIO  NUMERIC(18,6);

ALTER TABLE FED.FED_DOCUMENTO
    ADD COLUMN IF NOT EXISTS TOTAL_MONEDA NUMERIC(18,2);

-- La coherencia del 13.14 la sostiene el motor, no el codigo. Un documento en
-- divisas sin tasa de cambio no puede existir ni por error de programacion.
-- El CHECK va en los dos sentidos, no en uno. Que un documento en bolivares
-- traiga tasa de cambio no es inofensivo: seria un dato que nadie sabe leer, y
-- al imprimir el Art. 13.14 no se sabria si expresarlo o no.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fed_documento_moneda_ck') THEN
        ALTER TABLE FED.FED_DOCUMENTO DROP CONSTRAINT FED_DOCUMENTO_MONEDA_CK;
    END IF;

    ALTER TABLE FED.FED_DOCUMENTO
        ADD CONSTRAINT FED_DOCUMENTO_MONEDA_CK
        CHECK (
            (MONEDA = 'VES' AND TASA_CAMBIO IS NULL AND TOTAL_MONEDA IS NULL)
            OR (MONEDA <> 'VES' AND TASA_CAMBIO IS NOT NULL AND TASA_CAMBIO > 0
                AND TOTAL_MONEDA IS NOT NULL AND TOTAL_MONEDA >= 0)
        );
END $$;

COMMENT ON COLUMN FED.FED_DOCUMENTO.MONEDA       IS 'Codigo de moneda. VES por defecto; distinto obliga a tasa y monto (Art. 13.14, D-30).';
COMMENT ON COLUMN FED.FED_DOCUMENTO.TASA_CAMBIO  IS 'Tipo de cambio del Art. 13.14. Obligatorio cuando MONEDA no es VES.';
COMMENT ON COLUMN FED.FED_DOCUMENTO.TOTAL_MONEDA IS 'Valor total expresado en la moneda extranjera. El otro monto es TOTAL_GENERAL.';
