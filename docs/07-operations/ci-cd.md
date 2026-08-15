# CI/CD

## CI (`.github/workflows/ci.yml`)

Corre en cada push/PR contra `main`, en runners hospedados de GitHub (no requiere el self-hosted
runner del servidor). Tres jobs independientes:

1. **build-and-test** — restaura, corre la auditoría de vulnerabilidades de NuGet (`dotnet list
   package --vulnerable`, el mismo chequeo que ya detectó una vulnerabilidad real en MailKit 4.9.0
   durante este mismo desarrollo — no es un chequeo teórico), compila en `Release` y corre toda la
   suite `dotnet test`.
2. **frontend** — `npm ci` + `npm run build` (incluye el chequeo de tipos de TypeScript vía `tsc -b`
   antes de `vite build`).
3. **docker-build** — construye las 7 imágenes definidas en `deploy/compose/docker-compose.yml`,
   probando que cada Dockerfile sigue siendo válido — depende de que los dos jobs anteriores pasen
   primero.

Un run de CI en rojo bloquea el merge (configurar la protección de rama `main` en GitHub para
requerir este workflow) y, sobre todo, nunca dispara el deploy — son dos workflows separados a
propósito.

## Deploy (`.github/workflows/deploy.yml`)

Requiere un **self-hosted runner** registrado en el propio Windows Server (Settings → Actions →
Runners → New self-hosted runner, ejecutado bajo la cuenta de servicio `svc_sdpp` — ver
[windows-server-deploy.md](windows-server-deploy.md)). Solo corre `on: workflow_run` cuando el
workflow CI terminó en éxito sobre `main` — nunca directamente sobre un push, precisamente para que
un build roto nunca pueda desplegarse.

Pasos:

1. Checkout del commit exacto que pasó CI.
2. `docker compose ... build` — reconstruye las imágenes en el propio servidor.
3. `docker compose ... up -d` — recrea solo los contenedores cuya imagen cambió (Compose ya es
   idempotente respecto a esto).
4. **Health check** — espera hasta 60s a que `https://sdpp.intranet/health` (a través de Apache) y
   el `/health` de cada uno de los 5 módulos respondan `"status": "healthy"`.
5. Si el health check falla → **rollback**: `docker compose ... up -d` contra las imágenes
   previamente etiquetadas (Docker conserva la imagen anterior con su digest hasta que una nueva
   build la reemplaza; el workflow etiqueta explícitamente `sdpp-<servicio>:previous` antes de
   construir la nueva, para poder revertir a ese tag).
6. Si el health check pasa → se elimina el tag `:previous` de la ejecución anterior (ya no hace
   falta) y se registra el deploy en el log de despliegues.

Ningún paso ejecuta migraciones de base de datos "a mano" ni fuera de este flujo — cada API sigue
aplicando sus propias migraciones EF Core al arrancar (comportamiento ya existente, sin cambios),
así que un rollback de contenedor es también, automáticamente, la unidad de rollback de esquema
mientras las migraciones sean aditivas (ver la sección de migraciones destructivas en
backup-recovery-plan.md — esas requieren un procedimiento manual con backup previo, nunca deploy
automático).

## Ver también

- [windows-server-deploy.md](windows-server-deploy.md) — dónde y cómo corre el runner.
- `.github/workflows/ci.yml` y `.github/workflows/deploy.yml` — las definiciones reales.
