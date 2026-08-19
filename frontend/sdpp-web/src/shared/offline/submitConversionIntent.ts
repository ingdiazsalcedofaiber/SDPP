import { requestConversion, uploadDocument } from "../api/documents";
import type { ConversionIntent, ConversionIntentProgress } from "./db";

export interface ConversionIntentPayload {
  file: File;
  additionalFiles: File[];
  operationType: ConversionIntent["operationType"];
  operationParameters: Record<string, string>;
}

export interface ConversionIntentResult {
  documentId: string;
  jobId: string;
}

/** The exact upload→convert sequence ConvertWizardPage's convertMutation used to run inline (see
 * ConvertWizardPage.tsx) — extracted so both the immediate online submit and the queue processor's
 * replay (queueProcessor.ts) call this one function, never two copies of the same request logic.
 * `onProgress`, when given, is used by the queue processor to checkpoint the main file's uploaded
 * documentId before requesting the conversion, so a retry after a network blip doesn't re-upload
 * it. Additional files (Merge) are deliberately NOT checkpointed per-file — re-uploading a couple
 * of small extra files on a retry is a harmless, low-cost trade-off against the complexity of
 * tracking partial progress through them. */
export async function submitConversionIntent(
  intent: ConversionIntentPayload & { progress: ConversionIntentProgress },
  onProgress?: (progress: ConversionIntentProgress) => Promise<void> | void,
): Promise<ConversionIntentResult> {
  let documentId = intent.progress.documentId;

  if (!documentId) {
    const uploaded = await uploadDocument(intent.file);
    documentId = uploaded.documentId;
    await onProgress?.({ documentId });
  }

  const additionalDocumentIds: string[] = [];
  for (const extra of intent.additionalFiles) {
    const extraUploaded = await uploadDocument(extra);
    additionalDocumentIds.push(extraUploaded.documentId);
  }

  const finalParams =
    additionalDocumentIds.length > 0
      ? { ...intent.operationParameters, additionalDocumentIds: additionalDocumentIds.join(",") }
      : intent.operationParameters;

  const result = await requestConversion({ documentId, operationType: intent.operationType, operationParameters: finalParams });
  return { documentId, jobId: result.jobId };
}
