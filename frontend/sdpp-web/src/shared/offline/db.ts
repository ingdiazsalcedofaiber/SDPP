import { openDB } from "idb";
import type { DBSchema, IDBPDatabase } from "idb";
import type { OperationType } from "../api/types";
import type { DispatchedRecipient, FieldType, SigningMode } from "../api/signature";

export type IntentStatus = "pending" | "syncing" | "needs-login" | "failed";

export interface ConversionIntentProgress {
  documentId?: string;
}

export interface ConversionIntent {
  kind: "conversion";
  id: string;
  createdAt: number;
  status: IntentStatus;
  lastError?: string;
  file: File;
  additionalFiles: File[];
  operationType: OperationType;
  operationParameters: Record<string, string>;
  progress: ConversionIntentProgress;
}

export interface EnvelopeIntentRecipient {
  localId: string;
  email: string;
  fullName: string;
  order: number;
}

export interface EnvelopeIntentField {
  localId: string;
  recipientLocalId: string;
  type: FieldType;
  pageNumber: number;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  required: boolean;
}

// Checkpoints of what's already been done server-side — the piece that makes a retry after a
// network blip mid-sequence resumable instead of destructive (never re-creates the envelope, never
// re-adds a recipient/field that already has a real id). See submitEnvelopeIntent.ts.
export interface EnvelopeIntentProgress {
  sourceDocumentId?: string;
  envelopeId?: string;
  recipientIdMap?: Record<string, string>;
  fieldIdsAdded?: string[];
  sent?: boolean;
}

export interface EnvelopeIntent {
  kind: "envelope";
  id: string;
  createdAt: number;
  status: IntentStatus;
  lastError?: string;
  file: File;
  title: string;
  message?: string;
  signingMode: SigningMode;
  dueDateUtc?: string;
  recipients: EnvelopeIntentRecipient[];
  fields: EnvelopeIntentField[];
  progress: EnvelopeIntentProgress;
}

export type QueueIntent = ConversionIntent | EnvelopeIntent;

export interface ConversionIntentResult {
  kind: "conversion";
  documentId: string;
  jobId: string;
}

export interface EnvelopeIntentResult {
  kind: "envelope";
  envelopeId: string;
  dispatched: DispatchedRecipient[];
}

export type IntentResult = ConversionIntentResult | EnvelopeIntentResult;

interface OfflineDB extends DBSchema {
  intents: {
    key: string;
    value: QueueIntent;
  };
}

let dbPromise: Promise<IDBPDatabase<OfflineDB>> | null = null;

// IndexedDB natively structured-clones File/Blob (Chromium) — the queue's target browsers, per the
// "PWA install icon" reference the user gave, are Chrome/Edge on an intranet, so this isn't a
// cross-browser concern here.
function getDb(): Promise<IDBPDatabase<OfflineDB>> {
  dbPromise ??= openDB<OfflineDB>("sdpp-offline", 1, {
    upgrade(db) {
      db.createObjectStore("intents", { keyPath: "id" });
    },
  });
  return dbPromise;
}

export async function putIntent(intent: QueueIntent): Promise<void> {
  const db = await getDb();
  await db.put("intents", intent);
}

export async function deleteIntent(id: string): Promise<void> {
  const db = await getDb();
  await db.delete("intents", id);
}

export async function getAllIntents(): Promise<QueueIntent[]> {
  const db = await getDb();
  return db.getAll("intents");
}
