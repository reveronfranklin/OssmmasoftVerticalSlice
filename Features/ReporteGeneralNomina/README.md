# Reporte General de Nomina

Esta carpeta contiene el vertical slice del Reporte General de Nomina.

## Punto de entrada

El endpoint principal es:

```http
POST /api/ReporteGeneralNominaCompletoGetAll/GetAll
```

Clase de entrada:

```text
ReporteGeneralNominaCompletoGetAll.cs
```

El cliente debe enviar parametros de negocio. No debe enviar fragmentos SQL como `p_from_table1`, `p_from_table2` o `p_where`; esos valores se derivan internamente desde `p_tipo_generacion`, `p_codigo_periodo`, `p_fecha_pago` y `p_cedula`.

## Documentacion

- `ReglasNegocioFuncionales.md`: explica el comportamiento esperado desde la perspectiva del usuario funcional.
- `ReglasNegocioTecnicas.md`: describe el contrato, validaciones, derivacion de filtros y stored procedures usados.
- `Examples/`: contiene ejemplos JSON de ejecucion para el endpoint completo.

## Componentes

- `ReporteGeneralNominaCompletoGetAll.cs`: orquesta reporte general, detalle y firma.
- `ReporteGeneralNominaGetAll.cs`: obtiene resumen general por conceptos.
- `ReporteGeneralNominaDetalleGetAll.cs`: obtiene detalle por persona, cargo, concepto y monto.
- `ReporteGeneralNominaFirmaGetAll.cs`: obtiene firmas activas.

## Nota tecnica

Actualmente el SP de firma `RH.SP_REP_GRAL_NOM_FIR_GET_ALL` no recibe filtros de nomina. Por eso la seccion `Firma` se obtiene sin aplicar `tipo_nomina`, `empresa`, `fecha_pago`, `periodo` ni `cedula`.
