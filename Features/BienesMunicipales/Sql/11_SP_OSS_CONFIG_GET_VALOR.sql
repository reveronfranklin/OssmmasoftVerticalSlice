-- Lee un valor de configuracion de SIS.OSS_CONFIG por su clave.
-- Lo usa el generador de placas de Bienes Municipales para resolver ESCUDO_CHACAO y LOGO_CHACAO,
-- que guardan el nombre del archivo de imagen dentro de la carpeta de settings:BmFiles, igual que
-- hacia el sistema anterior.
CREATE OR REPLACE PROCEDURE SIS.SP_OSS_CONFIG_GET_VALOR (
    p_Clave   IN  VARCHAR2,
    p_Valor   OUT VARCHAR2,
    p_Message OUT VARCHAR2
) AS
BEGIN
    p_Valor := NULL;

    -- La tabla no tiene unicidad declarada sobre CLAVE. Se toma el ID mas alto, que es la ultima
    -- fila cargada, para no fallar con ORA-01422 si la clave quedara repetida.
    SELECT VALOR
      INTO p_Valor
      FROM (SELECT VALOR
              FROM SIS.OSS_CONFIG
             WHERE CLAVE = p_Clave
             ORDER BY ID DESC)
     WHERE ROWNUM = 1;

    p_Message := 'suscces';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Valor := NULL;
        p_Message := 'Clave no encontrada';
    WHEN OTHERS THEN
        p_Valor := NULL;
        p_Message := SUBSTR(SQLERRM, 1, 4000);
END SP_OSS_CONFIG_GET_VALOR;
/
