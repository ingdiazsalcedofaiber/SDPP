import { uploadDocument } from "../api/documents";
import { addField, addRecipient, createEnvelope, sendEnvelope } from "../api/signature";
import type { DispatchedRecipient, SigningMode } from "../api/signature";
import type { EnvelopeIntentField, EnvelopeIntentProgress, EnvelopeIntentRecipient } from "./db";

export interface EnvelopeIntentPayload {
  file: File;
  title: string;
  message?: string;
  signingMode: SigningMode;
  dueDateUtc?: string;
  recipients: EnvelopeIntentRecipient[];
  fields: EnvelopeIntentField[];
}

export interface EnvelopeIntentResult {
  envelopeId: string;
  dispatched: DispatchedRecipient[];
}

/** The create→recipients→fields→send sequence EnvelopeEditorPage's per-step mutations used to run
 * one network round trip per user click (see EnvelopeEditorPage.tsx) — now a single orchestrated
 * submit, called both for an immediate online send and by the queue processor's replay
 * (queueProcessor.ts). Resumes from `progress`: never re-creates the envelope, never re-adds a
 * recipient/field that already got a real server id on a previous attempt — see
 * envelopeEditorStore.ts's doc comment on why the server owns ids and db.ts's EnvelopeIntentProgress
 * for what gets checkpointed. `local-<uuid>` ids (EnvelopeIntentRecipient.localId /
 * EnvelopeIntentField.recipientLocalId) are resolved to real recipientIds via progress.recipientIdMap. */
export async function submitEnvelopeIntent(
  intent: EnvelopeIntentPayload & { progress: EnvelopeIntentProgress },
  onProgress?: (progress: EnvelopeIntentProgress) => Promise<void> | void,
): Promise<EnvelopeIntentResult> {
  const progress: EnvelopeIntentProgress = { ...intent.progress };

  if (!progress.sourceDocumentId) {
    const uploaded = await uploadDocument(intent.file);
    progress.sourceDocumentId = uploaded.documentId;
    await onProgress?.(progress);
  }

  if (!progress.envelopeId) {
    const created = await createEnvelope({
      sourceDocumentId: progress.sourceDocumentId,
      title: intent.title,
      message: intent.message,
      signingMode: intent.signingMode,
      dueDateUtc: intent.dueDateUtc,
    });
    progress.envelopeId = created.envelopeId;
    await onProgress?.(progress);
  }

  progress.recipientIdMap ??= {};
  for (const recipient of intent.recipients) {
    if (progress.recipientIdMap[recipient.localId]) continue;
    const result = await addRecipient(progress.envelopeId, recipient.email, recipient.fullName, recipient.order, recipient.inPerson);
    progress.recipientIdMap[recipient.localId] = result.recipientId;
    await onProgress?.(progress);
  }

  progress.fieldIdsAdded ??= [];
  for (const field of intent.fields) {
    if (progress.fieldIdsAdded.includes(field.localId)) continue;
    const recipientId = progress.recipientIdMap[field.recipientLocalId];
    await addField(progress.envelopeId, {
      recipientId,
      type: field.type,
      pageNumber: field.pageNumber,
      positionX: field.positionX,
      positionY: field.positionY,
      width: field.width,
      height: field.height,
      required: field.required,
    });
    progress.fieldIdsAdded.push(field.localId);
    await onProgress?.(progress);
  }

  const sendResult = await sendEnvelope(progress.envelopeId);
  progress.sent = true;
  await onProgress?.(progress);
  return { envelopeId: sendResult.envelopeId, dispatched: sendResult.dispatched };
}
