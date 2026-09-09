-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 8, correccion
--
-- FED_CONTING_UK era sensible a mayusculas/minusculas: "contingencia-000045" y
-- "CONTINGENCIA-000045" no chocaban entre si, aunque el Art. 16.3 y el CHECK
-- del prefijo (~*, insensible) y la validacion en C# (StartsWith con
-- OrdinalIgnoreCase) tratan las dos formas como la misma numeracion. Medido:
-- notificar las dos variantes para el mismo emisor insertaba dos filas.
--
-- Se reemplaza el UNIQIE por constraint por un indice unico sobre
-- UPPER(NUMERACION_FISICA), que es la forma estandar de unicidad
-- case-insensitive en PostgreSQL. No se normaliza el dato guardado: lo que el
-- emisor escribio se conserva tal cual para mostrarlo, solo la comparacion de
-- unicidad ignora el case.
--
-- Reejecutable: DROP CONSTRAINT IF EXISTS + CREATE UNIQUE INDEX IF NOT EXISTS.
-- =============================================================================

ALTER TABLE FED.FED_CONTINGENCIA DROP CONSTRAINT IF EXISTS FED_CONTING_UK;

CREATE UNIQUE INDEX IF NOT EXISTS FED_CONTING_UK_CI
    ON FED.FED_CONTINGENCIA (EMISOR_ID, UPPER(NUMERACION_FISICA));

COMMENT ON INDEX FED.FED_CONTING_UK_CI IS
    'Unicidad de (emisor, numeracion fisica) insensible a mayusculas/minusculas. Reemplaza a FED_CONTING_UK, que era sensible.';
