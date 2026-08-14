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
import DescriptionIcon from "@mui/icons-material/Description";
import StorageIcon from "@mui/icons-material/Storage";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import TransformIcon from "@mui/icons-material/Transform";
import FactCheckIcon from "@mui/icons-material/FactCheck";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { BarChart } from "@mui/x-charts/BarChart";
import { LineChart } from "@mui/x-charts/LineChart";
import { useAuthStore } from "../../app/auth";
import { BRAND_COLORS } from "../../shared/theme";
import { formatBytes, formatPeriodLabel, formatBogotaDateTime } from "../../shared/format";
import { getMyAccessHistory, getPersonalDocumentsReport } from "../../shared/api/reporting";
import {
  CONVERSION_JOB_STATUS_COLORS,
  translateAccessResult,
  translateJobStatus,
  translateOperationType,
} from "../../shared/translations";
import { StatCard } from "./StatCard";
import { DateRangeFilter, DEFAULT_DATE_RANGE, resolveDateRange, type DateRangeValue } from "./DateRangeFilter";

const ALERT_COLOR = "#E53935";

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

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 2, mb: 3 }}>
        <Typography variant="h5">Mi panel</Typography>
        <DateRangeFilter value={range} onChange={setRange} />
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" }, gap: 2.5, mb: 3 }}>
        <StatCard
          label="Conversiones totales"
          value={overview?.totalConversions ?? 0}
          icon={<SwapHorizIcon />}
          color={BRAND_COLORS.teal}
          loading={docsQuery.isPending}
        />
        <StatCard
          label="Documentos guardados"
          value={overview?.totalDocuments ?? 0}
          icon={<DescriptionIcon />}
          color={BRAND_COLORS.orange}
          loading={docsQuery.isPending}
        />
        <StatCard
          label="Almacenamiento usado"
          value={overview ? formatBytes(overview.storageUsedBytes) : "—"}
          subtitle={overview?.storageQuotaBytes ? `de ${formatBytes(overview.storageQuotaBytes)} disponibles` : undefined}
          icon={<StorageIcon />}
          color={BRAND_COLORS.magenta}
          loading={docsQuery.isPending}
        />
        <StatCard
          label="Conversiones con error"
          value={overview?.conversionsByStatus.failed ?? 0}
          icon={<ErrorOutlineIcon />}
          color={ALERT_COLOR}
          loading={docsQuery.isPending}
        />
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

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2.5, mb: 3 }}>
        <Paper sx={{ p: 2.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Conversiones por tipo de operación</Typography>
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
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Documentos por tipo de archivo</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : (
            <BarChart
              dataset={overview?.documentsByContentType ?? []}
              xAxis={[{ dataKey: "label", scaleType: "band" }]}
              series={[{ dataKey: "count", label: "Documentos", color: BRAND_COLORS.orange }]}
              height={260}
            />
          )}
        </Paper>
      </Box>

      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Conversiones en el tiempo</Typography>
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

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1.4fr 1fr" }, gap: 2.5 }}>
        <Paper>
          <Typography variant="body2" sx={{ fontWeight: 600, p: 2.5, pb: 1 }}>Historial de documentos procesados</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Archivo</TableCell>
                  <TableCell>Operación</TableCell>
                  <TableCell>Estado</TableCell>
                  <TableCell>Fecha</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {overview?.recentConversions.map((c) => (
                  <TableRow key={c.jobId}>
                    <TableCell>{c.originalFileName}</TableCell>
                    <TableCell>{translateOperationType(c.operationType)}</TableCell>
                    <TableCell>
                      <Chip size="small" label={translateJobStatus(c.status)} color={CONVERSION_JOB_STATUS_COLORS[c.status] ?? "default"} />
                    </TableCell>
                    <TableCell>{formatBogotaDateTime(c.createdAtUtc)}</TableCell>
                  </TableRow>
                ))}
                {overview?.recentConversions.length === 0 && (
                  <TableRow><TableCell colSpan={4}><Typography variant="body2" color="text.secondary" sx={{ py: 2, textAlign: "center" }}>Sin conversiones todavía.</Typography></TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          )}
        </Paper>

        <Paper>
          <Typography variant="body2" sx={{ fontWeight: 600, p: 2.5, pb: 1 }}>Mi actividad reciente</Typography>
          {historyQuery.isPending ? (
            <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Fecha</TableCell>
                  <TableCell>Resultado</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {historyQuery.data?.items.map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell>{formatBogotaDateTime(entry.occurredAtUtc)}</TableCell>
                    <TableCell>
                      <Chip size="small" label={translateAccessResult(entry.result)} color={entry.result === "Success" ? "success" : "warning"} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Paper>
      </Box>
    </Box>
  );
}
