-- =============================================================================
-- Motor de Formularios (MFO) - Fase 3 - Tipos de arreglo para el guardado masivo
-- Requerimiento 16.
--
-- Una respuesta trae N valores y hay que guardarlos en una sola llamada: el
-- autoguardado del renderizador dispara con debounce mientras el usuario
-- escribe, y N viajes a la base por cada pulsacion no es una opcion.
--
-- Se descartaron dos alternativas antes de llegar aqui:
--
--   1. Una llamada por valor. Simple, pero convierte cada autoguardado en 20 o
--      50 viajes.
--   2. Una cadena delimitada (CSV o similar) que se parsea en PL/SQL. Es el
--      patron que ya usa el repositorio para listas de ids, y ahi funciona
--      porque un id no puede contener el delimitador. Aqui los valores son
--      TEXTO ESCRITO POR EL USUARIO: cualquier delimitador que se elija puede
--      aparecer dentro del dato y romper el payload. Ademas obligaria a escribir
--      un parser a mano.
--
-- Los arreglos asociativos no tienen ninguno de los dos problemas: no hay
-- parseo, no hay delimitador que colisione, no hay superficie de inyeccion, y
-- ODP.NET los bindea de forma nativa (OracleCollectionType.PLSQLAssociativeArray).
-- Para poder bindearlos desde fuera, los tipos tienen que estar declarados en un
-- paquete; de ahi este paquete, que solo contiene tipos y ninguna logica.
--
-- Limite conocido: VARCHAR2(4000) es el maximo de un elemento. Los valores de
-- tipo CLB mas largos que eso viajan por el parametro CLOB dedicado de
-- SP_MFO_RESP_VAL_SAVE, uno por llamada.
-- =============================================================================

CREATE OR REPLACE PACKAGE MFO.PKG_MFO_ARRAYS AS
    TYPE t_num IS TABLE OF NUMBER          INDEX BY BINARY_INTEGER;
    TYPE t_txt IS TABLE OF VARCHAR2(4000)  INDEX BY BINARY_INTEGER;
    TYPE t_fec IS TABLE OF DATE            INDEX BY BINARY_INTEGER;
    TYPE t_cla IS TABLE OF VARCHAR2(30)    INDEX BY BINARY_INTEGER;
    TYPE t_eti IS TABLE OF VARCHAR2(200)   INDEX BY BINARY_INTEGER;
END PKG_MFO_ARRAYS;
/
