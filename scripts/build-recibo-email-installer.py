#!/usr/bin/env python3
"""Produce single-statement DBeaver installers from Oracle 10g sources."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1] / 'Features/RhReciboPago/Sql'
def blocks(name):
    return [x.strip() for x in re.split(r'^/\s*$', (root / name).read_text(), flags=re.M) if x.strip()]
def validation(owner, package):
    return f"DECLARE n NUMBER; BEGIN SELECT COUNT(*) INTO n FROM ALL_OBJECTS WHERE OWNER='{owner}' AND OBJECT_NAME='{package}' AND OBJECT_TYPE IN ('PACKAGE','PACKAGE BODY') AND STATUS='VALID'; IF n<>2 THEN RAISE_APPLICATION_ERROR(-20053,'Paquete {owner}.{package} invalido. Consulte ALL_ERRORS.'); END IF; END;"
def write(name, parts):
    output = '-- R53: seleccionar TODO y ejecutar como UNA sentencia en DBeaver. Sin slash final.\n-- No envia correos; DDL hace commit implicito. Respaldar esquema antes de instalar.\nBEGIN\n'
    for part in parts:
        if len(part.encode()) > 30000:
            raise ValueError('PL/SQL literal exceeds safe Oracle 10g limit')
        output += " EXECUTE IMMEDIATE '" + part.replace("'", "''") + "';\n"
    output += " DBMS_OUTPUT.PUT_LINE('R53 instalado. Asignar permisos y habilitar RhReciboEmail solo tras validar.');\nEND;\n"
    (root / name).write_text(output)
rh = blocks('01_RH_SCHEMA.sql') + blocks('PKG_RH_REC_MAIL.sql') + [validation('RH','PKG_RH_REC_MAIL')]
sis = [validation('SIS','PKG_EMAIL_MANUAL')] + blocks('02_SIS_SCHEMA.sql') + blocks('PKG_EMAIL_RECIBO.sql') + [validation('SIS','PKG_EMAIL_RECIBO')] + blocks('SEED_R53_PERM.sql')
write('INSTALL_R53_RH.sql', rh)
write('INSTALL_R53_SIS.sql', sis)
write('INSTALL_R53_COMPLETO.sql', [validation('SIS','PKG_EMAIL_MANUAL')] + rh + sis)
