-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - T4.0
--
-- Permisos del rol de aplicacion fed_app. SIN NUMERO a proposito: no es un paso
-- del orden de creacion, es un script que se REEJECUTA cada vez que una fase
-- agrega tablas. Correrlo de mas no hace nada; olvidarlo deja la fase nueva sin
-- permisos, y eso se nota al primer INSERT.
--
-- Se ejecuta conectado como fed, el propietario.
--
-- POR QUE EXISTE (decision D-20). El Articulo 18.2 exige integridad, autenticidad
-- y trazabilidad de los datos registrados, con auditoria de toda accion de
-- emision, modificacion o anulacion. Eso no se puede sostener si el usuario de la
-- aplicacion es el dueno de las tablas: en PostgreSQL el propietario siempre
-- puede escribir. De ahi el segundo rol.
--
-- EL CRITERIO: fed_app lee todo, inserta todo, NUNCA borra, y actualiza solo las
-- columnas que de verdad cambian. El UPDATE se otorga COLUMNA POR COLUMNA, no por
-- tabla, y eso convierte varias reglas de negocio en propiedades de la base:
--
--   - El RIF del emisor deja de ser editable de hecho, no solo por convencion.
--     El Articulo 30 ata la secuencia de numero de control al RIF, asi que
--     cambiarlo rompe todo lo ya asignado. Antes lo impedia el handler; ahora lo
--     impide el motor.
--   - Un numero de control asignado se vuelve inmutable en lo que importa:
--     emisor, identificador, secuencial, tipo y fecha de asignacion no se pueden
--     modificar ni desde la aplicacion. Es INV-1 sostenida por permisos, no por
--     cuidado.
--
-- Ninguna tabla recibe DELETE. El modulo no borra: un emisor se desactiva, un
-- documento se anula con otro documento.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Base: leer e insertar en todo lo que exista hoy. Lo que se cree despues lo
-- cubre el ALTER DEFAULT PRIVILEGES del script 00.
-- -----------------------------------------------------------------------------
GRANT USAGE ON SCHEMA FED TO fed_app;
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA FED TO fed_app;

-- Por si algun objeto futuro usa secuencias explicitas en vez de IDENTITY.
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA FED TO fed_app;

-- -----------------------------------------------------------------------------
-- FED_EMISOR - se edita, menos el RIF.
--
-- El RIF queda FUERA de la lista a proposito: el Art. 30 ata la secuencia de
-- numero de control a ese dato. Para corregir un RIF equivocado se desactiva el
-- emisor y se da de alta el correcto.
-- -----------------------------------------------------------------------------
GRANT UPDATE (
    RAZON_SOCIAL, DOMICILIO_FISCAL, CORREO, ESTADO,
    RIF_VERIFICADO_EL, RIF_VERIFICADO_ESTADO,
    USUARIO_UPD, FECHA_UPD
) ON FED.FED_EMISOR TO fed_app;

-- -----------------------------------------------------------------------------
-- FED_EMISOR_CONTADOR - se actualiza en cada asignacion. Es el corazon de INV-1:
-- sin este UPDATE el modulo no puede asignar un solo numero.
-- -----------------------------------------------------------------------------
GRANT UPDATE (IDENTIFICADOR, SECUENCIAL, FECHA_UPD)
    ON FED.FED_EMISOR_CONTADOR TO fed_app;

-- -----------------------------------------------------------------------------
-- FED_NUM_CONTROL - la asignacion es INMUTABLE; solo cambia su metadato.
--
-- Fuera de la lista quedan EMISOR_ID, IDENTIFICADOR, SECUENCIAL, TIPO_DOCUMENTO y
-- FECHA_ASIGNACION: un numero de control asignado no se reescribe. Lo que si
-- cambia despues es a que reporte pertenece, si ya se concilio con su documento,
-- y la clausula abierta del numeral 7.
-- -----------------------------------------------------------------------------
GRANT UPDATE (REPORTE_ID, ESTADO_CONCILIACION, DATOS_ADICIONALES, FACTURA_SERVICIO, DOCUMENTO_ID)
    ON FED.FED_NUM_CONTROL TO fed_app;

-- -----------------------------------------------------------------------------
-- FED_REPORTE_MENSUAL - cambia de estado a lo largo del ciclo.
--
-- PERIODO, FECHA_CIERRE y FECHA_VENCE quedan fuera: son la identidad del periodo
-- y su plazo legal. Corregirlos a mano seria corregir el Art. 29.7.
-- -----------------------------------------------------------------------------
GRANT UPDATE (ESTADO, CANTIDAD_REPORTADA, ENVIADO_EN, ULTIMO_INTENTO_EN, ULTIMO_ERROR)
    ON FED.FED_REPORTE_MENSUAL TO fed_app;

-- -----------------------------------------------------------------------------
-- FED_EMISOR_DOC_CONTADOR - Fase 4. Misma excepcion que el contador del numero
-- de control: sin este UPDATE no se puede emitir un solo documento.
--
-- EMISOR_ID, TIPO_DOCUMENTO y SERIE quedan fuera porque son la clave primaria:
-- mover un contador de emisor o de serie seria reescribir a quien pertenece una
-- numeracion ya usada.
-- -----------------------------------------------------------------------------
GRANT UPDATE (ULTIMO_NUMERO, FECHA_UPD)
    ON FED.FED_EMISOR_DOC_CONTADOR TO fed_app;

-- El modo de numeracion es dato editable del emisor, como su razon social.
GRANT UPDATE (MODO_NUMERACION) ON FED.FED_EMISOR TO fed_app;

-- Identificador del documento del emisor externo: se completa despues de asignar.
GRANT UPDATE (DOCUMENTO_EXTERNO) ON FED.FED_NUM_CONTROL TO fed_app;

-- -----------------------------------------------------------------------------
-- LAS TABLAS DE DOCUMENTOS NO APARECEN AQUI, Y ESA ES LA IDEA.
--
-- FED_DOCUMENTO, FED_DOCUMENTO_DETALLE, FED_DOC_IMPUESTO y FED_BITACORA se
-- quedan con SELECT + INSERT, que es lo que las hace append-only de verdad, y lo
-- reciben del ALTER DEFAULT PRIVILEGES del script 00 sin que nadie las liste. Un
-- documento fiscal emitido no se modifica: se corrige con una nota de credito o
-- de debito, que es otro documento.
--
-- Si una fase futura necesita UPDATE sobre alguna de ellas, se agrega aca con su
-- justificacion escrita. No por descuido y no en silencio.
-- -----------------------------------------------------------------------------
