import { apiClient, publicApiClient } from "./client";

export type SigningMode = "Sequential" | "Parallel";
export type FieldType = "Signature" | "Initials" | "Date" | "Name" | "Title" | "Text" | "Stamp" | "Checkbox" | "LegalApprovalStamp";
export type EnvelopeStatus =
  | "Draft" | "Sent" | "Pending" | "InProgress" | "PartiallySigned" | "Completed" | "Declined" | "Cancelled" | "Expired";
export type RecipientStatus = "Pending" | "Sent" | "Viewed" | "Signed" | "Declined" | "Expired";
export type SignatureMethodUsed = "Drawn" | "Typed" | "Uploaded" | "Reused";

export const FIELD_TYPE_LABELS: Record<FieldType, string> = {
  Signature: "Firma",
  Initials: "Iniciales",
  Date: "Fecha",
  Name: "Nombre",
  Title: "Cargo",
  Text: "Texto",
  Stamp: "Sello",
  Checkbox: "Casilla",
  LegalApprovalStamp: "Sello de Aprobación Legal",
};

export const ENVELOPE_STATUS_LABELS: Record<EnvelopeStatus, string> = {
  Draft: "Borrador",
  Sent: "Enviado",
  Pending: "Pendiente",
  InProgress: "En proceso",
  PartiallySigned: "Firmado parcialmente",
  Completed: "Completado",
  Declined: "Rechazado",
  Cancelled: "Cancelado",
  Expired: "Expirado",
};

export const RECIPIENT_STATUS_LABELS: Record<RecipientStatus, string> = {
  Pending: "Pendiente",
  Sent: "Enviado",
  Viewed: "Visto",
  Signed: "Firmado",
  Declined: "Rechazado",
  Expired: "Expirado",
};

// ---------------------------------------------------------------------------
// Creator-facing (authenticated, cookie session) — /api/v1/signature/envelopes
// ---------------------------------------------------------------------------

export interface CreateEnvelopeInput {
  sourceDocumentId: string;
  title: string;
  message?: string;
  signingMode: SigningMode;
  dueDateUtc?: string;
}

export function createEnvelope(input: CreateEnvelopeInput): Promise<{ envelopeId: string }> {
  return apiClient.post("/api/v1/signature/envelopes", input);
}

export function addRecipient(
  envelopeId: string, email: string, fullName: string, order: number, inPerson = false,
): Promise<{ recipientId: string }> {
  return apiClient.post(`/api/v1/signature/envelopes/${envelopeId}/recipients`, { email, fullName, order, inPerson });
}

export function removeRecipient(envelopeId: string, recipientId: string): Promise<void> {
  return apiClient.delete(`/api/v1/signature/envelopes/${envelopeId}/recipients/${recipientId}`);
}

export interface AddFieldInput {
  recipientId: string;
  type: FieldType;
  pageNumber: number;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  required: boolean;
}

export function addField(envelopeId: string, input: AddFieldInput): Promise<{ fieldId: string }> {
  return apiClient.post(`/api/v1/signature/envelopes/${envelopeId}/fields`, input);
}

export function removeField(envelopeId: string, fieldId: string): Promise<void> {
  return apiClient.delete(`/api/v1/signature/envelopes/${envelopeId}/fields/${fieldId}`);
}

export function updateField(
  envelopeId: string, fieldId: string, positionX: number, positionY: number, width: number, height: number,
): Promise<void> {
  return apiClient.patch(`/api/v1/signature/envelopes/${envelopeId}/fields/${fieldId}`, { positionX, positionY, width, height });
}

export interface DispatchedRecipient {
  recipientId: string;
  email: string;
  accessToken: string;
}

export function sendEnvelope(envelopeId: string): Promise<{ envelopeId: string; dispatched: DispatchedRecipient[] }> {
  return apiClient.post(`/api/v1/signature/envelopes/${envelopeId}/send`);
}

export function cancelEnvelope(envelopeId: string): Promise<void> {
  return apiClient.post(`/api/v1/signature/envelopes/${envelopeId}/cancel`);
}

export function resendRecipientAccess(envelopeId: string, recipientId: string): Promise<{ accessToken: string }> {
  return apiClient.post(`/api/v1/signature/envelopes/${envelopeId}/recipients/${recipientId}/resend-access`);
}

export interface EnvelopeSummary {
  envelopeId: string;
  title: string;
  status: EnvelopeStatus;
  signingMode: SigningMode;
  recipientCount: number;
  signedCount: number;
  createdAtUtc: string;
  dueDateUtc: string | null;
  completedAtUtc: string | null;
  // The two distinct reasons an envelope can appear under scope="pending" — see
  // ListEnvelopesQuery.cs's doc comment on EnvelopeSummaryDto. Only meaningful for that scope;
  // "all"/"sent" results carry them too but the inbox page ignores them there.
  isMyTurnToSign: boolean;
  hasPendingInPersonSignature: boolean;
}

export function listEnvelopes(scope: "sent" | "pending" | "all"): Promise<EnvelopeSummary[]> {
  return apiClient.get(`/api/v1/signature/envelopes?scope=${scope}`);
}

export interface RecipientDetail {
  recipientId: string;
  order: number;
  email: string;
  fullName: string;
  matchedUserId: string | null;
  status: RecipientStatus;
  sentAtUtc: string | null;
  viewedAtUtc: string | null;
  signedAtUtc: string | null;
  declinedAtUtc: string | null;
  declineReason: string | null;
  inPerson: boolean;
}

export interface FieldDetail {
  fieldId: string;
  recipientId: string;
  type: FieldType;
  pageNumber: number;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  required: boolean;
  isFilled: boolean;
  signatureHash: string | null;
}

export interface EnvelopeDetail {
  envelopeId: string;
  sourceDocumentId: string;
  title: string;
  message: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  signingMode: SigningMode;
  dueDateUtc: string | null;
  status: EnvelopeStatus;
  sentAtUtc: string | null;
  completedAtUtc: string | null;
  originalSha256Hash: string;
  finalDocumentId: string | null;
  finalDocumentVersionId: string | null;
  finalSha256Hash: string | null;
  envelopeHash: string | null;
  recipients: RecipientDetail[];
  fields: FieldDetail[];
}

export function getEnvelope(envelopeId: string): Promise<EnvelopeDetail> {
  return apiClient.get(`/api/v1/signature/envelopes/${envelopeId}`);
}

export async function downloadEnvelopeCertificate(envelopeId: string): Promise<void> {
  const { blob, fileName } = await apiClient.getBlob(`/api/v1/signature/envelopes/${envelopeId}/certificate`);
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName ?? `certificado-${envelopeId}.pdf`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

// ---------------------------------------------------------------------------
// Saved signatures (authenticated)
// ---------------------------------------------------------------------------

export interface SavedSignature {
  id: string;
  label: string;
  aspectRatio: number;
  createdAtUtc: string;
}

export function listSavedSignatures(): Promise<SavedSignature[]> {
  return apiClient.get("/api/v1/signature/saved-signatures");
}

export function addSavedSignature(imageBase64: string, aspectRatio: number, label: string): Promise<{ id: string }> {
  return apiClient.post("/api/v1/signature/saved-signatures", { imageBase64, aspectRatio, label });
}

export function deleteSavedSignature(id: string): Promise<void> {
  return apiClient.delete(`/api/v1/signature/saved-signatures/${id}`);
}

// ---------------------------------------------------------------------------
// Public signer-access flow (no SDPP session assumed) — /api/v1/signature/access
// ---------------------------------------------------------------------------

export interface AccessField {
  fieldId: string;
  type: FieldType;
  pageNumber: number;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  required: boolean;
  isFilled: boolean;
}

export interface EnvelopeAccessView {
  envelopeId: string;
  title: string;
  message: string | null;
  sourceDocumentId: string;
  envelopeStatus: EnvelopeStatus;
  recipientId: string;
  recipientEmail: string;
  recipientFullName: string;
  recipientStatus: RecipientStatus;
  requiresOtp: boolean;
  requiresSdppLogin: boolean;
  matchedUserId: string | null;
  consentAccepted: boolean;
  fields: AccessField[];
}

export function viewEnvelopeAccess(token: string): Promise<EnvelopeAccessView> {
  return publicApiClient.get(`/api/v1/signature/access/${token}`);
}

export function getEnvelopeAccessDocumentBlob(token: string): Promise<Blob> {
  return publicApiClient.getBlob(`/api/v1/signature/access/${token}/document`);
}

/** The code itself is never in the response — it's emailed (see the backend's RequestOtpResult doc
 * comment). This resolves once the send was attempted, not once it's actually in the recipient's
 * inbox. */
export function requestOtp(token: string): Promise<void> {
  return publicApiClient.post(`/api/v1/signature/access/${token}/otp/request`);
}

export function verifyOtp(token: string, code: string): Promise<{ sessionToken: string }> {
  return publicApiClient.post(`/api/v1/signature/access/${token}/otp/verify`, { code });
}

export function registerConsent(token: string, sessionToken?: string): Promise<void> {
  return publicApiClient.post(`/api/v1/signature/access/${token}/consent`, undefined, sessionToken);
}

export interface FieldSubmissionInput {
  fieldId: string;
  value?: string | null;
  signatureImageBase64?: string | null;
  method?: SignatureMethodUsed | null;
}

export interface CompleteSigningResult {
  envelopeStatus: EnvelopeStatus;
  envelopeCompleted: boolean;
  finalDocumentId: string | null;
}

export function completeRecipientSigning(
  token: string, fields: FieldSubmissionInput[], sessionToken?: string,
): Promise<CompleteSigningResult> {
  return publicApiClient.post(`/api/v1/signature/access/${token}/complete`, { fields }, sessionToken);
}

export function declineEnvelope(token: string, reason: string, sessionToken?: string): Promise<void> {
  return publicApiClient.post(`/api/v1/signature/access/${token}/decline`, { reason }, sessionToken);
}

// ---------------------------------------------------------------------------
// Public verification (no auth, no recipient token — anyone with an envelope ID) —
// /api/v1/signature/verify
// ---------------------------------------------------------------------------

export interface VerificationRecipient {
  fullName: string;
  email: string;
  status: RecipientStatus;
  signedAtUtc: string | null;
}

export interface EnvelopeVerificationResult {
  envelopeId: string;
  title: string;
  status: EnvelopeStatus;
  completedAtUtc: string | null;
  isIntact: boolean;
  documentHashMatches: boolean;
  certificateHashMatches: boolean;
  auditTrailIntact: boolean;
  auditRecordCount: number;
  originalSha256Hash: string;
  finalSha256Hash: string | null;
  envelopeHash: string | null;
  certificateHash: string | null;
  recipients: VerificationRecipient[];
}

export function verifyEnvelope(envelopeId: string): Promise<EnvelopeVerificationResult> {
  return publicApiClient.get(`/api/v1/signature/verify/${envelopeId}`);
}

export type NotificationType =
  | "EnvelopeSent" | "EnvelopeViewed" | "EnvelopeSigned" | "EnvelopeCompleted"
  | "EnvelopeDeclined" | "EnvelopeCancelled" | "EnvelopeExpired" | "ReminderSent";

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  envelopeId: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export function listNotifications(unreadOnly = false): Promise<AppNotification[]> {
  return apiClient.get(`/api/v1/signature/notifications?unreadOnly=${unreadOnly}`);
}

export function markNotificationRead(id: string): Promise<void> {
  return apiClient.post(`/api/v1/signature/notifications/${id}/read`);
}

export interface EnvelopeDashboardSummary {
  drafts: number;
  waitingForOthers: number;
  waitingForMe: number;
  completed: number;
  declined: number;
  cancelled: number;
  expired: number;
  avgCompletionHours: number | null;
  documentsSent: number;
  signaturesPerformed: number;
}

export function getDashboardSummary(): Promise<EnvelopeDashboardSummary> {
  return apiClient.get("/api/v1/signature/dashboard/summary");
}
