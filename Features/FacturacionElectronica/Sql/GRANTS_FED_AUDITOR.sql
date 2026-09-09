-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - T8.1
--
-- Permisos del rol fed_auditor. SIN NUMERO de ejecucion unica a proposito, igual
-- que GRANTS_FED_APP.sql: no es un paso del orden de creacion, es un script que
-- se REEJECUTA cada vez que una fase agrega tablas. Correrlo de mas no hace
-- nada; olvidarlo deja la fase nueva sin permisos de auditoria.
--
-- Se ejecuta conectado como fed, el propietario.
--
-- POR QUE EXISTE (decision D-51). El Art. 19.3 obliga a entregar al SENIAT las
-- "claves de acceso a la base de datos" donde queda el registro electronico de
-- toda accion de emision, modificacion o anulacion. El Art. 29.5 obliga a
-- garantizar acceso permanente al sistema, 365 dias al ano. En vez de construir
-- una API web nueva con su propio esquema de autenticacion, se sostiene con el
-- mismo mecanismo que ya hace cumplir el resto del modulo: permisos de rol
-- (D-10, D-20). fed_auditor es SELECT unicamente: no INSERT, no UPDATE, no
-- DELETE, nunca, sobre ninguna tabla del schema FED.
--
-- Lo que fed_auditor NO puede ver: nada fuera de FED. No tiene GRANT sobre
-- public (que es de report-server) ni sobre ningun otro schema de esta base
-- compartida.
-- =============================================================================

GRANT USAGE ON SCHEMA FED TO fed_auditor;
GRANT SELECT ON ALL TABLES IN SCHEMA FED TO fed_auditor;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA FED TO fed_auditor;
