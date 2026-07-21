-- Reporte Control Perceptivo (ADM) - monto total en letras.
-- Envoltorio delgado sobre SIS.SIS_MONTOESCRITO (funcion Oracle existente,
-- reutilizada tal cual, ver TOTAL_EN_LETRASFORMULA en ADM_CONTROL_PERCEPTIVO.rdf).
-- El monto total se calcula en el backend (suma de lineas + impuesto) y se
-- pasa aqui unicamente para convertirlo a letras.
CREATE OR REPLACE PROCEDURE ADM.SP_ADM_CTRL_PERCEP_LETRAS_GET (
    p_Monto   IN NUMBER,
    p_Texto   OUT VARCHAR2,
    p_Message OUT VARCHAR2
) AS
BEGIN
    p_Texto := UPPER(SIS.SIS_MONTOESCRITO(p_Monto));
    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Texto := NULL;
        p_Message := SUBSTR(SQLERRM, 1, 4000);
END SP_ADM_CTRL_PERCEP_LETRAS_GET;
/
