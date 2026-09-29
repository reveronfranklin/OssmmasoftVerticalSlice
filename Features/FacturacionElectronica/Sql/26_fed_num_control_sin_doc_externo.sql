-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - limpieza de campos muertos
--
-- Elimina FED_NUM_CONTROL.DOCUMENTO_EXTERNO y su UNIQUE, que agrego el script 09.
-- Se ejecuta conectado como el rol fed, propietario de la tabla:
--
--   psql -h <host> -p 5432 -U fed -d OSSMMASOFT -f 26_fed_num_control_sin_doc_externo.sql
--
-- Por que. La columna iba a guardar el identificador del documento de un emisor
-- en modo 'externa' (D-21). Ese caso termino resuelto por otro camino: la
-- numeracion que trae el emisor entra como NumeracionExterna y se guarda en
-- FED_DOCUMENTO.NUMERACION, donde INV-3 ya la hace unica por emisor, tipo y
-- serie. Ningun handler, vista ni funcion escribe ni lee DOCUMENTO_EXTERNO: lo
-- comprobo la auditoria de campos del 2026-09-29, que la encontro con un solo
-- valor, de una prueba de la Fase 2.
--
-- El GRANT UPDATE (DOCUMENTO_EXTERNO) que concedia GRANTS_FED_APP.sql se quito
-- de ese archivo el mismo dia; en una base donde ya se otorgo, el DROP COLUMN se
-- lleva el privilegio de columna con ella.
--
-- Reejecutable: los dos DROP llevan IF EXISTS.
-- =============================================================================

ALTER TABLE FED.FED_NUM_CONTROL
    DROP CONSTRAINT IF EXISTS FED_NUM_CONTROL_DOC_EXT_UK;

ALTER TABLE FED.FED_NUM_CONTROL
    DROP COLUMN IF EXISTS DOCUMENTO_EXTERNO;
