# Diseño de API REST

## 1. Convenciones
- Base path: `/api/v1`. Versionado en la URL (breaking changes → `/api/v2`); cambios aditivos no
  requieren nueva versión.
- Autenticación: `Authorization: Bearer <token>` (OIDC access token). Sistemas externos usan
  `client_credentials` con `scope` acotado (`sdpp.convert`, `sdpp.readonly`, etc.).
- Formato: JSON (`application/json`); subida de archivos vía `multipart/form-data`.
- Errores: [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457) uniforme en todos
  los servicios.
- Idempotencia: endpoints de creación aceptan header `Idempotency-Key` (persistida 24h en Redis)
  para tolerar reintentos de red sin duplicar jobs.
- Rate limiting: por `sub` (usuario) o `client_id` (sistema externo), política *sliding window* en
  el Gateway, respuesta `429` con `Retry-After`.
- Paginación: `?page=&pageSize=` con `X-Total-Count` en la respuesta; máximo `pageSize=100`.
- Todas las respuestas incluyen `X-Trace-Id` para correlación con logs/trazas distribuidas.

## 2. Recursos principales

| Recurso | Servicio propietario |
|---|---|
| `/documents` | Document API |
| `/documents/{id}/conversions` | Document API |
| `/conversions/{jobId}` | Document API |
| `/conversions/{jobId}/approve`, `/reject` | Identity API (Approval) |
| `/classification/inspections/{documentId}` | Classification API |
| `/classification/policies`, `/dlp-rules` | Classification API (admin) |
| `/audit/records`, `/audit/export` | Audit API |
| `/rbac/roles`, `/rbac/users/{id}/roles` | Admin API |
| `/dashboard/*` | Reporting API |

## 3. Endpoints clave (extracto — contrato completo en [openapi.yaml](openapi.yaml))

### `POST /api/v1/documents`
Sube un documento. `multipart/form-data`: `file`, más metadata opcional. Respuesta `201` con
`DocumentId`, `Sha256Hash`, `Status = Uploaded`. Dispara inspección automática en background.

### `GET /api/v1/documents/{id}`
Devuelve metadata (nunca contenido binario en este endpoint). Requiere `documents.view.self` o
equivalente ABAC.

### `GET /api/v1/documents/{id}/content`
Descarga el binario. Requiere clasificación resuelta (`Status != Inspecting/Blocked`) y permiso
explícito; genera `AuditRecord` de tipo `DocumentDownloaded`.

### `POST /api/v1/documents/{id}/conversions`
Crea un `ConversionJob`. Body incluye `operationType` + `ProcessingRequestForm` completo (ver
[use-cases.md §2](../04-use-cases/use-cases.md#2-formulario-obligatorio)). Responde:
- `201` + `jobId`, `status: Queued` si la política lo permite.
- `202` + `status: AwaitingApproval` si requiere aprobación.
- `422` si el formulario es inválido/incompleto.
- `403` si la política de clasificación bloquea la operación.

### `GET /api/v1/conversions/{jobId}`
Estado del job (`PendingForm|Queued|Processing|AwaitingApproval|Completed|Failed|Rejected`) +
`outputDocumentId` cuando aplica. Soporta *long polling* y notificación push vía WebSocket
(`/hubs/conversion-status`) para progreso en tiempo real en la SPA.

### `POST /api/v1/conversions/{jobId}/approve` / `/reject`
Solo `Supervisor` del área correspondiente (validado por ABAC). `reject` exige `comment`.

### `POST /api/v1/documents/batch` (US-110)
Conversión masiva: array de `documentIds` u origen `zipUploadId` + un único
`ProcessingRequestForm` aplicado a todos. Responde `batchId` para seguimiento agregado vía
`GET /api/v1/batches/{batchId}`.

### `GET /api/v1/classification/inspections/{documentId}`
Resultado completo de inspección (hallazgos, clasificación sugerida) — visible solo para el
dueño del documento y roles con `documents.view.*`.

### `GET /api/v1/audit/records?documentId=&userId=&from=&to=&eventType=`
Solo `Auditor`/`Administrador`. Devuelve registros paginados con verificación de integridad de
cadena incluida en la respuesta (`chainValid: true/false` por página consultada).

### `POST /api/v1/audit/export`
Genera paquete de evidencia firmado (job asíncrono, notifica cuando está listo para descarga).

## 4. Integración de sistemas externos (US-801)
1. El equipo de Plataforma aprovisiona un `client_id`/`client_secret` (o certificado) con scope
   acotado.
2. El sistema externo obtiene un token vía `client_credentials` contra el broker OIDC interno.
3. Llama a `POST /api/v1/documents` + `POST /api/v1/documents/{id}/conversions` igual que un
   usuario interactivo — **el formulario obligatorio se envía como payload estructurado**, no se
   omite nunca, incluso para integraciones automatizadas.
4. Rate limit y auditoría idénticos a un usuario humano; el actor queda registrado como el
   `client_id` (con su propio "nombre completo" lógico = nombre del sistema integrador).

## 5. Contrato OpenAPI
Ver [openapi.yaml](openapi.yaml) — esqueleto inicial cubriendo Documents y Conversions; se amplía
incrementalmente junto con la implementación de cada módulo, y se publica vía Swagger UI
(`/swagger`) solo en entornos no productivos; en producción el contrato se sirve como JSON
estático de solo lectura (superficie de ataque reducida — sin *try it out* interactivo contra
producción).
