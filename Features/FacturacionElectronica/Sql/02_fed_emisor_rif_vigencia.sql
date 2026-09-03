-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 1
--
-- Control de vigencia del RIF del emisor. RF-A.3.2, Articulo 29.2: la imprenta
-- digital debe "solicitar al emisor el comprobante digital del RIF vigente".
--
-- Es una verificacion recurrente, no una carga unica al dar de alta: por eso se
-- guarda cuando se verifico y con que resultado, y no un simple booleano.
--
-- Nota abierta: en la Gaceta 43.435 del 12/08/2026 se reformo el regimen del RIF
-- otorgandole vigencia indefinida. Si se confirma, "vigente" pasa a significar
-- existente y activo, no no-vencido. La columna soporta ambas lecturas.
-- =============================================================================

ALTER TABLE FED.FED_EMISOR
    ADD COLUMN IF NOT EXISTS RIF_VERIFICADO_EL     DATE,
    ADD COLUMN IF NOT EXISTS RIF_VERIFICADO_ESTADO VARCHAR(20);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fed_emisor_rif_verif_ck'
          AND conrelid = 'fed.fed_emisor'::regclass
    ) THEN
        ALTER TABLE FED.FED_EMISOR
            ADD CONSTRAINT FED_EMISOR_RIF_VERIF_CK
            CHECK (RIF_VERIFICADO_ESTADO IS NULL
                   OR RIF_VERIFICADO_ESTADO IN ('vigente', 'no_vigente', 'sin_verificar'));
    END IF;
END
$$;

COMMENT ON COLUMN FED.FED_EMISOR.RIF_VERIFICADO_EL     IS 'Art. 29.2: fecha de la ultima verificacion del RIF ante el emisor.';
COMMENT ON COLUMN FED.FED_EMISOR.RIF_VERIFICADO_ESTADO IS 'Resultado de esa verificacion. NULL = nunca se verifico.';
