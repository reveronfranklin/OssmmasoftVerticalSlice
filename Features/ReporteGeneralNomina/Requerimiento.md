# Requerimiento: Reporte General de Nomina

Dado el siguiente query dame un store procedure en Oracle 10, que retorne el resultado de la consulta y un parametro de salida message con posible error o `Success` cuando sea satisfactorio.

## Resumen de conceptos

### Parametros

```sql
&LP_FROM_TABLE1
,&LP_FROM_TABLE2
:P_TIPO_NOMINA
:P_FECHA_PAGO
&LP_WHERE
```

## Query base

```sql
SELECT
  RC.TIPO_CONCEPTO R_TIPO_CONCEPTO
 ,RC.CODIGO R_NUMERO_CONCEPTO
 ,RC.DENOMINACION R_DENOMINACION_CONCEPTO
 ,DECODE(SIGN(SUM(RTN.MONTO)),1,SUM(RTN.MONTO),0) R_ASIGNACION
 ,DECODE(SIGN(SUM(RTN.MONTO)),-1,-(SUM(RTN.MONTO)),0) R_DEDUCCION
 ,DECODE(SIGN(SUM(RTN.MONTO)),1,(SUM(RTN.MONTO)),-(SUM(RTN.MONTO))) R_MONTO_VISIBLE
 ,SUM(RTN.MONTO) R_MONTO
 ,RC.DEDUSIBLE R_DEDUCIBLE
FROM
  &LP_FROM_TABLE1
 ,&LP_FROM_TABLE2
 ,RH_CONCEPTOS RC
 ,PRE_INDICE_CAT_PRG PVIO
WHERE RVPC.CODIGO_TIPO_NOMINA  = :P_TIPO_NOMINA
      AND RTN.FECHA_NOMINA         = :P_FECHA_PAGO
      AND RVPC.CODIGO_TIPO_NOMINA  = RTN.CODIGO_TIPO_NOMINA
      AND RVPC.CODIGO_PERSONA      = RTN.CODIGO_PERSONA
      AND RVPC.CODIGO_EMPRESA      = :CODIGO_EMPRESA
      AND RC.CODIGO_CONCEPTO       = RTN.CODIGO_CONCEPTO
      AND PVIO.CODIGO_ICP          = RVPC.CODIGO_ICP
      &LP_WHERE
GROUP
   BY RC.TIPO_CONCEPTO,RC.CODIGO
     ,RC.DENOMINACION,DEDUSIBLE,RTN.FECHA_NOMINA
UNION
SELECT
  NULL R_TIPO_CONCEPTO
 ,NULL R_NUMERO_CONCEPTO
 ,RVPC.DESCRIPCION_BANCO R_DENOMINACION_CONCEPTO
 ,DECODE(SIGN(SUM(RTN.MONTO)),1,SUM(RTN.MONTO),0) R_ASIGNACION
 ,DECODE(SIGN(SUM(RTN.MONTO)),-1,-(SUM(RTN.MONTO)),0) R_DEDUCCION
 ,DECODE(SIGN(SUM(RTN.MONTO)),1,(SUM(RTN.MONTO)),-(SUM(RTN.MONTO))) R_MONTO_VISIBLE
 ,SUM(RTN.MONTO) R_MONTO
 ,COUNT(UNIQUE RTN.CODIGO_PERSONA) R_DEDUCIBLE
FROM
  &LP_FROM_TABLE1
 ,&LP_FROM_TABLE2
 ,RH_CONCEPTOS RC
 ,PRE_INDICE_CAT_PRG PVIO
WHERE RVPC.CODIGO_TIPO_NOMINA  = :P_TIPO_NOMINA
      AND RTN.FECHA_NOMINA         =  :P_FECHA_PAGO
      AND RVPC.CODIGO_TIPO_NOMINA  = RTN.CODIGO_TIPO_NOMINA
      AND RVPC.CODIGO_PERSONA      = RTN.CODIGO_PERSONA
      AND RVPC.CODIGO_EMPRESA      = :CODIGO_EMPRESA
      AND RC.CODIGO_CONCEPTO       = RTN.CODIGO_CONCEPTO
      AND PVIO.CODIGO_ICP          = RVPC.CODIGO_ICP
      &LP_WHERE
GROUP
   BY NULL,NULL,RVPC.DESCRIPCION_BANCO,RTN.FECHA_NOMINA
ORDER
   BY 1,2,4
```

## Guia de estructura del stored procedure

```sql
CREATE OR REPLACE PROCEDURE RH.SP_RH_TMP_MOV_NOMINA_GET_ALL (
    p_where         IN VARCHAR2,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER
) AS
    v_sql       VARCHAR2(32767);
    v_from      VARCHAR2(32767);
    v_where_base VARCHAR2(32767);
    v_where_full VARCHAR2(32767);
BEGIN
    -- 1. Definimos el bloque FROM y los Joins fijos
    v_from := ' FROM
                    RH.RH_TMP_NOMINA rtmn
                    INNER JOIN RH.RH_TIPOS_NOMINA RTN ON rtmn.CODIGO_TIPO_NOMINA = RTN.CODIGO_TIPO_NOMINA
                    INNER JOIN RH.RH_PERSONAS PE      ON rtmn.CODIGO_PERSONA = PE.CODIGO_PERSONA
                    INNER JOIN RH.RH_CONCEPTOS CO     ON rtmn.CODIGO_CONCEPTO = CO.CODIGO_CONCEPTO
                    INNER JOIN RH.RH_DESCRIPTIVAS DE  ON rtmn.FRECUENCIA_ID = DE.DESCRIPCION_ID
                    INNER JOIN RH.RH_RELACION_CARGOS RC ON  rtmn.CODIGO_TIPO_NOMINA =RC.CODIGO_TIPO_NOMINA AND rtmn.CODIGO_PERSONA =RC.CODIGO_PERSONA  AND RC.FECHA_FIN IS NULL
                    INNER JOIN PRE.PRE_INDICE_CAT_PRG  ICP ON  RC.CODIGO_ICP =  ICP.CODIGO_ICP';

    -- 2. Definimos las condiciones de integridad (puedes añadir filtros fijos aqui si los necesitas)
    v_where_base := ' WHERE 1=1 ';

    -- 3. Preparamos el WHERE final combinando la base con el p_where del usuario
    IF p_where IS NOT NULL AND TRIM(p_where) IS NOT NULL THEN
        v_where_full := v_where_base || ' AND (' || p_where || ')';
    ELSE
        v_where_full := v_where_base;
    END IF;

    -- 4. Obtener el total de registros dinamicamente
    EXECUTE IMMEDIATE 'SELECT COUNT(*) ' || v_from || v_where_full
    INTO p_TotalRecords;

    -- 5. Construir la consulta principal con todas las columnas solicitadas
    v_sql := 'SELECT
                 rtmn.CODIGO_PERIODO,
                 rtmn.CODIGO_MOV_NOMINA,
                 rtmn.CODIGO_TIPO_NOMINA,
                 RTN.DESCRIPCION AS TIPO_NOMINA,
                 rtmn.CODIGO_PERSONA,
                 PE.CEDULA,
                 PE.NOMBRE || '' '' || PE.APELLIDO AS PERSONA,
                 rtmn.CODIGO_CONCEPTO,
                 CO.CODIGO AS CONCEPTO,
                 CO.DENOMINACION,
                 CO.TIPO_CONCEPTO,
                 rtmn.COMPLEMENTO_CONCEPTO,
                 rtmn.TIPO,
                 rtmn.FRECUENCIA_ID,
                 DE.DESCRIPCION,
                 rtmn.MONTO,
                 RC.CODIGO_ICP,
                 ICP.DENOMINACION AS DENOMINACION_ICP'
              || v_from
              || v_where_full
              || ' ORDER BY rtmn.CODIGO_TIPO_NOMINA ASC';

    -- 6. Abrir el cursor
    OPEN p_ResultSet FOR v_sql;

    p_Message := 'Success';

EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        -- Cursor vacio con la misma estructura para evitar errores en C#
        OPEN p_ResultSet FOR
            SELECT * FROM RH.RH_TMP_MOV_NOMINA WHERE 1=0;
END SP_RH_TMP_MOV_NOMINA_GET_ALL;
```

## Requerimiento C#

Tambien necesito una clase en C# usando vertical slice como arquitectura, toma como guia la estructura de la clase de la carpeta `RhTmpMovNomina`.

Crea la clase en la carpeta existente `ReporteGeneralNomina`.
