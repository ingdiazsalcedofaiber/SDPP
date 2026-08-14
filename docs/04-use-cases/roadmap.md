# Roadmap

Roadmap por incrementos entregables (no por fechas fijas, dado que dependen de disponibilidad de
infraestructura on-prem corporativa: AD, Kubernetes, SQL Server AG). Cada fase es *shippable* y
deja el sistema en estado de producción segura (no hay una fase insegura "temporal").

## Fase 0 — Fundación (Sprint 0–2)
- Repositorio, `SDPP.sln`, `Directory.Build.props`, Building Blocks compartidos.
- Autenticación OIDC + AD/LDAP funcionando end-to-end (login real).
- RBAC básico (5 roles) + esquema de base de datos inicial + migraciones.
- Pipeline CI/CD con gates de seguridad (SAST, SCA, secretos, escaneo de imágenes) desde el día 1.
- `docker-compose` de desarrollo local (SQL Server, RabbitMQ, Redis, MinIO).
- Arquitectura de auditoría (hash-chain) y outbox pattern funcionando con un evento trivial.

**Salida de fase**: esqueleto desplegable en Kubernetes de desarrollo, sin funcionalidad de
negocio, pero con seguridad transversal ya operando.

## Fase 1 — MVP de conversión gobernada (Sprint 3–8)
- UC-01 completo: Word/Excel/PPT/Imagen ↔ PDF.
- Formulario obligatorio (US-201) con validación en las 3 capas.
- Motor de clasificación automática v1: regex + diccionarios + detección PII básica (US-301,
  US-302).
- Trazabilidad y auditoría inmutable completas (US-401, US-402, US-404).
- Etiquetado automático (pie de página + metadatos PDF) (US-402).
- Alertas por correo para clasificación ≥ Confidencial (subconjunto de US-501).
- Escaneo antimalware previo (US-503).
- Dashboard mínimo (contadores básicos).

**Salida de fase**: reemplazo funcional de "convertir Word/PDF" con gobierno de la información,
listo para un piloto con un área controlada (p. ej. Legal o RRHH).

## Fase 2 — Operaciones PDF y aprobaciones (Sprint 9–14)
- Unir, dividir, comprimir, OCR, marca de agua, numeración, rotar, eliminar/reordenar páginas,
  proteger/desbloquear (US-103 a US-108).
- Flujo de aprobación de Supervisor (US-203, UC-04).
- Motor de políticas configurable completo (bloqueo por combinación clasificación/operación/área).
- Alertas multicanal completas: Teams, Slack, SIEM/Syslog (US-501 completo).
- Detección de anomalías v1 (umbrales simples) (US-502).
- Conversión masiva / por lotes (US-110).

**Salida de fase**: paridad funcional con "iLovePDF interno" + gobernanza diferenciadora.

## Fase 3 — API pública interna, escalado y dashboard ejecutivo (Sprint 15–20)
- API REST documentada con OpenAPI + autenticación `client_credentials` (US-801).
- Autoscaling en Kubernetes validado con pruebas de carga a 5,000 usuarios concurrentes (US-802).
- Dashboard ejecutivo completo (US-701, ver [dashboard-spec.md](dashboard-spec.md)).
- Integración opcional con motor DLP corporativo externo (US-304).
- Exportación de evidencia para auditoría externa (US-403 avanzado).

**Salida de fase**: plataforma lista para adopción corporativa amplia (todas las áreas).

## Fase 4 — Extensiones enterprise (backlog "Could/Won't" reevaluado)
- Firma digital con HSM/PKI corporativa y sello de tiempo (US-109).
- Retención y purga automática con flujos de revisión (extensión de US-401/RetentionPeriod).
- Integración con Microsoft Purview Information Protection (etiquetado nativo de M365) si la
  organización ya lo usa.
- Motor de detección de anomalías con modelo estadístico/ML (evolución de US-502).
- Multi-tenant si la plataforma se ofrece a varias entidades del grupo corporativo.

## Hitos de cumplimiento transversales (aplican desde Fase 0, no son "fase aparte")
- STRIDE y ASVS: revisión antes de cada fase de subir a producción (gate de seguridad).
- ISO 27001/27701, NIST CSF 2.0, CIS Controls: control mapeado a la fase donde se implementa (ver
  [iso-nist-cis-mapping.md](../05-security/iso-nist-cis-mapping.md)); no se "certifica al final",
  se construye con el control ya activo.
