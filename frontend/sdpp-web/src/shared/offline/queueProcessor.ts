import { ApiError } from "../api/client";
import { deleteIntent, putIntent } from "./db";
import type { QueueIntent } from "./db";
import { useQueueStore } from "./queueStore";
import { submitConversionIntent } from "./submitConversionIntent";
import { submitEnvelopeIntent } from "./submitEnvelopeIntent";

let processing = false;

/** Runs pending/needs-login intents FIFO against the real backend, via the exact same submit*Intent
 * functions the immediate-online path uses — one place that knows how to actually create a
 * conversion/envelope, never two copies of the request logic. Triggered from app boot and on the
 * `online` event (see app/App.tsx); reentrancy-guarded since both can fire close together. */
export async function processQueue(): Promise<void> {
  if (processing) return;
  processing = true;
  try {
    const items = useQueueStore
      .getState()
      .items.filter((i) => i.status === "pending" || i.status === "needs-login")
      .sort((a, b) => a.createdAt - b.createdAt);

    for (const intent of items) {
      if (!navigator.onLine) break; // no point burning through the rest without a connection

      intent.status = "syncing";
      useQueueStore.getState().upsert(intent);

      try {
        if (intent.kind === "conversion") {
          const result = await submitConversionIntent(intent, async (progress) => {
            intent.progress = progress;
            await putIntent(intent);
            useQueueStore.getState().upsert(intent);
          });
          await deleteIntent(intent.id);
          useQueueStore.getState().remove(intent.id);
          useQueueStore.getState().setResult(intent.id, { kind: "conversion", ...result });
        } else {
          const result = await submitEnvelopeIntent(intent, async (progress) => {
            intent.progress = progress;
            await putIntent(intent);
            useQueueStore.getState().upsert(intent);
          });
          await deleteIntent(intent.id);
          useQueueStore.getState().remove(intent.id);
          useQueueStore.getState().setResult(intent.id, { kind: "envelope", ...result });
        }
      } catch (error) {
        const wasNetworkError = await handleIntentFailure(intent, error);
        if (wasNetworkError) break;
      }
    }
  } finally {
    processing = false;
  }
}

/** Returns true when the failure was a real network error (not offline yet when the run started,
 * but the fetch itself failed) — the caller stops the rest of this run in that case, rather than
 * burning through every remaining item against a connection that clearly isn't working. */
async function handleIntentFailure(intent: QueueIntent, error: unknown): Promise<boolean> {
  let wasNetworkError = false;

  if (error instanceof ApiError) {
    if (error.status === 401) {
      intent.status = "needs-login";
      intent.lastError = "La sesión expiró — inicia sesión de nuevo para que se envíe.";
    } else {
      // Validation/business error (e.g. a rejected recipient email, bad operationParameters) —
      // terminal, never auto-retried again.
      intent.status = "failed";
      intent.lastError = error.message;
    }
  } else {
    intent.status = "pending";
    wasNetworkError = true;
  }

  await putIntent(intent);
  useQueueStore.getState().upsert(intent);
  return wasNetworkError;
}
