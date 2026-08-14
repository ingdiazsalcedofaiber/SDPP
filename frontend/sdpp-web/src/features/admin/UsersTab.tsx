import { Fragment, useState } from "react";
import Avatar from "@mui/material/Avatar";
import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import IconButton from "@mui/material/IconButton";
import MenuItem from "@mui/material/MenuItem";
import Paper from "@mui/material/Paper";
import Select from "@mui/material/Select";
import Switch from "@mui/material/Switch";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import Collapse from "@mui/material/Collapse";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { listAllowedDomains, listRoles, listUserSessions, listUsers, updateUserRoles, updateUserStatus } from "../../shared/api/admin";
import { formatBogotaDateTime } from "../../shared/format";
import { useAuthStore } from "../../app/auth";

export function UsersTab() {
  const currentUserId = useAuthStore((s) => s.user?.id);
  const [search, setSearch] = useState("");
  const [domain, setDomain] = useState("");
  const [expandedUserId, setExpandedUserId] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const usersQuery = useQuery({
    queryKey: ["admin", "users", search, domain],
    queryFn: () => listUsers({ search: search || undefined, domain: domain || undefined, page: 1, pageSize: 50 }),
  });

  const rolesQuery = useQuery({ queryKey: ["admin", "roles"], queryFn: listRoles });
  const domainsQuery = useQuery({ queryKey: ["admin", "allowed-domains"], queryFn: listAllowedDomains });

  const rolesMutation = useMutation({
    mutationFn: ({ userId, roleIds }: { userId: string; roleIds: string[] }) => updateUserRoles(userId, roleIds),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "users"] }),
  });

  const statusMutation = useMutation({
    mutationFn: ({ userId, active }: { userId: string; active: boolean }) => updateUserStatus(userId, active),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "users"] }),
  });

  return (
    <Box>
      <Box sx={{ display: "flex", gap: 2, mb: 2, flexWrap: "wrap" }}>
        <TextField label="Buscar por nombre o correo" size="small" value={search} onChange={(e) => setSearch(e.target.value)} sx={{ minWidth: 260 }} />
        <TextField
          select label="Filtrar por dominio" size="small" value={domain} onChange={(e) => setDomain(e.target.value)} sx={{ minWidth: 200 }}
        >
          <MenuItem value="">Todos los dominios</MenuItem>
          {domainsQuery.data?.map((d) => (
            <MenuItem key={d.id} value={d.domain}>{d.domain}</MenuItem>
          ))}
        </TextField>
      </Box>

      {(usersQuery.isPending || rolesMutation.isError || statusMutation.isError) && rolesMutation.isError && (
        <Typography color="error" variant="body2" sx={{ mb: 1 }}>No se pudo actualizar los roles.</Typography>
      )}

      <Paper>
        {usersQuery.isPending ? (
          <Box sx={{ p: 3, display: "flex", justifyContent: "center" }}><CircularProgress size={24} /></Box>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell />
                <TableCell>Usuario</TableCell>
                <TableCell>Dominio</TableCell>
                <TableCell>Roles</TableCell>
                <TableCell>Último acceso</TableCell>
                <TableCell>Activo</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {usersQuery.data?.items.map((user) => (
                <Fragment key={user.id}>
                  <TableRow>
                    <TableCell>
                      <IconButton size="small" onClick={() => setExpandedUserId(expandedUserId === user.id ? null : user.id)}>
                        <ExpandMoreIcon
                          fontSize="small"
                          sx={{ transform: expandedUserId === user.id ? "rotate(180deg)" : "none", transition: "transform 0.15s" }}
                        />
                      </IconButton>
                    </TableCell>
                    <TableCell>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <Avatar src={user.photoUrl ?? undefined} sx={{ width: 28, height: 28 }}>
                          {user.fullName.charAt(0)}
                        </Avatar>
                        <Box>
                          <Typography variant="body2">{user.fullName}</Typography>
                          <Typography variant="caption" color="text.secondary">{user.email}</Typography>
                        </Box>
                      </Box>
                    </TableCell>
                    <TableCell>{user.domain}</TableCell>
                    <TableCell sx={{ minWidth: 220 }}>
                      <Select
                        multiple
                        size="small"
                        value={user.roles}
                        fullWidth
                        onChange={(e) => {
                          const selectedNames = typeof e.target.value === "string" ? e.target.value.split(",") : e.target.value;
                          const roleIds = (rolesQuery.data ?? []).filter((r) => selectedNames.includes(r.name)).map((r) => r.id);
                          rolesMutation.mutate({ userId: user.id, roleIds });
                        }}
                        renderValue={(selected) => (
                          <Box sx={{ display: "flex", gap: 0.5, flexWrap: "wrap" }}>
                            {(selected as string[]).map((name) => <Chip key={name} label={name} size="small" />)}
                          </Box>
                        )}
                      >
                        {rolesQuery.data?.map((role) => (
                          <MenuItem key={role.id} value={role.name}>{role.name}</MenuItem>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell>{user.lastLoginAtUtc ? formatBogotaDateTime(user.lastLoginAtUtc) : "Nunca"}</TableCell>
                    <TableCell>
                      <Tooltip title={user.id === currentUserId ? "No puedes desactivar tu propia cuenta" : ""}>
                        <span>
                          <Switch
                            checked={user.active}
                            disabled={user.id === currentUserId}
                            onChange={(e) => statusMutation.mutate({ userId: user.id, active: e.target.checked })}
                          />
                        </span>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell colSpan={6} sx={{ p: 0, borderBottom: expandedUserId === user.id ? undefined : "none" }}>
                      <Collapse in={expandedUserId === user.id} unmountOnExit>
                        <UserSessionsPanel userId={user.id} />
                      </Collapse>
                    </TableCell>
                  </TableRow>
                </Fragment>
              ))}
            </TableBody>
          </Table>
        )}
      </Paper>
    </Box>
  );
}

function UserSessionsPanel({ userId }: { userId: string }) {
  const sessionsQuery = useQuery({ queryKey: ["admin", "sessions", userId], queryFn: () => listUserSessions(userId) });

  if (sessionsQuery.isPending) {
    return <Box sx={{ p: 2 }}><CircularProgress size={18} /></Box>;
  }

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover" }}>
      <Typography variant="subtitle2" gutterBottom>Sesiones</Typography>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>IP</TableCell>
            <TableCell>Navegador</TableCell>
            <TableCell>Sistema operativo</TableCell>
            <TableCell>Inicio</TableCell>
            <TableCell>Estado</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {sessionsQuery.data?.map((s) => (
            <TableRow key={s.id}>
              <TableCell>{s.ipAddress ?? "—"}</TableCell>
              <TableCell sx={{ maxWidth: 260, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{s.userAgent ?? "—"}</TableCell>
              <TableCell>{s.operatingSystem ?? "—"}</TableCell>
              <TableCell>{formatBogotaDateTime(s.createdAtUtc)}</TableCell>
              <TableCell>
                <Chip size="small" label={s.isActive ? "Activa" : s.revokedAtUtc ? "Revocada" : "Expirada"} color={s.isActive ? "success" : "default"} />
              </TableCell>
            </TableRow>
          ))}
          {sessionsQuery.data?.length === 0 && (
            <TableRow><TableCell colSpan={5}><Typography variant="caption" color="text.secondary">Sin sesiones registradas.</Typography></TableCell></TableRow>
          )}
        </TableBody>
      </Table>
    </Box>
  );
}
