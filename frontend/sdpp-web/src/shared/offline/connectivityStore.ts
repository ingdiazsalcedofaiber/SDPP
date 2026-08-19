import { create } from "zustand";

interface ConnectivityState {
  isOnline: boolean;
}

/** Seeded from navigator.onLine, kept live via the online/offline window events registered once
 * below at module load — same "register a listener at import time" idiom as
 * registerUnauthorizedHandler in shared/api/client.ts. Deliberately knows nothing about the
 * offline queue (see queueProcessor.ts for what reacts to this) — single responsibility. */
export const useConnectivityStore = create<ConnectivityState>(() => ({
  isOnline: navigator.onLine,
}));

window.addEventListener("online", () => useConnectivityStore.setState({ isOnline: true }));
window.addEventListener("offline", () => useConnectivityStore.setState({ isOnline: false }));
