import { useState } from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Tab from "@mui/material/Tab";
import Tabs from "@mui/material/Tabs";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import AddIcon from "@mui/icons-material/Add";
import BoltIcon from "@mui/icons-material/Bolt";
import DraftsIcon from "@mui/icons-material/DraftsOutlined";
import HourglassTopIcon from "@mui/icons-material/HourglassTopOutlined";
import EditNoteIcon from "@mui/icons-material/EditNoteOutlined";
import TaskAltIcon from "@mui/icons-material/TaskAltOutlined";
import TimerIcon from "@mui/icons-material/TimerOutlined";
import ListAltIcon from "@mui/icons-material/ListAltOutlined";
import OutboxIcon from "@mui/icons-material/OutboxOutlined";
import InboxIcon from "@mui/icons-material/InboxOutlined";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { listEnvelopes, getDashboardSummary } from "../../shared/api/signature";
import type { EnvelopeStatus, EnvelopeSummary } from "../../shared/api/signature";
import { ENVELOPE_STATUS_LABELS } from "../../shared/api/signature";
import { BRAND_COLORS } from "../../shared/theme";
import { StatCard } from "../dashboard/StatCard";

const STATUS_COLORS: Record<EnvelopeStatus, string> = {
  Draft: BRAND_COLORS.textLight,
  Sent: "#1976D2",
  Pending: "#1976D2",
  InProgress: BRAND_COLORS.orange,
  PartiallySigned: BRAND_COLORS.orange,
  Completed: BRAND_COLORS.teal,
  Declined: "#B23A2E",
  Cancelled: BRAND_COLORS.textLight,
  Expired: "#B23A2E",
};

const TABS: { value: "all" | "sent" | "pending"; label: string; icon: React.ReactElement }[] = [
  { value: "all", label: "Todos", icon: <ListAltIcon fontSize="small" /> },
  { value: "pending", label: "Pendientes de mi firma", icon: <InboxIcon fontSize="small" /> },
  { value: "sent", label: "Enviados por mí", icon: <OutboxIcon fontSize="small" /> },
];

// Forces América/Bogotá — see EnvelopeDetailPage.tsx's formatDateTime for why this can't rely on
// the viewing device's own OS timezone.
function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString("es-CO", { year: "numeric", month: "short", day: "numeric", timeZone: "America/Bogota" }) : "—";
}

/** The same row layout used everywhere this list appears — shared so "Pendientes de mi firma"
 * can render two visually separated tables (see EnvelopeInboxPage) without duplicating markup. */
function EnvelopeTable({ envelopes, onRowClick }: { envelopes: EnvelopeSummary[]; onRowClick: (envelopeId: string) => void }) {
  return (
    <Box sx={{ overflowX: "auto" }}>
      <Table>
        <TableHead>
          <TableRow>
            <TableCell>Título</TableCell>
            <TableCell>Estado</TableCell>
            <TableCell>Modo</TableCell>
            <TableCell>Firmantes</TableCell>
            <TableCell>Creado</TableCell>
            <TableCell>Fecha límite</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {envelopes.map((envelope) => (
            <TableRow
              key={envelope.envelopeId}
              hover
              sx={{ cursor: "pointer", "& .MuiTableCell-root": { py: 1.75 } }}
              onClick={() => onRowClick(envelope.envelopeId)}
            >
              <TableCell sx={{ fontWeight: 700, color: BRAND_COLORS.teal }}>{envelope.title}</TableCell>
              <TableCell>
                <Chip
                  size="small"
                  label={ENVELOPE_STATUS_LABELS[envelope.status]}
                  sx={{ bgcolor: `${STATUS_COLORS[envelope.status]}1A`, color: STATUS_COLORS[envelope.status], fontWeight: 700 }}
                />
              </TableCell>
              <TableCell>{envelope.signingMode === "Sequential" ? "Secuencial" : "Simultánea"}</TableCell>
              <TableCell>{envelope.signedCount} / {envelope.recipientCount}</TableCell>
              <TableCell>{formatDate(envelope.createdAtUtc)}</TableCell>
              <TableCell>{formatDate(envelope.dueDateUtc)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

/** "Bandeja de firmas" — con el resumen de sobre el dashboard (conteos por estado + métricas
 * agregadas) arriba de la lista operativa del día a día. */
export function EnvelopeInboxPage() {
  const navigate = useNavigate();
  const [scope, setScope] = useState<"all" | "sent" | "pending">("pending");

  const query = useQuery({ queryKey: ["signature-envelopes", scope], queryFn: () => listEnvelopes(scope) });
  const summaryQuery = useQuery({ queryKey: ["signature-dashboard-summary"], queryFn: getDashboardSummary });
  const summary = summaryQuery.data;

  return (
    <Box sx={{ maxWidth: 1200, mx: "auto" }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Typography variant="h5">Bandeja de firmas</Typography>
        <Box sx={{ display: "flex", gap: 1.5 }}>
          <Button
            variant="outlined" startIcon={<BoltIcon />} onClick={() => navigate("/firmar/rapida")}
            sx={{ borderColor: BRAND_COLORS.orange, color: BRAND_COLORS.orange, "&:hover": { borderColor: BRAND_COLORS.orange, bgcolor: `${BRAND_COLORS.orange}14` } }}
          >
            Firma rápida
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate("/firmar/nuevo")}>
            Nuevo sobre
          </Button>
        </Box>
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(5, 1fr)" }, gap: 2, mb: 3 }}>
        <StatCard label="Borradores" value={summary?.drafts ?? 0} icon={<DraftsIcon />} color={BRAND_COLORS.textLight} loading={summaryQuery.isPending} />
        <StatCard label="Esperando a otros" value={summary?.waitingForOthers ?? 0} icon={<HourglassTopIcon />} color="#1976D2" loading={summaryQuery.isPending} />
        <StatCard label="Esperan mi firma" value={summary?.waitingForMe ?? 0} icon={<EditNoteIcon />} color={BRAND_COLORS.orange} loading={summaryQuery.isPending} />
        <StatCard label="Completados" value={summary?.completed ?? 0} icon={<TaskAltIcon />} color={BRAND_COLORS.teal} loading={summaryQuery.isPending} />
        <StatCard
          label="Tiempo prom. de firma"
          value={summary?.avgCompletionHours != null ? `${summary.avgCompletionHours} h` : "—"}
          icon={<TimerIcon />}
          color={BRAND_COLORS.magenta}
          loading={summaryQuery.isPending}
          subtitle={summary ? `${summary.documentsSent} enviados · ${summary.signaturesPerformed} firmas` : undefined}
        />
      </Box>

      <Tabs
        value={scope}
        onChange={(_e, v) => setScope(v)}
        variant="scrollable"
        scrollButtons="auto"
        allowScrollButtonsMobile
        sx={{
          mb: 2.5,
          minHeight: 0,
          bgcolor: "rgba(15, 40, 38, 0.05)",
          borderRadius: 999,
          p: 0.5,
          maxWidth: "100%",
          width: "fit-content",
          "& .MuiTabs-indicator": { display: "none" },
          "& .MuiTab-root": {
            minHeight: 0,
            textTransform: "none",
            fontSize: 13.5,
            fontWeight: 600,
            borderRadius: 999,
            px: 2,
            py: 1,
            minWidth: 0,
            color: BRAND_COLORS.textLight,
            transition: "background-color 0.15s ease, color 0.15s ease",
          },
          "& .Mui-selected": { bgcolor: `${BRAND_COLORS.teal} !important`, color: "#fff !important" },
        }}
      >
        {TABS.map((tab) => (
          <Tab key={tab.value} value={tab.value} label={tab.label} icon={tab.icon} iconPosition="start" />
        ))}
      </Tabs>

      {query.isPending && (
        <Paper sx={{ p: 4 }}>
          <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}>
            <CircularProgress />
          </Box>
        </Paper>
      )}

      {query.isSuccess && query.data.length === 0 && (
        <Paper sx={{ p: 4 }}>
          <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, py: 3, color: BRAND_COLORS.textLight }}>
            <InboxIcon sx={{ fontSize: 36 }} />
            <Typography variant="body2" color="text.secondary">No hay sobres en esta vista todavía.</Typography>
          </Box>
        </Paper>
      )}

      {/* "Pendientes de mi firma" mixes two different things that both land here for different
          reasons (see EnvelopeSummary's doc comment) — shown as two clearly separate, independently
          titled tables rather than one merged list, so "necesito firmar esto" and "tengo una firma
          rápida a medias" never get confused with each other. */}
      {query.isSuccess && query.data.length > 0 && scope === "pending" && (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 3 }}>
          {query.data.filter((e) => e.isMyTurnToSign).length > 0 && (
            <Paper sx={{ overflow: "hidden" }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, px: 2.5, py: 1.75, borderBottom: "1px solid rgba(15,40,38,0.06)" }}>
                <InboxIcon sx={{ fontSize: 18, color: BRAND_COLORS.orange }} />
                <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Pendientes de tu firma</Typography>
              </Box>
              <EnvelopeTable envelopes={query.data.filter((e) => e.isMyTurnToSign)} onRowClick={(id) => navigate(`/firmar/${id}`)} />
            </Paper>
          )}
          {query.data.filter((e) => e.hasPendingInPersonSignature).length > 0 && (
            <Paper sx={{ overflow: "hidden" }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, px: 2.5, py: 1.75, borderBottom: "1px solid rgba(15,40,38,0.06)" }}>
                <BoltIcon sx={{ fontSize: 18, color: BRAND_COLORS.orange }} />
                <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Firmas rápidas en espera</Typography>
              </Box>
              <EnvelopeTable envelopes={query.data.filter((e) => e.hasPendingInPersonSignature)} onRowClick={(id) => navigate(`/firmar/${id}`)} />
            </Paper>
          )}
        </Box>
      )}

      {query.isSuccess && query.data.length > 0 && scope !== "pending" && (
        <Paper sx={{ overflow: "hidden" }}>
          <EnvelopeTable envelopes={query.data} onRowClick={(id) => navigate(`/firmar/${id}`)} />
        </Paper>
      )}
    </Box>
  );
}
