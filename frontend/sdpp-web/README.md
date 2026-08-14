# SDPP Web

React 19 + TypeScript + MUI, estructurado según
[docs/01-architecture/solution-structure.md §2](../../docs/01-architecture/solution-structure.md).

## Desarrollo local

```bash
npm install
cp .env.example .env.local   # ajusta VITE_API_BASE_URL si no usas docker-compose
npm run dev
```

Ver [deploy/compose/README.md](../../deploy/compose/README.md) para la limitación conocida sobre
autenticación (no hay IdP local en el compose de desarrollo) y `src/app/auth.ts` para el punto de
reemplazo por el flujo OIDC real (Authorization Code + PKCE).

## Estructura

- `app/` — bootstrap (providers, router, auth store, query client)
- `shared/` — cliente HTTP, tipos (espejo manual de `docs/06-api/openapi.yaml`), tema MUI,
  componentes reutilizables
- `features/` — un módulo por caso de uso (`conversion` implementa UC-01 completo: subida →
  formulario obligatorio → estado del job; `audit` implementa UC-05)

## Decisión de seguridad registrada: advisory de react-router (RSC Mode CSRF)

`npm audit` reporta una vulnerabilidad *high* en `react-router` (GHSA-qwww-vcr4-c8h2, "RSC Mode
CSRF Bypass"). El fix sugerido por `npm audit fix --force` degrada a una versión anterior que
introduce un conjunto mayor de vulnerabilidades ya corregidas en la actual — es decir, el
"arreglo" automático empeora la postura de seguridad real.

Esta aplicación es una SPA cliente pura (`createBrowserRouter`, sin modo RSC/framework de React
Router), por lo que la superficie de ataque descrita en el advisory no aplica. Se documenta aquí
la decisión de mantener la versión actual en vez de degradar — revisar este archivo si el
advisory se actualiza o si el proyecto migra a un modo que sí use RSC.

## Pendiente conocido

- El cliente HTTP (`shared/api/types.ts`) está escrito a mano; debería generarse desde
  `docs/06-api/openapi.yaml` (p. ej. `openapi-typescript`) para no divergir del contrato.
- `features/dashboard` es un placeholder de maquetación — depende del módulo Reporting (no
  implementado aún en el backend, ver `docs/04-use-cases/roadmap.md`).
- Bundle sin code-splitting (593 KB) — aceptable para este scaffold; considerar `React.lazy` por
  ruta antes de producción.
