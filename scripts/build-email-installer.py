#!/usr/bin/env python3
"""Build one DBeaver PL/SQL statement, no SQL*Plus commands or slash terminators."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1] / 'Features/Email/Sql'
parts = ["""DECLARE n NUMBER; BEGIN
 IF SYS_CONTEXT('USERENV','CURRENT_SCHEMA')<>'SIS' THEN RAISE_APPLICATION_ERROR(-20001,'Seleccione SIS como esquema activo.'); END IF;
 SELECT COUNT(*) INTO n FROM ALL_OBJECTS WHERE OWNER='SIS' AND OBJECT_NAME='PKG_OSS_MENU_SIMPLE' AND OBJECT_TYPE IN ('PACKAGE','PACKAGE BODY') AND STATUS='VALID';
 IF n<>2 THEN RAISE_APPLICATION_ERROR(-20007,'Instale y valide R50 antes de instalar R52.'); END IF;
END;"""]
for name in ['03_MANUAL_SCHEMA.sql', 'PKG_EMAIL_MANUAL.sql']:
    parts.extend(x.strip() for x in re.split(r'^/\s*$', (root / name).read_text(), flags=re.M) if x.strip())
parts.append("""DECLARE n NUMBER; BEGIN SELECT COUNT(*) INTO n FROM ALL_OBJECTS WHERE OWNER='SIS' AND OBJECT_NAME='PKG_EMAIL_MANUAL' AND STATUS<>'VALID'; IF n>0 THEN RAISE_APPLICATION_ERROR(-20006,'PKG_EMAIL_MANUAL invalido: consulte ALL_ERRORS.'); END IF; END;""")
# R50 is required to expose this screen. Seed catalog only, never role/user assignments.
parts.append((root / 'SEED_R52_MENU.sql').read_text().strip())
output = '-- R52: seleccionar TODO y ejecutar como UNA sentencia en DBeaver. Esquema SIS.\n-- Requiere R50. DDL hace commit implicito. No contiene secretos ni envia correos.\nBEGIN\n'
for part in parts:
    if len(part.encode('utf-8')) > 30000:
        raise ValueError('Split the DDL below the Oracle 10g PL/SQL string limit')
    output += " EXECUTE IMMEDIATE '" + part.replace("'", "''") + "';\n"
output += " DBMS_OUTPUT.PUT_LINE('R52 instalado. Asignar menu/permiso al rol. Envio depende de configuracion privada.');\nEND;\n"
(root / 'INSTALL_R52_EMAIL.sql').write_text(output)
