import { useState } from "react";
import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import { useQuery } from "@tanstack/react-query";
import { listAccessHistory } from "../../shared/api/admin";
import { ACCESS_RESULT_COLORS, translateAccessResult } from "../../shared/translations";
import { formatBogotaDateTime } from "../../shared/format";

export function AccessHistoryTab() {
  const [email, setEmail] = useState("");

  const query = useQuery({
    queryKey: ["admin", "access-history", email],
    queryFn: () => listAccessHistory({ email: email || undefined, page: 1, pageSize: 50 }),
  });

  return (
    <Box>
      <TextField label="Filtrar por correo" size="small" value={email} onChange={(e) => setEmail(e.target.value)} sx={{ mb: 2, minWidth: 280 }} />

      <Paper>
        {query.isPending ? (
          <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell>
                <TableCell>Usuario</TableCell>
                <TableCell>Correo</TableCell>
                <TableCell>IP</TableCell>
                <TableCell>Proveedor</TableCell>
                <TableCell>Resultado</TableCell>
                <TableCell>Duración</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data?.items.map((entry) => (
                <TableRow key={entry.id}>
                  <TableCell>{formatBogotaDateTime(entry.occurredAtUtc)}</TableCell>
                  <TableCell>{entry.fullNameSnapshot}</TableCell>
                  <TableCell>{entry.email}</TableCell>
                  <TableCell>{entry.ipAddress ?? "—"}</TableCell>
                  <TableCell>{entry.provider}</TableCell>
                  <TableCell>
                    <Chip size="small" label={translateAccessResult(entry.result)} color={ACCESS_RESULT_COLORS[entry.result] ?? "default"} />
                  </TableCell>
                  <TableCell>{entry.durationMs} ms</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Paper>
    </Box>
  );
}
