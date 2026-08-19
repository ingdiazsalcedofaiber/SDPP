import { useState } from "react";
import Box from "@mui/material/Box";
import Badge from "@mui/material/Badge";
import Button from "@mui/material/Button";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import Menu from "@mui/material/Menu";
import Typography from "@mui/material/Typography";
import CloudOffOutlinedIcon from "@mui/icons-material/CloudOffOutlined";
import CloudSyncOutlinedIcon from "@mui/icons-material/CloudSyncOutlined";
import CloudQueueOutlinedIcon from "@mui/icons-material/CloudQueueOutlined";
import CloudDoneOutlinedIcon from "@mui/icons-material/CloudDoneOutlined";
import HourglassTopOutlinedIcon from "@mui/icons-material/HourglassTopOutlined";
import LoginOutlinedIcon from "@mui/icons-material/LoginOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import { useConnectivityStore } from "../offline/connectivityStore";
import { processQueue } from "../offline/queueProcessor";
import { useQueueStore } from "../offline/queueStore";
import type { QueueIntent } from "../offline/db";
import { BRAND_COLORS } from "../theme";
import { ActivityListRow } from "./ActivityListRow";

const ALERT_COLOR = "#E53935";

function describeIntent(intent: QueueIntent): { primary: string; secondary: string } {
  if (intent.kind === "conversion") {
    return { primary: intent.file.name, secondary: `Conversión: ${intent.operationType}` };
  }
  return { primary: intent.title, secondary: `${intent.recipients.length} firmante${intent.recipients.length === 1 ? "" : "s"}` };
}

/** Connectivity + offline-queue indicator — mirrors NotificationBell's icon+popover structure and
 * this app's brand language. Reassures the user that queued conversions/envelopes (see
 * shared/offline/*) will send themselves once connectivity returns, and surfaces the two states
 * that DON'T resolve on their own: needs-login (session expired mid-queue) and failed (a real
 * validation/business error, never auto-retried — see queueProcessor.ts's failure classification). */
export function OfflineQueueIndicator() {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const isOnline = useConnectivityStore((s) => s.isOnline);
  const items = useQueueStore((s) => s.items);

  const syncing = items.filter((i) => i.status === "syncing");
  const pending = items.filter((i) => i.status === "pending");
  const needsLogin = items.filter((i) => i.status === "needs-login");
  const failed = items.filter((i) => i.status === "failed");
  const badgeCount = pending.length + syncing.length + needsLogin.length + failed.length;

  const Icon = !isOnline
    ? CloudOffOutlinedIcon
    : syncing.length > 0
    ? CloudSyncOutlinedIcon
    : badgeCount > 0
    ? CloudQueueOutlinedIcon
    : CloudDoneOutlinedIcon;
  const iconColor = !isOnline || failed.length > 0 || needsLogin.length > 0 ? BRAND_COLORS.orange : BRAND_COLORS.textLight;

  return (
    <>
      <IconButton
        size="small"
        aria-label="Estado de conexión y cola de envío"
        onClick={(e) => setAnchorEl(e.currentTarget)}
        sx={{
          color: iconColor,
          transition: "background-color 0.15s ease, color 0.15s ease",
          "&:hover": { bgcolor: `${BRAND_COLORS.teal}14`, color: BRAND_COLORS.teal },
        }}
      >
        <Badge badgeContent={badgeCount} color={failed.length > 0 || needsLogin.length > 0 ? "error" : "warning"}>
          <Icon fontSize="small" />
        </Badge>
      </IconButton>
      <Menu
        anchorEl={anchorEl}
        open={!!anchorEl}
        onClose={() => setAnchorEl(null)}
        slotProps={{ paper: { sx: { width: 380, maxHeight: 460, borderRadius: 3, overflow: "hidden" } }, list: { sx: { p: 0 } } }}
      >
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", px: 2.5, py: 2 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Cola de sincronización</Typography>
          <Box
            sx={{
              fontSize: 11.5, fontWeight: 700, color: "#fff",
              bgcolor: isOnline ? BRAND_COLORS.teal : BRAND_COLORS.orange,
              borderRadius: 999, px: 1, py: 0.25, lineHeight: 1.4,
            }}
          >
            {isOnline ? "En línea" : "Sin conexión"}
          </Box>
        </Box>
        <Divider />

        {items.length === 0 ? (
          <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, py: 5, color: BRAND_COLORS.textLight }}>
            <CloudDoneOutlinedIcon sx={{ fontSize: 34 }} />
            <Typography variant="body2" color="text.secondary">Todo está sincronizado.</Typography>
          </Box>
        ) : (
          <Box sx={{ maxHeight: 380, overflowY: "auto" }}>
            {(pending.length > 0 || syncing.length > 0) && (
              <>
                <Typography variant="caption" sx={{ display: "block", px: 2.5, pt: 1.5, pb: 0.5, color: BRAND_COLORS.textLight, fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.5 }}>
                  En espera de conexión
                </Typography>
                {[...syncing, ...pending].map((intent) => {
                  const { primary, secondary } = describeIntent(intent);
                  return (
                    <ActivityListRow
                      key={intent.id}
                      icon={intent.status === "syncing" ? <CloudSyncOutlinedIcon /> : <HourglassTopOutlinedIcon />}
                      iconColor={BRAND_COLORS.orange}
                      primary={primary}
                      secondary={secondary}
                      meta={intent.status === "syncing" ? "Enviando…" : "En espera"}
                    />
                  );
                })}
              </>
            )}

            {needsLogin.length > 0 && (
              <>
                <Typography variant="caption" sx={{ display: "block", px: 2.5, pt: 1.5, pb: 0.5, color: BRAND_COLORS.textLight, fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.5 }}>
                  Necesita iniciar sesión de nuevo
                </Typography>
                {needsLogin.map((intent) => {
                  const { primary, secondary } = describeIntent(intent);
                  return (
                    <ActivityListRow
                      key={intent.id}
                      icon={<LoginOutlinedIcon />}
                      iconColor={ALERT_COLOR}
                      primary={primary}
                      secondary={secondary}
                      trailing={<Button size="small" onClick={() => void processQueue()}>Reintentar</Button>}
                    />
                  );
                })}
              </>
            )}

            {failed.length > 0 && (
              <>
                <Typography variant="caption" sx={{ display: "block", px: 2.5, pt: 1.5, pb: 0.5, color: BRAND_COLORS.textLight, fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.5 }}>
                  No se pudo enviar
                </Typography>
                {failed.map((intent) => {
                  const { primary } = describeIntent(intent);
                  return (
                    <ActivityListRow
                      key={intent.id}
                      icon={<ErrorOutlineIcon />}
                      iconColor={ALERT_COLOR}
                      primary={primary}
                      secondary={intent.lastError ?? undefined}
                      trailing={<Button size="small" onClick={() => void processQueue()}>Reintentar</Button>}
                    />
                  );
                })}
              </>
            )}
          </Box>
        )}
      </Menu>
    </>
  );
}
