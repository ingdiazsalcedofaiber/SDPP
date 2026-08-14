import { useState } from "react";
import Badge from "@mui/material/Badge";
import Box from "@mui/material/Box";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import Typography from "@mui/material/Typography";
import NotificationsIcon from "@mui/icons-material/Notifications";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { listNotifications, markNotificationRead } from "../api/signature";
import { BRAND_COLORS } from "../theme";

function formatRelative(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diffMs / 60000);
  if (minutes < 1) return "ahora";
  if (minutes < 60) return `hace ${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `hace ${hours} h`;
  return `hace ${Math.floor(hours / 24)} d`;
}

/** Polls every 60s — no push channel exists yet, and a minute of staleness is an acceptable
 * trade-off against adding websockets/SSE just for this. */
export function NotificationBell() {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ["notifications"],
    queryFn: () => listNotifications(false),
    refetchInterval: 60000,
  });

  const notifications = query.data ?? [];
  const unreadCount = notifications.filter((n) => !n.readAtUtc).length;

  const handleClick = async (id: string, envelopeId: string | null) => {
    setAnchorEl(null);
    await markNotificationRead(id);
    await queryClient.invalidateQueries({ queryKey: ["notifications"] });
    if (envelopeId) navigate(`/firmar/${envelopeId}`);
  };

  return (
    <>
      <IconButton size="small" onClick={(e) => setAnchorEl(e.currentTarget)} sx={{ color: BRAND_COLORS.textLight }}>
        <Badge badgeContent={unreadCount} color="error">
          <NotificationsIcon fontSize="small" />
        </Badge>
      </IconButton>
      <Menu anchorEl={anchorEl} open={!!anchorEl} onClose={() => setAnchorEl(null)} slotProps={{ paper: { sx: { width: 360, maxHeight: 420 } } }}>
        <Box sx={{ px: 2, py: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Notificaciones</Typography>
        </Box>
        <Divider />
        {notifications.length === 0 && (
          <Box sx={{ px: 2, py: 3, textAlign: "center" }}>
            <Typography variant="body2" color="text.secondary">No tienes notificaciones nuevas.</Typography>
          </Box>
        )}
        {notifications.map((n) => (
          <MenuItem key={n.id} onClick={() => handleClick(n.id, n.envelopeId)} sx={{ whiteSpace: "normal", py: 1 }}>
            <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
              <Typography variant="body2" sx={{ fontWeight: n.readAtUtc ? 400 : 700 }}>{n.title}</Typography>
              <Typography variant="caption" color="text.secondary">{n.message}</Typography>
              <Typography variant="caption" color="text.disabled">{formatRelative(n.createdAtUtc)}</Typography>
            </Box>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
