-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 7
--
-- La accion 'envio' en la bitacora.
--
-- El Art. 18.2 obliga a llevar "auditoria electronica de toda ACCION efectuada
-- para la emision, modificacion o anulacion" de los documentos. El CHECK original
-- admitia cuatro acciones -emision, modificacion, anulacion y rechazo- porque eran
-- las unicas que el modulo sabia hacer.
--
-- La Fase 7 agrega una quinta que es efectivamente una accion sobre el documento:
-- REMITIRLO al usuario final (Art. 18.9). No modifica el documento, igual que el
-- rechazo no lo crea, y por eso entra por el mismo criterio que ya habia hecho
-- entrar al rechazo: "toda accion", no "todo cambio".
--
-- Sin esta linea, el envio no se puede registrar. Y no registrarlo seria peor que
-- un hueco de auditoria: cuando un receptor diga que nunca recibio su factura, la
-- bitacora es lo unico que puede decir a que direccion se mando y cuando.
-- =============================================================================

ALTER TABLE FED.FED_BITACORA DROP CONSTRAINT IF EXISTS FED_BITACORA_ACCION_CK;

ALTER TABLE FED.FED_BITACORA
    ADD CONSTRAINT FED_BITACORA_ACCION_CK
    CHECK (ACCION IN ('emision', 'modificacion', 'anulacion', 'rechazo', 'envio'));

COMMENT ON COLUMN FED.FED_BITACORA.ACCION IS
    'Art. 18.2, "toda accion efectuada": emision, modificacion, anulacion, rechazo y envio al usuario final.';
