# SDPP — Secure Document Processing Platform

## 1. Visión

SDPP es la plataforma corporativa de conversión y gobierno documental que reemplaza el uso de
herramientas SaaS públicas (iLovePDF, SmallPDF, etc.) para el procesamiento de documentos que
puede contener información sensible. Todo el procesamiento — conversión, OCR, compresión, firma —
ocurre **dentro de la red corporativa**, sin llamadas salientes a servicios de terceros.

SDPP no es "un conversor de PDF": es una plataforma de **gobierno de la información** en la que la
conversión de formato es una operación secundaria que solo ocurre después de que el documento ha
sido clasificado, inspeccionado y justificado por el usuario, y que deja una traza inmutable y
auditable de cada acción.

## 2. Objetivos de negocio

1. Eliminar la fuga de información corporativa hacia SaaS públicos de conversión de documentos.
2. Dar trazabilidad completa (quién, qué, cuándo, por qué) sobre cada documento que se transforma.
3. Aplicar clasificación de la información de forma consistente (manual, automática o híbrida).
4. Detectar y prevenir la salida de datos sensibles (PII, financieros, médicos, secretos
   industriales, credenciales) mediante un motor DLP interno.
5. Servir a miles de usuarios concurrentes con un SLA empresarial (procesamiento < 30 s p95 para
   documentos < 20 MB).
6. Exponer una API REST interna para que otros sistemas (ERP, gestor documental, portal de
   trámites) integren conversión y clasificación como servicio.
7. Cumplir el marco normativo interno alineado a ISO 27001, ISO 27701, NIST CSF 2.0, CIS Controls
   y OWASP ASVS nivel 2 (nivel 3 en los endpoints que procesan clasificación "Secreta").

## 3. Alcance

**Dentro de alcance (v1–v3, ver [roadmap](04-use-cases/roadmap.md)):**
- Conversión de documentos ofimáticos y PDF en ambas direcciones.
- Operaciones sobre PDF (unir, dividir, comprimir, OCR, marca de agua, numeración, rotación,
  eliminar/reordenar páginas, proteger/desproteger, firma digital).
- Motor de clasificación e inspección automática con reglas configurables.
- Motor DLP con catálogo de detectores.
- Formulario obligatorio de justificación previo a cada conversión.
- Trazabilidad, auditoría inmutable, etiquetado automático de documentos procesados.
- Alertas multicanal (correo, Teams, Slack, SIEM/Syslog) para documentos sensibles.
- RBAC, dashboard ejecutivo, API REST pública interna.
- Despliegue on-premise en Kubernetes corporativo.

**Fuera de alcance (explícito):**
- Cualquier llamada saliente a Internet para procesar contenido de documentos (OCR, conversión,
  IA generativa en la nube). Los únicos flujos salientes permitidos son notificaciones
  (correo/Teams/Slack) y envío de eventos a SIEM, ambos dentro de la red corporativa o vía relay
  interno.
- Almacenamiento de documentos fuera del datacenter/nube privada corporativa.
- Firma electrónica avanzada con autoridad certificadora pública (se contempla como extensión
  futura con PKI corporativa, ver roadmap).

## 4. Principios rectores

### 4.1 Zero Trust
- **Nunca confiar, siempre verificar**: cada llamada a la API se autentica (OIDC/OAuth2 contra AD)
  y autoriza (RBAC + políticas ABAC) independientemente del origen de red.
- **Segmentación por diseño**: los workers de conversión (LibreOffice/Ghostscript/Tesseract)
  corren en una subred/namespace aislado sin salida a Internet (`NetworkPolicy` deny-all-egress
  salvo DNS interno y endpoints de notificación explícitos).
- **Mínimo privilegio**: cada microservicio tiene su propia identidad (Workload Identity /
  Service Account de Kubernetes), su propio secreto de base de datos y permisos SQL acotados a
  su esquema.
- **Verificación continua**: los tokens de acceso son de corta duración (15 min) con refresh
  rotativo; sesiones re-evaluadas contra AD en cada renovación (grupo deshabilitado ⇒ sesión
  revocada en el próximo refresh, máx. 15 min de exposición).
- **Cifrado en tránsito y en reposo por defecto**: mTLS entre microservicios (vía service mesh),
  TLS 1.2+ en todos los endpoints externos del clúster, AES-256 en reposo para blob storage y
  columnas sensibles de SQL Server (Always Encrypted en columnas de trazabilidad crítica).
- **Asumir brecha**: todo evento de negocio relevante genera un registro de auditoría inmutable;
  se opera bajo la premisa de que un componente puede ser comprometido, por lo que la
  segmentación limita el radio de impacto (blast radius).

### 4.2 Seguridad por diseño y por defecto
- Clasificación y DLP como **gate obligatorio**, no opcional, antes de cualquier conversión.
- "Fail closed": si el motor de clasificación o el motor DLP no responden, la conversión se
  bloquea (no se degrada a "permitir por defecto").
- Todo dato de auditoría es *append-only* (ver [auditoría inmutable](05-security/audit-and-traceability.md)).

### 4.3 Principios de arquitectura de software
- Clean Architecture + DDD por *bounded context* (ver [modelo de dominio](02-domain/domain-model.md)).
- CQRS con MediatR: separación de comandos (escritura, con validación de dominio) y queries
  (lectura, proyecciones optimizadas, pueden usar Dapper contra réplicas de solo lectura).
- Arquitectura orientada a eventos internos (outbox pattern + RabbitMQ) para desacoplar el motor
  de conversión, el motor de clasificación/DLP, la auditoría y las notificaciones.
- Diseño *API-first*: contrato OpenAPI como fuente de verdad, versionado semántico de la API.
- Todo servicio es *stateless* y *horizontally scalable*; el estado vive en SQL Server, Redis y el
  almacenamiento de blobs (file storage on-prem / NAS / MinIO on-prem S3-compatible).

## 5. Restricciones técnicas

| Restricción | Detalle |
|---|---|
| Sin dependencias cloud | Todo motor de conversión corre en contenedores propios dentro del clúster corporativo. |
| Identidad | Autenticación federada con Active Directory vía LDAP(S) y OIDC (AD FS / Entra ID en modo *hybrid on-prem* si aplica, o Keycloak como broker OIDC interno delante de AD). |
| Datos | SQL Server (Always On Availability Groups) como sistema de registro. Redis para caché/sesión/rate-limiting. RabbitMQ para colas de trabajo y eventos de dominio. |
| Cómputo | Kubernetes on-prem (o VMware Tanzu / OpenShift), con `NetworkPolicy` restrictivas por namespace. |
| Observabilidad | Serilog → Elasticsearch/Seq (structured logging), Prometheus + Grafana para métricas, OpenTelemetry para trazas distribuidas. |
| Concurrencia objetivo | 5,000 usuarios concurrentes, 50 conversiones/segundo sostenidas en horas pico, colas con burst hasta 500 documentos/segundo. |

## 6. Glosario rápido

| Término | Significado |
|---|---|
| **Clasificación** | Nivel de sensibilidad asignado a un documento: Pública, Uso Interno, Privada, Confidencial, Restringida, Secreta. |
| **Inspección** | Análisis automático (contenido, metadatos, nombre, regex, DLP) que produce una clasificación sugerida y hallazgos. |
| **Job de conversión** | Unidad de trabajo encolada que representa una transformación de uno o más documentos. |
| **Etiquetado** | Marca visible y/o en metadatos que se añade al documento de salida indicando clasificación, usuario, fecha y hash. |
| **Traza / Registro de procesamiento** | Registro inmutable de todos los atributos de un evento de conversión (ver [trazabilidad](05-security/audit-and-traceability.md)). |
| **Política** | Regla configurable que permite/bloquea/requiere aprobación para una combinación de clasificación, tipo de conversión, rol y área. |

## 7. Índice del paquete de arquitectura

1. [Arquitectura (C4, stack, estructura de solución)](01-architecture/)
2. [Modelo de dominio (DDD)](02-domain/domain-model.md)
3. [Modelo de datos (ER)](03-data/er-model.md)
4. [Casos de uso, backlog y roadmap](04-use-cases/)
5. [Seguridad: clasificación, DLP, auditoría, STRIDE, cumplimiento](05-security/)
6. [API REST y contrato OpenAPI](06-api/)
7. [Operación: despliegue, Kubernetes, CI/CD, backup/DR](07-operations/)
