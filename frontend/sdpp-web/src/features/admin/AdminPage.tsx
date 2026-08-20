import { useState } from "react";
import Box from "@mui/material/Box";
import Tab from "@mui/material/Tab";
import Tabs from "@mui/material/Tabs";
import Typography from "@mui/material/Typography";
import { UsersTab } from "./UsersTab";
import { AccessHistoryTab } from "./AccessHistoryTab";
import { AllowedDomainsTab } from "./AllowedDomainsTab";

// UC "Panel Administrativo" — gated behind the Administrador role both here (UX, see
// AppShell.tsx's requiresRole filter) and authoritatively at every /api/v1/admin/* endpoint
// (Identity.Api's "Administrador" RequireRole policy).
export function AdminPage() {
  const [tab, setTab] = useState(0);

  return (
    <Box>
      <Typography variant="h5" gutterBottom>
        Administración
      </Typography>

      <Tabs
        value={tab}
        onChange={(_, value: number) => setTab(value)}
        variant="scrollable"
        scrollButtons="auto"
        allowScrollButtonsMobile
        sx={{ mb: 2 }}
      >
        <Tab label="Usuarios" />
        <Tab label="Historial de accesos" />
        <Tab label="Dominios permitidos" />
      </Tabs>

      {tab === 0 && <UsersTab />}
      {tab === 1 && <AccessHistoryTab />}
      {tab === 2 && <AllowedDomainsTab />}
    </Box>
  );
}
