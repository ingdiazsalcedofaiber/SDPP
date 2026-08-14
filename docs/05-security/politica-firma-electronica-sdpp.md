# Política de Firma Electrónica SDPP

> Este documento describe, en términos honestos y verificables contra el código, qué es la firma
> electrónica del módulo `SDPP.Signature`, qué evidencia genera, y qué NO es. No sustituye una
> opinión legal formal — ver [aspectos-pendientes-validacion-juridica.md](aspectos-pendientes-validacion-juridica.md)
> para lo que un abogado colombiano debe confirmar antes de un uso productivo con fuerza probatoria
> plena.

## 1. Qué es SDPP y qué NO es

**SDPP Firma es una plataforma de firma electrónica con trazabilidad completa.** No es una entidad
de certificación digital, no está acreditada ante ONAC ni ante ninguna autoridad de certificación
colombiana, y no emite certificados digitales bajo un esquema de infraestructura de llave pública
(PKI/X.509) propio de terceros. SDPP nunca debe presentarse, comercializarse ni describirse como:

- "Firma digital certificada" (ese término, en Colombia, corresponde específicamente a la firma
  basada en certificados de una entidad de certificación acreditada — Ley 527/1999, art. 28).
- Una entidad de certificación o proveedor de servicios de certificación.
- Un servicio con "100% de validez jurídica" o "válido ante cualquier autoridad" — la fuerza
  probatoria de cualquier firma electrónica depende de la ley aplicable y de las circunstancias
  concretas de cada caso (ver sección 4).

Lo que SDPP sí es, y hace realmente (no simulado — cada punto de esta sección está respaldado por
código verificado en vivo, no por diseño únicamente):

- **Identificación del firmante**: por sesión SDPP autenticada (usuarios internos) o por enlace
  único + código de un solo uso enviado al correo del destinatario (firmantes externos) — ver
  `SignerAccessChallenge`, `RequestOtpCommand`/`VerifyOtpCommand`.
- **Consentimiento expreso**: el firmante debe aceptar explícitamente la declaración *"Estoy de
  acuerdo en utilizar medios electrónicos para firmar este documento y manifiesto mi intención de
  suscribirlo electrónicamente"* antes de poder firmar — nunca implícito por solo abrir el
  documento. Fail-closed: `SignatureEnvelope.RegisterSignature` rechaza la firma si no hay
  consentimiento registrado. Cada aceptación queda como un `ConsentRecord` propio, con el texto
  exacto mostrado, versión, fecha/hora, IP y user-agent.
- **Integridad del documento**: hash SHA-256 del documento original y del documento final firmado
  (`SignatureEnvelope.OriginalSha256Hash`/`FinalSha256Hash`), recalculado y comparado en cada
  verificación pública — no se confía únicamente en lo almacenado.
- **Atestación criptográfica de la plataforma**: cada firma queda protegida con una firma digital
  ECDSA P-256 (`DocumentSignature`, `IKeyManagementService`) generada por la propia plataforma SDPP
  sobre el registro canónico del evento de firma. **Esto es una atestación de integridad de la
  evidencia por parte de SDPP, no una firma digital personal del firmante bajo un certificado
  emitido por una entidad de certificación.** Protege contra la alteración posterior del registro,
  pero no sustituye ni equivale a la firma digital certificada del art. 28 de la Ley 527/1999.
- **Trazabilidad y auditoría**: cada evento relevante (creación, envío, visto, OTP solicitado/
  validado, consentimiento, firma, declinación, cancelación, vencimiento, completado, certificado
  generado, verificación realizada) queda registrado en una bitácora encadenada por hash
  (`AuditRecord`, módulo Audit) — cualquier alteración posterior de un evento, o la eliminación de
  un evento de la cadena, es detectable (`VerifyIntegrityQuery`).
- **Certificado de finalización**: documento propio, verificable, con hash del documento, hash del
  sobre, resumen de cada firmante (método de autenticación, fechas, IPs), algoritmo criptográfico
  usado, y un código QR que enlaza al verificador público.
- **Verificación pública**: cualquier persona con el identificador del sobre (por ejemplo, escaneando
  el QR del certificado) puede comprobar en `/firmar/verificar/{id}` si el documento, el certificado
  y la auditoría siguen íntegros — sin necesidad de sesión ni de revelar más datos personales de los
  ya impresos en el certificado.
- **Paquete de evidencia exportable**: documento firmado, certificado, bitácora de auditoría
  completa, registros de firma criptográfica, y un resumen consolidado, todo descargable en un
  único archivo ZIP para el titular del sobre.

## 2. Qué tipo de firma electrónica es esta (marco colombiano)

La Ley 527 de 1999 reconoce la firma electrónica en un sentido amplio (art. 7) y distingue la firma
digital (art. 28) como una categoría específica, técnica, basada en criptografía asimétrica y
vinculada a un certificado emitido por una entidad de certificación. El Decreto 2364 de 2012
desarrolla el concepto de "firma electrónica" con requisitos de fiabilidad (vinculación al
firmante, control exclusivo del firmante sobre el medio de firma, detectabilidad de alteraciones
posteriores). El Decreto 1074 de 2015 (Libro 2, Parte 2, Título 2, Capítulo 1) compila y actualiza
el régimen reglamentario de comercio electrónico y firmas.

SDPP está diseñado para satisfacer los requisitos de fiabilidad de una **firma electrónica con
trazabilidad** conforme al Decreto 2364/2012: identificación razonable del firmante, control del
firmante sobre el proceso (a través de su sesión o del enlace/OTP exclusivos), y detección de
alteraciones posteriores tanto del documento como de la bitácora de auditoría. SDPP **no** es una
firma digital en el sentido del art. 28 de la Ley 527/1999, porque no existe un certificado de una
entidad de certificación acreditada vinculado a la identidad de cada firmante individual.

## 3. Datos personales

Conforme a la Ley 1581 de 2012 y sus decretos reglamentarios, SDPP debe minimizar la exposición de
datos personales, en particular en el verificador público (que deliberadamente no expone IPs,
user-agents ni el contenido de los campos firmados — solo nombre, correo, estado y fecha de firma
por firmante, más los resultados de integridad). El paquete de evidencia completo, que sí contiene
más detalle (IPs, user-agents, payloads de auditoría), solo es accesible al creador del sobre o a
sus propios firmantes, nunca públicamente.

## 4. Sobre la validez jurídica

SDPP **no garantiza** que un documento firmado a través de la plataforma tendrá fuerza probatoria
plena o validez jurídica automática en cualquier proceso judicial o administrativo. La validez y el
peso probatorio de una firma electrónica dependen de:

- La ley aplicable al caso concreto y a las partes involucradas.
- El tipo de acto o negocio jurídico (algunos actos exigen formalidades adicionales que ninguna
  firma electrónica, por sí sola, puede suplir — p. ej. escrituras públicas).
- La suficiencia de la evidencia reunida (identificación, consentimiento, integridad, trazabilidad)
  para el estándar probatorio exigido.
- La eventual impugnación de la firma y la capacidad de la parte interesada de sustentar, con la
  evidencia generada por SDPP, que la firma es atribuible al firmante y que el documento no fue
  alterado.

Cualquier afirmación de "100% válido" o "válido ante cualquier autoridad" sería inexacta y no debe
usarse en comunicaciones comerciales ni en la interfaz de usuario.

## 5. Vigencia y cambios

Esta política debe revisarse cada vez que cambie materialmente el diseño evidenciario del módulo de
Firma (por ejemplo: integración de una autoridad de sellado de tiempo certificada, integración con
una entidad de certificación real, cambios al algoritmo criptográfico). La versión del texto de
consentimiento (`ConsentRecord.ConsentVersion`) debe incrementarse cada vez que cambie el texto
mostrado al firmante, preservando el histórico de qué versión aceptó cada firmante.
