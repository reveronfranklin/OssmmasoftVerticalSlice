# Publicacion automatica al ambiente de pruebas

Componente: vertical. Workflow: `.github/workflows/deploy-pruebas.yml`.

Cada push a develop compila y publica este componente en el Ubuntu de pruebas.
Main no dispara publicaciones. La compilacion se ejecuta en GitHub; las conexiones
Oracle y archivos persistentes permanecen en Ubuntu. No se ejecutan migraciones.

## Uso

1. Confirmar los cambios requeridos y subirlos a develop.
2. Revisar Actions > Publicar pruebas en este repositorio.
3. Esperar que ambas etapas, compilacion y publicacion, terminen correctamente.
4. Validar la funcionalidad en http://74.121.190.6/ y recargar el navegador.

Un commit local sin push no publica. El servidor recibe un artefacto Linux
identificado por commit, run ID e intento. Las publicaciones del repositorio
se serializan y el servidor tambien usa un bloqueo compartido entre componentes.

## Configuracion en GitHub

Variables de repositorio:

- QA_DEPLOY_ENABLED: true habilita las publicaciones; false las pausa.
- QA_SSH_HOST y QA_SSH_USER: destino de pruebas.
- En frontend, QA_REPORT_URL, QA_GATEWAY_URL y QA_N8N_URL: URLs publicas existentes.

Secretos de repositorio:

- QA_SSH_PRIVATE_KEY: clave dedicada a este componente, sin permisos de shell.
- QA_SSH_KNOWN_HOSTS: clave de host fijada desde la conexion verificada.

No agregar contrasenas Oracle, claves privadas ni tokens a archivos versionados.
Las claves de Actions se restringen en authorized_keys al receptor de artefactos
y al componente; no usar la clave personal del equipo para este workflow.

## Fallos y retorno

Si falla la compilacion, no se cambia la version activa. Si falla el arranque,
el script del servidor recupera y comprueba la version anterior. La salud HTTP
no sustituye la validacion funcional ni la prueba de las conexiones Oracle.

Retorno manual, conectado como ossmma en Ubuntu:

```bash
bash ~/ossmmasoft-deploy/VolverVersion.sh vertical
```

Consultar logs:

```bash
journalctl --user -u ossmmasoft-vertical -n 80 --no-pager
```

El retorno cambia binarios; no revierte bases de datos, configuracion compartida
ni recursos de public. Los recursos versionados se copian sin eliminar uploads;
las carpetas de datos conocidas quedan excluidas de esa copia.

Las entregas que requieren cambios coordinados entre componentes deben planearse:
los tres repositorios no forman una transaccion atomica de publicacion.
Para publicar manualmente siguen disponibles Publicar.sh y el procedimiento
Operacion.md del requerimiento 40 en el workspace MM. No se requiere sudo para
actualizar aplicaciones. workflow_dispatch solo se usa con ref develop; su
visibilidad manual depende de que el workflow exista en la rama predeterminada.
