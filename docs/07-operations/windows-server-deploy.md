# Despliegue en Windows Server (intranet corporativa)

Esta guía documenta cómo llevar SDPP a un Windows Server real, preservando exactamente la topología
ya construida y probada en `deploy/compose/docker-compose.yml` — no se reescribe la aplicación como
servicios nativos de Windows; se ejecuta el mismo stack Docker ya validado, con Apache como único
punto de entrada de la intranet.

## Decisión de arquitectura: Docker sobre binarios nativos

Dos caminos eran posibles:

1. **Docker Engine como servicio de Windows** (el elegido) — se reutiliza exactamente el mismo
   `docker-compose.yml`/Dockerfiles que ya se probaron a lo largo de todo este desarrollo, con
   `restart: unless-stopped` para que sobreviva un reinicio del servidor sin depender de una consola
   abierta.
2. Desplegar los 5 binarios .NET como Servicios de Windows nativos (`sc.exe`), sin Docker.

Se eligió (1) porque reconstruir el mecanismo de arranque de cada módulo como servicio de Windows
nativo — sin Docker — es trabajo adicional significativo que no aporta nada que Docker Engine (que
de por sí ya corre como servicio de Windows) no dé automáticamente, y porque introduce una topología
nunca antes probada en este proyecto.

## Requisitos

| Componente | Versión mínima | Notas |
|---|---|---|
| Windows Server | 2022 (Standard o Datacenter) | Con Hyper-V/contenedores habilitado para Docker Desktop/Engine |
| Docker Engine | 26.x+ | Ver instalación abajo — **no** Docker Desktop (licenciamiento comercial en servidor); usar Docker Engine + Docker Compose plugin directamente |
| Apache HTTP Server | 2.4.x (build para Windows, p.ej. Apache Lounge) | `mod_proxy`, `mod_proxy_http`, `mod_ssl`, `mod_headers`, `mod_rewrite`, `mod_deflate` |
| Certificado TLS | Emitido por la PKI interna | Para `sdpp.intranet` |
| Espacio en disco | 100 GB+ | Documentos, backups, logs, imágenes Docker |

## Instalación de Docker Engine (sin Docker Desktop)

```powershell
# Como Administrador
Install-WindowsFeature -Name Containers
Restart-Computer

# Instalar Docker Engine (no Docker Desktop) vía el script oficial de Moby/Docker para Windows Server
Invoke-WebRequest -UseBasicParsing "https://raw.githubusercontent.com/microsoft/Windows-Containers/Main/helpful_tools/Install-DockerCE/install-docker-ce.ps1" -OutFile install-docker-ce.ps1
.\install-docker-ce.ps1

# Verificar que el servicio "docker" quedó registrado como servicio de Windows con inicio automático
Get-Service docker
Set-Service docker -StartupType Automatic
```

## Estructura de carpetas en el servidor

```
C:\SDPP\
├── app\                  ← clon del repositorio (deploy vía CI/CD, ver ci-cd.md)
├── certs\                ← certificado TLS interno (sdpp.intranet.crt/.key)
├── logs\
│   └── apache\
├── backups\               ← ver docs/07-operations/backup-recovery-plan.md y scripts/backup/
└── sql-restore-test\       ← usado solo por scripts/backup/sql-restore-test.ps1, vacío en reposo
```

## Cuenta de servicio

Crear una cuenta de servicio dedicada (no la cuenta de un administrador humano) para ejecutar el
runner de CI/CD (ver ci-cd.md) y las tareas programadas de backup:

```powershell
New-LocalUser "svc_sdpp" -Description "Cuenta de servicio SDPP — CI/CD y backups" -NoPasswordExpiration
Add-LocalGroupMember -Group "Docker-Users" -Member "svc_sdpp"
```

No agregar `svc_sdpp` al grupo de Administradores — pertenecer a `Docker-Users` es suficiente para
ejecutar `docker`/`docker compose`.

## Firewall de Windows

Solo Apache expone puertos a la intranet; todo lo demás queda en `127.0.0.1`/red interna de Docker.

```powershell
New-NetFirewallRule -DisplayName "SDPP HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
New-NetFirewallRule -DisplayName "SDPP HTTP (redirect)" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow

# Explícitamente NO se abren reglas entrantes para 1433 (SQL Server), 5672/15672 (RabbitMQ),
# 9010/9011 (MinIO), 3310 (ClamAV), 5341 (Seq), 5080 (Gateway) ni 5090 (web/nginx) — esos servicios
# escuchan en 127.0.0.1 (ver docker-compose.prod.yml) y nunca deben ser alcanzables desde fuera del
# propio servidor.
```

## Primer despliegue

```powershell
cd C:\SDPP\app
Copy-Item deploy\compose\production\.env.example deploy\compose\production\.env
notepad deploy\compose\production\.env   # completar todos los valores

docker compose -f deploy\compose\docker-compose.yml -f deploy\compose\production\docker-compose.prod.yml --env-file deploy\compose\production\.env up -d --build
```

Luego instalar/configurar Apache con `deploy/apache/sdpp.conf` (ajustar las rutas de certificado y
logs a la estructura de carpetas de arriba) y verificar:

```powershell
Invoke-WebRequest https://sdpp.intranet/health   # vía Gateway, a través de Apache
```

## Ver también

- [ci-cd.md](ci-cd.md) — cómo llega el código nuevo a este servidor.
- [backup-recovery-plan.md](backup-recovery-plan.md) — estrategia de respaldo, ahora con
  automatización real en `scripts/backup/`.
- [apache-config.md](apache-config.md) — detalle de la configuración de Apache.
