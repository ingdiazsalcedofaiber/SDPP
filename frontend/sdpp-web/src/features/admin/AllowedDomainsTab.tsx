import { useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import FormControlLabel from "@mui/material/FormControlLabel";
import IconButton from "@mui/material/IconButton";
import Paper from "@mui/material/Paper";
import Switch from "@mui/material/Switch";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import DeleteIcon from "@mui/icons-material/Delete";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createAllowedDomain, deleteAllowedDomain, listAllowedDomains } from "../../shared/api/admin";
import { ApiError } from "../../shared/api/client";
import { formatBogotaDateTime } from "../../shared/format";

/** UC "Dominios Permitidos" — controls which email domains Identity.Api accepts at login (see
 * LoginCompletionService.CompleteAsync). isDevOnly entries (e.g. gmail.com) only count while the
 * backend runs in Development AND Identity:AllowDevDomainOverride=true. */
export function AllowedDomainsTab() {
  const [newDomain, setNewDomain] = useState("");
  const [isDevOnly, setIsDevOnly] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const query = useQuery({ queryKey: ["admin", "allowed-domains"], queryFn: listAllowedDomains });

  const createMutation = useMutation({
    mutationFn: () => createAllowedDomain(newDomain.trim(), isDevOnly),
    onSuccess: () => {
      setNewDomain("");
      setError(null);
      queryClient.invalidateQueries({ queryKey: ["admin", "allowed-domains"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "No se pudo agregar el dominio."),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAllowedDomain(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "allowed-domains"] }),
  });

  return (
    <Box>
      <Paper sx={{ p: 2, mb: 2 }}>
        <Typography variant="subtitle2" gutterBottom>Agregar dominio permitido</Typography>
        <Box sx={{ display: "flex", gap: 2, alignItems: "center", flexWrap: "wrap" }}>
          <TextField
            label="Dominio (ej. empresa.com)" size="small" value={newDomain}
            onChange={(e) => setNewDomain(e.target.value)} sx={{ minWidth: 240 }}
          />
          <FormControlLabel
            control={<Switch checked={isDevOnly} onChange={(e) => setIsDevOnly(e.target.checked)} />}
            label="Solo desarrollo (ej. cuentas Gmail personales durante pruebas)"
          />
          <Button variant="contained" disabled={!newDomain.trim() || createMutation.isPending} onClick={() => createMutation.mutate()}>
            Agregar
          </Button>
        </Box>
        {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
      </Paper>

      <Paper>
        {query.isPending ? (
          <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Dominio</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell>Creado</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data?.map((d) => (
                <TableRow key={d.id}>
                  <TableCell>{d.domain}</TableCell>
                  <TableCell>
                    {d.isDevOnly ? <Chip size="small" label="Solo desarrollo" color="warning" /> : <Chip size="small" label="Producción" color="success" />}
                  </TableCell>
                  <TableCell>{formatBogotaDateTime(d.createdAtUtc)}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => deleteMutation.mutate(d.id)} disabled={deleteMutation.isPending}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Paper>
    </Box>
  );
}
