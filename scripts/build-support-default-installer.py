#!/usr/bin/env python3
"""Build the SIS Oracle 10g single-statement support installer."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
paths = [root / name for name in (
    'Features/Support/Sql/06_SUPPORT_DEFAULT.sql',
    'Features/Email/Sql/PKG_EMAIL_MANUAL.sql',
    'Features/RhReciboPago/Sql/PKG_EMAIL_RECIBO.sql',
)]
parts = [part.strip() for path in paths
         for part in re.split(r'^/\s*$', path.read_text(), flags=re.M) if part.strip()]
names = ['SP_SOP_CFG_GET', 'SP_SOP_CFG_SAVE', 'SP_SOP_USR_MAIL',
         'SP_SOP_TKT_AUTO', 'PKG_EMAIL_MANUAL', 'PKG_EMAIL_RECIBO']
parts.append("DECLARE n NUMBER; BEGIN SELECT COUNT(*) INTO n FROM ALL_OBJECTS WHERE OWNER='SIS' AND OBJECT_NAME IN ("
             + ','.join("'" + name + "'" for name in names)
             + ") AND STATUS<>'VALID'; IF n>0 THEN RAISE_APPLICATION_ERROR(-20060,'Objetos invalidos. Revisar ALL_ERRORS.'); END IF; END;")
if any(len(part.encode()) >= 30000 for part in parts):
    raise ValueError('PL/SQL literal exceeds safe Oracle 10g limit')
output = ('-- SIS Oracle 10g. Ejecutar TODO como UNA sentencia en DBeaver. Sin slash final.\n'
          '-- No envia correos ni cambia tickets al instalar.\nBEGIN\n')
output += ''.join(" EXECUTE IMMEDIATE '" + part.replace("'", "''") + "';\n" for part in parts)
output += " DBMS_OUTPUT.PUT_LINE('Asignacion automatica de Soporte instalada. Configure el responsable.');\nEND;\n"
(root / 'Features/Support/Sql/INSTALL_SUPPORT_DEFAULT.sql').write_text(output)
