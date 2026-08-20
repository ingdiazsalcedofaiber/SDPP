import { useMemo, useRef, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import Paper from "@mui/material/Paper";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import HomeIcon from "@mui/icons-material/Home";
import PauseCircleOutlineIcon from "@mui/icons-material/PauseCircleOutlineOutlined";
import DrawIcon from "@mui/icons-material/Draw";
import VerifiedIcon from "@mui/icons-material/Verified";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import NavigateBeforeIcon from "@mui/icons-material/NavigateBefore";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import LoginIcon from "@mui/icons-material/Login";
import { Document, Page, pdfjs } from "react-pdf";
import "react-pdf/dist/Page/TextLayer.css";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router-dom";
import {
  viewEnvelopeAccess, getEnvelopeAccessDocumentBlob, requestOtp, verifyOtp, registerConsent, completeRecipientSigning, declineEnvelope,
} from "../../shared/api/signature";
import type { AccessField, FieldType, SignatureMethodUsed } from "../../shared/api/signature";
import { FIELD_TYPE_LABELS } from "../../shared/api/signature";
import { ApiError } from "../../shared/api/client";
import { BRAND_COLORS } from "../../shared/theme";
import { SdppLogo } from "../../shared/ui/SdppLogo";
import { useElementWidth } from "../../shared/hooks/useElementWidth";
import { useAuthStore } from "../../app/auth";
import { SignatureMethodDialog } from "./SignatureMethodDialog";

pdfjs.GlobalWorkerOptions.workerSrc = new URL("pdfjs-dist/build/pdf.worker.min.mjs", import.meta.url).toString();

const IMAGE_FIELD_TYPES: FieldType[] = ["Signature", "Initials", "Stamp"];
const BASE_WIDTH = 700;

type FieldValue = { text?: string; imageBlob?: Blob; imageUrl?: string; method?: SignatureMethodUsed };

async function blobToBase64(blob: Blob): Promise<string> {
  const buffer = await blob.arrayBuffer();
  let binary = "";
  new Uint8Array(buffer).forEach((byte) => (binary += String.fromCharCode(byte)));
  return btoa(binary);
}

/**
 * The recipient-facing signing experience — reached only through a per-recipient token
 * (/firmar/publico/:token), standalone outside the authenticated AppShell so it works the same way
 * whether the visitor has an SDPP account or not (see router.tsx). Internal recipients (matched to
 * an SDPP account) are authorized by their existing session cookie; external ones go through the
 * email-OTP challenge first — see ViewEnvelopeAccessCommand's requiresSdppLogin/requiresOtp flags.
 */
export function EnvelopeSigningPage() {
  const { token } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const authStatus = useAuthStore((s) => s.status);
  const authUser = useAuthStore((s) => s.user);

  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [otpCode, setOtpCode] = useState("");
  const [otpRequested, setOtpRequested] = useState(false);
  const [consentChecked, setConsentChecked] = useState(false);
  const [consentGiven, setConsentGiven] = useState(false);
  const [values, setValues] = useState<Record<string, FieldValue>>({});
  const [signingFieldId, setSigningFieldId] = useState<string | null>(null);
  const [declineDialogOpen, setDeclineDialogOpen] = useState(false);
  const [declineReason, setDeclineReason] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const viewerRef = useRef<HTMLDivElement | null>(null);
  const viewerWidth = useElementWidth(viewerRef);
  const pageWidth = Math.min(BASE_WIDTH, viewerWidth ?? BASE_WIDTH);
  const [completed, setCompleted] = useState<{ envelopeCompleted: boolean } | null>(null);

  const accessQuery = useQuery({ queryKey: ["envelope-access", token], queryFn: () => viewEnvelopeAccess(token!), enabled: !!token });
  const view = accessQuery.data;

  const pdfQuery = useQuery({
    queryKey: ["envelope-access-document", token],
    queryFn: () => getEnvelopeAccessDocumentBlob(token!),
    enabled: !!token && !!view,
  });

  const requestOtpMutation = useMutation({
    mutationFn: () => requestOtp(token!),
    onSuccess: () => setOtpRequested(true),
  });

  const verifyOtpMutation = useMutation({
    mutationFn: () => verifyOtp(token!, otpCode),
    onSuccess: (result) => setSessionToken(result.sessionToken),
  });

  const consentMutation = useMutation({
    mutationFn: () => registerConsent(token!, sessionToken ?? undefined),
    onSuccess: () => setConsentGiven(true),
  });

  const completeMutation = useMutation({
    mutationFn: async () => {
      const fields = await Promise.all(
        (view?.fields ?? []).map(async (f) => {
          const v = values[f.fieldId];
          return {
            fieldId: f.fieldId,
            value: v?.text ?? null,
            signatureImageBase64: v?.imageBlob ? await blobToBase64(v.imageBlob) : null,
            method: v?.method ?? null,
          };
        }),
      );
      return completeRecipientSigning(token!, fields, sessionToken ?? undefined);
    },
    onSuccess: (result) => setCompleted(result),
  });

  const declineMutation = useMutation({
    mutationFn: () => declineEnvelope(token!, declineReason, sessionToken ?? undefined),
    onSuccess: () => setDeclineDialogOpen(false),
  });

  // Three states, mirroring RecipientAccessAuthorization.CanAct on the backend: a matched SDPP
  // account needs its own session; an unmatched external recipient normally needs an OTP-verified
  // session token — EXCEPT when requiresOtp is also false, which only happens for an in-person
  // recipient (see EnvelopeRecipient.InPerson) — there the raw link itself is enough, no further
  // step needed here at all.
  const isAuthorized = view
    ? view.requiresSdppLogin
      ? authStatus === "authenticated" && authUser?.email === view.recipientEmail
      : view.requiresOtp
        ? sessionToken !== null
        : true
    : false;
  // Deliberately local-only, NOT view.consentAccepted — "Firma en espera" (below) sends the signer
  // back to / mid-session with nothing submitted yet, and the whole point of resuming later is that
  // they see and accept the consent screen again, every time, rather than a server-remembered
  // acceptance from a possibly much earlier visit silently skipping it.
  const hasConsented = consentGiven;

  const setFieldText = (fieldId: string, text: string) => setValues((prev) => ({ ...prev, [fieldId]: { ...prev[fieldId], text } }));
  const setFieldImage = (fieldId: string, blob: Blob, method: SignatureMethodUsed) =>
    setValues((prev) => ({ ...prev, [fieldId]: { ...prev[fieldId], imageBlob: blob, imageUrl: URL.createObjectURL(blob), method } }));

  const isFieldFilled = (f: AccessField) => f.isFilled || !!values[f.fieldId]?.text || !!values[f.fieldId]?.imageBlob;
  const allRequiredFilled = (view?.fields ?? []).filter((f) => f.required).every(isFieldFilled);

  const fieldsOnCurrentPage = useMemo(() => (view?.fields ?? []).filter((f) => f.pageNumber === currentPage), [view, currentPage]);

  return (
    <Box sx={{ minHeight: "100vh", bgcolor: "#F5F8F7", py: 4, px: 2 }}>
      <Box sx={{ maxWidth: 1100, mx: "auto" }}>
        <Box sx={{ display: "flex", justifyContent: "center", mb: 3 }}>
          <SdppLogo />
        </Box>

        {accessQuery.isPending && (
          <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
            <CircularProgress />
          </Box>
        )}

        {accessQuery.isError && (
          <Paper sx={{ p: 4, maxWidth: 480, mx: "auto", textAlign: "center" }}>
            <HighlightOffIcon sx={{ fontSize: 48, color: "#B23A2E", mb: 1 }} />
            <Typography variant="h6" gutterBottom>Enlace no válido</Typography>
            <Typography variant="body2" color="text.secondary">
              {accessQuery.error instanceof ApiError ? accessQuery.error.message : "Este enlace no es válido o ha expirado."}
            </Typography>
          </Paper>
        )}

        {view && completed && (
          <Paper sx={{ p: 4, maxWidth: 480, mx: "auto", textAlign: "center" }}>
            <CheckCircleIcon sx={{ fontSize: 48, color: BRAND_COLORS.teal, mb: 1 }} />
            <Typography variant="h6" gutterBottom>Firma registrada correctamente</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
              {completed.envelopeCompleted
                ? "Todos los firmantes completaron el documento. El sobre quedó marcado como completado."
                : "Tu firma quedó registrada. El documento continúa con el siguiente firmante."}
            </Typography>
            {/* Mainly for the "firma presencial" kiosk case: staff hands the device back and needs
                a way out of this recipient-scoped page without typing a URL — an anonymous external
                signer clicking this just lands on /login, harmless since they have nothing to go
                "back" to anyway. */}
            <Button variant="contained" startIcon={<HomeIcon />} onClick={() => navigate("/")}>
              Volver al inicio
            </Button>
          </Paper>
        )}

        {view && !completed && view.recipientStatus === "Declined" && (
          <Paper sx={{ p: 4, maxWidth: 480, mx: "auto", textAlign: "center" }}>
            <HighlightOffIcon sx={{ fontSize: 48, color: "#B23A2E", mb: 1 }} />
            <Typography variant="h6" gutterBottom>Ya rechazaste este documento</Typography>
            <Button variant="contained" startIcon={<HomeIcon />} sx={{ mt: 2 }} onClick={() => navigate("/")}>
              Volver al inicio
            </Button>
          </Paper>
        )}

        {view && !completed && view.recipientStatus === "Signed" && (
          <Paper sx={{ p: 4, maxWidth: 480, mx: "auto", textAlign: "center" }}>
            <CheckCircleIcon sx={{ fontSize: 48, color: BRAND_COLORS.teal, mb: 1 }} />
            <Typography variant="h6" gutterBottom>Ya firmaste este documento</Typography>
            <Button variant="contained" startIcon={<HomeIcon />} sx={{ mt: 2 }} onClick={() => navigate("/")}>
              Volver al inicio
            </Button>
          </Paper>
        )}

        {view && !completed && view.recipientStatus !== "Signed" && view.recipientStatus !== "Declined" && !isAuthorized && (
          <Paper sx={{ p: 4, maxWidth: 480, mx: "auto" }}>
            <Typography variant="h6" gutterBottom>{view.title}</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
              Firmante: {view.recipientFullName} ({view.recipientEmail})
            </Typography>

            {view.requiresSdppLogin ? (
              authStatus === "authenticated" ? (
                <Alert severity="warning">
                  Esta firma está asignada a <strong>{view.recipientEmail}</strong>, pero iniciaste sesión como{" "}
                  <strong>{authUser?.email}</strong>. Cierra sesión e inicia con la cuenta correcta para continuar.
                </Alert>
              ) : (
                <>
                  <Alert severity="info" sx={{ mb: 2 }}>
                    Este firmante tiene una cuenta SDPP. Inicia sesión para continuar.
                  </Alert>
                  <Button variant="contained" fullWidth startIcon={<LoginIcon />} onClick={() => navigate("/login")}>
                    Iniciar sesión
                  </Button>
                </>
              )
            ) : (
              <>
                {!otpRequested ? (
                  <Button variant="contained" fullWidth disabled={requestOtpMutation.isPending} onClick={() => requestOtpMutation.mutate()}>
                    {requestOtpMutation.isPending ? <CircularProgress size={20} /> : "Enviar código de verificación"}
                  </Button>
                ) : (
                  <>
                    <Alert severity="info" sx={{ mb: 2 }}>
                      Te enviamos un código de 6 dígitos por correo. Revisa tu bandeja de entrada (y spam) e ingrésalo abajo.
                    </Alert>
                    <TextField
                      label="Código de verificación" value={otpCode} onChange={(e) => setOtpCode(e.target.value)}
                      fullWidth sx={{ mb: 2 }} autoFocus
                    />
                    {verifyOtpMutation.isError && (
                      <Alert severity="error" sx={{ mb: 2 }}>
                        {verifyOtpMutation.error instanceof ApiError ? verifyOtpMutation.error.message : "Código inválido."}
                      </Alert>
                    )}
                    <Button variant="contained" fullWidth disabled={!otpCode || verifyOtpMutation.isPending} onClick={() => verifyOtpMutation.mutate()}>
                      {verifyOtpMutation.isPending ? <CircularProgress size={20} /> : "Verificar código"}
                    </Button>
                  </>
                )}
              </>
            )}
          </Paper>
        )}

        {view && !completed && view.recipientStatus !== "Signed" && view.recipientStatus !== "Declined" && isAuthorized && !hasConsented && (
          <Paper sx={{ p: 4, maxWidth: 560, mx: "auto" }}>
            <Typography variant="h6" gutterBottom>Consentimiento de firma electrónica</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Antes de continuar, SDPP necesita tu aceptación explícita para firmar este documento de
              forma electrónica. Al aceptar, confirmas que:
            </Typography>
            <Typography component="ul" variant="body2" color="text.secondary" sx={{ mb: 2, pl: 3 }}>
              <li>Aceptas usar una firma electrónica en lugar de una firma manuscrita en papel.</li>
              <li>Tu identidad, dirección IP, dispositivo y hora quedarán registrados como evidencia de este proceso.</li>
              <li>Entiendes que esta firma tiene el mismo efecto que tu firma manuscrita para este documento.</li>
            </Typography>
            <Box sx={{ display: "flex", alignItems: "flex-start", mb: 2 }}>
              <Checkbox checked={consentChecked} onChange={(e) => setConsentChecked(e.target.checked)} sx={{ mt: -1 }} />
              <Typography variant="body2">He leído y acepto firmar este documento electrónicamente.</Typography>
            </Box>
            {consentMutation.isError && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {consentMutation.error instanceof ApiError ? consentMutation.error.message : "No se pudo registrar el consentimiento."}
              </Alert>
            )}
            <Button
              fullWidth variant="contained" disabled={!consentChecked || consentMutation.isPending}
              onClick={() => consentMutation.mutate()}
            >
              {consentMutation.isPending ? <CircularProgress size={20} /> : "Aceptar y continuar"}
            </Button>
          </Paper>
        )}

        {view && !completed && view.recipientStatus !== "Signed" && view.recipientStatus !== "Declined" && isAuthorized && hasConsented && (
          <Box sx={{ display: "flex", gap: 2, flexWrap: "wrap" }}>
            <Paper sx={{ p: 2, flexGrow: 1, minWidth: 320 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>{view.title}</Typography>
              {view.message && <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{view.message}</Typography>}

              {pdfQuery.isPending && (
                <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}><CircularProgress /></Box>
              )}

              {pdfQuery.data && (
                <>
                  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 1, mb: 2 }}>
                    <IconButton onClick={() => setCurrentPage((p) => Math.max(1, p - 1))} disabled={currentPage <= 1}>
                      <NavigateBeforeIcon />
                    </IconButton>
                    <Typography variant="body2">Página {currentPage}</Typography>
                    <IconButton onClick={() => setCurrentPage((p) => p + 1)}>
                      <NavigateNextIcon />
                    </IconButton>
                  </Box>

                  {/* alignItems: "flex-start" — same fix as EnvelopeEditorPage's identical wrapper:
                      display:flex's default align-items:stretch clamps this box to maxHeight
                      (65vh) whenever the rendered page is taller than that, which then throws off
                      the percentage-positioned field overlays below (they'd be positioned relative
                      to the clamped box, not the real, visually-overflowing canvas height). */}
                  <Box ref={viewerRef} sx={{ display: "flex", justifyContent: "center", alignItems: "flex-start", overflow: "auto", maxHeight: "65vh", maxWidth: "100%" }}>
                    <Box sx={{ position: "relative", display: "inline-block", lineHeight: 0, "& canvas": { display: "block" } }}>
                      <Document file={pdfQuery.data}>
                        <Page pageNumber={currentPage} width={pageWidth} renderTextLayer renderAnnotationLayer={false} />
                      </Document>
                      {fieldsOnCurrentPage.map((f) => (
                        <Box
                          key={f.fieldId}
                          sx={{
                            position: "absolute",
                            left: `${f.positionX * 100}%`, top: `${f.positionY * 100}%`,
                            width: `${f.width * 100}%`, height: `${f.height * 100}%`,
                            border: `2px dashed ${isFieldFilled(f) ? BRAND_COLORS.teal : BRAND_COLORS.magenta}`,
                            borderRadius: 1,
                            bgcolor: isFieldFilled(f) ? `${BRAND_COLORS.teal}22` : `${BRAND_COLORS.magenta}11`,
                            display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden",
                          }}
                        >
                          {values[f.fieldId]?.imageUrl && (
                            <img src={values[f.fieldId]!.imageUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "contain" }} />
                          )}
                          {f.type === "LegalApprovalStamp" && isFieldFilled(f) && (
                            <Typography variant="caption" sx={{ fontWeight: 700, color: BRAND_COLORS.teal, textAlign: "center" }}>
                              APROBADO
                              <br />
                              GERENCIA LEGAL
                            </Typography>
                          )}
                        </Box>
                      ))}
                    </Box>
                  </Box>
                </>
              )}
            </Paper>

            <Paper sx={{ p: 2, width: { xs: "100%", sm: 320 }, flexShrink: 0 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>Tus campos</Typography>

              {view.fields.map((f) => (
                <Box key={f.fieldId} sx={{ mb: 2 }}>
                  <Typography variant="caption" color="text.secondary">
                    {FIELD_TYPE_LABELS[f.type]}{f.required ? " (obligatorio)" : ""}
                    {f.type === "LegalApprovalStamp" ? " — todas las páginas" : ` — página ${f.pageNumber}`}
                  </Typography>

                  {f.type === "LegalApprovalStamp" ? (
                    // Never opens the draw/upload dialog — SDPP generates this exact stamp
                    // server-side (see PdfSharpEnvelopeEmbeddingEngine.DrawLegalApprovalStamp), so
                    // there is nothing for the recipient to draw or upload, only to authorize. One
                    // click stamps every page of the final document (never the certificate, which
                    // is a separate file) — see the same method's doc comment.
                    <>
                      <Button
                        fullWidth variant={isFieldFilled(f) ? "outlined" : "contained"} startIcon={<VerifiedIcon />}
                        onClick={() => setFieldText(f.fieldId, "CONFIRMADO")} sx={{ mt: 0.5 }}
                      >
                        {isFieldFilled(f) ? "Autorizado" : "Autorizar"}
                      </Button>
                      <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                        Se colocará automáticamente en todas las páginas del documento (no en el certificado).
                      </Typography>
                    </>
                  ) : IMAGE_FIELD_TYPES.includes(f.type) ? (
                    <Button
                      fullWidth variant={isFieldFilled(f) ? "outlined" : "contained"} startIcon={<DrawIcon />}
                      onClick={() => setSigningFieldId(f.fieldId)} sx={{ mt: 0.5 }}
                    >
                      {isFieldFilled(f) ? "Rehacer" : "Firmar"}
                    </Button>
                  ) : f.type === "Checkbox" ? (
                    <Checkbox
                      checked={values[f.fieldId]?.text === "true"}
                      onChange={(e) => setFieldText(f.fieldId, e.target.checked ? "true" : "false")}
                    />
                  ) : f.type === "Date" ? (
                    <TextField
                      type="date" size="small" fullWidth sx={{ mt: 0.5 }}
                      value={values[f.fieldId]?.text ?? ""} onChange={(e) => setFieldText(f.fieldId, e.target.value)}
                    />
                  ) : (
                    <TextField
                      size="small" fullWidth sx={{ mt: 0.5 }}
                      placeholder={f.type === "Name" ? view.recipientFullName : undefined}
                      value={values[f.fieldId]?.text ?? ""} onChange={(e) => setFieldText(f.fieldId, e.target.value)}
                    />
                  )}
                </Box>
              ))}

              <Divider sx={{ my: 2 }} />

              {completeMutation.isError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {completeMutation.error instanceof ApiError ? completeMutation.error.message : "No se pudo completar la firma."}
                </Alert>
              )}

              <Button
                fullWidth size="large" variant="contained" startIcon={<CheckCircleIcon />} sx={{ mb: 1.5 }}
                disabled={!allRequiredFilled || completeMutation.isPending}
                onClick={() => completeMutation.mutate()}
              >
                {completeMutation.isPending ? <CircularProgress size={20} /> : "Completar firma"}
              </Button>
              {/* Nothing is actually persisted server-side until "Completar firma" (see
                  CompleteRecipientSigningCommand's doc comment) — so this is just navigation, no
                  API call. To resume: the creator reopens this same recipient from the envelope
                  detail page ("Firmar ahora" on a firma presencial pendiente), which mints a fresh
                  access link and lands back here — starting over from consent on purpose, see
                  hasConsented's doc comment above. */}
              <Button fullWidth variant="outlined" startIcon={<PauseCircleOutlineIcon />} sx={{ mb: 1.5 }} onClick={() => navigate("/")}>
                Firma en espera
              </Button>
              <Button fullWidth color="error" onClick={() => setDeclineDialogOpen(true)}>
                Rechazar documento
              </Button>
            </Paper>
          </Box>
        )}
      </Box>

      <SignatureMethodDialog
        open={!!signingFieldId}
        defaultName={view?.recipientFullName}
        onCancel={() => setSigningFieldId(null)}
        onConfirm={(blob, _aspectRatio, method) => {
          if (signingFieldId) setFieldImage(signingFieldId, blob, method);
          setSigningFieldId(null);
        }}
      />

      <Dialog open={declineDialogOpen} onClose={() => setDeclineDialogOpen(false)}>
        <DialogTitle>Rechazar documento</DialogTitle>
        <DialogContent>
          <DialogContentText sx={{ mb: 2 }}>Indica el motivo del rechazo. Esta acción cancela el sobre completo.</DialogContentText>
          <TextField label="Motivo" fullWidth multiline minRows={2} value={declineReason} onChange={(e) => setDeclineReason(e.target.value)} />
          {declineMutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {declineMutation.error instanceof ApiError ? declineMutation.error.message : "No se pudo rechazar el documento."}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeclineDialogOpen(false)}>Volver</Button>
          <Button color="error" variant="contained" disabled={!declineReason.trim() || declineMutation.isPending} onClick={() => declineMutation.mutate()}>
            Rechazar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
