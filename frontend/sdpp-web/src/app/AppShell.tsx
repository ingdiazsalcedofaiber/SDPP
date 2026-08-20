import { useState } from "react";
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
import MenuIcon from "@mui/icons-material/Menu";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { logout, useAuthStore } from "./auth";
import { SdppLogo } from "../shared/ui/SdppLogo";
import { NotificationBell } from "../shared/ui/NotificationBell";
import { OfflineQueueIndicator } from "../shared/ui/OfflineQueueIndicator";
import { BRAND_COLORS } from "../shared/theme";

const DRAWER_WIDTH = 240;
const ADMIN_ACCENT = "#6A1B9A";
// Explicit (not min-) height so both the real AppBar Toolbar and the Drawer's spacer below are
// guaranteed pixel-identical — two independent <Toolbar> elements relying on MUI's default/
// responsive min-height each resolving "close enough" is exactly what previously let them drift
// apart and made the sidebar's nav list start hard against the header with no breathing room.
const TOPBAR_HEIGHT = 84;
const STRIPE_HEIGHT = 3;

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
  const [mobileOpen, setMobileOpen] = useState(false);

  const handleLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  // Same colors as the sidebar's own role-scoped nav items (Auditoría/Administración), so the
  // account badge in the topbar reads as "the same role" at a glance, not an unrelated color.
  const primaryRole = hasRole("Administrador") ? "Administrador" : hasRole("Auditor") ? "Auditor" : "Usuario";
  const roleAccent = hasRole("Administrador") ? ADMIN_ACCENT : hasRole("Auditor") ? BRAND_COLORS.magenta : BRAND_COLORS.teal;

  // Shared between the permanent (desktop) and temporary (mobile, overlay) drawers below — same
  // nav list either way, only the Drawer variant/visibility differs per breakpoint. Mobile also
  // closes the overlay on navigation, since a temporary Drawer is a modal the user expects to
  // dismiss the moment they act on it (a permanent one has nothing to dismiss).
  const navList = (
    <List sx={{ px: 1.5, pt: { xs: 2, md: 3.5 }, pb: 2, display: "flex", flexDirection: "column", gap: 0.5 }}>
      {NAV_ITEMS.filter((item) => !item.requiresRole || item.requiresRole.some(hasRole))
        .map((item) => {
          // Exact match for "/", startsWith for everything else — "/firmar" now has nested
          // routes (/firmar/nuevo, /firmar/:envelopeId) that should keep the same nav item lit.
          const selected = item.path === "/" ? location.pathname === "/" : location.pathname.startsWith(item.path);
          return (
            <ListItemButton
              key={item.path}
              selected={selected}
              onClick={() => { navigate(item.path); setMobileOpen(false); }}
              sx={{
                borderRadius: 2.5,
                transition: "background-color 0.15s ease, box-shadow 0.15s ease",
                "&:hover": { bgcolor: "rgba(15, 40, 38, 0.05)" },
                "&.Mui-selected": {
                  bgcolor: item.color,
                  boxShadow: `0 4px 14px ${item.color}55`,
                  "&:hover": { bgcolor: item.color },
                  "& .MuiListItemText-primary": { color: "#fff", fontWeight: 700 },
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
                    bgcolor: selected ? "rgba(255,255,255,0.22)" : `${item.color}1A`,
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
  );

  return (
    <Box sx={{ display: "flex" }}>
      <AppBar
        position="fixed"
        color="inherit"
        sx={{ zIndex: (theme) => theme.zIndex.drawer + 1, bgcolor: "#fff" }}
      >
        <Toolbar
          sx={{
            justifyContent: "space-between", height: TOPBAR_HEIGHT, minHeight: `${TOPBAR_HEIGHT}px !important`,
            px: { xs: 1.5, sm: 3 }, gap: 1,
          }}
        >
          <Box sx={{ display: "flex", alignItems: "center", gap: { xs: 0.5, md: 1.5 }, minWidth: 0 }}>
            {/* Only the mobile/temporary Drawer needs a trigger — the desktop one is always
                visible, so this button (and the Drawer it opens) simply doesn't exist above "md". */}
            <IconButton
              onClick={() => setMobileOpen(true)}
              sx={{ display: { xs: "inline-flex", md: "none" }, color: BRAND_COLORS.textDark, flexShrink: 0 }}
              aria-label="Abrir menú"
            >
              <MenuIcon />
            </IconButton>
            <SdppLogo height={44} />
          </Box>
          {user && (
            <Box sx={{ display: "flex", alignItems: "center", gap: { xs: 0, sm: 0.5 }, flexShrink: 0 }}>
              <OfflineQueueIndicator />
              <NotificationBell />
              <Box sx={{ width: "1px", height: 26, bgcolor: "rgba(15, 40, 38, 0.1)", mx: 0.25, display: { xs: "none", sm: "block" } }} />
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 1.25,
                  pl: { xs: 0, sm: 0.75 },
                  pr: { xs: 0, sm: 1.75 },
                  py: 0.5,
                  borderRadius: 999,
                  transition: "background-color 0.15s ease",
                  "&:hover": { bgcolor: "rgba(15, 40, 38, 0.05)" },
                }}
              >
                <Avatar
                  src={user.photoUrl ?? undefined}
                  sx={{
                    width: { xs: 32, sm: 38 },
                    height: { xs: 32, sm: 38 },
                    bgcolor: roleAccent,
                    border: `2px solid ${roleAccent}`,
                    fontSize: 15,
                    fontWeight: 700,
                  }}
                >
                  {user.fullName.charAt(0)}
                </Avatar>
                {/* Name/role text is the first thing to go on a phone-width topbar — the avatar
                    alone (plus the role's own accent color on it) is enough to recognize "it's me,
                    logged in", and the full detail is one tap away in NotificationBell/logout
                    anyway. */}
                <Box sx={{ display: { xs: "none", sm: "flex" }, flexDirection: "column" }}>
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

              <Box sx={{ width: "1px", height: 26, bgcolor: "rgba(15, 40, 38, 0.1)", mx: 0.25, display: { xs: "none", sm: "block" } }} />

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
            height: STRIPE_HEIGHT,
            background: `linear-gradient(90deg, ${BRAND_COLORS.teal} 0%, ${BRAND_COLORS.orange} 50%, ${BRAND_COLORS.magenta} 100%)`,
          }}
        />
      </AppBar>

      {/* Mobile: an overlay drawer above the content (doesn't take up flex space, so main doesn't
          need a responsive width/margin adjustment for it). Desktop: the original always-visible
          sidebar. Exactly one of the two is ever mounted-and-visible at a given breakpoint. */}
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{
          display: { xs: "block", md: "none" },
          [`& .MuiDrawer-paper`]: { width: DRAWER_WIDTH, boxSizing: "border-box", bgcolor: "#FBFDFC" },
        }}
      >
        {navList}
      </Drawer>
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: "none", md: "block" },
          width: DRAWER_WIDTH,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: DRAWER_WIDTH, boxSizing: "border-box", bgcolor: "#FBFDFC", borderRight: "1px solid rgba(15, 40, 38, 0.06)" },
        }}
      >
        <Box sx={{ height: TOPBAR_HEIGHT + STRIPE_HEIGHT, flexShrink: 0 }} />
        {navList}
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 1.5, sm: 3 } }}>
        <Box sx={{ height: TOPBAR_HEIGHT + STRIPE_HEIGHT, flexShrink: 0 }} />
        <Outlet />
      </Box>
    </Box>
  );
}
