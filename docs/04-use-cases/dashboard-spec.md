# Dashboard ejecutivo — especificación

## 1. Principios
- Alimentado por proyecciones CQRS de solo lectura (esquema `reporting`, actualizado por
  consumidores de eventos de integración) — nunca consulta directamente las tablas
  transaccionales de `Documents`/`Audit` para no competir por recursos con el flujo operativo.
- Todo dato mostrado respeta RBAC/ABAC: un Supervisor solo ve su área, Auditor/Administrador ven
  alcance global (ver [rbac-matrix.md](../05-security/rbac-matrix.md)).
- Ningún widget muestra contenido de documentos, solo metadata agregada.

## 2. Indicadores (widgets)

| Indicador | Descripción | Desglose disponible |
|---|---|---|
| Archivos convertidos | Conteo total de `ConversionJob` completados en el período | Por día/semana/mes, por tipo de operación |
| Distribución por clasificación | % de documentos por nivel (Pública…Secreta) | Por área, por período |
| Usuarios activos | Usuarios distintos con al menos una acción en el período | Por área |
| Conversión por área | Volumen de conversiones agrupado por `Form.Area` | Comparativo mes anterior |
| Tipos de archivo | Distribución por `OperationType`/`ContentType` origen-destino | — |
| Top usuarios | Usuarios con mayor volumen de conversiones | Top 10, filtrable por clasificación |
| Top departamentos | Áreas con mayor volumen | Top 10 |
| Documentos confidenciales | Conteo de documentos con clasificación ≥ Confidencial | Tendencia temporal, alertable si hay salto anómalo |
| Alertas | Alertas generadas por canal y severidad | Tasa de entrega exitosa/fallida |
| Intentos fallidos | Login fallido, conversión bloqueada por política, aprobación rechazada | Por tipo, por usuario (solo Auditor/Admin) |

## 3. Wireframe conceptual

```
┌─────────────────────────────────────────────────────────────────┐
│  SDPP · Dashboard Ejecutivo         [Área: Todas ▾] [Período ▾]  │
├───────────────┬───────────────┬───────────────┬─────────────────┤
│ Convertidos    │ Usuarios      │ Confidenciales │ Alertas activas │
│ 128,430  ▲12%  │ 3,214  ▲4%    │ 4,102  ▲1%     │ 7  🔴 2 críticas│
├───────────────┴───────────────┴───────────────┴─────────────────┤
│  Distribución por clasificación (barra apilada, 6 niveles)       │
├───────────────────────────────┬──────────────────────────────────┤
│  Conversión por área (barras)  │  Tipos de archivo (donut)        │
├───────────────────────────────┼──────────────────────────────────┤
│  Top 10 usuarios (tabla)       │  Top 10 departamentos (tabla)     │
├───────────────────────────────┴──────────────────────────────────┤
│  Intentos fallidos y alertas (tabla, solo Auditor/Admin)          │
└─────────────────────────────────────────────────────────────────┘
```

## 4. Latencia de datos
Near real-time: proyecciones actualizadas por consumidor de eventos con objetivo de ≤ 5 minutos
de desfase respecto al evento origen (aceptable para un dashboard ejecutivo, evita acoplar el
camino caliente de conversión a la generación de reportes).

## 5. Exportación
Cada widget/tabla es exportable a CSV/Excel (generado igual que cualquier otro documento del
sistema — pasa por el mismo pipeline de etiquetado si el export contiene datos ≥ Confidencial,
por consistencia de gobierno).
