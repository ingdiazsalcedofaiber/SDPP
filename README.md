# SDPP — Secure Document Processing Platform

Plataforma corporativa de conversión, clasificación y gobierno documental para uso exclusivo
dentro de la intranet, sin dependencias de servicios cloud externos para el procesamiento de
contenido.

**Empieza por**: [docs/00-overview.md](docs/00-overview.md) — visión, objetivos, principios Zero
Trust y el índice completo del paquete de arquitectura.

## Estado actual

Este repositorio contiene, por ahora, el **paquete de arquitectura y diseño** (Fase 0 de
planificación, ver [roadmap](docs/04-use-cases/roadmap.md)): C4, modelo de dominio DDD, modelo de
datos, casos de uso/backlog/roadmap, diseño de los motores de clasificación y DLP, trazabilidad y
auditoría, RBAC, modelo de amenazas STRIDE, mapeo de cumplimiento (OWASP ASVS, ISO 27001/27701,
NIST CSF 2.0, CIS Controls), contrato OpenAPI inicial, y arquitectura de despliegue
(Docker/Kubernetes/CI-CD) y de respaldo/recuperación.

El código fuente (solución .NET, frontend React, Helm charts) se construye a partir de la
[estructura de solución](docs/01-architecture/solution-structure.md) ya definida, en los
siguientes incrementos del [roadmap](docs/04-use-cases/roadmap.md).

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
| [docs/07-operations/](docs/07-operations/) | Kubernetes, CI/CD, respaldo y recuperación |
