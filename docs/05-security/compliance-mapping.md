# Cumplimiento: OWASP ASVS, ISO 27001, ISO 27701, NIST CSF 2.0, CIS Controls

> Este documento mapea **controles de diseño de SDPP** a los marcos solicitados. No es una
> certificación (eso requiere auditoría externa formal), es la evidencia de que la arquitectura
> fue diseñada considerando estos marcos desde el inicio ("compliance by design").

## 1. OWASP ASVS (v4.0.3) — nivel objetivo

**Nivel 2 (estándar)** para toda la plataforma; **Nivel 3** para los endpoints que procesan
clasificación Restringida/Secreta y para el módulo de Identity/Auth.

| Capítulo ASVS | Requisito clave | Cómo lo cumple SDPP |
|---|---|---|
| V1 Architecture | Documentación de arquitectura, modelado de amenazas | Este paquete de documentación + [threat-model-stride.md](threat-model-stride.md) |
| V2 Authentication | MFA, gestión segura de credenciales | Delegado 100% a AD/broker OIDC (V2.1, V2.2); SDPP no almacena contraseñas |
| V3 Session Management | Tokens de corta duración, invalidación | Tokens OIDC 15 min + refresh rotativo, revocación en `Redis` blocklist |
| V4 Access Control | RBAC, denegar por defecto, anti-IDOR | [rbac-matrix.md](rbac-matrix.md); pruebas automatizadas de autorización en CI |
| V5 Validation, Sanitization | Validar todo input, encoding de salida | FluentValidation en cada Command, magic-byte validation de archivos, `CHECK` constraints en BD |
| V7 Error Handling and Logging | No exponer stack traces; logs sin datos sensibles | `ProblemDetails` genérico + Serilog con `@Sensitive` destructuring policy |
| V8 Data Protection | Cifrado en tránsito/reposo, clasificación de datos | TLS 1.2+, AES-256, Always Encrypted, motor de clasificación nativo |
| V9 Communications | TLS interno, mTLS entre servicios | Service mesh mTLS (ver [c4-diagrams.md](../01-architecture/c4-diagrams.md)) |
| V10 Malicious Code | Verificación de integridad, escaneo de dependencias | SCA en pipeline (Dependency-Check/Trivy), firma de imágenes |
| V11 Business Logic | Prevenir bypass de flujos (p. ej. saltar el formulario obligatorio) | Invariantes de dominio + `CHECK` constraint en BD (defensa en 3 capas, ver [use-cases.md](../04-use-cases/use-cases.md#2-formulario-obligatorio)) |
| V12 File and Resources | Validación de tipo/tamaño, prevención de path traversal, límites de descompresión | `StoragePath` opaco (VO), sin rutas de FS expuestas; límites por `NetworkPolicy`/`resources` (ver STRIDE D1/D3) |
| V13 API and Web Service | Autenticación en cada endpoint, rate limiting, versión de API | Gateway con rate limiting Redis, OpenAPI versionado ([06-api](../06-api/)) |
| V14 Configuration | Hardening, secretos fuera del código, cabeceras de seguridad | Vault/Kubernetes Secrets, `Directory.Build.props` con análisis estático obligatorio, CSP/HSTS en Gateway |

## 2. ISO/IEC 27001:2022 — Anexo A (controles seleccionados relevantes al diseño)

| Control | Descripción | Implementación en SDPP |
|---|---|---|
| A.5.9 | Inventario de activos de información | Catálogo de documentos con clasificación como metadato de primera clase (`Document.Classification`) |
| A.5.12 | Clasificación de la información | Motor de clasificación de 6 niveles, obligatorio (core del sistema) |
| A.5.13 | Etiquetado de la información | [audit-and-traceability.md §4](audit-and-traceability.md#4-etiquetado-automático) |
| A.5.15 | Control de acceso | RBAC + ABAC, ver [rbac-matrix.md](rbac-matrix.md) |
| A.5.18 | Derechos de acceso | Revisión trimestral de accesos privilegiados |
| A.5.23 | Seguridad en servicios cloud | N/A por diseño — sin dependencias cloud externas (requisito del proyecto) |
| A.5.28 | Recolección de evidencia | Exportación firmada de auditoría (UC-05) |
| A.5.34 | Privacidad y protección de PII | Detección de PII vía DLP, minimización de datos en logs |
| A.8.2 | Derechos de acceso privilegiado | Separación Administrador/Auditor, sin auto-escalamiento |
| A.8.9 | Gestión de configuración | Políticas/reglas versionadas y auditadas |
| A.8.10 | Eliminación de información | `RetentionPeriod` + purga automática programada |
| A.8.12 | Prevención de fuga de datos | Motor DLP completo (core del sistema) |
| A.8.15 | Registro (logging) | Auditoría inmutable con hash-chain |
| A.8.16 | Actividades de monitoreo | Prometheus/Grafana + alertas + SIEM |
| A.8.24 | Uso de criptografía | TLS, AES-256, Always Encrypted, firma digital |
| A.8.25 | Ciclo de vida de desarrollo seguro | DevSecOps: SAST/SCA/secret-scanning en CI/CD |
| A.8.26 | Requisitos de seguridad en aplicaciones | ASVS como checklist de aceptación por fase |
| A.8.28 | Codificación segura | Guías internas + `SDPP.Architecture.Tests` (reglas de capas) |
| A.8.29 | Pruebas de seguridad en desarrollo y aceptación | Gates de pipeline + revisión de amenazas por fase |

## 3. ISO/IEC 27701:2019 (extensión de privacidad sobre 27001) — controles PIMS relevantes

| Área | Implementación |
|---|---|
| Identificación de bases legales para tratamiento de PII detectada | El formulario obligatorio captura "Motivo"/"Justificación", que sirve como registro de finalidad del tratamiento cuando el documento contiene PII |
| Minimización de datos | El motor DLP permite, a futuro, ofuscar/redactar PII no esencial antes de exportar (extensión candidata en roadmap Fase 4) |
| Derechos del titular (acceso, rectificación, supresión) | Trazabilidad permite reconstruir qué documentos con PII de una persona fueron procesados, soportando solicitudes de derechos ARCO/GDPR-like |
| Notificación de incidentes de privacidad | Alertas automáticas ante hallazgos `Critical` de categoría PII/Médico alimentan el proceso de respuesta a incidentes de privacidad |
| Registro de actividades de tratamiento (RAT) | `AUDIT_RECORDS` + `Finding.Category = PII/Medical` constituyen la fuente de datos para construir el RAT exigido por 27701/GDPR |

## 4. NIST CSF 2.0 — funciones y categorías

| Función | Categoría relevante | Implementación |
|---|---|---|
| **GOVERN (GV)** | GV.PO — Políticas | `ClassificationPolicy`/`DlpRule` versionadas como política ejecutable, no solo documento |
| **IDENTIFY (ID)** | ID.AM — Gestión de activos | Inventario de documentos clasificados; ID.RA — Evaluación de riesgos: [threat-model-stride.md](threat-model-stride.md) |
| **PROTECT (PR)** | PR.AA — Gestión de identidad y acceso | RBAC/ABAC + AD/OIDC; PR.DS — Seguridad de datos: cifrado en tránsito/reposo; PR.PS — Seguridad de plataforma: hardening de contenedores |
| **DETECT (DE)** | DE.CM — Monitoreo continuo | Prometheus/Grafana, validación periódica de hash-chain; DE.AE — Análisis de anomalías: detección de volumen inusual (US-502) |
| **RESPOND (RS)** | RS.CO — Comunicación | Alertas multicanal automáticas (correo/Teams/Slack/SIEM); RS.AN — Análisis: exportación de evidencia para investigación |
| **RECOVER (RC)** | RC.RP — Plan de recuperación | [backup-recovery-plan.md](../07-operations/backup-recovery-plan.md) |

## 5. CIS Controls v8 (IG1/IG2 como línea base, IG3 en componentes críticos)

| Control CIS | Implementación |
|---|---|
| CIS 1 — Inventario de activos empresariales | Namespaces/servicios documentados en C4; CMDB vía Helm charts versionados |
| CIS 3 — Protección de datos | Clasificación + DLP + cifrado (core del sistema) |
| CIS 4 — Configuración segura de activos | Imágenes base hardened, `PodSecurity` restrictivo, `Directory.Build.props` |
| CIS 5 — Gestión de cuentas | Sin cuentas locales; todo vía AD |
| CIS 6 — Gestión de control de acceso | RBAC + revisión trimestral |
| CIS 8 — Gestión de logs de auditoría | Hash-chain inmutable + Serilog centralizado |
| CIS 9 — Protecciones de correo y navegador | N/A directo (SDPP no es cliente de correo), pero valida adjuntos antes de procesarlos (antimalware) |
| CIS 10 — Defensas contra malware | ClamAV/EDR en el pipeline de ingesta (obligatorio, no opcional) |
| CIS 11 — Recuperación de datos | Backups + pruebas de restauración periódicas |
| CIS 12 — Gestión de infraestructura de red | `NetworkPolicy` deny-by-default, segmentación de namespaces |
| CIS 13 — Monitoreo y defensa de red | Integración SIEM, Prometheus/Grafana |
| CIS 16 — Seguridad del software de aplicaciones | SAST/SCA en CI/CD, `SDPP.Architecture.Tests`, revisión de amenazas por fase |
| CIS 17 — Gestión de respuesta a incidentes | Alertas automáticas alimentan el proceso de IR corporativo existente |

## 6. Cómo se usa este mapeo en el proyecto
No es un documento de archivo: cada historia del [backlog](../04-use-cases/backlog.md) que
implemente un control de esta tabla debe referenciarlo en su descripción/PR, y el
[roadmap](../04-use-cases/roadmap.md) señala explícitamente que el cumplimiento se construye por
fase, no se audita solo al final.
