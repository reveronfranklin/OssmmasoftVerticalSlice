DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_count
      FROM ALL_TABLES
     WHERE OWNER = 'CNT'
       AND TABLE_NAME = 'CNT_AUT_LINE_WRK';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE CNT.CNT_AUT_LINE_WRK (
                RUN_ID VARCHAR2(32) NOT NULL,
                SECUENCIA NUMBER,
                CODIGO_MAYOR NUMBER,
                MAYOR VARCHAR2(400),
                CODIGO_AUXILIAR NUMBER,
                AUXILIAR VARCHAR2(400),
                REFERENCIA1 VARCHAR2(20),
                REFERENCIA2 VARCHAR2(20),
                REFERENCIA3 VARCHAR2(20),
                DESCRIPCION VARCHAR2(200),
                MONTO NUMBER(18,2),
                FECHA_REGISTRO DATE DEFAULT SYSDATE NOT NULL
            )';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_count
      FROM ALL_INDEXES
     WHERE OWNER = 'CNT'
       AND INDEX_NAME = 'IDX_CNT_AUT_LINE_WRK_RUN';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX CNT.IDX_CNT_AUT_LINE_WRK_RUN ON CNT.CNT_AUT_LINE_WRK (RUN_ID)';
    END IF;
END;
/

SELECT owner, table_name
  FROM all_tables
 WHERE owner = 'CNT'
   AND table_name = 'CNT_AUT_LINE_WRK';

SELECT owner, index_name, table_name
  FROM all_indexes
 WHERE owner = 'CNT'
   AND index_name = 'IDX_CNT_AUT_LINE_WRK_RUN';
