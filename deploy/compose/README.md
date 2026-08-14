# Entorno de desarrollo local

```bash
docker compose -f deploy/compose/docker-compose.yml up --build
```

Levanta: SQL Server, RabbitMQ, Redis, MinIO, ClamAV, Seq (logs), y los seis servicios de la
plataforma (`gateway`, `documents-api`, `classification-api`, `audit-api`,
`conversion-worker`, `web`).

## Verificación rápida

- `http://localhost:5090` — SPA (React + MUI)
- `GET http://localhost:5080/health` (Gateway) → `{"status":"healthy"}`
- `GET http://localhost:5081/health` (Document API)
- `GET http://localhost:5082/health` (Classification API)
- `GET http://localhost:5083/health` (Audit API)
- RabbitMQ management UI: http://localhost:15672 (sdpp_dev / sdpp_dev_secret)
- MinIO console: http://localhost:9001 (sdpp_dev / sdpp_dev_secret)
- Seq (logs estructurados): http://localhost:5341

## Limitación conocida: sin IdP local

Los endpoints de negocio (`/api/v1/...`) exigen un token OIDC válido emitido por
`Authentication:Authority` (ver `appsettings.Development.json` de cada servicio), que apunta a un
broker OIDC corporativo real y **no está incluido en este compose**. Para probar el flujo
completo end-to-end localmente:

1. Levanta un Keycloak de desarrollo (`quay.io/keycloak/keycloak`) con un realm `sdpp` y un
   cliente configurado con los roles de `docs/05-security/rbac-matrix.md`, o
2. Comenta temporalmente `app.UseAuthentication()/app.UseAuthorization()` y el
   `.RequireAuthorization()` de los endpoints solo en tu entorno local (nunca en una rama
   compartida) para probar el pipeline de conversión sin SSO.

Los health checks (`/health`) son siempre anónimos y sirven para confirmar que cada servicio
arrancó y se conectó a sus dependencias.

## Credenciales

Todas las contraseñas de este archivo son de desarrollo únicamente
(`Sdpp!DevOnly123`, `sdpp_dev_secret`) y están hardcodeadas a propósito para simplificar el
arranque local. **Nunca** se usan en `appsettings.json` (producción), que solo contiene
placeholders `__set-via-secret__` resueltos por Kubernetes Secrets/Vault — ver
[docs/07-operations/kubernetes-architecture.md](../../docs/07-operations/kubernetes-architecture.md).
