import { createBrowserRouter, Navigate, Outlet } from "react-router-dom";
import { AppShell } from "./AppShell";
import { DashboardPage } from "../features/dashboard/DashboardPage";
import { ConvertWizardPage } from "../features/conversion/ConvertWizardPage";
import { EnvelopeInboxPage } from "../features/signature-envelopes/EnvelopeInboxPage";
import { EnvelopeEditorPage } from "../features/signature-envelopes/EnvelopeEditorPage";
import { EnvelopeDetailPage } from "../features/signature-envelopes/EnvelopeDetailPage";
import { EnvelopeSigningPage } from "../features/signature-envelopes/EnvelopeSigningPage";
import { EnvelopeVerificationPage } from "../features/signature-envelopes/EnvelopeVerificationPage";
import { AuditSearchPage } from "../features/audit/AuditSearchPage";
import { AdminPage } from "../features/admin/AdminPage";
import { LoginPage } from "../features/auth/LoginPage";
import { MfaEnrollPage } from "../features/auth/MfaEnrollPage";
import { MfaVerifyPage } from "../features/auth/MfaVerifyPage";
import { useAuthStore } from "./auth";

/** Every route below this guard requires a real session — an unauthenticated visitor is always
 * sent to /login, there is no other way into the app (see docs on autenticación con Google). */
function RequireAuth() {
  const status = useAuthStore((s) => s.status);
  return status === "authenticated" ? <Outlet /> : <Navigate to="/login" replace />;
}

function RequireAdmin() {
  const hasRole = useAuthStore((s) => s.hasRole);
  return hasRole("Administrador") ? <Outlet /> : <Navigate to="/" replace />;
}

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  { path: "/login/mfa-setup", element: <MfaEnrollPage /> },
  { path: "/login/mfa-verify", element: <MfaVerifyPage /> },
  // Standalone, outside RequireAuth/AppShell on purpose — reached via a per-recipient token, not
  // a login. Works the same whether the visitor has an SDPP account or not (see
  // EnvelopeSigningPage's own doc comment); an internal recipient's session cookie, if present, is
  // still honored by the backend, it just isn't required to load this route.
  { path: "/firmar/publico/:token", element: <EnvelopeSigningPage /> },
  // Truly public, no token at all — reached by scanning the QR on a completion certificate (see
  // VerifyEnvelopeQuery/VerificationEndpoints.MapVerificationEndpoints on the backend).
  { path: "/firmar/verificar/:envelopeId", element: <EnvelopeVerificationPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        path: "/",
        element: <AppShell />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: "convertir", element: <ConvertWizardPage /> },
          { path: "firmar", element: <EnvelopeInboxPage /> },
          { path: "firmar/nuevo", element: <EnvelopeEditorPage /> },
          { path: "firmar/:envelopeId", element: <EnvelopeDetailPage /> },
          { path: "auditoria", element: <AuditSearchPage /> },
          {
            element: <RequireAdmin />,
            children: [{ path: "admin", element: <AdminPage /> }],
          },
        ],
      },
    ],
  },
]);
