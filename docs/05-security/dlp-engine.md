# Motor DLP (Data Loss Prevention)

## 1. Objetivo
Detectar, antes de permitir cualquier conversión, la presencia de información cuya salida del
formato/canal original representa un riesgo, y decidir automáticamente si la operación se
permite, se bloquea o requiere aprobación — de forma **configurable sin desplegar código**.

## 2. Arquitectura del motor

```mermaid
flowchart LR
    subgraph "Classification & DLP API"
        ORCH[DLP Orchestrator]
        REG[Detector Registry]
        R1[RegexDetector]
        R2[KeywordDetector]
        R3[ChecksumDetector<br/>Luhn/IBAN]
        R4[DictionaryDetector]
        R5[EncryptedFileDetector]
        R6[MLModelDetector<br/>opcional, futuro]
        EXT[ExternalDlpConnector<br/>Purview/Forcepoint/Symantec]
        POL[Policy Engine<br/>Specification pattern]
    end

    ORCH --> REG
    REG --> R1 & R2 & R3 & R4 & R5 & R6
    ORCH --> EXT
    ORCH --> POL
    POL --> DEC[PolicyDecision: Allow/Block/RequireApproval]
```

- **Detector Registry**: cada `DlpRule` persistida se traduce en una instancia de detector en
  tiempo de ejecución (Strategy pattern); recarga en caliente vía caché Redis invalidada al
  guardar una regla.
- **Policy Engine**: implementado como cadena de `ISpecification<PolicyContext>` combinables
  (AND/OR/NOT), evaluadas en orden de `Priority`; la primera regla que aplica con efecto `Block`
  o `RequireApproval` corta la evaluación (short-circuit), salvo que exista una regla `Allow`
  explícita de mayor prioridad (permite excepciones controladas, ej. "el área Legal sí puede
  convertir Restringidos a imagen").

## 3. Catálogo de reglas DLP (obligatorias del enunciado)

| Categoría solicitada | Detectores incluidos |
|---|---|
| PII | `PII.NationalId`, `PII.Email`, `PII.Phone`, `PII.FullNamePattern` (heurístico, baja confianza) |
| Datos financieros | `PII.CreditCard` (Luhn), `PII.BankAccount` (IBAN/formato local), `Financial.Keywords` (estados financieros, EBITDA, presupuesto) |
| Contratos | `Legal.ContractKeywords`, `Legal.PartyClauseDetector` |
| Secretos industriales | `IP.StrategicKeywords` (diccionario editable), `IP.PatentCodeDetector` |
| Información de clientes | `Customer.RecordPattern` (combinación nombre+identificador+cuenta en misma región del documento) |
| Información médica | `Medical.ICD10`, `Medical.TerminologyDictionary` |
| Código fuente | `SourceCode.SyntaxHeuristic` (multi-lenguaje) |
| Credenciales | `Credentials.PasswordKeywords`, `Credentials.ConnectionStringPattern` |
| Tokens | `Credentials.JwtPattern`, `Credentials.ApiKeyPattern` (prefijos conocidos: `AKIA`, `ghp_`, `sk-`, etc., catálogo actualizable) |
| Claves privadas | `Credentials.PrivateKeyPemDetector` (`-----BEGIN PRIVATE KEY-----` y variantes) |
| Archivos cifrados | `EncryptedFileDetector` (heurística de entropía + detección de contenedores cifrados conocidos: PDF con `/Encrypt`, ZIP con flag de cifrado, Office con `EncryptedPackage`) |

### 3.1 Caso especial: archivos cifrados
Un archivo cifrado que el usuario no puede/quiere abrir es en sí mismo una señal de riesgo (puede
ocultar exfiltración o ransomware). Regla por defecto: `EncryptedFileDetector` → severidad
`High`, `RequiresManualReview = true`, y el archivo **no se envía al motor de conversión hasta
que el usuario aporte la contraseña o se clasifique manualmente con justificación**.

## 4. Modelo de regla configurable (`DlpRule`, ver [er-model.md](../03-data/er-model.md))

```json
{
  "id": "b6e2...",
  "name": "PII.CreditCard",
  "detectorType": "Checksum",
  "patternOrConfigJson": {
    "algorithm": "Luhn",
    "candidateRegex": "\\b(?:\\d[ -]*?){13,19}\\b",
    "context": ["tarjeta", "card", "visa", "mastercard"]
  },
  "category": "Financial",
  "defaultSeverity": "High",
  "enabled": true,
  "version": 3
}
```

Todas las reglas se editan desde el módulo de administración (UC-06), con `dry-run` sobre un
documento de prueba y diff de versión antes de publicar.

## 5. Motor de políticas configurable (ejemplos del enunciado)

```json
{
  "name": "Bloqueo de exportación de Confidenciales a imagen",
  "scope": "Global",
  "rules": [
    {
      "conditionClassification": "Confidencial",
      "conditionOperationType": "PdfToImage",
      "effect": "Block",
      "priority": 10
    },
    {
      "conditionClassification": "Restringida",
      "conditionAreaEquals": "Legal",
      "effect": "Allow",
      "priority": 5
    },
    {
      "conditionClassification": "Restringida",
      "effect": "RequireApproval",
      "priority": 20
    }
  ]
}
```

Esto habilita exactamente los dos ejemplos citados por el usuario: *"los documentos Confidenciales
no pueden convertirse a imágenes"* y *"los Restringidos solo pueden procesarse por el área
Legal"* (con fallback a aprobación para el resto de áreas en vez de bloqueo total, configurable).

## 6. Flujo de aprobación integrado con DLP

```mermaid
sequenceDiagram
    participant U as Usuario
    participant D as Document API
    participant P as Policy Engine
    participant S as Supervisor
    participant N as Notification Service

    U->>D: Solicita conversión (form completo)
    D->>P: Evaluate(classification, operation, actor, area)
    P-->>D: RequireApproval
    D->>D: Job.Status = AwaitingApproval
    D->>N: Publica ApprovalRequired
    N->>S: Notifica (correo/Teams)
    S->>D: Aprueba / Rechaza
    alt Aprobado
        D->>D: Job.Status = Queued
    else Rechazado
        D->>D: Job.Status = Rejected
        D->>N: Notifica a Usuario
    end
```

## 7. Falsos positivos y tuning
- Cada `Finding` incluye `RuleVersion` para poder correlacionar tuning posterior.
- El Administrador puede marcar un hallazgo como falso positivo confirmado a nivel de un
  documento específico (no cambia la regla global) — queda auditado como excepción puntual, no
  como cambio de política.
- Métrica de calidad: tasa de `Finding` marcados como falso positivo / total, visible en el
  dashboard (ver [dashboard-spec.md](../04-use-cases/dashboard-spec.md)) para justificar ajuste
  de reglas.

## 8. Integración con motores DLP externos (extensión recomendada)
Puerto `IExternalDlpConnector` con implementaciones intercambiables (Microsoft Purview
Information Protection, Forcepoint, Symantec DLP). El resultado del conector externo se combina
con el interno tomando siempre **el nivel más restrictivo** entre ambos (nunca el motor externo
puede *bajar* una clasificación detectada internamente). Timeout corto + circuit breaker: si el
externo no responde, se continúa solo con el motor interno (no bloquea el negocio por una
dependencia opcional), pero se registra la degradación en auditoría.
