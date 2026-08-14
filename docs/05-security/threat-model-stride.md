# Modelo de amenazas (STRIDE)

## 1. Alcance del análisis
Se analiza el flujo crítico: **Usuario → SPA → Gateway → Document API → Classification/DLP API →
Conversion Worker → Almacenamiento**, incluyendo autenticación, mensajería y auditoría. Basado en
los diagramas de [c4-diagrams.md](../01-architecture/c4-diagrams.md).

## 2. Superficie y activos

| Activo | Por qué importa |
|---|---|
| Contenido de documentos en tránsito/reposo | Puede contener PII, secretos, información Restringida/Secreta |
| Tokens de acceso OIDC | Compromiso = suplantación de identidad |
| Motores de conversión (LibreOffice/Ghostscript/Tesseract) | Procesan archivos no confiables — superficie clásica de RCE |
| `AUDIT_RECORDS` (hash-chain) | Evidencia legal/forense; su integridad es el activo en sí mismo |
| Reglas DLP / Políticas de clasificación | Su manipulación puede desactivar controles sin que se note |
| Cola RabbitMQ | Puede usarse para inyectar jobs falsos o interceptar resultados |

## 3. Amenazas por categoría STRIDE

### S — Spoofing (suplantación)
| # | Amenaza | Mitigación |
|---|---|---|
| S1 | Suplantación de usuario mediante robo de token OIDC | Tokens de corta duración (15 min), refresh rotativo, `aud`/`iss` validados, binding opcional a IP/dispositivo para operaciones sensibles |
| S2 | Suplantación de servicio interno (un worker falso consume la cola y exfiltra jobs) | mTLS entre servicios (service mesh), autenticación de cliente en RabbitMQ (usuario/vhost por servicio, certificados) |
| S3 | Suplantación del broker OIDC (DNS spoofing interno) | Pinning de certificado / CA interna corporativa, validación estricta de `issuer` |

### T — Tampering (manipulación)
| # | Amenaza | Mitigación |
|---|---|---|
| T1 | Modificación de un `AuditRecord` existente | Hash-chain + `DENY UPDATE/DELETE` a nivel SQL + trigger `INSTEAD OF` (ver [audit-and-traceability.md §3](audit-and-traceability.md#3-inmutabilidad-a-nivel-de-plataforma)) |
| T2 | Manipulación de una `DlpRule`/`ClassificationPolicy` para debilitar controles | Versionado + doble aprobación + evento de auditoría `ConfigurationChanged` con diff |
| T3 | Manipulación del archivo en tránsito entre servicios | TLS/mTLS extremo a extremo; verificación de hash antes y después de cada etapa del pipeline |
| T4 | Manipulación del mensaje en la cola (RabbitMQ) para alterar `DocumentId`/clasificación | Mensajes firmados (HMAC con clave por servicio) además de TLS de transporte; validación de firma al consumir |

### R — Repudiation (repudio)
| # | Amenaza | Mitigación |
|---|---|---|
| R1 | Usuario niega haber solicitado una conversión | Formulario obligatorio + auditoría inmutable con IP/hash/timestamp + no-repudio reforzado por SSO corporativo (identidad federada, no anónima) |
| R2 | Administrador niega haber cambiado una política | Todo cambio de configuración genera `AuditRecord` con actor identificado; doble aprobación deja constancia de ambos aprobadores |

### I — Information Disclosure (divulgación)
| # | Amenaza | Mitigación |
|---|---|---|
| I1 | Fuga de documento Confidencial/Secreto vía respuesta de API mal autorizada (IDOR) | RBAC + ABAC por `OwnerId`/Área en cada endpoint; pruebas automatizadas de autorización (ASVS V4) |
| I2 | Exfiltración a través de conversión (p. ej. convertir a formato que elimina la marca de agua) | Motor de políticas bloquea combinaciones riesgosas (ver [dlp-engine.md §5](dlp-engine.md#5-motor-de-políticas-configurable-ejemplos-del-enunciado)); etiquetado aplicado también a metadatos, no solo visual |
| I3 | Fuga por logs (contenido de documento o secretos en logs de Serilog) | Política de logging: nunca loggear contenido de documento ni tokens; *destructuring* con filtros de campos sensibles (`ActorIp`, `PayloadJson` marcados `@Sensitive`) |
| I4 | Fuga por mensajes de error verbosos (stack traces al cliente) | `ProblemDetails` genérico en producción; detalle completo solo en logs internos correlacionado por `TraceId` |
| I5 | Acceso no autorizado a `AUDIT_RECORDS` completo por un rol no-Auditor | RLS + permisos SQL por rol de aplicación; Auditor no ve contenido, solo metadata |
| I6 | Fuga de información sensible por caché compartida en Redis mal segmentada | Namespacing por tenant/usuario en claves Redis, TTL corto, sin cachear contenido de documento — solo metadata no sensible |

### D — Denial of Service
| # | Amenaza | Mitigación |
|---|---|---|
| D1 | *Zip bomb* / PDF malicioso que agota CPU/memoria del worker de conversión | Límites de recursos por contenedor (`resources.limits` en K8s), timeout duro por job, `ulimit`, análisis de tamaño de descompresión estimado antes de procesar |
| D2 | Flood de subidas concurrentes agotando cola/almacenamiento | Rate limiting por usuario (Redis) en el Gateway, cuotas de almacenamiento por usuario/área, `NetworkPolicy` con límites de conexión |
| D3 | *Billion laughs* / XML bomb en documentos OOXML | Parsers configurados con límites de expansión de entidades (`XmlResolver = null`, `DtdProcessing.Prohibit`) |
| D4 | Saturación de la cola de auditoría bloqueando el flujo principal | Auditoría vía outbox asíncrono (no bloquea la transacción de negocio más allá de la escritura local), backpressure con alertas si la cola crece |

### E — Elevation of Privilege
| # | Amenaza | Mitigación |
|---|---|---|
| E1 | Usuario se auto-asigna rol Administrador manipulando el cliente | Roles vienen firmados en el token del IdP (AD/broker OIDC), nunca editables desde el cliente; verificación server-side en cada request |
| E2 | Escalamiento vía vulnerabilidad en el motor de conversión (RCE en LibreOffice/Ghostscript procesando un archivo malicioso) | Workers en contenedor sin privilegios (`runAsNonRoot`, `readOnlyRootFilesystem`, `seccomp=RuntimeDefault`, sin `NET_RAW`/capacidades extra), namespace de red aislado sin egress a Internet, actualización continua de imágenes (parcheo de CVEs conocidas de LibreOffice/Ghostscript) |
| E3 | Inyección de comandos vía nombre de archivo/parametrización al invocar `soffice`/`gs` por línea de comandos | Nunca concatenar input de usuario en shell; invocación por `Process.Start` con argumentos como arreglo (no shell), *allowlist* estricta de parámetros, sandboxing adicional (gVisor/Kata si el clúster lo soporta) |
| E4 | Administrador de base de datos (`sysadmin`) altera `AUDIT_RECORDS` directamente | Control organizacional: separación de funciones, acceso privilegiado con *just-in-time* y grabación de sesión (PAM), alerta ante acceso directo a la tabla fuera del principal de aplicación |

## 4. Prioridad de mitigación (top 5 antes de producción)
1. E2/E3 — aislamiento y hardening de los workers de conversión (mayor superficie de RCE del
   sistema, procesan input no confiable por diseño).
2. T1 — inmutabilidad verificable de auditoría (base de todo el valor de cumplimiento del
   proyecto).
3. I1 — pruebas de autorización exhaustivas (IDOR) en Document/Classification/Audit API.
4. D1/D3 — límites de recursos y parsers seguros antes de exponer subida masiva de archivos.
5. S1/T4 — mTLS y firma de mensajes entre servicios internos.

## 5. Proceso
Este modelo se revisa en cada fase del [roadmap](../04-use-cases/roadmap.md) antes de pasar a
producción esa fase, y ante cualquier cambio arquitectónico significativo (nuevo conector externo,
nuevo motor de conversión). Se mantiene como documento vivo versionado junto al código.
