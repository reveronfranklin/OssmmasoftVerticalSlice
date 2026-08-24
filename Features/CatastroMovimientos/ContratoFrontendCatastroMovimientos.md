# Contrato frontend - Historial de movimientos de inmueble

- Metodo: `POST`
- Ruta: `/api/CatastroMovimientos/getByInmueble`
- Request: `{ "codigoInmueble": 123, "codigoContribuyente": 456 }`

Devuelve dos colecciones: `diferencias`, consultada en CAT, y `movimientos`, consultada en RM para tributo inmobiliario (`TRIBUTO = 1`). El endpoint es estrictamente de solo lectura y no ejecuta `CAT_CALCULO_DIF_AFORO1`, `CAT_TO_RM_MOVIMIENTOS`, anulaciones ni reversos.

Los instaladores se separan por esquema: `INSTALL_CAT_MOV.sql` se ejecuta conectado a CAT y `INSTALL_RM_MOV.sql` conectado a RM.
