# SDPP — Secure Document Processing Platform

Plataforma corporativa de conversión, clasificación y gobierno documental para uso exclusivo
dentro de la intranet, sin dependencias de servicios cloud externos para el procesamiento de
contenido.

**Empieza por**: [docs/00-overview.md](docs/00-overview.md) — visión, objetivos, principios Zero
Trust y el índice completo del paquete de arquitectura.

## Estado actual

SDPP tiene una implementación funcional, no solo el paquete de diseño original: backend .NET 9
(Clean Architecture + CQRS/MediatR) con 5 módulos — Identity, Documents, Classification, Audit y
Signature (firma electrónica nativa, no una integración con un proveedor externo) — detrás de un
Gateway YARP, más un frontend React 19. Cada módulo tiene su propia base SQL Server (EF Core
Code-First), mensajería vía RabbitMQ/MassTransit con patrón Outbox, jobs recurrentes con Hangfire,
almacenamiento de documentos en MinIO con escaneo antivirus (ClamAV) previo, y una bitácora de
auditoría con cadena de hashes inmutable (bloqueada también a nivel de SQL Server).

El stack corre localmente vía Docker Compose ([`deploy/compose/`](deploy/compose/)) y está
preparado para producción sobre Windows Server + Apache como punto de entrada de la intranet — ver
[docs/07-operations/windows-server-deploy.md](docs/07-operations/windows-server-deploy.md) y
[docs/07-operations/apache-config.md](docs/07-operations/apache-config.md) — con CI/CD vía GitHub
Actions ([docs/07-operations/ci-cd.md](docs/07-operations/ci-cd.md)) y respaldo/restauración
automatizados ([`scripts/backup/`](scripts/backup/)).

Los documentos de diseño original (C4, modelo de dominio DDD, modelo de datos, backlog/roadmap,
STRIDE, mapeo de cumplimiento) siguen en `docs/` como referencia de arquitectura — algunos detalles
puntuales (p. ej. Kubernetes como target de despliegue) fueron reemplazados en la práctica por la
topología Docker Compose + Windows Server + Apache descrita arriba.

## Índice de documentación

| Documento | Contenido |
|---|---|
| [docs/00-overview.md](docs/00-overview.md) | Visión, objetivos, alcance, principios Zero Trust |
| [docs/01-architecture/](docs/01-architecture/) | Diagramas C4, stack tecnológico, estructura de solución |
| [docs/02-domain/domain-model.md](docs/02-domain/domain-model.md) | Modelo de dominio DDD (bounded contexts, agregados) |
| [docs/03-data/er-model.md](docs/03-data/er-model.md) | Modelo entidad-relación y diccionario de datos |
| [docs/04-use-cases/](docs/04-use-cases/) | Casos de uso, backlog, roadmap, dashboard |
| [docs/05-security/](docs/05-security/) | Clasificación, DLP, auditoría, RBAC, STRIDE, cumplimiento normativo |
| [docs/06-api/](docs/06-api/) | Diseño de API REST y contrato OpenAPI |
| [docs/07-operations/](docs/07-operations/) | Windows Server, Apache, CI/CD, respaldo y recuperación |
| [docs/07-operations/windows-server-deploy.md](docs/07-operations/windows-server-deploy.md) | Despliegue real sobre Windows Server + Docker Engine |
| [docs/07-operations/apache-config.md](docs/07-operations/apache-config.md) | Apache como punto de entrada de la intranet |
| [docs/07-operations/ci-cd.md](docs/07-operations/ci-cd.md) | Pipeline de GitHub Actions (CI + deploy con rollback) |
