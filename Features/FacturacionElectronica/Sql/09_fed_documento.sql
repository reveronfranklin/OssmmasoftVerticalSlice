-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 4
--
-- Tabla FED_DOCUMENTO. Aqui se sostiene INV-3:
--
--   Nunca emitir mas de un ejemplar de la misma factura o documento (Art. 21.2).
--   Es causal de revocatoria de la autorizacion DEL CLIENTE EMISOR, no de la
--   nuestra. Y la exposicion es doble: Ossmmasoft seria a la vez la imprenta
--   digital y el desarrollador del aplicativo que el cliente declara ante el
--   SENIAT (Art. 19.7), asi que un mismo defecto expone a las dos partes.
--
-- La restriccion que la sostiene es UNIQUE (EMISOR_ID, TIPO_DOCUMENTO, SERIE,
-- NUMERACION). No es una precaucion: es la definicion de "un solo ejemplar".
--
-- APPEND-ONLY DE VERDAD. El rol de aplicacion recibe SELECT e INSERT y nada mas
-- -lo hace el ALTER DEFAULT PRIVILEGES del script 00, sin que nadie tenga que
-- acordarse-. Sin UPDATE ni DELETE, ni siquiera para la aplicacion. Eso es lo que
-- pide el Art. 18.2 cuando exige integridad y trazabilidad, y por eso hizo falta
-- el rol fed_app: mientras la app fuera dueña de la tabla, el append-only era una
-- promesa del codigo.
--
-- INSTANTANEA, NO REFERENCIA. Los datos del emisor y del adquiriente se COPIAN al
-- emitir en vez de leerse por clave foranea. El Art. 29.3 obliga a la imprenta a
-- estampar razon social, domicilio y RIF del emisor en el documento: lo que se
-- estampo es un hecho historico y no puede cambiar porque manana el emisor mude
-- su domicilio. Lo mismo con los datos de la imprenta del numeral 7.14.
--
-- SIN COLUMNA DE ESTADO, Y ES A PROPOSITO. El MER preveia estado emitido|anulado,
-- pero una columna mutable contradice el append-only, y el regimen de anulacion
-- NO esta resuelto: RF-B.5 sigue abierto esperando la Providencia SNAT/2011/0071
-- (tarea T5.1). Poner hoy una columna que habria que poder actualizar seria abrir
-- el agujero antes de saber si hace falta. El estado se deriva de FED_BITACORA,
-- que es justamente el registro de "toda accion de emision, modificacion o
-- anulacion" que exige el Art. 18.2. Si la 0071 obliga a otra cosa, se agrega
-- entonces con su justificacion escrita.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_DOCUMENTO (
    ID                    BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    EMISOR_ID             BIGINT       NOT NULL,

    -- Los cuatro del alcance documental: DOC-1 a DOC-4.
    TIPO_DOCUMENTO        VARCHAR(20)  NOT NULL,

    -- Art. 13. Vacia cuando el emisor tiene facturacion centralizada. Vacia y no
    -- NULL porque entra en la clave de unicidad de INV-3, y dos NULL no son
    -- iguales entre si: con NULL, el UNIQUE dejaria pasar duplicados.
    SERIE                 VARCHAR(20)  NOT NULL DEFAULT '',

    -- Art. 7.2: numeracion consecutiva y unica del documento, distinta del numero
    -- de control. Ver D-21.
    NUMERACION            VARCHAR(20)  NOT NULL,

    -- Art. 7.6: fecha y hora de emision. Se guarda el instante completo; el
    -- formato de ocho digitos y la hora con a.m./p.m. son de la representacion.
    EMITIDO_EN            TIMESTAMPTZ  NOT NULL DEFAULT now(),

    -- Instantanea del emisor. Art. 29.3.
    EMISOR_RIF            VARCHAR(20)  NOT NULL,
    EMISOR_RAZON_SOCIAL   VARCHAR(200) NOT NULL,
    EMISOR_DOMICILIO      VARCHAR(300) NOT NULL,

    -- Art. 7.7: datos del adquiriente. Se puede prescindir del RIF para personas
    -- naturales que no requieran la factura a efectos tributarios, y en ese caso
    -- se exige como minimo cedula o pasaporte. De ahi que los tres sean opcionales
    -- por separado y la regla viva en el validador, no en un NOT NULL que no
    -- distingue el caso.
    ADQ_NOMBRE            VARCHAR(200),
    ADQ_RIF               VARCHAR(20),
    ADQ_DOCUMENTO_ID      VARCHAR(30),

    -- Art. 7.11 y 7.13. El detalle por alicuota vive en FED_DOC_IMPUESTO; aca
    -- quedan los totales que el documento tiene que mostrar.
    TOTAL_EXENTO          NUMERIC(18,2) NOT NULL DEFAULT 0,
    TOTAL_BASE            NUMERIC(18,2) NOT NULL DEFAULT 0,
    TOTAL_IVA             NUMERIC(18,2) NOT NULL DEFAULT 0,
    TOTAL_GENERAL         NUMERIC(18,2) NOT NULL DEFAULT 0,

    -- Numeral 7.14: razon social y RIF de la imprenta digital, MAS la nomenclatura
    -- y fecha de su providencia de autorizacion. Instantanea, igual que el emisor.
    --
    -- La providencia no existe hasta que el SENIAT autorice a Ossmmasoft, asi que
    -- viaja vacia y el documento queda marcado como de prueba. Es el unico dato
    -- del modulo que no se puede completar hoy, y por eso es configuracion y no
    -- constante (T4.7).
    IMPRENTA_RIF          VARCHAR(20),
    IMPRENTA_RAZON_SOCIAL VARCHAR(200),
    IMPRENTA_PROVIDENCIA  VARCHAR(200),
    ES_PRUEBA             BOOLEAN      NOT NULL DEFAULT TRUE,

    USUARIO_INS           VARCHAR(50)  NOT NULL,
    FECHA_INS             TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT FED_DOCUMENTO_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    -- INV-3. Un ejemplar y nada mas.
    CONSTRAINT FED_DOCUMENTO_UK UNIQUE (EMISOR_ID, TIPO_DOCUMENTO, SERIE, NUMERACION),

    CONSTRAINT FED_DOCUMENTO_TIPO_CK
        CHECK (TIPO_DOCUMENTO IN ('factura', 'debito', 'credito', 'entrega')),

    -- Art. 7.7: si no hay RIF del adquiriente, tiene que haber cedula o pasaporte.
    -- La nota de entrega es la excepcion: el Art. 10.2 no remite al 7.7, asi que
    -- no lleva datos del adquiriente sino del receptor, que viven en su propia
    -- tabla (Fase 6).
    CONSTRAINT FED_DOCUMENTO_ADQ_CK
        CHECK (TIPO_DOCUMENTO = 'entrega'
               OR ADQ_RIF IS NOT NULL
               OR ADQ_DOCUMENTO_ID IS NOT NULL),

    CONSTRAINT FED_DOCUMENTO_TOTALES_CK
        CHECK (TOTAL_EXENTO >= 0 AND TOTAL_BASE >= 0 AND TOTAL_IVA >= 0 AND TOTAL_GENERAL >= 0),

    -- Un documento marcado como definitivo tiene que traer los datos de la
    -- imprenta que el numeral 7.14 exige. Mientras no exista la autorizacion, el
    -- documento es de prueba y eso queda escrito en la fila, no en un comentario.
    CONSTRAINT FED_DOCUMENTO_PRUEBA_CK
        CHECK (ES_PRUEBA = TRUE
               OR (IMPRENTA_RIF IS NOT NULL
                   AND IMPRENTA_RAZON_SOCIAL IS NOT NULL
                   AND IMPRENTA_PROVIDENCIA IS NOT NULL))
);

-- Consulta natural: los documentos de un emisor por fecha. Es la del listado y la
-- del acceso del SENIAT (Art. 18.7).
CREATE INDEX IF NOT EXISTS FED_DOCUMENTO_EMI_FEC_IX
    ON FED.FED_DOCUMENTO (EMISOR_ID, EMITIDO_EN);

-- -----------------------------------------------------------------------------
-- Segunda mitad de la deuda de la Fase 2, y un desdoblamiento que la foranea
-- obliga a hacer.
--
-- El script 03 creo FED_NUM_CONTROL.DOCUMENTO_ID como columna suelta porque esta
-- tabla no existia. Al declarar ahora la foranea aparece el problema: en la Fase
-- 2 esa columna guardaba un identificador OPACO que traia quien llamaba al
-- endpoint, y un emisor en modo 'externa' (D-21) emite desde su propio sistema,
-- asi que su documento NO esta en nuestra base y nunca va a estarlo.
--
-- Una sola columna no puede ser las dos cosas: referencia a FED_DOCUMENTO cuando
-- emitimos nosotros, e identificador ajeno cuando emite el emisor. Se desdobla:
--
--   DOCUMENTO_ID        foranea real. Se usa cuando el documento es nuestro.
--   DOCUMENTO_EXTERNO   el identificador que trae el emisor externo.
--
-- INV-1 se sostiene igual en los dos caminos: DOCUMENTO_ID ya tenia su UNIQUE, y
-- DOCUMENTO_EXTERNO recibe el suyo por emisor. Dos numeros de control para el
-- mismo documento sigue siendo imposible, venga de donde venga el documento.
-- -----------------------------------------------------------------------------
ALTER TABLE FED.FED_NUM_CONTROL
    ADD COLUMN IF NOT EXISTS DOCUMENTO_EXTERNO VARCHAR(60);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fed_num_control_doc_ext_uk'
          AND conrelid = 'fed.fed_num_control'::regclass
    ) THEN
        ALTER TABLE FED.FED_NUM_CONTROL
            ADD CONSTRAINT FED_NUM_CONTROL_DOC_EXT_UK
            UNIQUE (EMISOR_ID, DOCUMENTO_EXTERNO);
    END IF;
END
$$;


-- Guarda antes de crear la foranea: si quedaron DOCUMENTO_ID que no apuntan a
-- ningun documento -datos previos a que esta tabla existiera- el ALTER fallaria
-- con un mensaje del motor que no dice que hacer. Se falla con uno que si.
DO $$
DECLARE
    v_huerfanos BIGINT;
BEGIN
    SELECT COUNT(*) INTO v_huerfanos
      FROM FED.FED_NUM_CONTROL nc
     WHERE nc.DOCUMENTO_ID IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM FED.FED_DOCUMENTO d WHERE d.ID = nc.DOCUMENTO_ID);

    IF v_huerfanos > 0 THEN
        RAISE EXCEPTION
            'Hay % numero(s) de control con DOCUMENTO_ID que no existe en FED_DOCUMENTO. Son asignaciones anteriores a esta tabla, cuando la columna guardaba un identificador ajeno. Moverlos a DOCUMENTO_EXTERNO antes de continuar: UPDATE FED.FED_NUM_CONTROL SET DOCUMENTO_EXTERNO = DOCUMENTO_ID::text, DOCUMENTO_ID = NULL WHERE DOCUMENTO_ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM FED.FED_DOCUMENTO d WHERE d.ID = DOCUMENTO_ID);',
            v_huerfanos;
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fed_num_control_documento_fk'
          AND conrelid = 'fed.fed_num_control'::regclass
    ) THEN
        ALTER TABLE FED.FED_NUM_CONTROL
            ADD CONSTRAINT FED_NUM_CONTROL_DOCUMENTO_FK
            FOREIGN KEY (DOCUMENTO_ID) REFERENCES FED.FED_DOCUMENTO (ID);
    END IF;
END
$$;

COMMENT ON COLUMN FED.FED_NUM_CONTROL.DOCUMENTO_EXTERNO IS
    'Identificador del documento cuando lo emite el propio emisor (MODO_NUMERACION = externa). UNIQUE por emisor: INV-1 vale igual para documentos que no son nuestros.';

COMMENT ON TABLE  FED.FED_DOCUMENTO                       IS 'Documentos fiscales emitidos. APPEND-ONLY: el rol de aplicacion no tiene UPDATE ni DELETE. Sostiene INV-3 (Art. 21.2).';
COMMENT ON COLUMN FED.FED_DOCUMENTO.NUMERACION            IS 'Art. 7.2: numeracion propia del documento, distinta del numero de control. Ver D-21.';
COMMENT ON COLUMN FED.FED_DOCUMENTO.SERIE                 IS 'Art. 13. Vacia si el emisor tiene facturacion centralizada. Vacia y no NULL porque entra en el UNIQUE de INV-3.';
COMMENT ON COLUMN FED.FED_DOCUMENTO.ES_PRUEBA             IS 'TRUE mientras el SENIAT no autorice a Ossmmasoft: sin la providencia del numeral 7.14 el documento no es legalmente valido.';
COMMENT ON COLUMN FED.FED_DOCUMENTO.IMPRENTA_PROVIDENCIA  IS 'Numeral 7.14: nomenclatura y fecha de la Providencia de autorizacion. No existe hasta que el SENIAT la emita (T4.7).';
