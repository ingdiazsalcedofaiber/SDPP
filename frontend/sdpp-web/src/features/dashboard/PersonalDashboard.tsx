import { useMemo, useState } from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import LinearProgress from "@mui/material/LinearProgress";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import StorageIcon from "@mui/icons-material/Storage";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import TransformIcon from "@mui/icons-material/Transform";
import FactCheckIcon from "@mui/icons-material/FactCheck";
import DescriptionIcon from "@mui/icons-material/Description";
import InboxIcon from "@mui/icons-material/Inbox";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import ShieldIcon from "@mui/icons-material/Shield";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { BarChart } from "@mui/x-charts/BarChart";
import { LineChart } from "@mui/x-charts/LineChart";
import { PieChart } from "@mui/x-charts/PieChart";
import { useAuthStore } from "../../app/auth";
import { BRAND_COLORS } from "../../shared/theme";
import { formatBytes, formatPeriodLabel, formatBogotaDateTime } from "../../shared/format";
import { getMyAccessHistory, getPersonalDocumentsReport } from "../../shared/api/reporting";
import {
  ACCESS_RESULT_COLORS,
  CONVERSION_JOB_STATUS_COLORS,
  translateAccessResult,
  translateJobStatus,
  translateOperationType,
} from "../../shared/translations";
import { RingIndicator } from "./RingIndicator";
import { GradientHeroCard } from "./GradientHeroCard";
import { ActivityListRow } from "../../shared/ui/ActivityListRow";
import { DateRangeFilter, DEFAULT_DATE_RANGE, resolveDateRange, type DateRangeValue } from "./DateRangeFilter";

const ALERT_COLOR = "#E53935";

// Derived from BRAND_COLORS (lighter -> darker within each hue) for the headline hero tiles — the
// fourth one sweeps teal -> magenta, echoing the logo's own swoosh gradient instead of introducing
// an unrelated color.
const HERO_GRADIENTS = {
  teal: `linear-gradient(135deg, #22C4B6 0%, #0E7A70 100%)`,
  magenta: `linear-gradient(135deg, #E8479A 0%, #A8125F 100%)`,
  orange: `linear-gradient(135deg, #FFA94D 0%, #D9670D 100%)`,
  swoosh: `linear-gradient(135deg, ${BRAND_COLORS.teal} 0%, ${BRAND_COLORS.magenta} 100%)`,
};

export function PersonalDashboard() {
  const navigate = useNavigate();
  const hasRole = useAuthStore((s) => s.hasRole);
  const [range, setRange] = useState<DateRangeValue>(DEFAULT_DATE_RANGE);
  const resolved = useMemo(() => resolveDateRange(range), [range]);

  const docsQuery = useQuery({
    queryKey: ["reporting", "personal", "documents", resolved.from.toISOString(), resolved.to.toISOString(), resolved.bucket],
    queryFn: () => getPersonalDocumentsReport(resolved),
  });

  const historyQuery = useQuery({
    queryKey: ["reporting", "personal", "access-history"],
    queryFn: () => getMyAccessHistory(8),
  });

  const overview = docsQuery.data;
  const storagePct = overview?.storageQuotaBytes ? Math.min(100, (overview.storageUsedBytes / overview.storageQuotaBytes) * 100) : 0;

  const statusTotal = overview
    ? overview.conversionsByStatus.completed + overview.conversionsByStatus.inProgress + overview.conversionsByStatus.failed
    : 0;
  const completionPct = statusTotal > 0 ? (overview!.conversionsByStatus.completed / statusTotal) * 100 : 0;
  const errorPct = statusTotal > 0 ? (overview!.conversionsByStatus.failed / statusTotal) * 100 : 0;

  const topContentTypes = overview?.documentsByContentType ?? [];
  const contentTypeTotal = topContentTypes.reduce((sum, t) => sum + t.count, 0);

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 2, mb: 3 }}>
        <Typography variant="h5">Mi panel</Typography>
        <DateRangeFilter value={range} onChange={setRange} />
      </Box>

      {/* Fila de indicadores en anillo — cada porcentaje es una proporción real (tasa de éxito,
          % de cuota usada, tasa de error), nunca un número decorativo. */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" }, gap: 2.5, mb: 3 }}>
        <Paper sx={{ p: 2.5, display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.5 }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
              <SwapHorizIcon sx={{ fontSize: 18, color: BRAND_COLORS.teal }} />
              <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.2 }}>Completadas</Typography>
            </Box>
            {docsQuery.isPending ? <Skeleton /> : <Typography variant="h4" sx={{ fontWeight: 700 }}>{overview?.conversionsByStatus.completed ?? 0}</Typography>}
          </Box>
          <RingIndicator percent={completionPct} color={BRAND_COLORS.teal} />
        </Paper>

        <Paper sx={{ p: 2.5, display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.5 }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
              <StorageIcon sx={{ fontSize: 18, color: BRAND_COLORS.orange }} />
              <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.2 }}>Almacenamiento</Typography>
            </Box>
            {docsQuery.isPending ? <Skeleton /> : <Typography variant="h5" sx={{ fontWeight: 700 }}>{formatBytes(overview?.storageUsedBytes ?? 0)}</Typography>}
          </Box>
          <RingIndicator percent={storagePct} color={BRAND_COLORS.orange} />
        </Paper>

        <Paper sx={{ p: 2.5, display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.5 }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
              <ErrorOutlineIcon sx={{ fontSize: 18, color: ALERT_COLOR }} />
              <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.2 }}>Con error</Typography>
            </Box>
            {docsQuery.isPending ? <Skeleton /> : <Typography variant="h4" sx={{ fontWeight: 700 }}>{overview?.conversionsByStatus.failed ?? 0}</Typography>}
          </Box>
          <RingIndicator percent={errorPct} color={ALERT_COLOR} />
        </Paper>

        <Paper sx={{ p: 2, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center" }}>
          <Box sx={{ width: "100%", display: "flex", flexDirection: "column", gap: 0.5, mb: 1 }}>
            {([
              ["Completadas", BRAND_COLORS.teal, overview?.conversionsByStatus.completed ?? 0],
              ["En proceso", BRAND_COLORS.orange, overview?.conversionsByStatus.inProgress ?? 0],
              ["Con error", ALERT_COLOR, overview?.conversionsByStatus.failed ?? 0],
            ] as const).map(([label, color]) => (
              <Box key={label} sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                <Box sx={{ width: 8, height: 8, borderRadius: "50%", bgcolor: color, flexShrink: 0 }} />
                <Typography variant="caption" color="text.secondary">{label}</Typography>
              </Box>
            ))}
          </Box>
          <PieChart
            series={[{
              innerRadius: 26, outerRadius: 40, paddingAngle: 2, cornerRadius: 3,
              data: [
                { id: 0, value: overview?.conversionsByStatus.completed ?? 0, color: BRAND_COLORS.teal },
                { id: 1, value: overview?.conversionsByStatus.inProgress ?? 0, color: BRAND_COLORS.orange },
                { id: 2, value: overview?.conversionsByStatus.failed ?? 0, color: ALERT_COLOR },
              ],
            }]}
            width={110}
            height={100}
            hideLegend
          />
        </Paper>
      </Box>

      <Box sx={{ display: "flex", gap: 1.5, mb: 3, flexWrap: "wrap" }}>
        <Button variant="contained" startIcon={<TransformIcon />} onClick={() => navigate("/convertir")}>
          Nueva conversión
        </Button>
        {(hasRole("Auditor") || hasRole("Administrador")) && (
          <Button variant="outlined" startIcon={<FactCheckIcon />} onClick={() => navigate("/auditoria")}>
            Ir a auditoría
          </Button>
        )}
      </Box>

      {/* Tiles grandes de titulares — decoración de onda puramente visual, nunca datos inventados. */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" }, gap: 2.5, mb: 3 }}>
        <GradientHeroCard label="Conversiones totales" value={overview?.totalConversions ?? 0} gradient={HERO_GRADIENTS.teal} loading={docsQuery.isPending} />
        <GradientHeroCard label="Documentos guardados" value={overview?.totalDocuments ?? 0} gradient={HERO_GRADIENTS.magenta} loading={docsQuery.isPending} />
        <GradientHeroCard
          label={topContentTypes[0] ? `Más subido: ${topContentTypes[0].label}` : "Tipo más subido"}
          value={topContentTypes[0]?.count ?? 0}
          gradient={HERO_GRADIENTS.orange}
          loading={docsQuery.isPending}
        />
        <GradientHeroCard
          label={topContentTypes[1] ? `Segundo tipo: ${topContentTypes[1].label}` : "Segundo tipo"}
          value={topContentTypes[1]?.count ?? 0}
          gradient={HERO_GRADIENTS.swoosh}
          loading={docsQuery.isPending}
        />
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1.4fr 1fr" }, gap: 2.5, mb: 3 }}>
        <Paper sx={{ p: 2.5 }}>
          <Typography variant="h6" sx={{ fontWeight: 700, mb: 2 }}>Detalle por tipo de documento</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : topContentTypes.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{ py: 4, textAlign: "center" }}>Sin documentos todavía.</Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Tipo</TableCell>
                  <TableCell align="right">Cantidad</TableCell>
                  <TableCell align="right">%</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {topContentTypes.map((t, i) => (
                  <TableRow key={t.label}>
                    <TableCell>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <Box sx={{ width: 8, height: 8, borderRadius: "50%", bgcolor: PIE_PALETTE[i % PIE_PALETTE.length] }} />
                        {t.label}
                      </Box>
                    </TableCell>
                    <TableCell align="right">{t.count}</TableCell>
                    <TableCell align="right">{contentTypeTotal > 0 ? ((t.count / contentTypeTotal) * 100).toFixed(1) : "0.0"}%</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Paper>

        <Paper sx={{ p: 2.5, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center" }}>
          {docsQuery.isPending ? (
            <CircularProgress size={24} />
          ) : topContentTypes.length === 0 ? (
            <Typography variant="body2" color="text.secondary">Sin datos.</Typography>
          ) : (
            <PieChart
              series={[{
                innerRadius: 55, outerRadius: 90, paddingAngle: 2, cornerRadius: 4,
                data: topContentTypes.map((t, i) => ({ id: i, value: t.count, label: t.label, color: PIE_PALETTE[i % PIE_PALETTE.length] })),
                arcLabel: "value",
              }]}
              width={260}
              height={220}
              hideLegend
            />
          )}
        </Paper>
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2.5, mb: 3 }}>
        <Paper sx={{ p: 2.5 }}>
          <Typography variant="h6" sx={{ fontWeight: 700, mb: 2 }}>Conversiones por tipo de operación</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : (
            <BarChart
              dataset={(overview?.conversionsByType ?? []).map((d) => ({ ...d, label: translateOperationType(d.label) }))}
              xAxis={[{ dataKey: "label", scaleType: "band" }]}
              series={[{ dataKey: "count", label: "Conversiones", color: BRAND_COLORS.teal }]}
              height={260}
            />
          )}
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Typography variant="h6" sx={{ fontWeight: 700, mb: 2 }}>Conversiones en el tiempo</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : (
            <LineChart
              dataset={overview?.timeSeries ?? []}
              xAxis={[{ dataKey: "periodStart", scaleType: "band", valueFormatter: (v: string) => formatPeriodLabel(v, resolved.bucket) }]}
              series={[{ dataKey: "count", label: "Conversiones", color: BRAND_COLORS.magenta, area: true }]}
              height={260}
            />
          )}
        </Paper>
      </Box>

      {overview?.storageQuotaBytes != null && (
        <Paper sx={{ p: 2.5, mb: 3 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Espacio de almacenamiento</Typography>
          <LinearProgress
            variant="determinate"
            value={storagePct}
            sx={{ height: 8, borderRadius: 4, mb: 1, bgcolor: "rgba(15, 40, 38, 0.08)" }}
            color={storagePct > 90 ? "error" : storagePct > 70 ? "warning" : "primary"}
          />
          <Typography variant="caption" color="text.secondary">
            {formatBytes(overview.storageUsedBytes)} usados de {formatBytes(overview.storageQuotaBytes)} ({storagePct.toFixed(1)}%)
          </Typography>
        </Paper>
      )}

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1.4fr 1fr" }, gap: 2.5 }}>
        <Paper sx={{ overflow: "hidden" }}>
          <Typography variant="h6" sx={{ fontWeight: 700, px: 2.5, pt: 2.5, pb: 1.5 }}>Historial de documentos procesados</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
          ) : overview?.recentConversions.length === 0 ? (
            <EmptyRow icon={<InboxIcon />} text="Sin conversiones todavía." />
          ) : (
            <Box>
              {overview?.recentConversions.map((c) => {
                const severity = CONVERSION_JOB_STATUS_COLORS[c.status] ?? "default";
                return (
                  <ActivityListRow
                    key={c.jobId}
                    icon={<DescriptionIcon />}
                    iconColor={severityColor(severity)}
                    primary={c.originalFileName}
                    secondary={translateOperationType(c.operationType)}
                    trailing={<Chip size="small" label={translateJobStatus(c.status)} color={severity} />}
                    meta={formatBogotaDateTime(c.createdAtUtc)}
                  />
                );
              })}
            </Box>
          )}
        </Paper>

        <Paper sx={{ overflow: "hidden" }}>
          <Typography variant="h6" sx={{ fontWeight: 700, px: 2.5, pt: 2.5, pb: 1.5 }}>Mi actividad reciente</Typography>
          {historyQuery.isPending ? (
            <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
          ) : historyQuery.data?.items.length === 0 ? (
            <EmptyRow icon={<InfoOutlinedIcon />} text="Sin actividad todavía." />
          ) : (
            <Box>
              {historyQuery.data?.items.map((entry) => {
                const severity = ACCESS_RESULT_COLORS[entry.result] ?? "default";
                return (
                  <ActivityListRow
                    key={entry.id}
                    icon={resultIcon(severity)}
                    iconColor={severityColor(severity)}
                    primary={translateAccessResult(entry.result)}
                    meta={formatBogotaDateTime(entry.occurredAtUtc)}
                  />
                );
              })}
            </Box>
          )}
        </Paper>
      </Box>
    </Box>
  );
}

const PIE_PALETTE = [BRAND_COLORS.teal, BRAND_COLORS.orange, BRAND_COLORS.magenta, "#6A1B9A", "#2E7D32", "#546E7A"];

function Skeleton() {
  return <Box sx={{ width: 90, height: 36, borderRadius: 1, bgcolor: "rgba(15, 40, 38, 0.08)" }} />;
}

// Same "success/error/warning/default" severity vocabulary CONVERSION_JOB_STATUS_COLORS and
// ACCESS_RESULT_COLORS already speak (MUI Chip color names) — this just gives the feed rows'
// icon backgrounds a matching hex instead of a second, disconnected color scale.
function severityColor(severity: "success" | "error" | "warning" | "default"): string {
  switch (severity) {
    case "success": return BRAND_COLORS.teal;
    case "error": return ALERT_COLOR;
    case "warning": return BRAND_COLORS.orange;
    default: return BRAND_COLORS.textLight;
  }
}

// Unlike the document rows (always a file, only the color changes with status), an access-history
// entry's icon SHAPE varies with what kind of event it was — a completed login reads differently
// at a glance from an MFA step or an outright rejection, not just "same icon, different color".
function resultIcon(severity: "success" | "error" | "warning" | "default") {
  switch (severity) {
    case "success": return <CheckCircleIcon />;
    case "error": return <CancelIcon />;
    case "warning": return <ShieldIcon />;
    default: return <InfoOutlinedIcon />;
  }
}

function EmptyRow({ icon, text }: { icon: React.ReactNode; text: string }) {
  return (
    <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, py: 5, color: BRAND_COLORS.textLight }}>
      <Box sx={{ "& svg": { fontSize: 32 } }}>{icon}</Box>
      <Typography variant="body2" color="text.secondary">{text}</Typography>
    </Box>
  );
}
