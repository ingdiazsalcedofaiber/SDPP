import { apiClient } from "./client";
import type { DocumentStatusResult, OperationType } from "./types";

// Matches UploadDocumentResult on the backend (SDPP.Documents.Api/Endpoints/DocumentEndpoints.cs)
// — the upload response is intentionally lean and does NOT include the classification, since
// that's only known once inspection runs; fetch it via getDocumentStatus right after upload.
export interface UploadDocumentResult {
  documentId: string;
  sha256Hash: string;
  status: string;
}

export function uploadDocument(file: File): Promise<UploadDocumentResult> {
  const form = new FormData();
  form.append("file", file);
  return apiClient.postForm<UploadDocumentResult>("/api/v1/documents", form);
}

export function getDocumentStatus(documentId: string): Promise<DocumentStatusResult> {
  return apiClient.get<DocumentStatusResult>(`/api/v1/documents/${documentId}`);
}

export interface RequestConversionInput {
  documentId: string;
  operationType: OperationType;
  operationParameters?: Record<string, string>;
}

export interface RequestConversionResult {
  jobId: string;
  status: string;
}

export function requestConversion(input: RequestConversionInput): Promise<RequestConversionResult> {
  return apiClient.post<RequestConversionResult>(`/api/v1/documents/${input.documentId}/conversions`, {
    operationType: input.operationType,
    operationParameters: input.operationParameters ?? {},
  });
}

// react-pdf's <Document> accepts a Blob/File directly — separate from downloadDocument() below,
// which triggers a save-as flow instead of returning bytes for in-page rendering.
export async function getDocumentBlob(documentId: string): Promise<Blob> {
  const { blob } = await apiClient.getBlob(`/api/v1/documents/${documentId}/download`);
  return blob;
}

export async function downloadDocument(documentId: string, fallbackFileName: string): Promise<void> {
  const { blob, fileName } = await apiClient.getBlob(`/api/v1/documents/${documentId}/download`);
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName ?? fallbackFileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
