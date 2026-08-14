# Plan de despliegue, respaldo, recuperación y retención

## 1. Plan de despliegue (resumen operativo)

1. **Pre-requisitos de ambiente**: namespace K8s provisionado, secretos (Vault/K8s Secrets)
   cargados, certificados TLS de la PKI corporativa instalados, conectividad LDAPS a AD validada.
2. **Orden de despliegue** (dependencias): `sdpp-data` (SQL Server AG, Redis, RabbitMQ, MinIO) →
   migraciones EF Core (Job K8s) → `sdpp-apps` (Identity primero, luego Documents/Classification/
   Audit/Admin/Reporting) → `sdpp-workers` → `sdpp-gateway` (último, expone tráfico).
3. **Validación post-despliegue**: *smoke tests* automatizados (login OIDC real, subida +
   conversión de un documento de prueba no sensible, verificación de que se generó `AuditRecord`
   y que la cadena de hash es válida).
4. **Rollback**: Helm `rollback` a la revisión anterior; migraciones de BD diseñadas para ser
   *backward compatible* durante una versión (expand/contract pattern) para permitir rollback sin
   pérdida de datos.
5. **Ventanas de cambio**: siguiendo el proceso de Change Management corporativo; despliegues a
   producción requieren aprobación manual explícita (ver pipeline en
   [kubernetes-architecture.md §6](kubernetes-architecture.md#6-cicd-devsecops)).

## 2. Plan de respaldo

| Componente | Método | Frecuencia | Retención del backup |
|---|---|---|---|
| SQL Server (todas las bases) | Backup completo + log shipping continuo (Always On AG entre datacenters/salas) | Full semanal, diferencial diario, log cada 15 min | 90 días rodantes + copia mensual a almacenamiento *cold* por 7 años (regulatorio) |
| `AUDIT_RECORDS` (partición) | Incluido en backup de SQL Server + exportación WORM adicional a almacenamiento inmutable (object-lock) | Diaria | 7 años (WORM, no eliminable ni por administradores) |
| MinIO / almacenamiento de objetos | Replicación *erasure-coded* + snapshot | Continua (replicación) + snapshot diario | Igual a `RetentionPolicy` del documento + 30 días de gracia antes de purga física |
| RabbitMQ | No requiere backup de datos (colas transitorias); definiciones de topología (exchanges/queues) versionadas como código (IaC) | N/A | N/A |
| Redis | No es fuente de verdad (caché/sesión); reconstruible desde SQL Server | N/A | N/A |
| Configuración (Helm values, políticas DLP versionadas) | Repositorio Git + backup de Vault/Secrets | Continua (Git) | Historial completo en Git |
| Certificados / claves de cifrado | Backup en HSM/KMS corporativo con procedimiento de custodia dual (*dual control*) | Según política de PKI corporativa | Según política de PKI corporativa |

Todos los backups de datos que contienen documentos/auditoría se cifran con una clave distinta a
la de producción (rotación independiente), y se prueban con restauraciones periódicas (ver §4).

## 3. Plan de recuperación (Disaster Recovery)

**Objetivos**:
- **RTO (Recovery Time Objective)**: 4 horas para el servicio completo; 1 hora para el módulo de
  Identity/Auth (bloquea todo lo demás si no está disponible).
- **RPO (Recovery Point Objective)**: 15 minutos para datos transaccionales (SQL Server, gracias
  a log shipping/AG síncrona entre salas cercanas o asíncrona entre datacenters distantes);
  near-zero para `AUDIT_RECORDS` dado el log shipping frecuente.

**Escenarios**:

| Escenario | Procedimiento |
|---|---|
| Caída de un nodo/pod | Kubernetes reprograma automáticamente (self-healing); sin intervención manual si hay capacidad disponible en el clúster. |
| Caída de un namespace completo | Re-despliegue vía Helm desde el último release conocido bueno; datos persistentes no se pierden (viven en `sdpp-data`). |
| Caída del datacenter primario | Failover de SQL Server AG al datacenter secundario (manual o automático según configuración de quórum); DNS interno (o balanceador) redirige tráfico; RTO objetivo 4h. |
| Corrupción de datos detectada (p. ej. ruptura de hash-chain) | Aislar el componente afectado (aplicación no borra ni sobrescribe datos), restaurar desde el backup verificado más reciente anterior a la corrupción, re-ejecutar validación de cadena, notificar a Auditor/Oficial de Seguridad como incidente. |
| Pérdida de clave de cifrado (Always Encrypted / AES documentos) | Procedimiento de recuperación de clave desde HSM con custodia dual — **sin esto, los datos cifrados son irrecuperables por diseño** (documentado explícitamente como riesgo a gestionar operacionalmente). |

**Runbook resumido** (documento operativo detallado vive junto al equipo de Infraestructura, este
es el resumen de arquitectura):
1. Declarar incidente → activar equipo de continuidad.
2. Confirmar alcance (¿solo aplicación, o también datos?).
3. Restaurar infraestructura base (K8s, red) si aplica.
4. Restaurar/mover a réplica de datos (SQL AG failover o restore de backup).
5. Re-desplegar aplicaciones vía pipeline (nunca manualmente, para garantizar consistencia y
   trazabilidad del propio proceso de recuperación).
6. Ejecutar *smoke tests* + validación de integridad de `AUDIT_RECORDS`.
7. Reanudar tráfico, comunicar cierre del incidente, generar informe post-mortem.

## 4. Pruebas de restauración
Simulacro trimestral: restaurar un backup completo en un ambiente aislado, validar integridad
(incluyendo verificación completa del hash-chain de auditoría) y medir el tiempo real contra el
RTO/RPO objetivo. Resultados documentados y usados para ajustar el plan (control alineado a ISO
27001 A.5.29/A.5.30, NIST CSF RC.RP).

## 5. Retención y eliminación (ligado a `RetentionPeriod` del dominio)

- Job programado (Hangfire, `RetentionEnforcementJob`, diario) identifica documentos cuyo
  `RetentionPeriod` venció:
  - `Delete`: mueve a `PendingDeletion` (periodo de gracia de 7 días con posibilidad de
    cancelación por el dueño/Supervisor), luego purga física + `AuditRecord` de tipo
    `DocumentPurged` (el registro de auditoría persiste aunque el documento se elimine).
  - `Archive`: mueve a almacenamiento frío (clase de almacenamiento MinIO de menor costo),
    conserva metadata y trazabilidad.
  - `ReviewRequired`: genera tarea/alerta al Supervisor del área para decidir manualmente.
- Nunca se purgan físicamente los `AUDIT_RECORDS` correspondientes, independientemente de la
  retención del documento — la evidencia de que algo ocurrió sobrevive al documento mismo (ver
  [audit-and-traceability.md §6](../05-security/audit-and-traceability.md#6-retención-de-la-propia-auditoría)).
- Archivos temporales generados durante el procesamiento (`/tmp` de los workers) se eliminan
  automáticamente al finalizar cada Job de Kubernetes (ciclo de vida efímero del propio Pod, sin
  necesidad de limpieza manual) — ver [kubernetes-architecture.md §4](kubernetes-architecture.md#4-podsecurity--hardening).
