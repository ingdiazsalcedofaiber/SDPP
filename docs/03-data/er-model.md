# Modelo de datos (Entidad-Relación)

> Cada Bounded Context posee su propio esquema SQL Server (`docs`, `classification`, `audit`,
> `identity`, `notifications`) — **schema-per-module** dentro de una única base de datos física en
> esta fase (simplicidad operativa), con *foreign keys* solo dentro del mismo esquema. Las
> referencias entre esquemas se hacen por `Id` sin FK física (consistencia eventual vía eventos),
> preservando la autonomía del bounded context y permitiendo partir a bases de datos físicas
> separadas en el futuro sin reescribir el dominio.

## 1. Diagrama ER

```mermaid
erDiagram
    DOCUMENTS ||--o{ CONVERSION_JOBS : "tiene"
    DOCUMENTS ||--o{ INSPECTION_RESULTS : "es inspeccionado por"
    CONVERSION_JOBS ||--|| PROCESSING_REQUEST_FORMS : "requiere"
    CONVERSION_JOBS ||--o| DOCUMENTS : "genera OutputDocument"
    CONVERSION_JOBS ||--o| APPROVAL_REQUESTS : "puede requerir"
    INSPECTION_RESULTS ||--o{ FINDINGS : "produce"
    CLASSIFICATION_POLICIES ||--o{ POLICY_RULES : "compuesta de"
    DLP_RULES ||--o{ FINDINGS : "detecta (referencia lógica)"
    USERS ||--o{ ROLE_ASSIGNMENTS : "tiene"
    ROLES ||--o{ ROLE_ASSIGNMENTS : "asignado a"
    ROLES ||--o{ ROLE_PERMISSIONS : "otorga"
    PERMISSIONS ||--o{ ROLE_PERMISSIONS : "otorgado por"
    AUDIT_RECORDS ||--o| DOCUMENTS : "referencia (sin FK física)"
    NOTIFICATION_LOG ||--o| AUDIT_RECORDS : "originada por"

    DOCUMENTS {
        uniqueidentifier Id PK
        uniqueidentifier OwnerId FK
        nvarchar OriginalFileName
        varchar ContentType
        bigint SizeBytes
        char64 Sha256Hash
        varchar StorageBucket
        varchar StorageObjectKey
        tinyint Classification
        tinyint ClassificationSource
        int PageCount
        tinyint Status
        int RetentionDays
        tinyint RetentionAction
        datetime2 CreatedAtUtc
        uniqueidentifier CreatedBy
        rowversion RowVersion
    }

    CONVERSION_JOBS {
        uniqueidentifier Id PK
        uniqueidentifier DocumentId FK
        tinyint OperationType
        tinyint Status
        varchar EngineUsed
        int DurationMs
        uniqueidentifier OutputDocumentId FK
        nvarchar ErrorDetail
        bit ApprovalRequired
        uniqueidentifier ApprovedBy
        datetime2 CreatedAtUtc
        datetime2 CompletedAtUtc
        rowversion RowVersion
    }

    PROCESSING_REQUEST_FORMS {
        uniqueidentifier ConversionJobId PK_FK
        nvarchar Reason
        nvarchar Project
        nvarchar Area
        nvarchar Process
        nvarchar Client
        nvarchar CaseNumber
        nvarchar Justification
        int RetentionDays
        tinyint DeclaredClassification
        nvarchar Destination
        datetime2 SubmittedAtUtc
    }

    INSPECTION_RESULTS {
        uniqueidentifier Id PK
        uniqueidentifier DocumentId FK
        tinyint TriggeredBy
        tinyint SuggestedClassification
        tinyint FinalClassification
        bit RequiresManualReview
        tinyint Status
        uniqueidentifier OverriddenBy
        nvarchar OverrideReason
        datetime2 CreatedAtUtc
    }

    FINDINGS {
        uniqueidentifier Id PK
        uniqueidentifier InspectionResultId FK
        varchar DetectorId
        tinyint Category
        tinyint Severity
        int MatchCount
        nvarchar Location
        varchar RuleVersion
    }

    CLASSIFICATION_POLICIES {
        uniqueidentifier Id PK
        nvarchar Name
        nvarchar Description
        tinyint Scope
        nvarchar ScopeValue
        bit Active
        int Version
        datetime2 CreatedAtUtc
        uniqueidentifier CreatedBy
    }

    POLICY_RULES {
        uniqueidentifier Id PK
        uniqueidentifier ClassificationPolicyId FK
        tinyint ConditionClassification
        tinyint ConditionOperationType
        nvarchar ConditionAreaEquals
        tinyint Effect
        int Priority
    }

    DLP_RULES {
        uniqueidentifier Id PK
        nvarchar Name
        tinyint DetectorType
        nvarchar PatternOrConfigJson
        tinyint Category
        tinyint DefaultSeverity
        bit Enabled
        int Version
    }

    APPROVAL_REQUESTS {
        uniqueidentifier Id PK
        uniqueidentifier ConversionJobId FK
        uniqueidentifier RequestedBy
        datetime2 RequestedAtUtc
        tinyint RequiredApproverRole
        tinyint Status
        uniqueidentifier DecidedBy
        datetime2 DecidedAtUtc
        nvarchar Comment
        datetime2 ExpiresAtUtc
    }

    USERS {
        uniqueidentifier Id PK
        varchar SamAccountName
        varchar ObjectSid
        nvarchar FullName
        varchar Email
        varchar Domain
        nvarchar Department
        bit Active
        datetime2 LastSyncedAtUtc
    }

    ROLES {
        uniqueidentifier Id PK
        varchar Name
        nvarchar Description
        bit IsSystemRole
    }

    PERMISSIONS {
        uniqueidentifier Id PK
        varchar Code
        nvarchar Description
    }

    ROLE_ASSIGNMENTS {
        uniqueidentifier UserId PK_FK
        uniqueidentifier RoleId PK_FK
        datetime2 AssignedAtUtc
        uniqueidentifier AssignedBy
    }

    ROLE_PERMISSIONS {
        uniqueidentifier RoleId PK_FK
        uniqueidentifier PermissionId PK_FK
    }

    AUDIT_RECORDS {
        bigint Id PK
        uniqueidentifier RecordGuid
        datetime2 OccurredAtUtc
        varchar EventType
        uniqueidentifier ActorUserId
        nvarchar ActorFullName
        varchar ActorEmail
        varchar ActorDomain
        varchar ActorIp
        varchar ActorMac
        nvarchar ActorHostname
        nvarchar ActorOs
        nvarchar ActorUserAgent
        uniqueidentifier SubjectDocumentId
        nvarchar PayloadJson
        char64 PreviousRecordHash
        char64 RecordHash
    }

    NOTIFICATION_LOG {
        uniqueidentifier Id PK
        bigint AuditRecordId FK
        tinyint Channel
        nvarchar Target
        tinyint Status
        int AttemptCount
        datetime2 SentAtUtc
        nvarchar ErrorDetail
    }
```

## 2. Notas de diseño físico

- **Particionado**: `AUDIT_RECORDS` particionada por rango mensual (`OccurredAtUtc`) para
  mantener rendimiento de escritura *append-only* a escala de miles de eventos/minuto; políticas
  de *sliding window* mueven particiones frías a almacenamiento de solo lectura (`FILEGROUP`
  read-only) tras el período de retención activo.
- **Always Encrypted**: columnas `ActorIp`, `ActorMac`, `PayloadJson` (cuando contiene PII) y
  `Sha256Hash` de `DOCUMENTS` con *randomized encryption*; el resto de columnas usadas en
  filtros/joins usa *deterministic encryption* solo si es estrictamente necesario para búsquedas.
- **Row-Level Security (RLS)**: política RLS en `DOCUMENTS` y `CONVERSION_JOBS` que restringe
  lectura a `OwnerId = SessionContext('UserId')` salvo para roles `Auditor`/`Administrador`
  (predicado de seguridad evalúa `SessionContext('Role')`), como capa adicional de defensa en
  profundidad detrás del RBAC de aplicación.
- **Integridad del hash-chain**: `AUDIT_RECORDS.PreviousRecordHash` referencia el `RecordHash`
  del registro anterior (por partición lógica global, no por partición física); un job
  programado (Hangfire) valida periódicamente la cadena completa y genera una alerta `Critical`
  si detecta ruptura (ver [audit-and-traceability.md](../05-security/audit-and-traceability.md)).
- **`RowVersion`**: control de concurrencia optimista (EF Core `[Timestamp]`) en agregados
  mutables (`DOCUMENTS`, `CONVERSION_JOBS`); `AUDIT_RECORDS` es inmutable por lo que no lo
  requiere (nunca se actualiza tras el insert).
- **Índices clave**:
  - `DOCUMENTS(Sha256Hash)` — detectar duplicados / verificar integridad.
  - `CONVERSION_JOBS(Status, CreatedAtUtc)` — cola de trabajo y dashboard.
  - `AUDIT_RECORDS(SubjectDocumentId, OccurredAtUtc)` — reconstrucción de traza de un documento.
  - `AUDIT_RECORDS(ActorUserId, OccurredAtUtc)` — reconstrucción de actividad de un usuario.
  - Índice columnstore en `AUDIT_RECORDS` para las consultas analíticas del dashboard/Reporting
    (a través de vista materializada / ETL ligero hacia esquema `reporting`, no consultas directas
    sobre la tabla operacional).
- **Cifrado en reposo**: TDE (Transparent Data Encryption) a nivel de base de datos como capa
  base, más Always Encrypted en columnas críticas como capa adicional (defensa en profundidad).

## 3. Diccionario de datos (extracto — tablas núcleo)

### `DOCUMENTS`

| Columna | Tipo | Null | Descripción |
|---|---|---|---|
| Id | uniqueidentifier | No | PK. Generado server-side (`NEWSEQUENTIALID()` para evitar fragmentación de índice). |
| OwnerId | uniqueidentifier | No | FK lógica a `USERS.Id` (esquema `identity`). |
| Sha256Hash | char(64) | No | Hash calculado server-side sobre el contenido original; nunca confiar en valor del cliente. |
| Classification | tinyint | No | 0=Pública … 5=Secreta. Ver `ClassificationLevel`. |
| ClassificationSource | tinyint | No | 0=Manual, 1=Automática, 2=Híbrida. |
| Status | tinyint | No | 0=Uploaded, 1=Inspecting, 2=Blocked, 3=Ready, 4=Archived, 5=PendingDeletion, 6=Deleted. |
| RetentionDays / RetentionAction | int / tinyint | Sí* | *Obligatorio si `Classification >= 3 (Confidencial)`, validado a nivel de aplicación y con `CHECK` constraint condicional. |

### `PROCESSING_REQUEST_FORMS`

Uno a uno con `CONVERSION_JOBS` (PK = FK). Todas las columnas son `NOT NULL` — **la base de
datos rechaza a nivel de esquema** cualquier intento de crear un `ConversionJob` sin formulario
completo (constraint `CHECK` adicional a la validación de aplicación: defensa en profundidad
descrita en [00-overview §4.2](../00-overview.md#42-seguridad-por-diseño-y-por-defecto)).

### `AUDIT_RECORDS`

| Columna | Tipo | Null | Descripción |
|---|---|---|---|
| Id | bigint IDENTITY | No | PK secuencial, garantiza orden de inserción para el hash-chain. |
| PayloadJson | nvarchar(max) | No | Snapshot completo del evento; esquema documentado por `EventType` en [audit-and-traceability.md](../05-security/audit-and-traceability.md). |
| RecordHash | char(64) | No | `SHA256(PreviousRecordHash \|\| PayloadJson \|\| OccurredAtUtc \|\| EventType)`. |
| — | — | — | Tabla protegida a nivel de permisos SQL: el rol de aplicación tiene **solo `INSERT` y `SELECT`**, nunca `UPDATE`/`DELETE` (revocado explícitamente, ver control de acceso en [audit-and-traceability.md](../05-security/audit-and-traceability.md#3-inmutabilidad-a-nivel-de-plataforma)). |

El diccionario completo (todas las columnas de las 16 tablas) se mantiene versionado junto a las
migraciones EF Core en `src/Modules/*/Infrastructure/Persistence/Migrations` — este documento es
la vista de diseño, las migraciones son la fuente de verdad ejecutable.
