# Stack tecnológico y decisiones (ADR resumido)

## 1. Resumen por capa

| Capa | Tecnología | Justificación |
|---|---|---|
| Frontend | React 18, TypeScript, MUI v6, React Query, Zustand | SPA reactiva, tipado fuerte, componentes accesibles (MUI cumple WCAG 2.1 AA) |
| API / Backend | ASP.NET Core 9 (Minimal APIs + Controllers donde aplique), C# 13 | Rendimiento, soporte LTS-like, ecosistema maduro para enterprise |
| Patrón de aplicación | Clean Architecture, DDD táctico, CQRS + MediatR | Aislar el dominio de infraestructura; testabilidad; separación lectura/escritura |
| ORM | Entity Framework Core 9 (escritura) + Dapper (lecturas de alto rendimiento/dashboard) | EF Core para consistencia transaccional del dominio; Dapper para proyecciones CQRS de solo lectura |
| Base de datos | SQL Server 2022 Enterprise, Always On Availability Groups | Estándar corporativo, soporta Always Encrypted, TDE, particionado, row-level security |
| Caché | Redis Cluster (o Azure Cache for Redis en modo on-prem/Redis Enterprise) | Sesión, rate limiting, caché de queries, locks distribuidos (RedLock) |
| Mensajería | RabbitMQ (cluster con mirrored/quorum queues) | Colas de trabajo para conversión, outbox de eventos de dominio, DLQ |
| Trabajos en background | Hangfire (SQL Server storage) | Reintentos, jobs recurrentes (limpieza de temporales, escaneo de retención), dashboard operativo |
| Autenticación | Active Directory (LDAP/LDAPS + Kerberos), OpenIddict o Keycloak como broker OIDC/OAuth2 | SSO corporativo, tokens de corta duración, soporte MFA vía AD FS/Entra si aplica |
| Autorización | RBAC (roles) + ABAC (políticas por clasificación/área) vía `Microsoft.AspNetCore.Authorization` con requisitos custom | Roles gruesos + reglas finas contextuales |
| Motores de conversión | LibreOffice (headless, `soffice --headless --convert-to`), Ghostscript, Poppler (`pdftoppm`, `pdftotext`), Apache PDFBox (operaciones PDF vía servicio Java embebido o wrapper), Tesseract OCR | 100% on-prem, sin llamadas cloud, licencias libres |
| Antimalware | ClamAV (daemon `clamd` vía socket/ICAP) o integración EDR corporativo | Escaneo previo obligatorio a cualquier procesamiento |
| Contenedores | Docker (build multi-stage), imágenes base *distroless*/`mcr.microsoft.com/dotnet/aspnet` hardened | Reproducibilidad, superficie de ataque mínima |
| Orquestación | Kubernetes (on-prem: RKE2 / OpenShift / VMware Tanzu) | Escalado horizontal, `NetworkPolicy`, `PodSecurity`, autoscaling |
| Logging | Serilog (sinks: Console/JSON, Elasticsearch o Seq) | Structured logging correlacionado por `TraceId` |
| Métricas | Prometheus + Grafana | Estándar de facto en Kubernetes, alerting integrado |
| Trazas distribuidas | OpenTelemetry (OTLP → Grafana Tempo / Jaeger interno) | Correlación entre BFF, APIs, workers y colas |
| Almacenamiento de objetos | MinIO on-prem (S3-compatible) o NAS con cifrado a nivel de volumen | Archivos originales/convertidos cifrados en reposo, versionado, retención |
| CI/CD | GitLab CI / Azure DevOps Server (on-prem) + Harbor como registry interno | Pipelines que no dependen de servicios cloud públicos |
| Escaneo de seguridad en pipeline | `dotnet list package --vulnerable`, Trivy (imágenes), SonarQube (SAST), Gitleaks (secretos), OWASP Dependency-Check | DevSecOps shift-left |

## 2. Por qué CQRS + MediatR + Clean Architecture (y no Vertical Slice puro)

Se adopta un híbrido: **Clean Architecture como frontera de capas** (Domain → Application →
Infrastructure → Presentation) **y Vertical Slice dentro de Application** (cada caso de uso —
comando o query — es una carpeta autocontenida con su Command/Query, Handler, Validator y
mapeos). Esto da:

- Independencia de infraestructura en el dominio (testable sin DB/colas).
- Bajo acoplamiento entre casos de uso (agregar "PDF → PPTX" no toca los demás).
- CQRS permite escalar independientemente la ruta de lectura (dashboard, listados) de la de
  escritura (conversión), incluyendo réplicas de solo lectura para el dashboard ejecutivo.

## 3. Por qué no usar servicios cloud de conversión/OCR

Requisito no negociable del proyecto: la información no puede salir de la red corporativa. Se
descartan explícitamente: Azure Cognitive Services, AWS Textract, Google Document AI, APIs
públicas de conversión. Todo el pipeline de conversión corre en contenedores propios,
desplegados en el mismo clúster, sin egress a Internet (ver
[NetworkPolicy en kubernetes-architecture.md](../07-operations/kubernetes-architecture.md)).

## 4. Motores de conversión — matriz de uso

| Conversión | Motor primario | Motor de respaldo/alternativo |
|---|---|---|
| Word/Excel/PowerPoint → PDF | LibreOffice headless | — |
| Imagen → PDF | LibreOffice / ImageMagick (empaquetado) | Ghostscript |
| PDF → Word/Excel/PowerPoint | LibreOffice headless (`--convert-to docx:"MS Word 2007 XML"`) | — |
| PDF → Imagen | Poppler (`pdftoppm`) | Ghostscript (`gs -sDEVICE=png16m`) |
| Unir / dividir PDF | PDFBox (servicio Java interno vía gRPC) | Ghostscript |
| Comprimir PDF | Ghostscript (`-dPDFSETTINGS=/ebook`) | — |
| OCR | Tesseract (+ `ocrmypdf` como orquestador) | — |
| Marca de agua, numeración, rotación, reordenar, eliminar páginas | PDFBox | iText (solo si licencia AGPL/comercial es viable; por defecto PDFBox) |
| Protección / desbloqueo con contraseña | PDFBox (AES-256 handler) | qpdf |
| Firma digital | PDFBox + PKCS#11 (HSM corporativo) o certificado X.509 corporativo | — |

Cada motor se ejecuta **en un contenedor worker aislado**, sin privilegios, con `seccomp`,
sistema de archivos de solo lectura salvo `/tmp` efímero (`emptyDir` cifrado en memoria),
y timeout/kill duro por job para mitigar *zip bombs* / *billion laughs* / archivos maliciosos
que intenten agotar recursos (ver [threat-model-stride.md](../05-security/threat-model-stride.md)).

## 5. Alternativas consideradas y descartadas

| Alternativa | Por qué se descarta |
|---|---|
| Vertical Slice Architecture puro (sin capas) | Dificulta gobernar reglas transversales de seguridad (clasificación obligatoria, auditoría) que deben aplicar uniformemente; se prefiere el híbrido. |
| NoSQL (MongoDB) como almacén primario | El dominio es fuertemente relacional y transaccional (RBAC, auditoría con integridad referencial); SQL Server ya es estándar corporativo. |
| Kafka en vez de RabbitMQ | RabbitMQ es suficiente para el volumen esperado (miles, no millones de eventos/seg) y es más simple de operar on-prem; Kafka queda como opción de escalado futuro si el volumen de eventos lo justifica. |
| Microservicios por conversión (uno por tipo de archivo) | Sobre-fragmentación operativa; se opta por un *worker pool* genérico parametrizado por tipo de trabajo, escalable horizontalmente. |
