import { create } from "zustand";
import { getAllIntents } from "./db";
import type { IntentResult, QueueIntent } from "./db";

interface QueueState {
  items: QueueIntent[];
  // Short-lived: the queue processor drops a real result here right before deleting the intent
  // (see queueProcessor.ts), and the originating wizard/editor page — if still mounted with a
  // matching queuedIntentId — consumes it once to flip its own local state from "en espera" to
  // the real result, then clears it. Not meant to be a durable history of past syncs.
  results: Record<string, IntentResult>;
  hydrate: () => Promise<void>;
  upsert: (intent: QueueIntent) => void;
  remove: (id: string) => void;
  setResult: (id: string, result: IntentResult) => void;
  clearResult: (id: string) => void;
}

/** In-memory mirror of the IndexedDB `intents` store (see db.ts) so React components can react to
 * it — neither the AppShell indicator nor the wizard/editor pages talk to db.ts directly. */
export const useQueueStore = create<QueueState>((set) => ({
  items: [],
  results: {},
  hydrate: async () => {
    const items = await getAllIntents();
    set({ items });
  },
  upsert: (intent) =>
    set((s) => ({
      items: [...s.items.filter((i) => i.id !== intent.id), intent].sort((a, b) => a.createdAt - b.createdAt),
    })),
  remove: (id) => set((s) => ({ items: s.items.filter((i) => i.id !== id) })),
  setResult: (id, result) => set((s) => ({ results: { ...s.results, [id]: result } })),
  clearResult: (id) =>
    set((s) => ({ results: Object.fromEntries(Object.entries(s.results).filter(([key]) => key !== id)) })),
}));
