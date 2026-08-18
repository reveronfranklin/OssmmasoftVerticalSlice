-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Semilla del catalogo de tipos (19)
-- Requerimiento 16.
--
-- Cada fila de aqui se corresponde con un componente registrado en
-- registroTipos.ts del frontend (Fase 6). Agregar un tipo de campo al motor es
-- exactamente eso: una fila aqui mas un componente alli. COMPONENTE es la clave
-- que los une; si un tipo llega al renderizador sin componente registrado, el
-- frontend debe degradar a un aviso visible y no romper la pantalla.
--
-- COLUMNA_VALOR decide en cual de las cuatro columnas de MFO_VALOR se guarda el
-- dato. Es la pieza que hace funcionar el EAV tipado, y la respeta
-- SP_MFO_RESP_VAL_SAVE.
--
-- Idempotente: se puede reejecutar sin duplicar.
-- =============================================================================

-- Los CAST de la primera rama no son decorativos: en un UNION ALL el tipo y la
-- longitud de cada columna los fija la primera rama, y sin ellos codigos como
-- 'MULTI_SELECT' o nombres como 'Lista de seleccion multiple' quedarian sujetos
-- al ancho del primer literal.
MERGE INTO MFO_TIPO_CAMPO t
USING (
    SELECT CAST('TEXTO' AS VARCHAR2(30)) CODIGO, CAST('Texto' AS VARCHAR2(60)) NOMBRE, CAST('TXT' AS VARCHAR2(3)) COLUMNA_VALOR, CAST('N' AS CHAR(1)) ADMITE_OPCIONES, CAST('N' AS CHAR(1)) ADMITE_MULTIPLE, CAST('N' AS CHAR(1)) ES_PRESENTACION, CAST('N' AS CHAR(1)) ADMITE_ARCHIVO, CAST('CampoTexto' AS VARCHAR2(60)) COMPONENTE, 10 ORDEN, CAST('mdi:form-textbox' AS VARCHAR2(60)) ICONO FROM DUAL UNION ALL
    SELECT 'TEXTO_LARGO'          , 'Texto largo'              , 'CLB'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoTextoLargo'            ,  20      , 'mdi:text-long'                       FROM DUAL UNION ALL
    SELECT 'NUMERO'               , 'Numero entero'            , 'NUM'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoNumero'                ,  30      , 'mdi:numeric'                         FROM DUAL UNION ALL
    SELECT 'DECIMAL'              , 'Numero decimal'           , 'NUM'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoDecimal'               ,  40      , 'mdi:decimal'                         FROM DUAL UNION ALL
    SELECT 'MONEDA'               , 'Monto'                    , 'NUM'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoMoneda'                ,  50      , 'mdi:cash'                            FROM DUAL UNION ALL
    SELECT 'PORCENTAJE'           , 'Porcentaje'               , 'NUM'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoPorcentaje'            ,  60      , 'mdi:percent'                         FROM DUAL UNION ALL
    SELECT 'FECHA'                , 'Fecha'                    , 'FEC'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoFecha'                 ,  70      , 'mdi:calendar'                        FROM DUAL UNION ALL
    SELECT 'FECHA_HORA'           , 'Fecha y hora'             , 'FEC'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoFechaHora'             ,  80      , 'mdi:calendar-clock'                  FROM DUAL UNION ALL
    SELECT 'BOOLEANO'             , 'Si / No'                  , 'TXT'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoBooleano'              ,  90      , 'mdi:toggle-switch'                   FROM DUAL UNION ALL
    SELECT 'SELECT'               , 'Lista desplegable'        , 'TXT'              , 'S'                , 'N'                , 'N'                , 'N'               , 'CampoSelect'                , 100      , 'mdi:form-dropdown'                   FROM DUAL UNION ALL
    SELECT 'MULTI_SELECT'         , 'Lista de seleccion multiple','TXT'             , 'S'                , 'S'                , 'N'                , 'N'               , 'CampoMultiSelect'           , 110      , 'mdi:format-list-checks'              FROM DUAL UNION ALL
    SELECT 'RADIO'                , 'Opcion unica'             , 'TXT'              , 'S'                , 'N'                , 'N'                , 'N'               , 'CampoRadio'                 , 120      , 'mdi:radiobox-marked'                 FROM DUAL UNION ALL
    SELECT 'CHECK_GRUPO'          , 'Grupo de casillas'        , 'TXT'              , 'S'                , 'S'                , 'N'                , 'N'               , 'CampoCheckGrupo'            , 130      , 'mdi:checkbox-multiple-marked'        FROM DUAL UNION ALL
    SELECT 'EMAIL'                , 'Correo electronico'       , 'TXT'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoEmail'                 , 140      , 'mdi:email'                           FROM DUAL UNION ALL
    SELECT 'TELEFONO'             , 'Telefono'                 , 'TXT'              , 'N'                , 'N'                , 'N'                , 'N'               , 'CampoTelefono'              , 150      , 'mdi:phone'                           FROM DUAL UNION ALL
    SELECT 'CATALOGO'             , 'Catalogo del sistema'     , 'TXT'              , 'S'                , 'N'                , 'N'                , 'N'               , 'CampoCatalogo'              , 160      , 'mdi:database-search'                 FROM DUAL UNION ALL
    SELECT 'ARCHIVO'              , 'Archivo adjunto'          , 'TXT'              , 'N'                , 'S'                , 'N'                , 'S'               , 'CampoArchivo'               , 170      , 'mdi:paperclip'                       FROM DUAL UNION ALL
    SELECT 'TITULO'               , 'Titulo de bloque'         , 'NUL'              , 'N'                , 'N'                , 'S'                , 'N'               , 'BloqueTitulo'               , 180      , 'mdi:format-title'                    FROM DUAL UNION ALL
    SELECT 'NOTA'                 , 'Nota informativa'         , 'NUL'              , 'N'                , 'N'                , 'S'                , 'N'               , 'BloqueNota'                 , 190      , 'mdi:information'                     FROM DUAL
) s
ON (t.CODIGO = s.CODIGO)
WHEN MATCHED THEN UPDATE SET
    t.NOMBRE          = s.NOMBRE,
    t.COLUMNA_VALOR   = s.COLUMNA_VALOR,
    t.ADMITE_OPCIONES = s.ADMITE_OPCIONES,
    t.ADMITE_MULTIPLE = s.ADMITE_MULTIPLE,
    t.ES_PRESENTACION = s.ES_PRESENTACION,
    t.ADMITE_ARCHIVO  = s.ADMITE_ARCHIVO,
    t.COMPONENTE      = s.COMPONENTE,
    t.ORDEN           = s.ORDEN,
    t.ICONO           = s.ICONO
WHEN NOT MATCHED THEN INSERT (
    TIPO_CAMPO_ID, CODIGO, NOMBRE, COLUMNA_VALOR, ADMITE_OPCIONES,
    ADMITE_MULTIPLE, ES_PRESENTACION, ADMITE_ARCHIVO, COMPONENTE, ORDEN,
    ICONO, ACTIVO
) VALUES (
    SEQ_MFO_TIPO_CAMPO.NEXTVAL, s.CODIGO, s.NOMBRE, s.COLUMNA_VALOR, s.ADMITE_OPCIONES,
    s.ADMITE_MULTIPLE, s.ES_PRESENTACION, s.ADMITE_ARCHIVO, s.COMPONENTE, s.ORDEN,
    s.ICONO, 'S'
);

COMMIT;

-- Verificacion: deben quedar 19 tipos activos, 2 de presentacion y 5 con
-- opciones (SELECT, MULTI_SELECT, RADIO, CHECK_GRUPO, CATALOGO).
-- SELECT COUNT(*) TOTAL,
--        SUM(CASE WHEN ES_PRESENTACION = 'S' THEN 1 ELSE 0 END) PRESENTACION,
--        SUM(CASE WHEN ADMITE_OPCIONES = 'S' THEN 1 ELSE 0 END) CON_OPCIONES
--   FROM MFO_TIPO_CAMPO WHERE ACTIVO = 'S';
