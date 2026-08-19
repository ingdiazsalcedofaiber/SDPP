import { putIntent } from "./db";
import type { ConversionIntent, EnvelopeIntent } from "./db";
import { useQueueStore } from "./queueStore";

export async function enqueueConversion(
  payload: Pick<ConversionIntent, "file" | "additionalFiles" | "operationType" | "operationParameters">,
): Promise<string> {
  const intent: ConversionIntent = {
    kind: "conversion",
    id: crypto.randomUUID(),
    createdAt: Date.now(),
    status: "pending",
    progress: {},
    ...payload,
  };
  await putIntent(intent);
  useQueueStore.getState().upsert(intent);
  return intent.id;
}

export async function enqueueEnvelope(
  payload: Pick<EnvelopeIntent, "file" | "title" | "message" | "signingMode" | "dueDateUtc" | "recipients" | "fields">,
): Promise<string> {
  const intent: EnvelopeIntent = {
    kind: "envelope",
    id: crypto.randomUUID(),
    createdAt: Date.now(),
    status: "pending",
    progress: {},
    ...payload,
  };
  await putIntent(intent);
  useQueueStore.getState().upsert(intent);
  return intent.id;
}
