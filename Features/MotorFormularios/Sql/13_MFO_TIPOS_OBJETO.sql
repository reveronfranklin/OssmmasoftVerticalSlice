-- =============================================================================
-- Motor de Formularios (MFO) - Fase 2 - Tipos de objeto
-- Requerimiento 16.
--
-- SP_MFO_VER_VALIDAR devuelve una lista de hallazgos que no sale de ninguna
-- tabla: se arma en memoria mientras corren las comprobaciones. Para poder
-- entregarla como ref cursor -que es lo que consume el backend- hace falta un
-- tipo de coleccion SQL, porque una coleccion PL/SQL no se puede abrir en un
-- cursor.
--
-- Se prefiere esto a una tabla temporal global: no deja estado de sesion, no
-- necesita limpieza, y dos validaciones concurrentes no se pisan.
-- =============================================================================

CREATE OR REPLACE TYPE MFO_HALLAZGO_OBJ AS OBJECT (
    SEVERIDAD   VARCHAR2(10),   -- ERROR bloquea la publicacion; AVISO no
    CODIGO      VARCHAR2(30),   -- identificador estable del tipo de hallazgo
    ENTIDAD     VARCHAR2(10),   -- VERSION / SECCION / CAMPO / CONDICION / REGLA
    ENTIDAD_ID  NUMBER,
    CLAVE       VARCHAR2(30),   -- CLAVE del elemento, para señalarlo en la UI
    MENSAJE     VARCHAR2(500)
);
/

CREATE OR REPLACE TYPE MFO_HALLAZGO_TAB AS TABLE OF MFO_HALLAZGO_OBJ;
/
