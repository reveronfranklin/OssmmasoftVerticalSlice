-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - correccion de la hora UTC
--
-- Fija la zona horaria de las SESIONES del modulo en la hora legal de Venezuela.
-- Se ejecuta conectado como SUPERUSUARIO, igual que 00_fed_bootstrap.sql: un
-- ALTER ROLE ... SET sobre otro rol lo exige.
--
--   psql -h <host> -p 5432 -U postgres -d OSSMMASOFT -f 25_fed_zona_horaria.sql
--
-- Por que. El SQL del modulo convierte instantes en fechas: el registro del
-- Art. 32 que se remite al SENIAT formatea la fecha de asignacion con
-- TO_CHAR(..., 'DDMMYYYY'), los periodos mensuales salen de TO_CHAR(..., 'YYYYMM')
-- y date_trunc, y los vencimientos de CURRENT_DATE. Todo eso usa la zona de la
-- sesion. Hasta ahora esa zona salia del postgresql.conf de cada servidor -en la
-- base local, America/Caracas porque la tomo del sistema al instalarse-, asi que
-- el resultado dependia de como estuviera configurado el servidor. En uno en UTC,
-- un numero asignado despues de las 8:00 p.m. del ultimo dia del mes caia en el
-- periodo siguiente.
--
-- Por que por rol y no por base. La base OSSMMASOFT es compartida con
-- report-server (schema public). Un ALTER DATABASE le cambiaria la zona tambien
-- a el. Por rol, solo se tocan las sesiones de este modulo, con el mismo
-- mecanismo que ya fija su search_path en el script 00.
--
-- El lado C# se corrigio aparte (FacturaFormato.HoraVenezuela): Npgsql entrega
-- los timestamptz en UTC sin importar la zona de la sesion, asi que este script
-- no lo arregla ni lo rompe.
--
-- Reejecutable: ALTER ROLE ... SET reemplaza el valor anterior.
-- =============================================================================

ALTER ROLE fed         SET timezone = 'America/Caracas';
ALTER ROLE fed_app     SET timezone = 'America/Caracas';
ALTER ROLE fed_auditor SET timezone = 'America/Caracas';
