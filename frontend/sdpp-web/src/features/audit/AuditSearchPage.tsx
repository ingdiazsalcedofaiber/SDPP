import { useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useMutation, useQuery } from "@tanstack/react-query";
import { apiClient, ApiError } from "../../shared/api/client";
import { findDocumentByHash } from "../../shared/api/classification";
import { useAuthStore } from "../../app/auth";
import { formatBogotaDateTime } from "../../shared/format";

interface AuditRecordDto {
  id: number;
  occurredAtUtc: string;
  eventType: string;
  actorFullName: string;
  actorEmail: string;
  actorIp?: string;
  subjectDocumentId?: string;
  recordHash: string;
}

interface QueryTraceResult {
  items: AuditRecordDto[];
  totalCount: number;
  chainValid: boolean;
}

// UC-05 (docs/04-use-cases/use-cases.md) — restricted to Auditor/Administrador both here (UX)
// and, authoritatively, at the Audit API's authorization policy (docs/05-security/rbac-matrix.md).
export function AuditSearchPage() {
  const hasRole = useAuthStore((s) => s.hasRole);
  const [documentId, setDocumentId] = useState("");
  const [eventType, setEventType] = useState("");
  const [hash, setHash] = useState("");
  const [searchDocumentId, setSearchDocumentId] = useState<string | undefined>(undefined);
  const [enabled, setEnabled] = useState(false);
  const [hashError, setHashError] = useState<string | null>(null);

  // El hash SHA-256 de un documento vive en el módulo Classification (DocumentIntegrityRecord),
  // no en el registro de auditoría — buscar "por hash" primero resuelve a qué documento
  // pertenece ese hash, y luego reutiliza el filtro por documentId que ya existía y funcionaba.
  const hashLookupMutation = useMutation({ mutationFn: (value: string) => findDocumentByHash(value) });

  const query = useQuery({
    queryKey: ["auditTrace", searchDocumentId, eventType],
    queryFn: () =>
      apiClient.get<QueryTraceResult>(
        `/api/v1/audit/records?${new URLSearchParams({
          ...(searchDocumentId ? { documentId: searchDocumentId } : {}),
          ...(eventType ? { eventType } : {}),
        }).toString()}`,
      ),
    enabled,
  });

  const handleSearch = async () => {
    setHashError(null);
    setEnabled(false);

    if (hash.trim()) {
      try {
        const result = await hashLookupMutation.mutateAsync(hash.trim());
        setSearchDocumentId(result.documentId);
        setEnabled(true);
      } catch (err) {
        setSearchDocumentId(undefined);
        setHashError(
          err instanceof ApiError && err.problem?.errorCode === "HASH_NOT_FOUND"
            ? "No se encontró ningún documento con ese hash."
            : err instanceof ApiError
              ? err.message
              : "No se pudo buscar por hash.",
        );
      }
      return;
    }

    setSearchDocumentId(documentId.trim() || undefined);
    setEnabled(true);
  };

  if (!hasRole("Auditor") && !hasRole("Administrador")) {
    return (
      <Alert severity="warning">
        Esta sección requiere el rol Auditor o Administrador (docs/05-security/rbac-matrix.md).
      </Alert>
    );
  }

  return (
    <Box>
      <Typography variant="h5" gutterBottom>
        Trazabilidad y auditoría
      </Typography>

      <Paper sx={{ p: 2, mb: 2, display: "flex", gap: 2, alignItems: "center", flexWrap: "wrap" }}>
        <TextField
          label="Hash SHA-256 del documento"
          value={hash}
          onChange={(e) => setHash(e.target.value)}
          size="small"
          sx={{ minWidth: 320 }}
          helperText="Coincidencia exacta — 64 caracteres hexadecimales"
        />
        <TextField label="Document ID" value={documentId} onChange={(e) => setDocumentId(e.target.value)} size="small" disabled={!!hash.trim()} />
        <TextField label="Tipo de evento" value={eventType} onChange={(e) => setEventType(e.target.value)} size="small" />
        <Button variant="contained" onClick={handleSearch} disabled={hashLookupMutation.isPending}>
          {hashLookupMutation.isPending ? <CircularProgress size={20} /> : "Buscar"}
        </Button>
      </Paper>

      {hashError && <Alert severity="warning" sx={{ mb: 2 }}>{hashError}</Alert>}

      {query.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {query.error instanceof ApiError ? query.error.message : "No se pudo consultar el registro de auditoría."}
        </Alert>
      )}

      {query.data && (
        <Alert severity={query.data.chainValid ? "success" : "error"} sx={{ mb: 2 }}>
          Integridad de la cadena de hash en esta página de resultados:{" "}
          {query.data.chainValid ? "válida" : "¡ROTA! — ver docs/05-security/audit-and-traceability.md §2"}
        </Alert>
      )}

      {query.data && query.data.items.length === 0 && (
        <Alert severity="info" sx={{ mb: 2 }}>No se encontraron registros para esta búsqueda.</Alert>
      )}

      {query.data && query.data.items.length > 0 && (
        <Paper>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha (UTC)</TableCell>
                <TableCell>Evento</TableCell>
                <TableCell>Actor</TableCell>
                <TableCell>IP</TableCell>
                <TableCell>Documento</TableCell>
                <TableCell>Hash</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data.items.map((record) => (
                <TableRow key={record.id}>
                  <TableCell>{formatBogotaDateTime(record.occurredAtUtc)}</TableCell>
                  <TableCell><Chip label={record.eventType} size="small" /></TableCell>
                  <TableCell>{record.actorFullName}</TableCell>
                  <TableCell>{record.actorIp ?? "—"}</TableCell>
                  <TableCell>{record.subjectDocumentId ?? "—"}</TableCell>
                  <TableCell sx={{ fontFamily: "monospace", fontSize: 11 }}>
                    {record.recordHash.slice(0, 12)}…
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      )}
    </Box>
  );
}
