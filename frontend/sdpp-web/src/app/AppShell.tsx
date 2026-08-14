import AppBar from "@mui/material/AppBar";
import Avatar from "@mui/material/Avatar";
import Box from "@mui/material/Box";
import Drawer from "@mui/material/Drawer";
import IconButton from "@mui/material/IconButton";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import Toolbar from "@mui/material/Toolbar";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import DashboardIcon from "@mui/icons-material/Dashboard";
import TransformIcon from "@mui/icons-material/Transform";
import DrawIcon from "@mui/icons-material/Draw";
import FactCheckIcon from "@mui/icons-material/FactCheck";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import LogoutIcon from "@mui/icons-material/Logout";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { logout, useAuthStore } from "./auth";
import { SdppLogo } from "../shared/ui/SdppLogo";
import { NotificationBell } from "../shared/ui/NotificationBell";
import { BRAND_COLORS } from "../shared/theme";

const DRAWER_WIDTH = 240;
const ADMIN_ACCENT = "#6A1B9A";

const NAV_ITEMS = [
  { path: "/", label: "Dashboard", icon: <DashboardIcon />, color: BRAND_COLORS.teal },
  { path: "/convertir", label: "Nueva conversión", icon: <TransformIcon />, color: BRAND_COLORS.orange },
  { path: "/firmar", label: "Firma de Documentos", icon: <DrawIcon />, color: BRAND_COLORS.magenta },
  { path: "/auditoria", label: "Auditoría", icon: <FactCheckIcon />, color: BRAND_COLORS.magenta, requiresRole: ["Auditor", "Administrador"] },
  { path: "/admin", label: "Administración", icon: <AdminPanelSettingsIcon />, color: ADMIN_ACCENT, requiresRole: ["Administrador"] },
];

export function AppShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, hasRole } = useAuthStore();

  const handleLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  // Same colors as the sidebar's own role-scoped nav items (Auditoría/Administración), so the
  // account badge in the topbar reads as "the same role" at a glance, not an unrelated color.
  const primaryRole = hasRole("Administrador") ? "Administrador" : hasRole("Auditor") ? "Auditor" : "Usuario";
  const roleAccent = hasRole("Administrador") ? ADMIN_ACCENT : hasRole("Auditor") ? BRAND_COLORS.magenta : BRAND_COLORS.teal;

  return (
    <Box sx={{ display: "flex" }}>
      <AppBar
        position="fixed"
        color="inherit"
        sx={{ zIndex: (theme) => theme.zIndex.drawer + 1, bgcolor: "#fff" }}
      >
        <Toolbar sx={{ justifyContent: "space-between", py: 0.75 }}>
          <SdppLogo />
          {user && (
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
              <NotificationBell />
              <Box sx={{ width: "1px", height: 26, bgcolor: "rgba(15, 40, 38, 0.1)", mx: 0.25 }} />
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 1.25,
                  pl: 0.75,
                  pr: 1.75,
                  py: 0.5,
                  borderRadius: 999,
                  transition: "background-color 0.15s ease",
                  "&:hover": { bgcolor: "rgba(15, 40, 38, 0.05)" },
                }}
              >
                <Avatar
                  src={user.photoUrl ?? undefined}
                  sx={{
                    width: 38,
                    height: 38,
                    bgcolor: roleAccent,
                    border: `2px solid ${roleAccent}`,
                    fontSize: 15,
                    fontWeight: 700,
                  }}
                >
                  {user.fullName.charAt(0)}
                </Avatar>
                <Box sx={{ display: "flex", flexDirection: "column" }}>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: BRAND_COLORS.textDark, lineHeight: 1.3 }}>
                    {user.fullName}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{ color: roleAccent, fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.4, fontSize: 10.5 }}
                  >
                    {primaryRole}
                  </Typography>
                </Box>
              </Box>

              <Box sx={{ width: "1px", height: 26, bgcolor: "rgba(15, 40, 38, 0.1)", mx: 0.25 }} />

              <Tooltip title="Cerrar sesión">
                <IconButton
                  size="small"
                  onClick={handleLogout}
                  sx={{
                    color: BRAND_COLORS.textLight,
                    transition: "background-color 0.15s ease, color 0.15s ease",
                    "&:hover": { bgcolor: "rgba(178, 58, 46, 0.1)", color: "#B23A2E" },
                  }}
                >
                  <LogoutIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </Box>
          )}
        </Toolbar>
        {/* Franja de firma visual en el degradado de marca — mismo lenguaje que el swoosh del
            logo y el gradiente de fondo del login, conecta la barra superior con el resto de la
            identidad visual en vez de terminar en un borde plano. */}
        <Box
          sx={{
            height: 3,
            background: `linear-gradient(90deg, ${BRAND_COLORS.teal} 0%, ${BRAND_COLORS.orange} 50%, ${BRAND_COLORS.magenta} 100%)`,
          }}
        />
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: DRAWER_WIDTH, boxSizing: "border-box", bgcolor: "#FBFDFC", borderRight: "1px solid rgba(15, 40, 38, 0.06)" },
        }}
      >
        <Toolbar />
        <List sx={{ px: 1.5, py: 2, display: "flex", flexDirection: "column", gap: 0.5 }}>
          {NAV_ITEMS.filter((item) => !item.requiresRole || item.requiresRole.some(hasRole))
            .map((item) => {
              // Exact match for "/", startsWith for everything else — "/firmar" now has nested
              // routes (/firmar/nuevo, /firmar/:envelopeId) that should keep the same nav item lit.
              const selected = item.path === "/" ? location.pathname === "/" : location.pathname.startsWith(item.path);
              return (
                <ListItemButton
                  key={item.path}
                  selected={selected}
                  onClick={() => navigate(item.path)}
                  sx={{
                    borderRadius: 2.5,
                    transition: "background-color 0.15s ease",
                    "&:hover": { bgcolor: "rgba(15, 40, 38, 0.05)" },
                    "&.Mui-selected": {
                      bgcolor: `${item.color}1A`,
                      "&:hover": { bgcolor: `${item.color}26` },
                      "& .MuiListItemText-primary": { color: item.color, fontWeight: 600 },
                    },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 44 }}>
                    <Box
                      sx={{
                        width: 32,
                        height: 32,
                        borderRadius: "50%",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        bgcolor: selected ? item.color : `${item.color}1A`,
                        color: selected ? "#fff" : item.color,
                        transition: "background-color 0.15s ease, color 0.15s ease",
                        "& svg": { fontSize: 18 },
                      }}
                    >
                      {item.icon}
                    </Box>
                  </ListItemIcon>
                  <ListItemText primary={item.label} />
                </ListItemButton>
              );
            })}
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  );
}
