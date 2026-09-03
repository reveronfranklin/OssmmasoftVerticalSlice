-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 0
--
-- Bootstrap del modulo. Se ejecuta UNA sola vez, conectado como superusuario.
-- Los scripts 01 en adelante se ejecutan conectados como el rol fed.
--
-- Decisiones que sostiene este script:
--
--   D-10  Rol propio "fed", propietario del schema. No se reutiliza ossmmapg,
--         que es el usuario de report-server sobre esta misma base. En
--         PostgreSQL el propietario de una tabla siempre puede escribirla, asi
--         que el append-only que exige el Articulo 18.2 solo se puede sostener
--         con permisos si el usuario de aplicacion no es el dueno de la tabla.
--
--   D-6   La regla Oracle de 30 caracteres no aplica aqui: esto es PostgreSQL,
--         cuyo limite es 63. check-oracle-identifiers.sh no debe correrse sobre
--         esta carpeta.
--
-- IMPORTANTE - la base OSSMMASOFT es compartida. report-server trabaja sobre el
-- schema public de esta misma base. Este script NO toca public, no concede ni
-- revoca nada sobre el, y no altera ningun objeto existente.
--
-- Identificadores sin comillas a proposito: PostgreSQL los pliega a minusculas
-- de forma consistente, asi que FED y FED_EMISOR existen fisicamente como fed y
-- fed_emisor. Mientras nada se escriba entre comillas, el plegado es parejo.
--
-- Ejecucion:
--   psql -h <host> -p 5432 -U postgres -d OSSMMASOFT -f 00_fed_bootstrap.sql
-- =============================================================================

-- Rol de aplicacion del modulo. Reejecutable.
-- La clave de este script es la de desarrollo. En cualquier otro ambiente el DBA
-- la cambia al crear el rol y actualiza DefaultConnectionFed en appsettings.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fed') THEN
        CREATE ROLE fed WITH LOGIN PASSWORD 'fed';
    END IF;
END
$$;

-- Schema propio, con el rol como propietario.
CREATE SCHEMA IF NOT EXISTS FED AUTHORIZATION fed;

-- Permisos minimos: conectarse a la base y trabajar dentro de su propio schema.
GRANT CONNECT ON DATABASE "OSSMMASOFT" TO fed;
GRANT USAGE, CREATE ON SCHEMA FED TO fed;

-- Defensa contra un search_path inesperado: si un script olvidara calificar un
-- objeto, cae en FED y no en public, que es de report-server.
ALTER ROLE fed SET search_path = FED;

-- Nota deliberada: no se ejecuta REVOKE sobre public. Desde PostgreSQL 15 el
-- schema public ya no concede CREATE a PUBLIC, asi que fed no puede crear ahi.
-- Revocarlo de todos modos cambiaria permisos de un schema ajeno. Se verifica en
-- lugar de imponerlo; la comprobacion esta en el reporte de cierre de la fase.
