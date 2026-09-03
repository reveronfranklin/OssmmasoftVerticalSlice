# Prueba de concurrencia - Asignación del número de control

Tarea `T2.7` del requerimiento 32. Fecha: **2026-09-03**.

## Qué se quiere demostrar

`INV-1`: **nunca dos números de control distintos para el mismo documento de un mismo emisor**, y nunca el mismo número dos veces. Es la causal del Art. 34.2 de la Providencia SNAT/2024/000102, y su consecuencia es la revocatoria de la autorización de Ossmmasoft como imprenta digital.

Un endpoint que responde no demuestra nada. Lo que hay que demostrar es que **bajo carga concurrente sobre el mismo emisor** la secuencia sale consecutiva, sin duplicados y sin huecos.

## Cómo se hizo

Con `pgbench`, que viene con PostgreSQL. Mantiene conexiones persistentes y solapa las transacciones de verdad.

**Un intento anterior no sirvió y queda registrado para que nadie lo repita:** treinta procesos `psql` lanzados con `&` desde bash. Pasó limpio, pero también pasó limpio el control negativo — los procesos arrancan escalonados y nunca llegan a solaparse. Una prueba de concurrencia sin contención mide el arranque del proceso, no la contención.

```txt
20 clientes · 4 hilos · 15 transacciones por cliente = 300 asignaciones
todas sobre el MISMO emisor, que es el peor caso: el bloqueo es por emisor
```

Cada transacción reproduce exactamente la secuencia del handler `FacturacionElectronicaNumeroControlAsignarHandler`:

1. `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` sobre `FED_EMISOR_CONTADOR` — crea la fila si no existe y **la bloquea**.
2. El cliente calcula el siguiente valor (en el handler lo hace `CalcularSiguiente`).
3. `UPDATE` del contador.
4. `INSERT` en `FED_NUM_CONTROL`.
5. `COMMIT`.

## Resultado

| Medición | Valor |
|---|---|
| Transacciones procesadas | **300 / 300** |
| Transacciones fallidas | **0 (0,000 %)** |
| Números asignados | **300** |
| Pares `(identificador, secuencial)` distintos | **300** |
| Rango | **1 a 300, sin huecos** |
| Contador final | `00 / 300`, coincide con el máximo asignado |
| Latencia media | 19,74 ms |
| Throughput | 1.013 tps |

Sin duplicados. Sin huecos. El contador quedó exactamente donde debía.

## Control negativo

Un test que siempre pasa no prueba nada. Se corrió **la forma prohibida** por `T2.3` —`SELECT MAX(SECUENCIAL) + 1` sin bloquear— con la misma carga:

```txt
20 clientes · 4 hilos · 15 transacciones = 300 intentos
resultado: 17 números asignados. El resto abortó.
ERROR: duplicate key value violates unique constraint "fed_num_control_sec_uk"
```

**El 94 % de las asignaciones falló.** Eso confirma tres cosas:

1. La prueba tiene poder de detección: distingue la implementación correcta de la incorrecta.
2. `SELECT MAX() + 1` está roto bajo concurrencia. No es una preferencia de estilo.
3. Los `UNIQUE` de `FED_NUM_CONTROL` **atrapan** lo que el bloqueo previene. Son dos defensas independientes y las dos funcionan: si el bloqueo fallara, la base todavía impide el duplicado.

## Qué NO cubre esta prueba

Se ejecutó la **secuencia SQL** del handler, no el **código C#** que la ejecuta. La estrategia de bloqueo está demostrada; que el handler la aplique bien —parámetros, transacción, manejo del error de carrera— requiere N peticiones HTTP simultáneas con token, y eso necesita sesión iniciada.

Queda pendiente junto con `T1.10`.

## Cómo reproducirla

Los scripts están en el scratchpad de la sesión, no en el repositorio, porque dependen de un emisor de prueba. Para rehacerla:

```bash
pgbench -h 127.0.0.1 -U fed -d OSSMMASOFT -n -c 20 -j 4 -t 15 -D emi=<id> -f handler.sql
```

Los datos de prueba se borraron al terminar: el esquema quedó como estaba.
