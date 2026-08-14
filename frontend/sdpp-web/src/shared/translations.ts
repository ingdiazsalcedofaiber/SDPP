import type { OperationType } from "./api/types";

// Central place for every enum-shaped value the backend returns in English/PascalCase (matching
// the C# type names) that still needs a Spanish label in the UI — one map per enum, reused by
// every page instead of each screen inventing its own partial translation.

export const OPERATION_TYPE_LABELS: Record<OperationType, string> = {
  WordToPdf: "Word → PDF",
  ExcelToPdf: "Excel → PDF",
  PptToPdf: "PowerPoint → PDF",
  ImageToPdf: "Imagen → PDF",
  PdfToWord: "PDF → Word",
  PdfToExcel: "PDF → Excel",
  PdfToImage: "PDF → Imagen",
  PdfToPpt: "PDF → PowerPoint",
  Merge: "Combinar PDF",
  Split: "Separar PDF",
  Compress: "Comprimir PDF",
  Ocr: "OCR",
  Watermark: "Marca de agua",
  PageNumbering: "Numerar páginas",
  Rotate: "Rotar páginas",
  DigitalSign: "Firma digital",
  DeletePages: "Eliminar páginas",
  ReorderPages: "Reordenar páginas",
  Protect: "Proteger con contraseña",
  Unlock: "Quitar contraseña",
};

export function translateOperationType(value: string): string {
  return OPERATION_TYPE_LABELS[value as OperationType] ?? value;
}

export const CONVERSION_JOB_STATUS_LABELS: Record<string, string> = {
  PendingForm: "Pendiente de formulario",
  Queued: "En cola",
  Inspecting: "Inspeccionando",
  Approved: "Aprobada",
  AwaitingApproval: "Esperando aprobación",
  Rejected: "Rechazada",
  Processing: "Procesando",
  Completed: "Completada",
  Failed: "Con error",
};

export function translateJobStatus(value: string): string {
  return CONVERSION_JOB_STATUS_LABELS[value] ?? value;
}

// MUI Chip `color` prop values — grouped with the labels above so a status's wording and its
// visual severity never drift apart across screens.
export const CONVERSION_JOB_STATUS_COLORS: Record<string, "success" | "error" | "warning" | "default"> = {
  Completed: "success",
  Approved: "success",
  Failed: "error",
  Rejected: "error",
  Processing: "warning",
  AwaitingApproval: "warning",
  Queued: "default",
  Inspecting: "default",
  PendingForm: "default",
};

export const ACCESS_RESULT_LABELS: Record<string, string> = {
  Success: "Exitoso",
  Failed: "Fallido",
  DomainRejected: "Dominio no permitido",
  AccountInactive: "Cuenta inactiva",
  InvalidToken: "Token inválido",
  MfaEnrollmentRequired: "Requiere registro de MFA",
  MfaChallengeIssued: "Desafío MFA emitido",
  MfaVerificationFailed: "Verificación MFA fallida",
};

export function translateAccessResult(value: string): string {
  return ACCESS_RESULT_LABELS[value] ?? value;
}

export const ACCESS_RESULT_COLORS: Record<string, "success" | "error" | "warning" | "default"> = {
  Success: "success",
  Failed: "error",
  DomainRejected: "warning",
  AccountInactive: "warning",
  InvalidToken: "error",
  MfaEnrollmentRequired: "warning",
  MfaChallengeIssued: "warning",
  MfaVerificationFailed: "error",
};
