-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase Q (D-56)
--
-- Cupo de documentos obligatorio. SOLO actualiza el comentario de la tabla:
-- no cambia estructura ni datos.
--
-- Se ejecuta conectado como el rol fed, propietario de la tabla:
--
--   psql -h <host> -p 5432 -U fed -d OSSMMASOFT -f 27_fed_emisor_cupo_obligatorio.sql
--
-- Por que. El script 24 dejo escrito en el catalogo "Sin filas = sin limite":
-- un emisor sin ninguna carga de cupo emitia sin tope, para no romper a los
-- emisores que ya existian (D-54). El 2026-10-13 el usuario decidio que ese
-- modo no exista (D-56): todo emisor debe tener cupo para asignar numeros de
-- control, y el que no lo tiene queda bloqueado hasta que se le cargue. La
-- regla vive en EmisorCupoDb.VerificarAsync y en el alta del emisor; este
-- script solo evita que el catalogo siga describiendo la regla vieja.
--
-- Reejecutable: COMMENT ON reemplaza el comentario anterior.
-- =============================================================================

COMMENT ON TABLE FED.FED_EMISOR_CUPO IS
    'Cupo de documentos por emisor: una fila por carga o renovacion, solo insercion. Sin filas = sin cupo: el emisor no puede asignar numeros de control hasta que se le cargue (D-56, Fase Q). D-54, Fase M.';
