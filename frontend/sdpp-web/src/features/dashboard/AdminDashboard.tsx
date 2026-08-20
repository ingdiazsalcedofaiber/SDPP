import { useMemo, useState } from "react";
import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import GroupIcon from "@mui/icons-material/Group";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import StorageIcon from "@mui/icons-material/Storage";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import { useQuery } from "@tanstack/react-query";
import { BarChart } from "@mui/x-charts/BarChart";
import { LineChart } from "@mui/x-charts/LineChart";
import { BRAND_COLORS } from "../../shared/theme";
import { formatBytes, formatPeriodLabel, formatBogotaDateTime } from "../../shared/format";
import { getAdminDocumentsReport, getAdminUsersReport } from "../../shared/api/reporting";
import { listAccessHistory } from "../../shared/api/admin";
import {
  ACCESS_RESULT_COLORS,
  CONVERSION_JOB_STATUS_COLORS,
  translateAccessResult,
  translateJobStatus,
  translateOperationType,
} from "../../shared/translations";
import { StatCard } from "./StatCard";
import { DateRangeFilter, DEFAULT_DATE_RANGE, resolveDateRange, type DateRangeValue } from "./DateRangeFilter";

const ALERT_COLOR = "#E53935";
const ADMIN_ACCENT = "#6A1B9A";

// Platform-wide view: every query here forces ownerId=null / no userId filter on the backend and
// is gated by the "Administrador" policy — see ReportingEndpoints.cs / AdminEndpoints.cs. Never
// shares a query with PersonalDashboard's scoped calls.
export function AdminDashboard() {
  const [range, setRange] = useState<DateRangeValue>(DEFAULT_DATE_RANGE);
  const resolved = useMemo(() => resolveDateRange(range), [range]);

  const docsQuery = useQuery({
    queryKey: ["reporting", "admin", "documents", resolved.from.toISOString(), resolved.to.toISOString(), resolved.bucket],
    queryFn: () => getAdminDocumentsReport(resolved),
  });

  const usersQuery = useQuery({
    queryKey: ["reporting", "admin", "users", resolved.from.toISOString(), resolved.to.toISOString(), resolved.bucket],
    queryFn: () => getAdminUsersReport(resolved),
  });

  const accessHistoryQuery = useQuery({
    queryKey: ["reporting", "admin", "access-history"],
    queryFn: () => listAccessHistory({ page: 1, pageSize: 8 }),
  });

  const docs = docsQuery.data;
  const users = usersQuery.data;

  const statusDataset: Array<{ [key: string]: string | number; status: string; count: number }> = docs
    ? [
        { status: "Completadas", count: docs.conversionsByStatus.completed },
        { status: "En proceso", count: docs.conversionsByStatus.inProgress },
        { status: "Con error", count: docs.conversionsByStatus.failed },
      ]
    : [];

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 2, mb: 3 }}>
        <Typography variant="h5">Panel de administración</Typography>
        <DateRangeFilter value={range} onChange={setRange} />
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(5, 1fr)" }, gap: 2.5, mb: 3 }}>
        <StatCard label="Usuarios registrados" value={users?.totalUsers ?? 0} icon={<GroupIcon />} color={BRAND_COLORS.teal} loading={usersQuery.isPending} />
        <StatCard label="Usuarios activos" value={users?.activeUsers ?? 0} icon={<GroupIcon />} color={ADMIN_ACCENT} loading={usersQuery.isPending} />
        <StatCard label="Conversiones totales" value={docs?.totalConversions ?? 0} icon={<SwapHorizIcon />} color={BRAND_COLORS.orange} loading={docsQuery.isPending} />
        <StatCard
          label="Almacenamiento usado"
          value={docs ? formatBytes(docs.storageUsedBytes) : "—"}
          icon={<StorageIcon />}
          color={BRAND_COLORS.magenta}
          loading={docsQuery.isPending}
        />
        <StatCard
          label="Conversiones con error"
          value={docs?.conversionsByStatus.failed ?? 0}
          icon={<ErrorOutlineIcon />}
          color={ALERT_COLOR}
          loading={docsQuery.isPending}
        />
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2.5, mb: 3 }}>
        <Paper sx={{ p: 2.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Estado de las conversiones</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : (
            <BarChart
              dataset={statusDataset}
              xAxis={[{ dataKey: "status", scaleType: "band" }]}
              series={[{ dataKey: "count", label: "Conversiones", color: BRAND_COLORS.teal }]}
              height={260}
            />
          )}
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Documentos procesados por tipo</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : (
            <BarChart
              dataset={docs?.documentsByContentType ?? []}
              xAxis={[{ dataKey: "label", scaleType: "band" }]}
              series={[{ dataKey: "count", label: "Documentos", color: BRAND_COLORS.orange }]}
              height={260}
            />
          )}
        </Paper>
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2.5, mb: 3 }}>
        <Paper sx={{ p: 2.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Documentos procesados (día / semana / mes)</Typography>
          {docsQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : (
            <LineChart
              dataset={docs?.timeSeries ?? []}
              xAxis={[{ dataKey: "periodStart", scaleType: "band", valueFormatter: (v: string) => formatPeriodLabel(v, resolved.bucket) }]}
              series={[{ dataKey: "count", label: "Conversiones", color: BRAND_COLORS.magenta, area: true }]}
              height={260}
            />
          )}
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>Nuevos usuarios registrados</Typography>
          {usersQuery.isPending ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={24} /></Box>
          ) : (
            <LineChart
              dataset={users?.newUsersTimeSeries ?? []}
              xAxis={[{ dataKey: "periodStart", scaleType: "band", valueFormatter: (v: string) => formatPeriodLabel(v, resolved.bucket) }]}
              series={[{ dataKey: "count", label: "Nuevos usuarios", color: ADMIN_ACCENT, area: true }]}
              height={260}
            />
          )}
        </Paper>
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1.4fr 1fr" }, gap: 2.5 }}>
        <Paper sx={{ overflowX: "auto" }}>
          <Typography variant="body2" sx={{ fontWeight: 600, p: 2.5, pb: 1 }}>Conversiones recientes (toda la plataforma)</Typography>
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
                {docs?.recentConversions.map((c) => (
                  <TableRow key={c.jobId}>
                    <TableCell>{c.originalFileName}</TableCell>
                    <TableCell>{translateOperationType(c.operationType)}</TableCell>
                    <TableCell>
                      <Chip size="small" label={translateJobStatus(c.status)} color={CONVERSION_JOB_STATUS_COLORS[c.status] ?? "default"} />
                    </TableCell>
                    <TableCell>{formatBogotaDateTime(c.createdAtUtc)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Paper>

        <Paper sx={{ overflowX: "auto" }}>
          <Typography variant="body2" sx={{ fontWeight: 600, p: 2.5, pb: 1 }}>Actividad reciente de usuarios</Typography>
          {accessHistoryQuery.isPending ? (
            <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Usuario</TableCell>
                  <TableCell>Resultado</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {accessHistoryQuery.data?.items.map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell>
                      <Typography variant="body2">{entry.fullNameSnapshot}</Typography>
                      <Typography variant="caption" color="text.secondary">{formatBogotaDateTime(entry.occurredAtUtc)}</Typography>
                    </TableCell>
                    <TableCell>
                      <Chip size="small" label={translateAccessResult(entry.result)} color={ACCESS_RESULT_COLORS[entry.result] ?? "default"} />
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
