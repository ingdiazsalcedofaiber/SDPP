import { apiClient } from "./client";

export interface FindDocumentByHashResult {
  documentId: string;
  documentVersionId: string;
}

// Backs the audit trail's "buscar por hash" filter — resolves a document's SHA-256 hash to its
// DocumentId (exact match only), so the audit search can then reuse the existing documentId
// filter. Throws ApiError (404) when no document has that hash.
export function findDocumentByHash(sha256Hash: string): Promise<FindDocumentByHashResult> {
  return apiClient.get<FindDocumentByHashResult>(`/api/v1/classification/documents/by-hash/${encodeURIComponent(sha256Hash)}`);
}
