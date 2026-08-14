# Product Backlog (historias de usuario)

Formato: `ID | Historia | Criterios de aceptación (resumen) | Prioridad (MoSCoW) | Estimación (T-shirt)`

## Épica 1 — Ingesta y conversión base

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-101 | Como Usuario, quiero subir un documento Word/Excel/PPT/Imagen para convertirlo a PDF, para compartirlo en formato estándar. | Archivo validado por magic bytes; SHA-256 calculado; job creado solo con formulario completo; resultado descargable con etiqueta aplicada. | Must | L |
| US-102 | Como Usuario, quiero convertir un PDF a Word/Excel/PPT/Imagen, para editar su contenido. | Igual a US-101 en dirección inversa; preserva formato razonablemente (tablas, imágenes). | Must | L |
| US-103 | Como Usuario, quiero unir varios PDF en uno solo, para consolidar un expediente. | Orden configurable; valida que todos los documentos de entrada tengan clasificación compatible según política. | Must | M |
| US-104 | Como Usuario, quiero dividir un PDF por rango de páginas, para extraer una sección. | Genera N documentos de salida, cada uno con su propio hash y clasificación heredada. | Must | M |
| US-105 | Como Usuario, quiero comprimir un PDF pesado, para facilitar su envío. | Reducción configurable de calidad; reporta % de reducción. | Should | S |
| US-106 | Como Usuario, quiero aplicar OCR a un PDF escaneado, para poder buscar texto en él. | Detecta idioma o permite seleccionarlo; genera capa de texto invisible sobre imagen original. | Must | M |
| US-107 | Como Usuario, quiero agregar marca de agua/numeración/rotar/eliminar/reordenar páginas de un PDF. | Cada operación es un job independiente auditado; vista previa antes de confirmar. | Should | M |
| US-108 | Como Usuario, quiero proteger un PDF con contraseña y también desbloquear uno existente (con contraseña conocida). | AES-256; desbloqueo exige contraseña correcta, nunca fuerza bruta; auditoría reforzada. | Must | M |
| US-109 | Como Usuario, quiero firmar digitalmente un PDF con mi certificado corporativo. | Integración PKCS#11/HSM o certificado emitido por PKI interna; sello de tiempo (RFC 3161 interno). | Could | L |
| US-110 | Como Usuario o Sistema externo, quiero convertir múltiples documentos en un solo lote. | `BatchId` de seguimiento; formulario aplicado consistentemente; progreso parcial visible. | Should | L |

## Épica 2 — Formulario obligatorio y gobierno de la conversión

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-201 | Como Usuario, debo diligenciar un formulario de justificación antes de convertir, para dejar constancia del propósito. | Todos los campos obligatorios validados en 3 capas (UI, aplicación, BD); bloqueo total si falta uno. | Must | M |
| US-202 | Como Administrador, quiero configurar qué campos del formulario son obligatorios por área, para adaptar el control a cada unidad de negocio. | Configuración versionada y auditada; cambio no afecta jobs ya en curso. | Should | M |
| US-203 | Como Supervisor, quiero aprobar o rechazar conversiones que lo requieran, para controlar el uso de información restringida. | Cola de aprobaciones por área; expiración automática; comentario obligatorio al rechazar. | Must | M |

## Épica 3 — Clasificación e inspección automática

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-301 | Como Sistema, quiero inspeccionar automáticamente contenido/metadatos/nombre de archivo antes de convertir, para sugerir una clasificación. | Detecta al menos: PII (cédulas, correos), tarjetas (Luhn), cuentas bancarias, palabras clave configurables, regex configurables. | Must | XL |
| US-302 | Como Administrador, quiero configurar reglas de detección (regex, diccionarios, severidad), sin desplegar código. | Editor de reglas con versión, prueba en caliente ("dry run") sobre un documento de muestra. | Must | L |
| US-303 | Como Usuario, quiero poder ajustar manualmente la clasificación sugerida, dejando constancia del motivo. | Toda sobre-escritura se audita con `OverriddenBy` y `OverrideReason`; no permite bajar clasificación sin permiso elevado. | Must | M |
| US-304 | Como Administrador, quiero integrar un motor DLP externo (Purview/Forcepoint) como fuente adicional de clasificación. | Adaptador por puerto `IExternalDlpConnector`; resultado combinado toma el nivel más restrictivo (fail-safe). | Could | L |

## Épica 4 — Trazabilidad, auditoría y etiquetado

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-401 | Como Sistema, quiero registrar cada conversión con todos los atributos de trazabilidad exigidos (usuario, IP, hash, clasificación, etc.), de forma inmutable. | Hash-chain verificable; permisos SQL sin UPDATE/DELETE para el rol de aplicación. | Must | L |
| US-402 | Como Sistema, quiero etiquetar automáticamente cada documento de salida (pie de página/marca de agua/metadatos), para que sea identificable. | Etiqueta incluye clasificación, usuario, fecha, hash, ID de proceso; aplicable a los 4 mecanismos descritos. | Must | L |
| US-403 | Como Auditor, quiero buscar y exportar trazas de un usuario o documento, para investigaciones. | Exportación firmada, verificable independientemente. | Must | M |
| US-404 | Como Sistema, quiero validar periódicamente la integridad de la cadena de auditoría, y alertar si se rompe. | Job programado; alerta `Critical` a SIEM y Administrador de seguridad. | Must | M |

## Épica 5 — Alertas y DLP

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-501 | Como Sistema, quiero generar alertas automáticas (evento, correo, Teams, Slack, SIEM/Syslog) cuando se procese información Confidencial/Restringida/Secreta. | Multicanal configurable por clasificación; reintentos con backoff; log de entrega. | Must | L |
| US-502 | Como Sistema, quiero detectar patrones anómalos (volumen inusual de conversiones, accesos repetidos a documentos clasificados), y alertar. | Reglas de umbral configurables por rol/área; falso-positivo gestionable. | Should | L |
| US-503 | Como Sistema, quiero escanear cada archivo contra malware antes de procesarlo. | Integración ClamAV/EDR; archivo infectado nunca llega al motor de conversión. | Must | M |

## Épica 6 — Seguridad, identidad y permisos

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-601 | Como Usuario, quiero autenticarme con mi cuenta corporativa (SSO), sin credenciales adicionales. | OIDC/OAuth2 contra broker federado con AD; MFA si está configurado en AD FS/Entra. | Must | L |
| US-602 | Como Administrador, quiero asignar roles (Administrador, Auditor, Supervisor, Usuario, Invitado) a usuarios sincronizados de AD. | No se crean usuarios locales; auto-asignación de Administrador bloqueada. | Must | M |
| US-603 | Como Sistema, quiero cifrar documentos en tránsito y en reposo con gestión centralizada de claves. | TLS 1.2+, AES-256, integración con KMS/HSM corporativo o Azure Key Vault on-prem/Managed HSM. | Must | L |

## Épica 7 — Dashboard y reportes

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-701 | Como Supervisor/Auditor, quiero ver un dashboard con indicadores de uso y clasificación. | Ver [dashboard-spec.md](dashboard-spec.md); datos con máx. 5 min de desfase (near real-time vía proyección CQRS). | Should | L |

## Épica 8 — Plataforma, API y operación

| ID | Historia | Criterios de aceptación | Prioridad | Tamaño |
|---|---|---|---|---|
| US-801 | Como Sistema externo, quiero integrar SDPP vía API REST documentada (OpenAPI), para automatizar conversiones desde otros sistemas. | Swagger publicado, versionado semántico, autenticación `client_credentials`, mismo gate de formulario/clasificación. | Must | L |
| US-802 | Como Equipo de Plataforma, quiero desplegar SDPP en Kubernetes con autoscaling, para soportar miles de usuarios concurrentes. | HPA configurado, `NetworkPolicy` restrictiva, sin egress a Internet en los workers. | Must | XL |
| US-803 | Como Equipo de Plataforma, quiero un pipeline CI/CD con gates de seguridad (SAST, SCA, escaneo de imágenes, secretos), para evitar desplegar vulnerabilidades. | Build falla si hay hallazgos `High`/`Critical` no mitigados. | Must | L |

## Priorización MoSCoW — resumen
- **Must have (v1)**: US-101, 102, 103, 104, 106, 108, 201, 203, 301, 302, 303, 401, 402, 403,
  404, 501, 503, 601, 602, 603, 801, 802, 803.
- **Should have (v2)**: US-105, 107, 110, 202, 502, 701.
- **Could have (v3)**: US-109, 304.
- **Won't have (esta fase)**: integración con IA generativa cloud para resumen de documentos;
  firma con autoridad certificadora pública externa.
