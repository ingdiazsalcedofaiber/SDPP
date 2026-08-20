import { useRef, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import IconButton from "@mui/material/IconButton";
import Paper from "@mui/material/Paper";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import BoltIcon from "@mui/icons-material/Bolt";
import NavigateBeforeIcon from "@mui/icons-material/NavigateBefore";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import { Document, Page, pdfjs } from "react-pdf";
import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { ApiError } from "../../shared/api/client";
import { BRAND_COLORS } from "../../shared/theme";
import { isValidEmail } from "../../shared/utils/validation";
import { useConnectivityStore } from "../../shared/offline/connectivityStore";
import { submitEnvelopeIntent } from "../../shared/offline/submitEnvelopeIntent";
import { EditableFieldBox, NewFieldPlacementCatcher } from "./FieldOverlay";
import type { FieldRect } from "./FieldOverlay";

pdfjs.GlobalWorkerOptions.workerSrc = new URL("pdfjs-dist/build/pdf.worker.min.mjs", import.meta.url).toString();

const BASE_WIDTH = 700;
const DEFAULT_FIELD_ID = "quick-sign-field";

/**
 * "Firma rápida" — the front-desk fast path for órdenes médicas/autorizaciones: no envelope title,
 * no signing-mode choice, no multi-recipient list, none of EnvelopeEditorPage's steps. Upload,
 * click once on the page for where the signature goes, hand the device to the patient. Under the
 * hood this is still exactly one envelope with one InPerson recipient — reuses
 * submitEnvelopeIntent (the same orchestrator the full editor's "Enviar sobre" uses) rather than
 * a second copy of the create→recipient→field→send sequence, then hands off to the existing
 * EnvelopeSigningPage for consent+signing+certificate — nothing about that flow changes here.
 * Requires connectivity: the whole point is an immediate, server-issued certificate, so unlike the
 * full editor there is no offline-queued fallback — see the "Sin conexión" alert below.
 */
export function QuickSignPage() {
  const navigate = useNavigate();
  const isOnline = useConnectivityStore((s) => s.isOnline);

  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState("");
  const [patientEmail, setPatientEmail] = useState("");
  const [patientName, setPatientName] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [pageCount, setPageCount] = useState(0);
  const [fieldRect, setFieldRect] = useState<(FieldRect & { pageNumber: number }) | null>(null);
  const pageContainerRef = useRef<HTMLDivElement | null>(null);

  const infoComplete = !!file && isValidEmail(patientEmail) && patientName.trim() !== "";
  const readyToSign = infoComplete && fieldRect !== null;

  const submitMutation = useMutation({
    // Same reason as ConvertWizardPage/EnvelopeEditorPage: TanStack Query's default
    // networkMode:"online" would silently hold this mutation if navigator.onLine is false instead
    // of ever calling mutationFn — irrelevant here in practice since the button is disabled while
    // offline, but keeping the same discipline as the rest of the offline-aware mutations avoids a
    // surprise if that guard is ever loosened.
    networkMode: "always",
    mutationFn: async () => {
      const recipientLocalId = `local-${crypto.randomUUID()}`;
      return submitEnvelopeIntent({
        file: file!,
        title: title.trim() || file!.name.replace(/\.pdf$/i, ""),
        signingMode: "Sequential",
        recipients: [{ localId: recipientLocalId, email: patientEmail.trim(), fullName: patientName.trim(), order: 1, inPerson: true }],
        fields: [{
          localId: `local-${crypto.randomUUID()}`, recipientLocalId, type: "Signature",
          pageNumber: fieldRect!.pageNumber, positionX: fieldRect!.positionX, positionY: fieldRect!.positionY,
          width: fieldRect!.width, height: fieldRect!.height, required: true,
        }],
        progress: {},
      });
    },
    onSuccess: (result) => {
      const token = result.dispatched[0]?.accessToken;
      if (token) navigate(`/firmar/publico/${token}`, { replace: true });
    },
  });

  return (
    <Box sx={{ maxWidth: 900, mx: "auto" }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
        <BoltIcon sx={{ color: BRAND_COLORS.orange }} />
        <Typography variant="h5">Firma rápida</Typography>
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Para órdenes médicas, autorizaciones y documentos que el paciente firma ahí mismo, en este equipo.
        El documento firmado y el certificado de firma se envían automáticamente al correo que indiques.
      </Typography>

      {!isOnline && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Sin conexión — la firma rápida necesita internet para generar el certificado al instante.
          Intenta de nuevo cuando vuelva la señal, o usa "Nuevo sobre" desde la bandeja si el documento puede esperar.
        </Alert>
      )}

      <Paper sx={{ p: 3, mb: 2 }}>
        <Button component="label" variant="outlined" size="large" startIcon={<CloudUploadIcon />} sx={{ mb: 2 }} fullWidth>
          {file ? file.name : "Seleccionar documento PDF"}
          <input
            type="file" accept="application/pdf" hidden
            onChange={(e) => { setFile(e.target.files?.[0] ?? null); setFieldRect(null); setCurrentPage(1); setPageCount(0); }}
          />
        </Button>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, mb: 2 }}>
          <TextField
            label="Correo del paciente" type="email" value={patientEmail} onChange={(e) => setPatientEmail(e.target.value)}
            error={patientEmail.trim() !== "" && !isValidEmail(patientEmail)}
            helperText={patientEmail.trim() !== "" && !isValidEmail(patientEmail) ? "Correo inválido" : " "}
            required fullWidth
          />
          <TextField label="Nombre del paciente" value={patientName} onChange={(e) => setPatientName(e.target.value)} helperText=" " required fullWidth />
        </Box>
        <TextField
          label="Descripción (opcional)" value={title} onChange={(e) => setTitle(e.target.value)}
          placeholder={file ? file.name.replace(/\.pdf$/i, "") : "Ej. Autorización de procedimiento"}
          fullWidth
        />
      </Paper>

      {file && (
        <Paper sx={{ p: 2, mb: 2 }}>
          {!fieldRect && (
            <Alert severity="info" sx={{ mb: 2 }}>Toca el documento donde debe ir la firma del paciente.</Alert>
          )}
          {pageCount > 1 && (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 1, mb: 1.5 }}>
              <IconButton size="small" onClick={() => setCurrentPage((p) => Math.max(1, p - 1))} disabled={currentPage <= 1}>
                <NavigateBeforeIcon />
              </IconButton>
              <Typography sx={{ minWidth: 100, textAlign: "center" }}>Página {currentPage} de {pageCount}</Typography>
              <IconButton size="small" onClick={() => setCurrentPage((p) => Math.min(pageCount, p + 1))} disabled={currentPage >= pageCount}>
                <NavigateNextIcon />
              </IconButton>
            </Box>
          )}
          <Box sx={{ display: "flex", justifyContent: "center" }}>
            <Box ref={pageContainerRef} sx={{ position: "relative", display: "inline-block", lineHeight: 0, "& canvas": { display: "block" } }}>
              <Document file={file} onLoadSuccess={({ numPages }) => setPageCount(numPages)}>
                <Page pageNumber={currentPage} width={BASE_WIDTH} renderTextLayer={false} renderAnnotationLayer={false} />
              </Document>
              {!fieldRect && (
                <NewFieldPlacementCatcher
                  pageContainerRef={pageContainerRef}
                  armedType="Signature"
                  onPlace={(rect) => setFieldRect({ ...rect, pageNumber: currentPage })}
                />
              )}
              {fieldRect && fieldRect.pageNumber === currentPage && (
                <EditableFieldBox
                  pageContainerRef={pageContainerRef}
                  field={{ fieldId: DEFAULT_FIELD_ID, type: "Signature", required: true, ...fieldRect }}
                  color={BRAND_COLORS.magenta}
                  label={patientName.split(" ")[0] || "Paciente"}
                  onCommit={(_id, rect) => setFieldRect({ ...rect, pageNumber: currentPage })}
                  onDelete={() => setFieldRect(null)}
                />
              )}
            </Box>
          </Box>
        </Paper>
      )}

      {submitMutation.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {submitMutation.error instanceof ApiError ? submitMutation.error.message : "No se pudo procesar la firma."}
        </Alert>
      )}

      <Button
        size="large" variant="contained" fullWidth startIcon={<BoltIcon />}
        disabled={!readyToSign || !isOnline || submitMutation.isPending}
        onClick={() => submitMutation.mutate()}
      >
        {submitMutation.isPending ? <CircularProgress size={20} /> : "Firmar ahora"}
      </Button>
    </Box>
  );
}
