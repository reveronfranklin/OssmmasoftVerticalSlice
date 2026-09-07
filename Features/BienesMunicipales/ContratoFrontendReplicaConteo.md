# Replica independiente del conteo

Pantalla: `/apps/Bm/BmReplicaConteo/`. El listado de conteos ofrece un enlace;
la ejecucion y confirmacion ahora pertenecen a esta pantalla. Crear un conteo
no inicia la replica. El worker programado conserva su intervalo y utiliza
el mismo estado y exclusion de ejecuciones que la accion manual.

## Endpoints

- `POST /api/BmReplicaConteo/Iniciar`: sin body. Reserva una ejecucion y la
  entrega a un worker del servidor; responde sin esperar la copia.
- `GET /api/BmReplicaConteo/Estado`: sin body. Devuelve la ultima ejecucion
  del proceso, manual o programada. Consultar cada 2 segundos.
- `POST /api/BmReplicaConteo/Replicar`: contrato anterior conservado; espera
  la finalizacion y tambien publica avance en Estado.

Los endpoints nuevos devuelven `ResultDto<BmReplicaEstadoDto>` con `data`
como objeto, `isValid` y `message`. Los campos heredados de paginacion no
se utilizan: el estado siempre incluye las siete tablas completas.

Ejemplo de respuesta de Estado (tablas abreviadas en este ejemplo):

```json
{
  "isValid": true,
  "message": "Copiando: BMC.BM_BIENES",
  "data": {
    "ejecucionId": "ba337d01-b9fa-4e0d-9c2f-0f1d5d814af2",
    "enCurso": true,
    "mensaje": "Copiando: BMC.BM_BIENES",
    "inicio": "2026-09-06T18:00:00Z",
    "fin": null,
    "tablas": [
      { "tabla": "BMC.BM_BIENES", "estado": "Copiando", "total": 10000, "copiados": 2300 }
    ]
  }
}
```

Iniciar responde `isValid = false` si `settings:ReplicarConteo` no es `1`
o existe una ejecucion en curso. No encola replicas adicionales. No hay
reintento automatico del POST desde el frontend. Si se pierde su respuesta,
consultar Estado antes de decidir iniciar otra ejecucion.

## Avance y consistencia

Tablas: BMC.BM_ARTICULOS, BMC.BM_BIENES, BMC.BM_MOV_BIENES,
BMC.BM_DIR_BIEN, BMC.BM_CLASIFICACION_BIENES, BMC.BM_DESCRIPTIVAS y
RHC.RH_PERSONAS. Estados: Pendiente, Leyendo, Leida, Copiando, Verificando,
Confirmada y No confirmada. Durante lectura se muestra progreso indeterminado;
al terminar se conoce el total. Copiados se actualiza cada 100 filas y al
terminar cada tabla. El porcentaje representa copia, no tiempo restante.

Las seis tablas BMC conservan una sola transaccion y solo se marcan
Confirmada despues del commit. RHC confirma personas por separado, como en
el proceso anterior: un fallo posterior en BMC no revierte RHC. Las tablas
no confirmadas se muestran como tales al fallar, incluso si habian llegado
al 100% de copia. No se modifican tablas de conteos capturados.

## Instalacion y limites

Desplegar backend y frontend juntos. Para el menu servido desde SIS, aplicar
`Sql/06_SEED_BM_MENU.sql`, que incluye la opcion 7150 con los mismos roles y
permiso de menu del modulo. El enlace del listado funciona sin actualizar el
menu. No se ejecuto este script en Oracle durante el desarrollo.

El avance se guarda en memoria del proceso API, no en Oracle. Navegar o cerrar
la pantalla no cancela la ejecucion. Reiniciar la API pierde el estado y puede
interrumpir la copia: verificar el resultado antes de reintentar. Este mecanismo
requiere una sola instancia API; multiples instancias necesitan coordinacion y
estado compartidos antes de habilitarlo. No ofrece historial durable ni cancelacion.

## Verificacion con Oracle

Iniciar desde la pantalla; observar lectura, filas copiadas y confirmacion.
Abrir una segunda pestana: debe mostrar la misma ejecucion y bloquear otro
inicio. Salir y regresar: debe recuperar el avance. Ante fallo BMC, comprobar
rollback BMC y que una confirmacion previa RHC permanezca visible. Crear un
conteo sigue usando la replica disponible sin disparar una nueva replica.
