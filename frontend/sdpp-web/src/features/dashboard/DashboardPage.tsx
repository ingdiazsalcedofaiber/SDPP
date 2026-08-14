import { useAuthStore } from "../../app/auth";
import { AdminDashboard } from "./AdminDashboard";
import { PersonalDashboard } from "./PersonalDashboard";

// Two entirely separate views sharing no query — an Administrador sees platform-wide aggregates
// (AdminDashboard, gated server-side by the "Administrador" policy on every endpoint it calls),
// everyone else (Usuario, Auditor, future roles) sees only their own data (PersonalDashboard).
export function DashboardPage() {
  const hasRole = useAuthStore((s) => s.hasRole);
  return hasRole("Administrador") ? <AdminDashboard /> : <PersonalDashboard />;
}
