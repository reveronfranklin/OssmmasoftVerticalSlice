CREATE OR REPLACE PROCEDURE BM.SP_BM_UBI_GET_ALL (
    p_CodigoEmpresa IN NUMBER,
    p_SearchText IN VARCHAR2,
    p_Page IN NUMBER,
    p_PageSize IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Page NUMBER := NVL(p_Page, 1);
    v_PageSize NUMBER := NVL(p_PageSize, 50);
    v_FromRow NUMBER;
    v_ToRow NUMBER;
BEGIN
    v_FromRow := ((v_Page - 1) * v_PageSize) + 1;
    v_ToRow := v_Page * v_PageSize;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_DIR_BIEN D,
           PRE.PRE_INDICE_CAT_PRG P
     WHERE P.CODIGO_ICP = D.CODIGO_ICP
       AND D.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (
            p_SearchText IS NULL
            OR UPPER(NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION)) LIKE '%' || UPPER(p_SearchText) || '%'
            OR TO_CHAR(D.CODIGO_ICP) LIKE '%' || p_SearchText || '%'
            OR UPPER(NVL(D.COMPLEMENTO_DIR, '')) LIKE '%' || UPPER(p_SearchText) || '%'
       );

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT D.CODIGO_DIR_BIEN,
                               D.CODIGO_ICP,
                               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
                               NVL(D.PAIS_ID, 0) PAIS_ID,
                               NVL(D.ESTADO_ID, 0) ESTADO_ID,
                               NVL(D.MUNICIPIO_ID, 0) MUNICIPIO_ID,
                               NVL(D.CIUDAD_ID, 0) CIUDAD_ID,
                               NVL(D.PARROQUIA_ID, 0) PARROQUIA_ID,
                               NVL(D.SECTOR_ID, 0) SECTOR_ID,
                               NVL(D.URBANIZACION_ID, 0) URBANIZACION_ID,
                               NVL(D.MANZANA_ID, 0) MANZANA_ID,
                               NVL(D.PARCELA_ID, 0) PARCELA_ID,
                               NVL(D.VIALIDAD_ID, 0) VIALIDAD_ID,
                               D.VIALIDAD,
                               NVL(D.TIPO_VIVIENDA_ID, 0) TIPO_VIVIENDA_ID,
                               D.VIVIENDA,
                               NVL(D.TIPO_NIVEL_ID, 0) TIPO_NIVEL_ID,
                               D.NIVEL,
                               NVL(D.TIPO_UNIDAD_ID, 0) TIPO_UNIDAD_ID,
                               D.NUMERO_UNIDAD,
                               D.COMPLEMENTO_DIR,
                               NVL(D.TENENCIA_ID, 0) TENENCIA_ID,
                               NVL(D.CODIGO_POSTAL, 0) CODIGO_POSTAL,
                               D.FECHA_INI,
                               D.FECHA_FIN,
                               NVL(D.UNIDAD_TRABAJO_ID, 0) UNIDAD_TRABAJO_ID,
                               UPPER(TRIM(NVL(D.VIALIDAD, '') || ' ' || NVL(D.VIVIENDA, '') || ' ' || NVL(D.NIVEL, '') || ' ' || NVL(D.NUMERO_UNIDAD, '') || ' ' || NVL(D.COMPLEMENTO_DIR, ''))) DIRECCION,
                               UPPER(TO_CHAR(D.CODIGO_ICP) || ' ' || NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) || ' ' || NVL(D.COMPLEMENTO_DIR, '')) SEARCH_TEXT
                          FROM BM.BM_DIR_BIEN D,
                               PRE.PRE_INDICE_CAT_PRG P
                         WHERE P.CODIGO_ICP = D.CODIGO_ICP
                           AND D.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (
                                p_SearchText IS NULL
                                OR UPPER(NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION)) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR TO_CHAR(D.CODIGO_ICP) LIKE '%' || p_SearchText || '%'
                                OR UPPER(NVL(D.COMPLEMENTO_DIR, '')) LIKE '%' || UPPER(p_SearchText) || '%'
                           )
                         ORDER BY NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION), D.CODIGO_DIR_BIEN
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DIR_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_UBI_GET_ICP (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoIcp IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_DIR_BIEN
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_ICP = p_CodigoIcp;

    OPEN p_ResultSet FOR
        SELECT D.CODIGO_DIR_BIEN,
               D.CODIGO_ICP,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               NVL(D.PAIS_ID, 0) PAIS_ID,
               NVL(D.ESTADO_ID, 0) ESTADO_ID,
               NVL(D.MUNICIPIO_ID, 0) MUNICIPIO_ID,
               NVL(D.CIUDAD_ID, 0) CIUDAD_ID,
               NVL(D.PARROQUIA_ID, 0) PARROQUIA_ID,
               NVL(D.SECTOR_ID, 0) SECTOR_ID,
               NVL(D.URBANIZACION_ID, 0) URBANIZACION_ID,
               NVL(D.MANZANA_ID, 0) MANZANA_ID,
               NVL(D.PARCELA_ID, 0) PARCELA_ID,
               NVL(D.VIALIDAD_ID, 0) VIALIDAD_ID,
               D.VIALIDAD,
               NVL(D.TIPO_VIVIENDA_ID, 0) TIPO_VIVIENDA_ID,
               D.VIVIENDA,
               NVL(D.TIPO_NIVEL_ID, 0) TIPO_NIVEL_ID,
               D.NIVEL,
               NVL(D.TIPO_UNIDAD_ID, 0) TIPO_UNIDAD_ID,
               D.NUMERO_UNIDAD,
               D.COMPLEMENTO_DIR,
               NVL(D.TENENCIA_ID, 0) TENENCIA_ID,
               NVL(D.CODIGO_POSTAL, 0) CODIGO_POSTAL,
               D.FECHA_INI,
               D.FECHA_FIN,
               NVL(D.UNIDAD_TRABAJO_ID, 0) UNIDAD_TRABAJO_ID,
               UPPER(TRIM(NVL(D.VIALIDAD, '') || ' ' || NVL(D.VIVIENDA, '') || ' ' || NVL(D.NIVEL, '') || ' ' || NVL(D.NUMERO_UNIDAD, '') || ' ' || NVL(D.COMPLEMENTO_DIR, ''))) DIRECCION,
               UPPER(TO_CHAR(D.CODIGO_ICP) || ' ' || NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) || ' ' || NVL(D.COMPLEMENTO_DIR, '')) SEARCH_TEXT
          FROM BM.BM_DIR_BIEN D,
               PRE.PRE_INDICE_CAT_PRG P
         WHERE P.CODIGO_ICP = D.CODIGO_ICP
           AND D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.CODIGO_ICP = p_CodigoIcp
         ORDER BY D.CODIGO_DIR_BIEN;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DIR_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_UBI_GET_ICP_LST (
    p_CodigoEmpresa IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM PRE.PRE_INDICE_CAT_PRG
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa;

    OPEN p_ResultSet FOR
        SELECT CODIGO_ICP,
               NVL(UNIDAD_EJECUTORA, DENOMINACION) UNIDAD_TRABAJO
          FROM PRE.PRE_INDICE_CAT_PRG
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
         ORDER BY NVL(UNIDAD_EJECUTORA, DENOMINACION);

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ICP FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_UBI_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDirBien IN NUMBER,
    p_CodigoIcp IN NUMBER,
    p_PaisId IN NUMBER,
    p_EstadoId IN NUMBER,
    p_MunicipioId IN NUMBER,
    p_CiudadId IN NUMBER,
    p_ParroquiaId IN NUMBER,
    p_SectorId IN NUMBER,
    p_UrbanizacionId IN NUMBER,
    p_ManzanaId IN NUMBER,
    p_ParcelaId IN NUMBER,
    p_VialidadId IN NUMBER,
    p_Vialidad IN VARCHAR2,
    p_TipoViviendaId IN NUMBER,
    p_Vivienda IN VARCHAR2,
    p_TipoNivelId IN NUMBER,
    p_Nivel IN VARCHAR2,
    p_TipoUnidadId IN NUMBER,
    p_NumeroUnidad IN VARCHAR2,
    p_ComplementoDir IN VARCHAR2,
    p_TenenciaId IN NUMBER,
    p_CodigoPostal IN NUMBER,
    p_FechaIni IN DATE,
    p_FechaFin IN DATE,
    p_UnidadTrabajoId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
BEGIN
    IF NVL(p_CodigoIcp, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el ICP de la ubicacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DIR_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_DIR_BIEN), 0) + 1
      INTO v_Codigo
      FROM BM.BM_DIR_BIEN;

    INSERT INTO BM.BM_DIR_BIEN (
        CODIGO_DIR_BIEN, CODIGO_ICP, PAIS_ID, ESTADO_ID, MUNICIPIO_ID, CIUDAD_ID,
        PARROQUIA_ID, SECTOR_ID, URBANIZACION_ID, MANZANA_ID, PARCELA_ID,
        VIALIDAD_ID, VIALIDAD, TIPO_VIVIENDA_ID, VIVIENDA, TIPO_NIVEL_ID,
        NIVEL, TIPO_UNIDAD_ID, NUMERO_UNIDAD, COMPLEMENTO_DIR, TENENCIA_ID,
        CODIGO_POSTAL, FECHA_INI, FECHA_FIN, FECHA_INS, CODIGO_EMPRESA,
        UNIDAD_TRABAJO_ID
    ) VALUES (
        v_Codigo, p_CodigoIcp, p_PaisId, p_EstadoId, p_MunicipioId, p_CiudadId,
        p_ParroquiaId, p_SectorId, p_UrbanizacionId, p_ManzanaId, p_ParcelaId,
        p_VialidadId, p_Vialidad, p_TipoViviendaId, p_Vivienda, p_TipoNivelId,
        p_Nivel, p_TipoUnidadId, p_NumeroUnidad, p_ComplementoDir, p_TenenciaId,
        p_CodigoPostal, NVL(p_FechaIni, SYSDATE), p_FechaFin, SYSDATE,
        p_CodigoEmpresa, p_UnidadTrabajoId
    );

    COMMIT;

    BM.SP_BM_UBI_GET_ICP(p_CodigoEmpresa, p_CodigoIcp, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DIR_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_UBI_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDirBien IN NUMBER,
    p_CodigoIcp IN NUMBER,
    p_PaisId IN NUMBER,
    p_EstadoId IN NUMBER,
    p_MunicipioId IN NUMBER,
    p_CiudadId IN NUMBER,
    p_ParroquiaId IN NUMBER,
    p_SectorId IN NUMBER,
    p_UrbanizacionId IN NUMBER,
    p_ManzanaId IN NUMBER,
    p_ParcelaId IN NUMBER,
    p_VialidadId IN NUMBER,
    p_Vialidad IN VARCHAR2,
    p_TipoViviendaId IN NUMBER,
    p_Vivienda IN VARCHAR2,
    p_TipoNivelId IN NUMBER,
    p_Nivel IN VARCHAR2,
    p_TipoUnidadId IN NUMBER,
    p_NumeroUnidad IN VARCHAR2,
    p_ComplementoDir IN VARCHAR2,
    p_TenenciaId IN NUMBER,
    p_CodigoPostal IN NUMBER,
    p_FechaIni IN DATE,
    p_FechaFin IN DATE,
    p_UnidadTrabajoId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    IF NVL(p_CodigoIcp, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el ICP de la ubicacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DIR_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_DIR_BIEN
       SET CODIGO_ICP = p_CodigoIcp,
           PAIS_ID = p_PaisId,
           ESTADO_ID = p_EstadoId,
           MUNICIPIO_ID = p_MunicipioId,
           CIUDAD_ID = p_CiudadId,
           PARROQUIA_ID = p_ParroquiaId,
           SECTOR_ID = p_SectorId,
           URBANIZACION_ID = p_UrbanizacionId,
           MANZANA_ID = p_ManzanaId,
           PARCELA_ID = p_ParcelaId,
           VIALIDAD_ID = p_VialidadId,
           VIALIDAD = p_Vialidad,
           TIPO_VIVIENDA_ID = p_TipoViviendaId,
           VIVIENDA = p_Vivienda,
           TIPO_NIVEL_ID = p_TipoNivelId,
           NIVEL = p_Nivel,
           TIPO_UNIDAD_ID = p_TipoUnidadId,
           NUMERO_UNIDAD = p_NumeroUnidad,
           COMPLEMENTO_DIR = p_ComplementoDir,
           TENENCIA_ID = p_TenenciaId,
           CODIGO_POSTAL = p_CodigoPostal,
           FECHA_INI = NVL(p_FechaIni, FECHA_INI),
           FECHA_FIN = p_FechaFin,
           FECHA_UPD = SYSDATE,
           UNIDAD_TRABAJO_ID = p_UnidadTrabajoId
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DIR_BIEN = p_CodigoDirBien;

    COMMIT;

    BM.SP_BM_UBI_GET_ICP(p_CodigoEmpresa, p_CodigoIcp, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DIR_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_UBI_HIST_GET (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDirBien IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_DIR_H_BIEN
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DIR_BIEN = p_CodigoDirBien;

    OPEN p_ResultSet FOR
        SELECT H.CODIGO_H_DIR_BIEN,
               H.CODIGO_DIR_BIEN,
               H.CODIGO_ICP,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               UPPER(TRIM(NVL(H.VIALIDAD, '') || ' ' || NVL(H.VIVIENDA, '') || ' ' || NVL(H.NIVEL, '') || ' ' || NVL(H.NUMERO_UNIDAD, '') || ' ' || NVL(H.COMPLEMENTO_DIR, ''))) DIRECCION,
               H.FECHA_INI,
               H.FECHA_FIN,
               H.FECHA_H_INS
          FROM BM.BM_DIR_H_BIEN H,
               PRE.PRE_INDICE_CAT_PRG P
         WHERE P.CODIGO_ICP(+) = H.CODIGO_ICP
           AND H.CODIGO_EMPRESA = p_CodigoEmpresa
           AND H.CODIGO_DIR_BIEN = p_CodigoDirBien
         ORDER BY H.FECHA_H_INS DESC, H.CODIGO_H_DIR_BIEN DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_H_DIR_BIEN FROM DUAL WHERE 1 = 0;
END;
/
