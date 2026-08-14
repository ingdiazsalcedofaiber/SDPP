// Mirrors docs/06-api/openapi.yaml — kept hand-written for now; a future iteration should
// generate this file from the OpenAPI contract (openapi-typescript) so the two can never drift,
// see docs/01-architecture/solution-structure.md §2.

export type OperationType =
  | "WordToPdf"
  | "ExcelToPdf"
  | "PptToPdf"
  | "ImageToPdf"
  | "PdfToWord"
  | "PdfToExcel"
  | "PdfToImage"
  | "PdfToPpt"
  | "Merge"
  | "Split"
  | "Compress"
  | "Ocr"
  | "Watermark"
  | "PageNumbering"
  | "Rotate"
  | "DigitalSign"
  | "DeletePages"
  | "ReorderPages"
  | "Protect"
  | "Unlock";

export interface ConversionJob {
  id: string;
  documentId: string;
  operationType: OperationType;
  status:
    | "PendingForm"
    | "Queued"
    | "Inspecting"
    | "Approved"
    | "AwaitingApproval"
    | "Rejected"
    | "Processing"
    | "Completed"
    | "Failed";
  engineUsed?: string | null;
  durationMs?: number | null;
  outputDocumentId?: string | null;
  errorDetail?: string | null;
  createdAtUtc: string;
}

// The Panel de Conversión only needs id/status/errorDetail/outputDocumentId to drive its own
// select→format→convert→download flow — classification/risk/protection fields the backend status
// endpoint still returns (for other consumers) are intentionally left undeclared here.
export interface DocumentStatusResult {
  id: string;
  originalFileName: string;
  status: string;
  jobs: {
    id: string;
    operationType: string;
    status: string;
    outputDocumentId?: string | null;
    errorDetail?: string | null;
  }[];
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
  errorCode?: string;
}
