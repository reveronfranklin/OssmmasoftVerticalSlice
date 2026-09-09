-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 8 (T8.5)
--
-- La accion 'contingencia' en la bitacora.
--
-- El Art. 16 obliga al emisor a notificar a la imprenta digital los documentos
-- fisicos emitidos durante una caida. Es una ACCION sobre el sistema en el
-- sentido del Art. 18.2 -"toda accion efectuada"-, igual que 'envio' entro en
-- la Fase 7: no modifica ningun documento existente, notifica un hecho nuevo.
--
-- La CONCILIACION -asociar la notificacion con el documento que la regulariza-
-- NO agrega una sexta accion. Sigue el mismo criterio que ENTREGADO_EN en
-- FED_RETENCION: completar un dato pendiente en FED_CONTINGENCIA (CONCILIADO_EN,
-- DOCUMENTO_ID, USUARIO_CONCILIA) queda registrado en esa misma fila, que no se
-- borra ni se reescribe fuera de esas tres columnas. Duplicarlo en la bitacora
-- seria un segundo lugar para la misma verdad.
-- =============================================================================

ALTER TABLE FED.FED_BITACORA DROP CONSTRAINT IF EXISTS FED_BITACORA_ACCION_CK;

ALTER TABLE FED.FED_BITACORA
    ADD CONSTRAINT FED_BITACORA_ACCION_CK
    CHECK (ACCION IN ('emision', 'modificacion', 'anulacion', 'rechazo', 'envio', 'contingencia'));

COMMENT ON COLUMN FED.FED_BITACORA.ACCION IS
    'Art. 18.2, "toda accion efectuada": emision, modificacion, anulacion, rechazo, envio y contingencia (notificacion del Art. 16).';
