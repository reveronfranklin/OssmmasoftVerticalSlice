-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Indices del nucleo (11)
-- Requerimiento 16.
--
-- No se crean indices sobre las columnas ya cubiertas por PK o UK: Oracle crea
-- el indice al crear la restriccion.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- El invariante mas importante del modelo: como maximo una version PUBLICADA por
-- formulario. Indice unico basado en funcion (soportado en 10g): las filas cuyo
-- ESTADO no es PUBLICADA producen NULL y no participan del indice, asi que
-- pueden convivir N ARCHIVADA y una BORRADOR sin conflicto.
--
-- Se resuelve en la base y no en la aplicacion a proposito: dos publicaciones
-- concurrentes del mismo formulario no pueden pasar aunque el backend valide.
-- -----------------------------------------------------------------------------
CREATE UNIQUE INDEX IDX_MFO_VER_PUBL_UNQ ON MFO_VERSION
    (CASE WHEN ESTADO = 'PUBLICADA' THEN FORMULARIO_ID END);

-- Carga de la definicion completa: campos de una seccion en su orden.
CREATE INDEX IDX_MFO_CAMPO_SEC_ORD ON MFO_CAMPO (SECCION_ID, ORDEN);

-- -----------------------------------------------------------------------------
-- Busqueda por valor de campo (SP_MFO_RESP_SEARCH). CLAVE_CAMPO al frente
-- porque toda busqueda por valor parte de saber que campo se busca; el valor
-- tipado va detras para que el indice resuelva el filtro completo.
-- Son tres indices y no uno solo porque el tipo del campo decide en cual de las
-- tres columnas esta el dato.
-- -----------------------------------------------------------------------------
CREATE INDEX IDX_MFO_VALOR_CLAVE ON MFO_VALOR (CLAVE_CAMPO);
CREATE INDEX IDX_MFO_VALOR_TXT   ON MFO_VALOR (CLAVE_CAMPO, VALOR_TXT);
CREATE INDEX IDX_MFO_VALOR_NUM   ON MFO_VALOR (CLAVE_CAMPO, VALOR_NUM);
CREATE INDEX IDX_MFO_VALOR_FEC   ON MFO_VALOR (CLAVE_CAMPO, VALOR_FEC);

-- Bandeja de respuestas: por formulario y estado, lo mas reciente primero.
CREATE INDEX IDX_MFO_RESP_FORM_EST ON MFO_RESPUESTA (FORMULARIO_ID, ESTADO, FECHA_INICIO);

-- Respuestas asociadas a un registro de negocio.
CREATE INDEX IDX_MFO_RESP_REF ON MFO_RESPUESTA (ENTIDAD_REF, CLAVE_REF);

-- "Mis respuestas" y el control de MAX_RESP_USUARIO.
CREATE INDEX IDX_MFO_RESP_USUARIO ON MFO_RESPUESTA (USUARIO_LLENA, FORMULARIO_ID);

-- DESTINO_ID es polimorfico y no tiene FK, asi que sin este indice la validacion
-- de destinos huerfanos de SP_MFO_VER_VALIDAR recorreria la tabla entera.
CREATE INDEX IDX_MFO_COND_DESTINO ON MFO_CONDICION (DESTINO_TIPO, DESTINO_ID);

-- Bitacora por entidad auditada.
CREATE INDEX IDX_MFO_AUD_ENTIDAD ON MFO_AUDITORIA (ENTIDAD, ENTIDAD_ID, FECHA);
