CREATE OR REPLACE TRIGGER RH.RH_TRG_BEF_UPD_CONCEPTOS
 BEFORE UPDATE OR DELETE
 ON RH_CONCEPTOS
 FOR EACH ROW
BEGIN
    Declare
        V_USUARIO varchar2(100):=null;
        V_MAQUINA varchar2(100):=null;
        V_FECHA varchar2(100) :=null;
    BEGIN
        SELECT SYS_CONTEXT('USERENV','OS_USER'),
               SYS_CONTEXT('USERENV','HOST'),
               TO_CHAR(SYSDATE,'DD/MM/RRRR HH:MI:SS')
          INTO V_USUARIO,V_MAQUINA,V_FECHA
          FROM DUAL;

     IF UPDATING THEN
        INSERT INTO RH.RH_H_CONCEPTOS VALUES (RH_S_CODIGO_H_CONCEPTO.NEXTVAL, SYSDATE, :NEW.USUARIO_UPD, 
                                                :OLD.CODIGO_CONCEPTO,
                                                :OLD.CODIGO,
                                                :OLD.CODIGO_TIPO_NOMINA,
                                                :OLD.DENOMINACION,
                                                :OLD.DESCRIPCION,
                                                :OLD.TIPO_CONCEPTO,
                                                :OLD.MODULO_ID,
                                                :OLD.CODIGO_PUC,
                                                :OLD.STATUS,
                                                :OLD.EXTRA1,
                                                :OLD.EXTRA2,
                                                :OLD.EXTRA3,
                                                :OLD.USUARIO_INS,
                                                :OLD.FECHA_INS,
                                                :OLD.USUARIO_UPD,
                                                :OLD.FECHA_UPD,
                                                :OLD.CODIGO_EMPRESA,
                                                :OLD.FRECUENCIA_ID,
                                                :OLD.DEDUSIBLE,
                                                :OLD.AUTOMATICO, V_USUARIO,V_MAQUINA); 
     ELSIF DELETING THEN
            Raise_Application_Error (-20001,'No es Permitido Delete Sobre Esta Tabla');
     END IF;
  End;                                                  
END; 
/
