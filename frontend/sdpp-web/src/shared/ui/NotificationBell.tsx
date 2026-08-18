import { useState } from "react";
import Badge from "@mui/material/Badge";
import Box from "@mui/material/Box";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import Menu from "@mui/material/Menu";
import Typography from "@mui/material/Typography";
import NotificationsIcon from "@mui/icons-material/Notifications";
import NotificationsNoneIcon from "@mui/icons-material/NotificationsNoneOutlined";
import SendIcon from "@mui/icons-material/SendOutlined";
import VisibilityIcon from "@mui/icons-material/VisibilityOutlined";
import DrawIcon from "@mui/icons-material/DrawOutlined";
import TaskAltIcon from "@mui/icons-material/TaskAlt";
import BlockIcon from "@mui/icons-material/BlockOutlined";
import CancelIcon from "@mui/icons-material/CancelOutlined";
import HourglassDisabledIcon from "@mui/icons-material/HourglassDisabledOutlined";
import NotificationsActiveIcon from "@mui/icons-material/NotificationsActiveOutlined";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { listNotifications, markNotificationRead } from "../api/signature";
import type { NotificationType } from "../api/signature";
import { BRAND_COLORS } from "../theme";
import { ActivityListRow } from "./ActivityListRow";

const ALERT_COLOR = "#E53935";

const NOTIFICATION_STYLE: Record<NotificationType, { icon: React.ReactNode; color: string }> = {
  EnvelopeSent: { icon: <SendIcon />, color: "#1976D2" },
  EnvelopeViewed: { icon: <VisibilityIcon />, color: "#1976D2" },
  EnvelopeSigned: { icon: <DrawIcon />, color: BRAND_COLORS.magenta },
  EnvelopeCompleted: { icon: <TaskAltIcon />, color: BRAND_COLORS.teal },
  EnvelopeDeclined: { icon: <BlockIcon />, color: ALERT_COLOR },
  EnvelopeCancelled: { icon: <CancelIcon />, color: BRAND_COLORS.textLight },
  EnvelopeExpired: { icon: <HourglassDisabledIcon />, color: ALERT_COLOR },
  ReminderSent: { icon: <NotificationsActiveIcon />, color: BRAND_COLORS.orange },
};

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
      <IconButton
        size="small"
        aria-label="Notificaciones"
        onClick={(e) => setAnchorEl(e.currentTarget)}
        sx={{
          color: BRAND_COLORS.textLight,
          transition: "background-color 0.15s ease, color 0.15s ease",
          "&:hover": { bgcolor: `${BRAND_COLORS.teal}14`, color: BRAND_COLORS.teal },
        }}
      >
        <Badge badgeContent={unreadCount} color="error">
          <NotificationsIcon fontSize="small" />
        </Badge>
      </IconButton>
      <Menu
        anchorEl={anchorEl}
        open={!!anchorEl}
        onClose={() => setAnchorEl(null)}
        slotProps={{ paper: { sx: { width: 380, maxHeight: 460, borderRadius: 3, overflow: "hidden" } }, list: { sx: { p: 0 } } }}
      >
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", px: 2.5, py: 2 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Notificaciones</Typography>
          {unreadCount > 0 && (
            <Box
              sx={{
                fontSize: 11.5, fontWeight: 700, color: "#fff", bgcolor: ALERT_COLOR,
                borderRadius: 999, px: 1, py: 0.25, lineHeight: 1.4,
              }}
            >
              {unreadCount} nueva{unreadCount === 1 ? "" : "s"}
            </Box>
          )}
        </Box>
        <Divider />
        {notifications.length === 0 ? (
          <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, py: 5, color: BRAND_COLORS.textLight }}>
            <NotificationsNoneIcon sx={{ fontSize: 34 }} />
            <Typography variant="body2" color="text.secondary">No tienes notificaciones nuevas.</Typography>
          </Box>
        ) : (
          <Box sx={{ maxHeight: 380, overflowY: "auto" }}>
            {notifications.map((n) => {
              const style = NOTIFICATION_STYLE[n.type];
              return (
                <Box
                  key={n.id}
                  onClick={() => handleClick(n.id, n.envelopeId)}
                  sx={{ cursor: "pointer", position: "relative" }}
                >
                  {!n.readAtUtc && (
                    <Box sx={{ position: "absolute", left: 10, top: "50%", transform: "translateY(-50%)", width: 6, height: 6, borderRadius: "50%", bgcolor: BRAND_COLORS.magenta }} />
                  )}
                  <Box sx={{ pl: 1 }}>
                    <ActivityListRow
                      icon={style.icon}
                      iconColor={style.color}
                      primary={n.title}
                      secondary={n.message}
                      meta={formatRelative(n.createdAtUtc)}
                    />
                  </Box>
                </Box>
              );
            })}
          </Box>
        )}
      </Menu>
    </>
  );
}
