import { useEffect, useRef, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import FormControlLabel from "@mui/material/FormControlLabel";
import IconButton from "@mui/material/IconButton";
import Paper from "@mui/material/Paper";
import Step from "@mui/material/Step";
import StepLabel from "@mui/material/StepLabel";
import Stepper from "@mui/material/Stepper";
import TextField from "@mui/material/TextField";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import CloudOffOutlinedIcon from "@mui/icons-material/CloudOffOutlined";
import SendIcon from "@mui/icons-material/Send";
import DeleteIcon from "@mui/icons-material/Delete";
import PersonAddIcon from "@mui/icons-material/PersonAdd";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import NavigateBeforeIcon from "@mui/icons-material/NavigateBefore";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import ZoomInIcon from "@mui/icons-material/ZoomIn";
import ZoomOutIcon from "@mui/icons-material/ZoomOut";
import { Document, Page, pdfjs } from "react-pdf";
import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import type { FieldType, SigningMode } from "../../shared/api/signature";
import { FIELD_TYPE_LABELS } from "../../shared/api/signature";
import { ApiError } from "../../shared/api/client";
import { BRAND_COLORS } from "../../shared/theme";
import { useConnectivityStore } from "../../shared/offline/connectivityStore";
import { enqueueEnvelope } from "../../shared/offline/enqueue";
import { processQueue } from "../../shared/offline/queueProcessor";
import { submitEnvelopeIntent } from "../../shared/offline/submitEnvelopeIntent";
import { useQueueStore } from "../../shared/offline/queueStore";
import { useEnvelopeEditorStore } from "./envelopeEditorStore";
import { EditableFieldBox, NewFieldPlacementCatcher } from "./FieldOverlay";
import type { FieldRect } from "./FieldOverlay";

pdfjs.GlobalWorkerOptions.workerSrc = new URL("pdfjs-dist/build/pdf.worker.min.mjs", import.meta.url).toString();

const steps = ["Documento y datos", "Firmantes", "Colocar campos", "Sobre enviado"];
const RECIPIENT_COLORS = [BRAND_COLORS.teal, BRAND_COLORS.orange, BRAND_COLORS.magenta, "#6A1B9A", "#1976D2", "#00897B"];
const FIELD_TYPES: FieldType[] = ["Signature", "Initials", "Date", "Name", "Title", "Text", "Stamp", "Checkbox", "LegalApprovalStamp"];
// UX filtering only — the actual, enforced restriction lives in the backend
// (Signature:LegalApprovalStampEmail / ILegalApprovalStampPolicy). This just keeps the option out of
// the palette for every OTHER recipient so it isn't offered where it would just fail server-side.
const LEGAL_APPROVAL_STAMP_EMAIL = "gerencia.legal@clinaltec.com.co";
const BASE_WIDTH = 700;

/**
 * Creator-facing envelope wizard — replaces the old single-signer SignDocumentPage. Covers spec
 * point 1 (crear/configurar/enviar) end to end: upload → título/mensaje/modo/fecha límite →
 * agregar firmantes → colocar campos por firmante → enviar. Signing itself (both the internal and
 * the public external flow) is a separate page — see EnvelopeSigningPage.
 */
export function EnvelopeEditorPage() {
  const navigate = useNavigate();
  const s = useEnvelopeEditorStore();
  const [scale, setScale] = useState(1);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageCount, setPageCount] = useState(0);
  const [requiredDefault, setRequiredDefault] = useState(true);
  const [newRecipientEmail, setNewRecipientEmail] = useState("");
  const [newRecipientName, setNewRecipientName] = useState("");
  const [selectedFieldId, setSelectedFieldId] = useState<string | null>(null);
  const pageContainerRef = useRef<HTMLDivElement | null>(null);

  const colorFor = (recipientId: string) => {
    const idx = s.recipients.findIndex((r) => r.recipientId === recipientId);
    return RECIPIENT_COLORS[idx % RECIPIENT_COLORS.length];
  };
  const labelFor = (recipientId: string) => {
    const recipient = s.recipients.find((r) => r.recipientId === recipientId);
    return recipient ? recipient.fullName.split(" ")[0] : "";
  };

  const isOnline = useConnectivityStore((connectivity) => connectivity.isOnline);
  const queuedItem = useQueueStore((q) => q.items.find((i) => i.id === s.queuedIntentId));
  const queuedResult = useQueueStore((q) => (s.queuedIntentId ? q.results[s.queuedIntentId] : undefined));
  const clearQueueResult = useQueueStore((q) => q.clearResult);

  // Same "the queue processor may finish this after the page already moved on" pickup as
  // ConvertWizardPage — once a queued envelope actually sends for real (immediately if
  // connectivity returns while this page is still open, or on a later app boot), swap the "en
  // espera" panel for the real access-token links with zero user action.
  useEffect(() => {
    if (queuedResult?.kind === "envelope" && s.queuedIntentId) {
      s.setEnvelopeId(queuedResult.envelopeId);
      s.setDispatched(queuedResult.dispatched);
      clearQueueResult(s.queuedIntentId);
      s.setQueuedIntentId(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [queuedResult]);

  // Step 0–2 are entirely local now — adding a recipient, placing a field, dragging it, typing
  // exact coordinates, none of it touches the network. Only "Enviar sobre" (submitMutation, below)
  // does. Recipients/fields get a client-only `local-<uuid>` id here; submitEnvelopeIntent resolves
  // those to real server ids when it actually runs (see envelopeEditorStore.ts's doc comment).
  const addRecipientLocalHandler = () => {
    s.addRecipientLocal({
      recipientId: `local-${crypto.randomUUID()}`,
      email: newRecipientEmail,
      fullName: newRecipientName || newRecipientEmail,
      order: s.recipients.length + 1,
    });
    setNewRecipientEmail("");
    setNewRecipientName("");
  };

  const addFieldLocalHandler = (rect: FieldRect) => {
    const fieldId = `local-${crypto.randomUUID()}`;
    s.addFieldLocal({
      fieldId, recipientId: s.activeRecipientId!, type: s.armedFieldType!, pageNumber: currentPage, ...rect, required: requiredDefault,
    });
    setSelectedFieldId(fieldId);
  };

  // Numeric position/size editing — the remitente decides exactly where a field goes; dragging
  // with the mouse is one way, typing exact percentages is another. No cross-field clamping beyond
  // each value's own 0–100% range: the field is allowed anywhere on the page, including partially
  // past an edge, since constraining that would contradict "coloca donde tú quieras".
  const selectedField = s.fields.find((f) => f.fieldId === selectedFieldId) ?? null;
  const round1 = (n: number) => Math.round(n * 10) / 10;
  const percentToFraction = (raw: string) => {
    const n = Number(raw);
    return Math.min(100, Math.max(0, Number.isFinite(n) ? n : 0)) / 100;
  };
  const commitSelectedFieldPercent = (patch: Partial<FieldRect>) => {
    if (!selectedField) return;
    const rect: FieldRect = {
      positionX: selectedField.positionX, positionY: selectedField.positionY,
      width: selectedField.width, height: selectedField.height,
      ...patch,
    };
    s.updateFieldLocal(selectedField.fieldId, rect);
  };

  const removeFieldLocalHandler = (fieldId: string) => {
    s.removeFieldLocal(fieldId);
    setSelectedFieldId((current) => (current === fieldId ? null : current));
  };

  const submitMutation = useMutation({
    // See ConvertWizardPage.tsx's convertMutation for why this is required: TanStack Query's
    // default networkMode: "online" would otherwise silently pause this mutation itself whenever
    // navigator.onLine is false, and mutationFn (including our own isOnline branch below) would
    // never even start running until its own onlineManager decided to resume it.
    networkMode: "always",
    mutationFn: async () => {
      const payload = {
        file: s.file!,
        title: s.title,
        message: s.message || undefined,
        signingMode: s.signingMode,
        dueDateUtc: s.dueDateUtc ? new Date(s.dueDateUtc).toISOString() : undefined,
        recipients: s.recipients.map((r) => ({ localId: r.recipientId, email: r.email, fullName: r.fullName, order: r.order })),
        fields: s.fields.map((f) => ({
          localId: f.fieldId, recipientLocalId: f.recipientId, type: f.type, pageNumber: f.pageNumber,
          positionX: f.positionX, positionY: f.positionY, width: f.width, height: f.height, required: f.required,
        })),
      };

      if (!isOnline) {
        const id = await enqueueEnvelope(payload);
        return { kind: "queued" as const, id };
      }

      const result = await submitEnvelopeIntent({ ...payload, progress: {} });
      return { kind: "immediate" as const, ...result };
    },
    onSuccess: (result) => {
      if (result.kind === "immediate") {
        s.setEnvelopeId(result.envelopeId);
        s.setDispatched(result.dispatched);
      } else {
        s.setQueuedIntentId(result.id);
      }
      s.setActiveStep(3);
    },
  });

  const recipientsWithoutFields = s.recipients.filter((r) => !s.fields.some((f) => f.recipientId === r.recipientId));
  const fieldsOnCurrentPage = s.fields.filter((f) => f.pageNumber === currentPage);

  return (
    <Box sx={{ maxWidth: 1200, mx: "auto" }}>
      <Typography variant="h5" gutterBottom>
        Nuevo sobre de firma
      </Typography>

      <Stepper activeStep={s.activeStep} sx={{ mb: 4 }}>
        {steps.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      {s.activeStep === 0 && (
        <Paper sx={{ p: 3, maxWidth: 640, mx: "auto" }}>
          <Button component="label" variant="outlined" size="large" startIcon={<CloudUploadIcon />} sx={{ mb: 3 }} fullWidth>
            {s.file ? s.file.name : "Seleccionar archivo PDF"}
            <input type="file" accept="application/pdf" hidden onChange={(e) => s.setFile(e.target.files?.[0] ?? null)} />
          </Button>

          <TextField label="Título del sobre" value={s.title} onChange={(e) => s.setTitle(e.target.value)} fullWidth required sx={{ mb: 2 }} />
          <TextField
            label="Mensaje para los firmantes (opcional)" value={s.message} onChange={(e) => s.setMessage(e.target.value)}
            fullWidth multiline minRows={2} sx={{ mb: 2 }}
          />

          <Typography variant="subtitle2" sx={{ mb: 1, color: "text.secondary" }}>
            Modo de firma
          </Typography>
          <ToggleButtonGroup
            exclusive value={s.signingMode} onChange={(_e, v) => v && s.setSigningMode(v as SigningMode)} sx={{ mb: 2 }}
          >
            <ToggleButton value="Sequential">Secuencial (uno a la vez, en orden)</ToggleButton>
            <ToggleButton value="Parallel">Simultánea (todos a la vez)</ToggleButton>
          </ToggleButtonGroup>

          <TextField
            label="Fecha límite (opcional)" type="date" value={s.dueDateUtc ?? ""}
            onChange={(e) => s.setDueDateUtc(e.target.value || null)} fullWidth sx={{ mb: 2 }} slotProps={{ inputLabel: { shrink: true } }}
          />

          <Button
            size="large" variant="contained" fullWidth
            disabled={!s.file || !s.title.trim()}
            onClick={() => s.setActiveStep(1)}
          >
            Continuar
          </Button>
        </Paper>
      )}

      {s.activeStep === 1 && (
        <Paper sx={{ p: 3, maxWidth: 640, mx: "auto" }}>
          <Typography variant="subtitle2" sx={{ mb: 2, color: "text.secondary", fontWeight: 700 }}>
            Firmantes {s.signingMode === "Sequential" ? "(firmarán en este orden)" : "(firmarán simultáneamente)"}
          </Typography>

          {s.recipients.map((r, i) => (
            <Box key={r.recipientId} sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1.5, p: 1, borderRadius: 2, bgcolor: `${colorFor(r.recipientId)}14` }}>
              <Box sx={{ width: 28, height: 28, borderRadius: "50%", bgcolor: colorFor(r.recipientId), color: "#fff", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 13, fontWeight: 700 }}>
                {s.signingMode === "Sequential" ? i + 1 : "•"}
              </Box>
              <Box sx={{ flexGrow: 1 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{r.fullName}</Typography>
                <Typography variant="caption" color="text.secondary">{r.email}</Typography>
              </Box>
              <IconButton size="small" onClick={() => s.removeRecipientLocal(r.recipientId)}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Box>
          ))}

          <Box sx={{ display: "flex", gap: 1, mt: 2, flexWrap: "wrap" }}>
            <TextField label="Correo" size="small" value={newRecipientEmail} onChange={(e) => setNewRecipientEmail(e.target.value)} sx={{ flexGrow: 1, minWidth: 200 }} />
            <TextField label="Nombre completo" size="small" value={newRecipientName} onChange={(e) => setNewRecipientName(e.target.value)} sx={{ flexGrow: 1, minWidth: 200 }} />
            <Button
              variant="outlined" startIcon={<PersonAddIcon />}
              disabled={!newRecipientEmail.trim()}
              onClick={addRecipientLocalHandler}
            >
              Agregar
            </Button>
          </Box>

          <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 3 }}>
            <Button size="large" variant="contained" disabled={s.recipients.length === 0} onClick={() => s.setActiveStep(2)}>
              Continuar
            </Button>
          </Box>
        </Paper>
      )}

      {s.activeStep === 2 && s.file && (
        <Box sx={{ display: "flex", gap: 2, flexWrap: "wrap" }}>
          <Paper sx={{ p: 2, flexGrow: 1, minWidth: 320 }}>
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 1, mb: 2, flexWrap: "wrap" }}>
              <IconButton size="large" onClick={() => setCurrentPage((p) => Math.max(1, p - 1))} disabled={currentPage <= 1}>
                <NavigateBeforeIcon />
              </IconButton>
              <Typography sx={{ minWidth: 110, textAlign: "center" }}>Página {currentPage} de {pageCount || "…"}</Typography>
              <IconButton size="large" onClick={() => setCurrentPage((p) => Math.min(pageCount, p + 1))} disabled={currentPage >= pageCount}>
                <NavigateNextIcon />
              </IconButton>
              <Box sx={{ width: 16 }} />
              <IconButton size="large" onClick={() => setScale((v) => Math.max(0.5, v - 0.15))}><ZoomOutIcon /></IconButton>
              <IconButton size="large" onClick={() => setScale((v) => Math.min(2, v + 0.15))}><ZoomInIcon /></IconButton>
            </Box>

            {/* alignItems: "flex-start" is load-bearing, not cosmetic: display:flex's default
                align-items:stretch forces the single flex child (pageContainerRef) to the
                container's own cross-size — which resolves to maxHeight (70vh) once content
                exceeds it — even though overflow:auto is set. The canvas then visually overflows
                its own parent (no clipping, since pageContainerRef has no overflow:hidden), but
                getBoundingClientRect() on pageContainerRef reports that clamped 70vh height
                instead of the canvas's real size, corrupting every click-to-fraction calculation
                once a zoomed page exceeds 70vh (found via a position-precision regression test:
                identical clicks landed at very different Y fractions zoomed vs. unzoomed). */}
            <Box sx={{ display: "flex", justifyContent: "center", alignItems: "flex-start", overflow: "auto", maxHeight: "70vh" }}>
              <Box ref={pageContainerRef} sx={{ position: "relative", display: "inline-block", lineHeight: 0, "& canvas": { display: "block" } }}>
                <Document file={s.file} onLoadSuccess={({ numPages }) => setPageCount(numPages)}>
                  <Page pageNumber={currentPage} width={BASE_WIDTH * scale} renderTextLayer={false} renderAnnotationLayer={false} />
                </Document>
                <NewFieldPlacementCatcher pageContainerRef={pageContainerRef} armedType={s.armedFieldType} onPlace={addFieldLocalHandler} />
                {fieldsOnCurrentPage.map((f) => (
                  <EditableFieldBox
                    key={f.fieldId}
                    pageContainerRef={pageContainerRef}
                    field={f}
                    color={colorFor(f.recipientId)}
                    label={labelFor(f.recipientId)}
                    selected={selectedFieldId === f.fieldId}
                    onSelect={setSelectedFieldId}
                    onCommit={(fieldId, rect) => s.updateFieldLocal(fieldId, rect)}
                    onDelete={removeFieldLocalHandler}
                  />
                ))}
              </Box>
            </Box>
          </Paper>

          <Paper sx={{ p: 2, width: 300, flexShrink: 0 }}>
            <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700 }}>Firmante activo</Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75, mb: 2 }}>
              {s.recipients.map((r) => (
                <Chip
                  key={r.recipientId}
                  label={r.fullName.split(" ")[0]}
                  onClick={() => s.setActiveRecipientId(r.recipientId)}
                  sx={{
                    bgcolor: s.activeRecipientId === r.recipientId ? colorFor(r.recipientId) : `${colorFor(r.recipientId)}22`,
                    color: s.activeRecipientId === r.recipientId ? "#fff" : colorFor(r.recipientId),
                    fontWeight: 700,
                  }}
                />
              ))}
            </Box>

            <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700 }}>Tipo de campo</Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75, mb: 1 }}>
              {FIELD_TYPES.filter((type) => {
                if (type !== "LegalApprovalStamp") return true;
                const activeRecipient = s.recipients.find((r) => r.recipientId === s.activeRecipientId);
                return activeRecipient?.email.toLowerCase() === LEGAL_APPROVAL_STAMP_EMAIL;
              }).map((type) => (
                <Chip
                  key={type}
                  label={FIELD_TYPE_LABELS[type]}
                  disabled={!s.activeRecipientId}
                  onClick={() => s.setArmedFieldType(s.armedFieldType === type ? null : type)}
                  variant={s.armedFieldType === type ? "filled" : "outlined"}
                  color={s.armedFieldType === type ? "primary" : "default"}
                />
              ))}
            </Box>
            <FormControlLabel
              control={<Checkbox checked={requiredDefault} onChange={(e) => setRequiredDefault(e.target.checked)} />}
              label="Campo obligatorio"
            />
            {s.armedFieldType === "LegalApprovalStamp" && (
              <Alert severity="info" sx={{ mt: 1, mb: 2 }}>
                Toca el documento para fijar tamaño y posición. Esa misma posición se repetirá en
                todas las páginas (no en el certificado), así que conviene elegir una esquina o
                margen que normalmente esté libre de texto en todo el documento.
              </Alert>
            )}
            {s.armedFieldType && s.armedFieldType !== "LegalApprovalStamp" && (
              <Alert severity="info" sx={{ mt: 1, mb: 2 }}>Toca el documento para colocar el campo.</Alert>
            )}

            <Typography variant="subtitle2" sx={{ mt: 2, mb: 1, fontWeight: 700 }}>Campos colocados</Typography>
            {s.fields.length === 0 && <Typography variant="body2" color="text.secondary">Ninguno todavía.</Typography>}
            {s.recipients.map((r) => {
              const count = s.fields.filter((f) => f.recipientId === r.recipientId).length;
              return (
                <Typography key={r.recipientId} variant="body2" sx={{ color: count === 0 ? "#B23A2E" : "text.secondary" }}>
                  {r.fullName.split(" ")[0]}: {count} campo{count === 1 ? "" : "s"}
                </Typography>
              );
            })}

            {selectedField && (
              <>
                <Typography variant="subtitle2" sx={{ mt: 2, mb: 1, fontWeight: 700 }}>
                  Posición y tamaño ({FIELD_TYPE_LABELS[selectedField.type]})
                </Typography>
                {/* Uncontrolled + commit-on-blur (not onChange): committing on every keystroke fired
                    one API call per digit typed, which — combined with normal drag commits — was
                    enough to trip the Gateway's per-user rate limit during ordinary editing. Keyed
                    by fieldId so switching the selection resets the shown default; a drag update to
                    the SAME field won't live-refresh these while focused, which is an acceptable
                    trade-off since dragging and typing aren't done at the same time. */}
                <Box key={selectedField.fieldId} sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
                  <TextField
                    label="X (%)" size="small" type="number" slotProps={{ htmlInput: { step: 0.1, min: 0, max: 100 } }}
                    defaultValue={round1(selectedField.positionX * 100)}
                    onBlur={(e) => commitSelectedFieldPercent({ positionX: percentToFraction(e.target.value) })}
                    onKeyDown={(e) => e.key === "Enter" && e.currentTarget.blur()}
                  />
                  <TextField
                    label="Y (%)" size="small" type="number" slotProps={{ htmlInput: { step: 0.1, min: 0, max: 100 } }}
                    defaultValue={round1(selectedField.positionY * 100)}
                    onBlur={(e) => commitSelectedFieldPercent({ positionY: percentToFraction(e.target.value) })}
                    onKeyDown={(e) => e.key === "Enter" && e.currentTarget.blur()}
                  />
                  <TextField
                    label="Ancho (%)" size="small" type="number" slotProps={{ htmlInput: { step: 0.1, min: 1, max: 100 } }}
                    defaultValue={round1(selectedField.width * 100)}
                    onBlur={(e) => commitSelectedFieldPercent({ width: percentToFraction(e.target.value) })}
                    onKeyDown={(e) => e.key === "Enter" && e.currentTarget.blur()}
                  />
                  <TextField
                    label="Alto (%)" size="small" type="number" slotProps={{ htmlInput: { step: 0.1, min: 1, max: 100 } }}
                    defaultValue={round1(selectedField.height * 100)}
                    onBlur={(e) => commitSelectedFieldPercent({ height: percentToFraction(e.target.value) })}
                    onKeyDown={(e) => e.key === "Enter" && e.currentTarget.blur()}
                  />
                </Box>
                <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                  Posición exacta en porcentaje de la página (0–100). También puedes arrastrar el campo con el mouse.
                </Typography>
              </>
            )}

            {submitMutation.isError && (
              <Alert severity="error" sx={{ mt: 2 }}>
                {submitMutation.error instanceof ApiError ? submitMutation.error.message : "No se pudo enviar el sobre."}
              </Alert>
            )}

            <Button
              size="large" variant="contained" fullWidth startIcon={<SendIcon />} sx={{ mt: 3 }}
              disabled={recipientsWithoutFields.length > 0 || submitMutation.isPending}
              onClick={() => submitMutation.mutate()}
            >
              {submitMutation.isPending ? <CircularProgress size={20} /> : "Enviar sobre"}
            </Button>
            {recipientsWithoutFields.length > 0 && (
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
                Todos los firmantes necesitan al menos un campo antes de enviar.
              </Typography>
            )}
          </Paper>
        </Box>
      )}

      {s.activeStep === 3 && s.queuedIntentId && !s.dispatched && (
        <Paper sx={{ p: 3, maxWidth: 640, mx: "auto", textAlign: "center" }}>
          <CloudOffOutlinedIcon sx={{ fontSize: 48, color: BRAND_COLORS.orange, mb: 1 }} />
          <Typography variant="h6" gutterBottom>
            {queuedItem?.status === "failed"
              ? "No se pudo enviar"
              : queuedItem?.status === "needs-login"
              ? "Necesita iniciar sesión de nuevo"
              : "El sobre se enviará automáticamente"}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            {queuedItem?.status === "failed" || queuedItem?.status === "needs-login"
              ? (queuedItem.lastError ?? "Quedó pendiente en la cola local de este equipo.")
              : "En cuanto vuelva la conexión se creará y enviará solo — los enlaces para firmar aparecerán aquí, no hace falta que hagas nada más."}
          </Typography>
          {(queuedItem?.status === "failed" || queuedItem?.status === "needs-login") && (
            <Button variant="outlined" onClick={() => void processQueue()}>
              Reintentar
            </Button>
          )}
        </Paper>
      )}

      {s.activeStep === 3 && s.dispatched && (
        <Paper sx={{ p: 3, maxWidth: 640, mx: "auto", textAlign: "center" }}>
          <CheckCircleIcon sx={{ fontSize: 48, color: BRAND_COLORS.teal, mb: 1 }} />
          <Typography variant="h6" gutterBottom>Sobre enviado correctamente</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            El envío de correo automático todavía no está activo — comparte el enlace manualmente con cada firmante de la primera tanda.
          </Typography>

          {s.dispatched?.map((d) => {
            const url = `${window.location.origin}/firmar/publico/${d.accessToken}`;
            return (
              <Box key={d.recipientId} sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5, p: 1.5, borderRadius: 2, bgcolor: "#F5F8F7", textAlign: "left" }}>
                <Box sx={{ flexGrow: 1, overflow: "hidden" }}>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{d.email}</Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ wordBreak: "break-all" }}>{url}</Typography>
                </Box>
                <IconButton onClick={() => navigator.clipboard.writeText(url)}>
                  <ContentCopyIcon fontSize="small" />
                </IconButton>
              </Box>
            );
          })}

          <Box sx={{ display: "flex", justifyContent: "center", gap: 2, mt: 3 }}>
            <Button size="large" variant="contained" onClick={() => navigate(`/firmar/${s.envelopeId}`)}>
              Ver sobre
            </Button>
            <Button size="large" variant="outlined" onClick={() => { s.reset(); navigate("/firmar"); }}>
              Volver a la bandeja
            </Button>
          </Box>
        </Paper>
      )}
    </Box>
  );
}
