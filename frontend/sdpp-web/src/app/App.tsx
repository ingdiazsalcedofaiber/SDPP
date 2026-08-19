import { useEffect } from "react";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import CssBaseline from "@mui/material/CssBaseline";
import { ThemeProvider } from "@mui/material/styles";
import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { queryClient } from "./queryClient";
import { router } from "./router";
import { sdppTheme } from "../shared/theme";
import { bootstrapSession, useAuthStore } from "./auth";
import { processQueue } from "../shared/offline/queueProcessor";
import { useQueueStore } from "../shared/offline/queueStore";

export function App() {
  const status = useAuthStore((s) => s.status);

  useEffect(() => {
    // Restores a session from its HttpOnly cookie on every app load (page refresh, reopened tab)
    // — see auth.ts. There is no client-side auto-login anymore; an unauthenticated visitor always
    // lands on /login (see router.tsx's RequireAuth guard).
    void bootstrapSession();

    // Offline queue: load whatever's in IndexedDB (see shared/offline/db.ts), then try to run it
    // right away (a no-op if nothing's pending or the app is offline) — and again every time
    // connectivity comes back, which is what actually makes queued conversions/envelopes "send
    // themselves automatically" per the feature's whole point.
    void useQueueStore.getState().hydrate().then(() => processQueue());
    window.addEventListener("online", () => void processQueue());
  }, []);

  return (
    <ThemeProvider theme={sdppTheme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        {status === "idle" ? (
          <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
            <CircularProgress />
          </Box>
        ) : (
          <RouterProvider router={router} />
        )}
      </QueryClientProvider>
    </ThemeProvider>
  );
}
