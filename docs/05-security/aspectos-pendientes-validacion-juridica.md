# Aspectos pendientes de validación jurídica profesional (módulo de Firma)

> Lista concreta de lo que un abogado colombiano especializado en comercio electrónico/derecho
> probatorio debe revisar antes de un uso productivo del módulo de Firma con expectativa de fuerza
> probatoria plena. Complementa [politica-firma-electronica-sdpp.md](politica-firma-electronica-sdpp.md).
> Ninguno de estos puntos bloquea el uso interno/operativo de la plataforma — son riesgos a
> gestionar conscientemente, no defectos de implementación.

## 1. Calificación jurídica de la "atestación criptográfica de plataforma"

SDPP firma criptográficamente (ECDSA P-256) el registro canónico de cada evento de firma con una
clave que es propiedad y bajo control exclusivo de SDPP, no del firmante individual
(`DocumentSignature`, `IKeyManagementService`, ver `docs/05-security/politica-firma-electronica-sdpp.md §1`).
**Pendiente de confirmación legal**: si esta atestación aporta valor probatorio adicional más allá
de "prueba de integridad técnica del registro", y cómo debe describirse ante un juez o contraparte
sin sugerir que equivale a una firma digital personal certificada.

## 2. Suficiencia del mecanismo de autenticación para firmantes externos

Los firmantes sin cuenta SDPP se autentican mediante un enlace único (token de un solo uso) más un
código OTP de 6 dígitos enviado al correo declarado. **Pendiente**: si este nivel de aseguramiento
de identidad ("algo que tienes": acceso al correo) es suficiente para el tipo de documentos que la
organización planea firmar, o si para categorías de mayor riesgo se requiere un factor adicional
(p. ej. validación de cédula, biometría, doble canal). El código está arquitectado para admitir
factores adicionales (TOTP/WebAuthn/passkeys) sin rediseño, pero ninguno está implementado hoy.

## 3. Envío real de correo electrónico (SMTP)

**Estado actual**: `IEmailSender`/`LoggingEmailSender` (Fase I) registra el intento de envío en
logs pero no despacha correo real — no hay proveedor SMTP configurado todavía (pendiente de que el
cliente entregue las credenciales). Hasta que se conecte un proveedor real, la entrega de enlaces de
firma/OTP/recordatorios depende del flujo manual "copiar enlace" del creador del sobre. **Pendiente
legal**: si la ausencia de envío automático de correo afecta la trazabilidad de "notificación
efectiva" al firmante en la evidencia reunida.

## 4. Sello de tiempo (timestamp)

El campo `TimestampSource` de cada firma se registra honestamente como `SERVER_TIMESTAMP` — el
reloj propio del servidor SDPP, no un sello de tiempo de una autoridad de sellado de tiempo (TSA)
certificada bajo RFC 3161. **Pendiente**: evaluar si el caso de uso requiere una TSA certificada
(la arquitectura ya expone `ITimestampAuthorityService` como abstracción lista para ese reemplazo
sin tocar el resto del sistema).

## 5. Conservación y disponibilidad de la evidencia a largo plazo

El documento firmado, el certificado, la bitácora de auditoría y las claves públicas de firma se
conservan en las bases de datos operativas del módulo (`SDPP_Signature`, `SDPP_Audit`) sin una
política de retención/archivado a largo plazo definida todavía, ni un procedimiento formal de
exportación periódica a almacenamiento de preservación. **Pendiente**: definir el período de
conservación exigible según el tipo de documento/relación jurídica, y si se requiere preservar
copias fuera de la base de datos operativa (el Evidence Package en ZIP, Fase G, ya provee un
formato exportable apto para archivado, pero su generación es bajo demanda, no automática/periódica).

## 6. Rotación y custodia de la clave criptográfica de plataforma

La clave privada ECDSA de SDPP se cifra en reposo (AES-256-GCM) con una clave de cifrado
configurada como variable de entorno del contenedor (mismo patrón que el secreto MFA de Identity).
**Pendiente**: definir un procedimiento formal de custodia de esa clave de cifrado (¿HSM? ¿gestor de
secretos dedicado?), un plan de rotación de la clave de firma, y qué ocurre jurídicamente con las
firmas ya emitidas si la clave de plataforma llegara a comprometerse (el diseño ya soporta revocar
una clave sin invalidar firmas pasadas — `SignatureKey.Revoke()` — pero el procedimiento operativo
y su comunicación a las partes no está definido).

## 7. Aislamiento multi-organización

El módulo de Firma ya aplica aislamiento por `OrganizationId` en el backend (Fase H), pero hoy solo
existe una organización real en la plataforma (Identity no tiene todavía el concepto de
organizaciones). **Pendiente**: si el cliente planea operar múltiples organizaciones/clientes
jurídicamente independientes sobre la misma instalación de SDPP, se requiere primero extender
Identity con organizaciones reales antes de que el aislamiento tenga efecto práctico más allá de
"listo para cuando exista esa necesidad".

## 8. Idioma, jurisdicción y foro aplicables

El texto de consentimiento, el certificado y el verificador están redactados en español para
Colombia. **Pendiente**: si la plataforma se usará con firmantes en otras jurisdicciones, evaluar si
el marco legal citado (Ley 527/1999, Decreto 2364/2012, Decreto 1074/2015) sigue siendo el aplicable
o si se requiere adaptar el texto/evidencia al marco del país del firmante.

## 9. Términos de uso y aviso de privacidad de cara al firmante externo

Los firmantes externos (sin cuenta SDPP) interactúan con la plataforma únicamente a través del
enlace de firma — hoy no existe un aviso de privacidad ni términos de uso específicos mostrados en
ese flujo público. **Pendiente**: redacción de un aviso de tratamiento de datos personales conforme
a la Ley 1581/2012 para ese punto de contacto específico.

## 10. Revisión final antes de producción

Se recomienda que estos diez puntos se revisen con acompañamiento de un abogado antes de:
(a) firmar documentos con efectos jurídicos vinculantes de alto valor o riesgo, o
(b) presentar el certificado/evidencia de SDPP como prueba en un proceso judicial o administrativo.
Ninguno de estos puntos impide el uso interno/operativo actual del módulo.
