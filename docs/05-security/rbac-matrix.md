# RBAC — Roles y matriz de permisos

## 1. Modelo
RBAC clásico (Usuario → Rol → Permiso) reforzado con reglas ABAC contextuales evaluadas en el
`Policy Engine` (clasificación del documento, área del usuario, propiedad del recurso). Los roles
se sincronizan desde grupos de Active Directory (`memberOf`) hacia `ROLE_ASSIGNMENTS`; no existen
usuarios ni contraseñas locales.

## 2. Roles

| Rol | Descripción | Grupo AD sugerido |
|---|---|---|
| **Administrador** | Configura políticas, reglas DLP, roles, integraciones, monitorea salud de la plataforma. No opera como usuario final (separación de funciones). | `SDPP-Admins` |
| **Auditor** | Solo lectura sobre trazabilidad, auditoría, alertas y reportes de cumplimiento. No puede convertir ni administrar. | `SDPP-Auditors` |
| **Supervisor** | Aprueba/rechaza conversiones restringidas de su área; ve dashboard de su alcance; hereda permisos de Usuario. | `SDPP-Supervisors-<Area>` |
| **Usuario** | Sube, convierte, gestiona sus propios documentos; diligencia formulario obligatorio. | `SDPP-Users` (o grupo general de empleados) |
| **Invitado** | Acceso muy limitado: ver estado de un job compartido explícitamente con él (link firmado de un solo uso), sin subir archivos. | `SDPP-Guests` |

## 3. Matriz de permisos

`✓` permitido · `✓*` permitido con restricción ABAC (ver notas) · `—` no permitido

| Permiso (código) | Administrador | Auditor | Supervisor | Usuario | Invitado |
|---|---|---|---|---|---|
| `documents.upload` | — | — | ✓ | ✓ | — |
| `documents.convert.self` | — | — | ✓ | ✓ | — |
| `documents.view.self` | — | — | ✓ | ✓ | ✓* (solo compartido) |
| `documents.view.area` | — | — | ✓* (su área) | — | — |
| `documents.view.all` | ✓* (solo metadata, no contenido) | ✓* (solo metadata, no contenido) | — | — | — |
| `pdf.operations` (unir/dividir/OCR/etc.) | — | — | ✓ | ✓ | — |
| `classification.override.self` | — | — | ✓ | ✓* (no puede bajar de nivel sin aprobación) | — |
| `approvals.decide` | — | — | ✓* (su área) | — | — |
| `audit.query` | ✓ | ✓ | ✓* (su área) | — | — |
| `audit.export` | ✓ | ✓ | — | — | — |
| `policies.manage` | ✓* (doble aprobación en cambios masivos) | — | — | — | — |
| `dlp-rules.manage` | ✓* (doble aprobación) | — | — | — | — |
| `rbac.manage` | ✓* (no auto-asignación de Administrador) | — | — | — | — |
| `dashboard.view.global` | ✓ | ✓ | — | — | — |
| `dashboard.view.area` | ✓ | ✓ | ✓ | — | — |
| `system.configuration` | ✓ | — | — | — | — |
| `api.integration` (client_credentials) | ✓ (aprovisiona credenciales de sistema) | — | — | — | — |

## 4. Reglas ABAC complementarias (evaluadas junto al RBAC)

1. `documents.view.self`: `resource.OwnerId == actor.UserId`.
2. `documents.view.area` (Supervisor): `resource.Form.Area == actor.Department`.
3. `documents.view.all` (Administrador/Auditor): **nunca** incluye el contenido binario del
   documento por defecto — solo metadata, hash, clasificación, traza. Ver contenido completo
   requiere un permiso adicional explícito `documents.viewcontent.privileged`, que en sí mismo
   genera una alerta de auditoría de alta severidad (acceso privilegiado a contenido ajeno).
4. `classification.override` a un nivel **inferior** al sugerido por el motor: requiere
   `classification.override.downgrade`, permiso que por defecto **nadie** tiene salvo excepción
   explícita documentada — reduce el riesgo de que un usuario esconda información sensible
   bajando manualmente su clasificación.
5. `rbac.manage`: un Administrador no puede asignarse ni removerse a sí mismo el rol
   Administrador (control de escalamiento de privilegios / separación de funciones, ISO 27001
   A.8.2, A.5.15).

## 5. Principio de mínimo privilegio en la implementación
- Cada permiso se materializa como una `AuthorizationPolicy` de ASP.NET Core (`RequirePermission("audit.export")`),
  evaluada vía `IAuthorizationHandler` que consulta roles + reglas ABAC.
- Los tokens OIDC llevan los roles como *claims* firmados por el broker (Keycloak/OpenIddict), no
  se confía en headers/roles enviados por el cliente.
- Revisión periódica de accesos (*access recertification*) trimestral: reporte de usuarios con
  rol Administrador/Auditor para revalidación por el dueño del proceso (control organizacional
  alineado a ISO 27001 A.5.18 / CIS Control 6).
