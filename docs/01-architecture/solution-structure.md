# Estructura de solución

## 1. Backend (.NET) — organización por Bounded Context

Se usa **un solo repositorio (monorepo)** con una solución .NET que agrupa varios *bounded
contexts*, cada uno con su propia Clean Architecture interna, más un `Gateway` (BFF) y un
`Shared Kernel` mínimo. Cada bounded context es desplegable de forma independiente (un
`Dockerfile` por servicio) aunque viva en el mismo repo, para facilitar cambios atómicos y
revisión de código conjunta en esta fase del proyecto.

```
SDPP/
├── src/
│   ├── Gateway/
│   │   └── SDPP.Gateway/                      # YARP reverse proxy / BFF, rate limiting, TLS
│   │
│   ├── BuildingBlocks/                        # Shared Kernel (sin lógica de negocio)
│   │   ├── SDPP.BuildingBlocks.Domain/         # BaseEntity, AggregateRoot, IDomainEvent, ValueObject
│   │   ├── SDPP.BuildingBlocks.Application/    # IPipelineBehavior genéricos, IUnitOfWork, Result<T>
│   │   ├── SDPP.BuildingBlocks.Infrastructure/ # Outbox genérico, EF interceptors, Serilog config
│   │   └── SDPP.BuildingBlocks.Contracts/      # Integration Events compartidos (versión de contrato)
│   │
│   ├── Modules/
│   │   ├── Documents/                          # Bounded Context: Document Processing
│   │   │   ├── SDPP.Documents.Domain/
│   │   │   │   ├── Aggregates/                 # Document, ConversionJob
│   │   │   │   ├── ValueObjects/               # FileHash, FileSize, ClassificationLevel
│   │   │   │   ├── Events/                     # DocumentUploaded, ConversionRequested, ConversionCompleted
│   │   │   │   └── Rules/                      # Invariantes de negocio (specifications)
│   │   │   ├── SDPP.Documents.Application/
│   │   │   │   ├── UseCases/
│   │   │   │   │   ├── UploadDocument/         # Command + Handler + Validator + Mapper
│   │   │   │   │   ├── ConvertDocument/
│   │   │   │   │   ├── MergePdf/
│   │   │   │   │   ├── SplitPdf/
│   │   │   │   │   ├── CompressPdf/
│   │   │   │   │   ├── WatermarkPdf/
│   │   │   │   │   ├── ProtectPdf/
│   │   │   │   │   ├── UnlockPdf/
│   │   │   │   │   ├── SignPdf/
│   │   │   │   │   ├── ReorderPages/
│   │   │   │   │   ├── DeletePages/
│   │   │   │   │   ├── BulkConvert/
│   │   │   │   │   ├── GetDocumentStatus/
│   │   │   │   │   └── ListDocuments/
│   │   │   │   ├── Behaviors/                  # Validation, Logging, Authorization, MandatoryFormCheck
│   │   │   │   ├── Ports/                      # IBlobStorage, IClassificationClient, IVirusScanner, IConversionEngine
│   │   │   │   └── DependencyInjection.cs
│   │   │   ├── SDPP.Documents.Infrastructure/
│   │   │   │   ├── Persistence/                # DbContext, EF Configurations, Migrations, Repositories
│   │   │   │   ├── Storage/                    # MinIoBlobStorage, NasBlobStorage
│   │   │   │   ├── Engines/                    # LibreOfficeEngine, GhostscriptEngine, PdfBoxClient, TesseractEngine
│   │   │   │   ├── Messaging/                  # RabbitMqPublisher, OutboxProcessor
│   │   │   │   └── DependencyInjection.cs
│   │   │   └── SDPP.Documents.Api/             # Minimal API endpoints, Program.cs, appsettings
│   │   │
│   │   ├── Classification/                     # Bounded Context: Classification & DLP
│   │   │   ├── SDPP.Classification.Domain/     # ClassificationPolicy, DlpRule, InspectionResult
│   │   │   ├── SDPP.Classification.Application/# InspectDocument, EvaluatePolicy, ManageDlpRules
│   │   │   ├── SDPP.Classification.Infrastructure/ # Regex engine, keyword matcher, DLP connectors, ML hook
│   │   │   └── SDPP.Classification.Api/
│   │   │
│   │   ├── Audit/                              # Bounded Context: Audit & Traceability
│   │   │   ├── SDPP.Audit.Domain/              # AuditRecord (append-only), ProcessingTrace
│   │   │   ├── SDPP.Audit.Application/         # RecordEvent, QueryTrace, ExportEvidence
│   │   │   ├── SDPP.Audit.Infrastructure/      # Append-only store, hash chain, WORM policy
│   │   │   └── SDPP.Audit.Api/
│   │   │
│   │   ├── Identity/                           # Bounded Context: Identity & Access
│   │   │   ├── SDPP.Identity.Domain/           # User, Role, Permission, ApprovalRequest
│   │   │   ├── SDPP.Identity.Application/      # AuthenticateUser, ManageRoles, RequestApproval
│   │   │   ├── SDPP.Identity.Infrastructure/   # LDAP/AD connector, OpenIddict config
│   │   │   └── SDPP.Identity.Api/
│   │   │
│   │   ├── Notifications/                      # Bounded Context: Notifications
│   │   │   ├── SDPP.Notifications.Domain/
│   │   │   ├── SDPP.Notifications.Application/ # SendAlert (email/Teams/Slack/Syslog)
│   │   │   ├── SDPP.Notifications.Infrastructure/
│   │   │   └── SDPP.Notifications.Worker/      # Hangfire consumer
│   │   │
│   │   └── Reporting/                          # Bounded Context: Executive Dashboard (solo lectura)
│   │       ├── SDPP.Reporting.Application/     # Queries con Dapper sobre proyecciones
│   │       ├── SDPP.Reporting.Infrastructure/
│   │       └── SDPP.Reporting.Api/
│   │
│   └── Workers/
│       └── SDPP.Conversion.Worker/             # Worker Service: consume RabbitMQ, orquesta motores
│
├── tests/
│   ├── SDPP.Documents.UnitTests/
│   ├── SDPP.Documents.IntegrationTests/        # Testcontainers: SQL Server, RabbitMQ, MinIO
│   ├── SDPP.Classification.UnitTests/
│   ├── SDPP.Architecture.Tests/                # NetArchTest: valida reglas de Clean Architecture
│   └── SDPP.E2E.Tests/                         # Playwright contra ambiente docker-compose
│
├── frontend/
│   └── sdpp-web/                               # ver sección 2
│
├── deploy/
│   ├── docker/                                 # Dockerfiles por servicio
│   ├── compose/                                # docker-compose.yml (desarrollo local)
│   └── k8s/                                    # Helm charts / manifiestos (ver 07-operations)
│
├── docs/                                       # este paquete de documentación
├── SDPP.sln
└── Directory.Build.props                       # análisis estático, nullable enable, treat warnings as errors
```

### Reglas de dependencia (validadas con `SDPP.Architecture.Tests` / NetArchTest)

- `Domain` no referencia a ningún otro proyecto.
- `Application` referencia solo `Domain` y `BuildingBlocks.Application`.
- `Infrastructure` referencia `Application` y `Domain`, nunca al revés.
- `Api` referencia `Application` e `Infrastructure` solo para *composition root* (DI en `Program.cs`).
- Ningún módulo referencia directamente la capa `Infrastructure` de otro módulo; la comunicación
  entre bounded contexts es **solo** vía eventos de integración (RabbitMQ) o clientes HTTP/gRPC
  tipados definidos como puertos (`Ports/I*Client`).

## 2. Frontend (React + TypeScript + MUI)

```
frontend/sdpp-web/
├── src/
│   ├── app/                      # Bootstrap, providers (QueryClient, Theme, Auth), router
│   ├── shared/
│   │   ├── ui/                   # Componentes MUI reutilizables (design system SDPP)
│   │   ├── hooks/
│   │   ├── api/                  # Cliente HTTP generado desde OpenAPI (openapi-typescript)
│   │   └── utils/
│   ├── features/
│   │   ├── conversion/           # Wizard de conversión + formulario obligatorio
│   │   ├── pdf-tools/            # Unir, dividir, comprimir, marca de agua, firmar, etc.
│   │   ├── documents/            # Listado, detalle, descarga
│   │   ├── classification/       # Resultado de inspección, override manual
│   │   ├── audit/                # Búsqueda de trazas (solo Auditor/Admin)
│   │   ├── admin/                # Roles, políticas, reglas DLP, usuarios
│   │   └── dashboard/            # Indicadores ejecutivos (gráficas)
│   ├── i18n/
│   └── main.tsx
├── public/
├── vite.config.ts
├── package.json
└── tsconfig.json
```

- Cada `feature` sigue una estructura por *slice* (components, hooks, api, types) espejando los
  casos de uso del backend.
- El cliente HTTP se genera automáticamente desde el contrato OpenAPI (ver
  [06-api](../06-api/openapi.yaml)) para evitar *drift* entre frontend y backend.
- Autenticación en el navegador: **Authorization Code + PKCE** contra el broker OIDC, tokens
  guardados en memoria (no `localStorage`) para mitigar XSS/robo de token.
