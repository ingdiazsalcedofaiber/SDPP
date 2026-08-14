# Diagramas C4

## Nivel 1 — Contexto del sistema

```mermaid
C4Context
title SDPP - Diagrama de Contexto

Person(employee, "Empleado", "Usuario corporativo que convierte/gestiona documentos")
Person(supervisor, "Supervisor", "Aprueba conversiones restringidas")
Person(auditor, "Auditor / Oficial de Seguridad", "Revisa trazabilidad y alertas")
Person(admin, "Administrador SDPP", "Configura políticas, reglas DLP, roles")

System(sdpp, "SDPP", "Secure Document Processing Platform. Convierte, clasifica y audita documentos dentro de la intranet.")

System_Ext(ad, "Active Directory / LDAP", "Identidad y grupos corporativos")
System_Ext(oidc, "Proveedor OIDC interno", "Keycloak / AD FS - broker de autenticación")
System_Ext(mail, "Servidor SMTP corporativo", "Notificaciones por correo")
System_Ext(teams, "Microsoft Teams (conector interno)", "Notificaciones a canales")
System_Ext(slack, "Slack (Enterprise Grid on-prem gateway)", "Notificaciones a canales")
System_Ext(siem, "SIEM corporativo", "Splunk / Sentinel / QRadar / Elastic - vía Syslog/CEF")
System_Ext(dlp, "Motor DLP corporativo (opcional)", "Microsoft Purview / Forcepoint / Symantec DLP")
System_Ext(erp, "Sistemas corporativos (ERP, Gestor Documental)", "Consumidores de la API REST de SDPP")
System_Ext(av, "Motor Antimalware", "ClamAV / EDR corporativo - escaneo previo")

Rel(employee, sdpp, "Sube, convierte y descarga documentos", "HTTPS")
Rel(supervisor, sdpp, "Aprueba/rechaza solicitudes", "HTTPS")
Rel(auditor, sdpp, "Consulta trazabilidad y alertas", "HTTPS")
Rel(admin, sdpp, "Configura políticas, roles, reglas", "HTTPS")

Rel(sdpp, ad, "Autentica usuarios y resuelve grupos", "LDAPS")
Rel(sdpp, oidc, "Delega login (OAuth2/OIDC)", "HTTPS")
Rel(sdpp, mail, "Envía alertas", "SMTP/TLS")
Rel(sdpp, teams, "Envía notificaciones", "Webhook interno HTTPS")
Rel(sdpp, slack, "Envía notificaciones", "Webhook interno HTTPS")
Rel(sdpp, siem, "Envía eventos de seguridad", "Syslog TLS / CEF")
Rel(sdpp, dlp, "Consulta clasificación adicional (opcional)", "API interna HTTPS")
Rel(sdpp, av, "Escanea archivos antes de procesar", "ICAP / API interna")
Rel(erp, sdpp, "Convierte y clasifica documentos vía API", "HTTPS REST")

UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

Todos los sistemas externos son **internos a la red corporativa** o alcanzables solo vía relay
interno; no existe ninguna dependencia de servicio SaaS público para el procesamiento de
contenido.

## Nivel 2 — Contenedores

```mermaid
C4Container
title SDPP - Diagrama de Contenedores

Person(user, "Usuario / Sistema externo")

System_Boundary(sdpp, "SDPP") {
    Container(spa, "SPA Web", "React 18 + TypeScript + MUI", "UI de conversión, dashboard, administración")
    Container(bff, "API Gateway / BFF", "ASP.NET Core 9, YARP", "Enrutamiento, agregación, rate limiting, terminación TLS")
    Container(identity, "Identity Service", "ASP.NET Core 9, OpenIddict", "Emisión/validación de tokens OIDC, integración AD/LDAP")
    Container(docapi, "Document API", "ASP.NET Core 9, CQRS/MediatR", "Casos de uso de subida, conversión, gestión de documentos")
    Container(classapi, "Classification & DLP API", "ASP.NET Core 9, CQRS/MediatR", "Inspección, clasificación, reglas DLP, políticas")
    Container(auditapi, "Audit & Traceability API", "ASP.NET Core 9", "Registro inmutable, consulta de trazabilidad, alertas")
    Container(adminapi, "Admin & RBAC API", "ASP.NET Core 9", "Gestión de roles, usuarios, políticas, configuración")
    Container(notifapi, "Notification Service", "ASP.NET Core 9, Hangfire", "Envío de correo, Teams, Slack, Syslog/SIEM")
    Container(worker, "Conversion Worker Pool", "Worker Service .NET, Hangfire", "Ejecuta LibreOffice/Ghostscript/Poppler/Tesseract en sandbox")
    Container(gateway_mq, "Message Broker", "RabbitMQ", "Colas de jobs y eventos de dominio (outbox)")
    ContainerDb(sql, "SQL Server", "SQL Server 2022 AG", "Documentos (metadata), trazabilidad, auditoría, RBAC, políticas")
    ContainerDb(redis, "Redis", "Redis Cluster", "Caché, sesiones, rate limiting, locks distribuidos")
    ContainerDb(blob, "Almacenamiento de objetos", "MinIO on-prem (S3 compatible) / NAS cifrado", "Archivos originales y convertidos, cifrados AES-256")
    Container(dashboard, "Reporting/Dashboard API", "ASP.NET Core 9, proyecciones CQRS", "Indicadores ejecutivos")
}

System_Ext(ad, "Active Directory/LDAP")
System_Ext(siem, "SIEM")
System_Ext(av, "Antimalware")
System_Ext(mon, "Prometheus / Grafana")
System_Ext(log, "Serilog Sink (Elasticsearch/Seq)")

Rel(user, spa, "HTTPS")
Rel(spa, bff, "HTTPS/JSON, WebSocket (progreso)")
Rel(bff, identity, "Valida token", "HTTPS")
Rel(bff, docapi, "HTTPS/JSON")
Rel(bff, classapi, "HTTPS/JSON")
Rel(bff, auditapi, "HTTPS/JSON")
Rel(bff, adminapi, "HTTPS/JSON")
Rel(bff, dashboard, "HTTPS/JSON")

Rel(identity, ad, "LDAPS / Kerberos")

Rel(docapi, classapi, "Solicita inspección previa (sync)", "gRPC/HTTP interno")
Rel(docapi, gateway_mq, "Publica DocumentUploaded / ConversionRequested")
Rel(gateway_mq, worker, "Consume ConversionRequested")
Rel(worker, classapi, "Reporta resultado inspección post-conversión")
Rel(worker, av, "Escanea archivo", "ICAP")
Rel(worker, blob, "Lee/escribe archivos")
Rel(worker, gateway_mq, "Publica ConversionCompleted/Failed")

Rel(classapi, gateway_mq, "Publica SensitiveDocumentDetected")
Rel(gateway_mq, auditapi, "Consume todos los eventos de dominio (append-only)")
Rel(gateway_mq, notifapi, "Consume SensitiveDocumentDetected, ApprovalRequired")
Rel(notifapi, siem, "Syslog/CEF")

Rel(docapi, sql, "EF Core")
Rel(classapi, sql, "EF Core")
Rel(auditapi, sql, "EF Core, append-only")
Rel(adminapi, sql, "EF Core")
Rel(dashboard, sql, "Dapper, réplica de lectura")
Rel(docapi, redis, "Caché / locks")
Rel(bff, redis, "Rate limiting / sesión")
Rel(docapi, blob, "Lee/escribe archivos")

Rel(docapi, log, "Serilog")
Rel(classapi, log, "Serilog")
Rel(worker, log, "Serilog")
Rel(docapi, mon, "métricas /metrics")
Rel(worker, mon, "métricas /metrics")
```

## Nivel 3 — Componentes (Document API)

```mermaid
C4Component
title SDPP - Componentes de Document API

Container_Boundary(docapi, "Document API") {
    Component(controllers, "Endpoints Minimal API", "ASP.NET Core", "Upload, Convert, Merge, Split, Compress, Watermark, Sign, etc.")
    Component(mediatr, "MediatR Pipeline", "CQRS", "Commands/Queries + Pipeline Behaviors")
    Component(behaviors, "Pipeline Behaviors", ".NET", "Validation (FluentValidation), Logging, Transaction, Authorization, Idempotency")
    Component(commandhandlers, "Command Handlers", "MediatR", "UploadDocument, RequestConversion, MergePdf, ProtectPdf, SignPdf...")
    Component(queryhandlers, "Query Handlers", "MediatR + Dapper", "GetDocumentStatus, ListDocuments, GetConversionHistory")
    Component(domain, "Document Domain Model", "DDD", "Aggregate Document, ValueObjects (Classification, FileHash), Domain Events")
    Component(repo, "Repositories / Unit of Work", "EF Core", "DocumentRepository, JobRepository")
    Component(outbox, "Outbox Publisher", "EF Core + Hangfire", "Garantiza publicación exactly-once de eventos de dominio")
    Component(storageport, "IBlobStorage (puerto)", "Interfaz", "Abstracción de almacenamiento de archivos")
    Component(classclient, "IClassificationClient (puerto)", "Interfaz", "Cliente hacia Classification & DLP API")
    Component(mandatoryform, "Mandatory Form Validator", "Dominio", "Valida formulario obligatorio antes de habilitar conversión")
}

ContainerDb(sql, "SQL Server")
Container(mq, "RabbitMQ")
ContainerDb(blob, "Almacenamiento de objetos")
Container(classapi, "Classification & DLP API")

Rel(controllers, mediatr, "Send/Publish")
Rel(mediatr, behaviors, "pipeline")
Rel(behaviors, commandhandlers, "")
Rel(behaviors, queryhandlers, "")
Rel(commandhandlers, domain, "invoca reglas de negocio")
Rel(commandhandlers, mandatoryform, "valida antes de convertir")
Rel(commandhandlers, classclient, "solicita inspección")
Rel(classclient, classapi, "gRPC/HTTP")
Rel(commandhandlers, repo, "persiste")
Rel(repo, sql, "EF Core")
Rel(repo, outbox, "escribe evento en misma transacción")
Rel(outbox, mq, "publica asíncrono")
Rel(commandhandlers, storageport, "lee/escribe archivo")
Rel(storageport, blob, "implementación MinIO/NAS")
Rel(queryhandlers, sql, "Dapper read")
```

## Nivel 4 — Nota sobre diagramas de despliegue

El diagrama de despliegue en Kubernetes (namespaces, NetworkPolicies, nodepools) se documenta en
[07-operations/kubernetes-architecture.md](../07-operations/kubernetes-architecture.md) para
mantener este documento centrado en la arquitectura lógica.
