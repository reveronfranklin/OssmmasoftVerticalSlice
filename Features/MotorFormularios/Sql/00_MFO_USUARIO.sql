-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Creacion del schema propio
-- Requerimiento 16. Decision 1 de la Fase 0: schema Oracle propio MFO.
--
-- Se ejecuta UNA sola vez, conectado como DBA. El resto de los scripts (01 en
-- adelante) se ejecutan conectado como MFO.
--
-- Consecuencia aceptada de tener schema propio: una transaccion no puede cruzar
-- dos schemas en este repositorio, asi que el enlace de una respuesta a una
-- entidad de negocio de otro modulo (ENTIDAD_REF / CLAVE_REF) es eventual, no
-- transaccional.
-- =============================================================================
--Prueba Update
CREATE USER MFO IDENTIFIED BY MFO
    DEFAULT TABLESPACE SAMI_DATA01
    TEMPORARY TABLESPACE TEMP;

ALTER USER MFO QUOTA UNLIMITED ON SAMI_DATA01;

GRANT CREATE SESSION      TO MFO;
GRANT CREATE TABLE        TO MFO;
GRANT CREATE SEQUENCE     TO MFO;
GRANT CREATE VIEW         TO MFO;
GRANT CREATE TRIGGER      TO MFO;
GRANT CREATE PROCEDURE    TO MFO;

-- El motor no consulta tablas de otros schemas. La resolucion de catalogos
-- (CATALOGO_CLAVE) ocurre en el backend C# contra la lista blanca, con la
-- conexion del schema dueño de cada catalogo, no desde MFO.
