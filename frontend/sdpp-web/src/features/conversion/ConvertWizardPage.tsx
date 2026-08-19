import type { ReactNode } from "react";
import { useEffect } from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Paper from "@mui/material/Paper";
import Step from "@mui/material/Step";
import StepLabel from "@mui/material/StepLabel";
import Stepper from "@mui/material/Stepper";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import Alert from "@mui/material/Alert";
import CircularProgress from "@mui/material/CircularProgress";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import DescriptionIcon from "@mui/icons-material/Description";
import TableChartIcon from "@mui/icons-material/TableChart";
import SlideshowIcon from "@mui/icons-material/Slideshow";
import ImageIcon from "@mui/icons-material/Image";
import CallMergeIcon from "@mui/icons-material/CallMerge";
import CallSplitIcon from "@mui/icons-material/CallSplit";
import RotateRightIcon from "@mui/icons-material/RotateRight";
import DeleteSweepIcon from "@mui/icons-material/DeleteSweep";
import ReorderIcon from "@mui/icons-material/Reorder";
import FormatListNumberedIcon from "@mui/icons-material/FormatListNumbered";
import CompressIcon from "@mui/icons-material/Compress";
import DocumentScannerIcon from "@mui/icons-material/DocumentScanner";
import OpacityIcon from "@mui/icons-material/Opacity";
import LockIcon from "@mui/icons-material/Lock";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import CloudOffOutlinedIcon from "@mui/icons-material/CloudOffOutlined";
import { useMutation, useQuery } from "@tanstack/react-query";
import { downloadDocument, getDocumentStatus } from "../../shared/api/documents";
import { ApiError } from "../../shared/api/client";
import type { OperationType } from "../../shared/api/types";
import { BRAND_COLORS, CLASSIFICATION_COLORS } from "../../shared/theme";
import { translateJobStatus } from "../../shared/translations";
import { useConnectivityStore } from "../../shared/offline/connectivityStore";
import { enqueueConversion } from "../../shared/offline/enqueue";
import { processQueue } from "../../shared/offline/queueProcessor";
import { submitConversionIntent } from "../../shared/offline/submitConversionIntent";
import { useQueueStore } from "../../shared/offline/queueStore";
import { useConversionWizardStore } from "./wizardStore";

interface OperationDef {
  value: OperationType;
  label: string;
  description?: string;
  icon: ReactNode;
}

interface OperationCategory {
  title: string;
  color: string;
  operations: OperationDef[];
}

// DigitalSign is deliberately absent — see SDPP.Documents.Domain/Enums/Enums.cs on OperationType
// for why (no real PKI integration yet; a fake signature would be worse than none at all).
// Grouped + iconified + color-coded per category (same "color = quick visual scan" idea already
// used for the Dashboard's metric cards) so the picker below reads as a set of tools rather than
// a plain dropdown.
const OPERATION_CATEGORIES: OperationCategory[] = [
  {
    title: "Convertir a PDF",
    color: BRAND_COLORS.teal,
    operations: [
      { value: "WordToPdf", label: "Word → PDF", icon: <DescriptionIcon /> },
      { value: "ExcelToPdf", label: "Excel → PDF", icon: <TableChartIcon /> },
      { value: "PptToPdf", label: "PowerPoint → PDF", icon: <SlideshowIcon /> },
      { value: "ImageToPdf", label: "Imagen → PDF", icon: <ImageIcon /> },
    ],
  },
  {
    title: "Convertir desde PDF",
    color: BRAND_COLORS.orange,
    operations: [
      { value: "PdfToWord", label: "PDF → Word", description: "Texto extraído, sin diseño original", icon: <DescriptionIcon /> },
      { value: "PdfToExcel", label: "PDF → Excel", description: "Texto extraído, sin diseño original", icon: <TableChartIcon /> },
      { value: "PdfToImage", label: "PDF → Imagen", icon: <ImageIcon /> },
      { value: "PdfToPpt", label: "PDF → PowerPoint", description: "Una imagen de página por diapositiva", icon: <SlideshowIcon /> },
    ],
  },
  {
    title: "Organizar PDF",
    color: BRAND_COLORS.magenta,
    operations: [
      { value: "Merge", label: "Combinar PDF", icon: <CallMergeIcon /> },
      { value: "Split", label: "Separar PDF", icon: <CallSplitIcon /> },
      { value: "Rotate", label: "Rotar páginas", icon: <RotateRightIcon /> },
      { value: "DeletePages", label: "Eliminar páginas", icon: <DeleteSweepIcon /> },
      { value: "ReorderPages", label: "Reordenar páginas", icon: <ReorderIcon /> },
      { value: "PageNumbering", label: "Numerar páginas", icon: <FormatListNumberedIcon /> },
    ],
  },
  {
    title: "Optimizar y reconocer",
    color: "#6A1B9A",
    operations: [
      { value: "Compress", label: "Comprimir PDF", icon: <CompressIcon /> },
      { value: "Ocr", label: "OCR", description: "Hacer buscable un PDF/imagen escaneado", icon: <DocumentScannerIcon /> },
      { value: "Watermark", label: "Marca de agua", icon: <OpacityIcon /> },
    ],
  },
  {
    title: "Contraseña",
    color: CLASSIFICATION_COLORS.Restringida,
    operations: [
      { value: "Protect", label: "Proteger con contraseña", icon: <LockIcon /> },
      { value: "Unlock", label: "Quitar contraseña", icon: <LockOpenIcon /> },
    ],
  },
];

const steps = ["Seleccionar archivo y formato", "Convertir y descargar"];

/** Which extra fields (Documents.Application's IReadOnlyDictionary<string,string>
 * operationParameters) each operation actually reads — see the matching engine under
 * SDPP.Documents.Infrastructure/Engines for the authoritative parameter names. */
const PARAM_FIELDS: Partial<Record<OperationType, { key: string; label: string; required?: boolean; helperText?: string }[]>> = {
  Rotate: [
    { key: "angle", label: "Ángulo (90, 180, 270, -90, -180, -270)", required: true },
    { key: "pages", label: "Páginas (ej. 1-3,5) — vacío = todas" },
  ],
  DeletePages: [{ key: "pages", label: "Páginas a eliminar (ej. 2,4-6)", required: true }],
  ReorderPages: [{ key: "order", label: "Nuevo orden de páginas (ej. 3,1,2)", required: true }],
  Protect: [
    { key: "password", label: "Contraseña", required: true },
    { key: "ownerPassword", label: "Contraseña de propietario (opcional)" },
  ],
  Unlock: [{ key: "password", label: "Contraseña actual del documento", required: true }],
  Watermark: [{ key: "text", label: "Texto de la marca de agua", required: true }],
  PageNumbering: [{ key: "format", label: "Formato", helperText: "Usa {page} y {total} — por defecto 'Página {page} de {total}'" }],
  Compress: [{ key: "preset", label: "Calidad (screen, ebook, printer, prepress)", helperText: "Por defecto 'ebook'" }],
  PdfToImage: [{ key: "format", label: "Formato de imagen (png, jpeg)", helperText: "Por defecto 'png'" }],
};

/**
 * The Conversion Panel's single responsibility: convert a file. Seleccionar archivo → elegir
 * formato → convertir → descargar — nothing about classification, information security, hashing,
 * watermarking, auditing, or document management lives here; the backend's Documents/Classification
 * modules handle those on their own, invisibly, without gating or being shown in this flow.
 */
export function ConvertWizardPage() {
  // Kept in a store (not local useState) so switching to another section and coming back doesn't
  // lose progress — this page unmounts on every route change, see wizardStore.ts.
  const activeStep = useConversionWizardStore((s) => s.activeStep);
  const setActiveStep = useConversionWizardStore((s) => s.setActiveStep);
  const file = useConversionWizardStore((s) => s.file);
  const setFile = useConversionWizardStore((s) => s.setFile);
  const additionalFiles = useConversionWizardStore((s) => s.additionalFiles);
  const setAdditionalFiles = useConversionWizardStore((s) => s.setAdditionalFiles);
  const operationType = useConversionWizardStore((s) => s.operationType);
  const setOperationType = useConversionWizardStore((s) => s.setOperationType);
  const operationParams = useConversionWizardStore((s) => s.operationParams);
  const setOperationParams = useConversionWizardStore((s) => s.setOperationParams);
  const documentId = useConversionWizardStore((s) => s.documentId);
  const setDocumentId = useConversionWizardStore((s) => s.setDocumentId);
  const jobId = useConversionWizardStore((s) => s.jobId);
  const setJobId = useConversionWizardStore((s) => s.setJobId);
  const queuedIntentId = useConversionWizardStore((s) => s.queuedIntentId);
  const setQueuedIntentId = useConversionWizardStore((s) => s.setQueuedIntentId);
  const resetWizard = useConversionWizardStore((s) => s.reset);

  const isOnline = useConnectivityStore((s) => s.isOnline);
  const queuedItem = useQueueStore((s) => s.items.find((i) => i.id === queuedIntentId));
  const queuedResult = useQueueStore((s) => (queuedIntentId ? s.results[queuedIntentId] : undefined));
  const clearQueueResult = useQueueStore((s) => s.clearResult);

  // Once the queue processor actually runs this intent (immediately if connectivity returns while
  // the page is still open, or on a later app boot), its real result lands in queueStore.results —
  // pick it up here and switch to the normal polling view exactly as if the conversion had just
  // been requested online.
  useEffect(() => {
    if (queuedResult?.kind === "conversion" && queuedIntentId) {
      setDocumentId(queuedResult.documentId);
      setJobId(queuedResult.jobId);
      clearQueueResult(queuedIntentId);
      setQueuedIntentId(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [queuedResult]);

  const isMerge = operationType === "Merge";
  const paramFields = PARAM_FIELDS[operationType] ?? [];

  const convertMutation = useMutation({
    // TanStack Query defaults mutations to networkMode: "online", which PAUSES mutationFn from
    // ever running at all while navigator.onLine is false — its own onlineManager silently queues
    // the call and only invokes mutationFn once IT detects connectivity again. That fights this
    // feature directly: the whole point is to run the isOnline check ourselves and enqueue right
    // away instead of leaving the request in TanStack Query's own (invisible, IndexedDB-less) hold
    // state. "always" makes mutate() invoke mutationFn immediately regardless, so our own
    // connectivityStore check below is the only thing deciding submit-now vs. enqueue.
    networkMode: "always",
    mutationFn: async (): Promise<{ kind: "immediate"; documentId: string; jobId: string } | { kind: "queued"; id: string }> => {
      const payload = { file: file!, additionalFiles, operationType, operationParameters: operationParams };

      if (!isOnline) {
        const id = await enqueueConversion(payload);
        return { kind: "queued", id };
      }

      const result = await submitConversionIntent({ ...payload, progress: {} });
      return { kind: "immediate", ...result };
    },
    onSuccess: (result) => {
      if (result.kind === "immediate") {
        setDocumentId(result.documentId);
        setJobId(result.jobId);
      } else {
        setQueuedIntentId(result.id);
      }
      setActiveStep(1);
    },
  });

  const statusQuery = useQuery({
    queryKey: ["documentStatus", documentId],
    queryFn: () => getDocumentStatus(documentId!),
    enabled: !!documentId && activeStep === 1,
    refetchInterval: (query) => {
      const job = query.state.data?.jobs.find((j) => j.id === jobId);
      return job && ["Completed", "Failed", "Rejected"].includes(job.status) ? false : 2000;
    },
  });

  const requiredParamsFilled = paramFields.every((f) => !f.required || (operationParams[f.key] ?? "").trim().length > 0);
  const mergeReady = !isMerge || additionalFiles.length >= 1;
  const canConvert = !!file && mergeReady && requiredParamsFilled;

  const currentJob = statusQuery.data?.jobs.find((j) => j.id === jobId);

  return (
    <Box sx={{ maxWidth: 1200, mx: "auto" }}>
      <Typography variant="h5" gutterBottom>
        Nueva conversión
      </Typography>

      <Stepper activeStep={activeStep} sx={{ mb: 4 }}>
        {steps.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      {activeStep === 0 && (
        <Paper sx={{ p: 3 }}>
          <Typography variant="subtitle2" sx={{ mb: 2, color: "text.secondary", fontWeight: 700 }}>
            ¿Qué quieres hacer?
          </Typography>

          <Stack spacing={3} sx={{ mb: 3 }}>
            {OPERATION_CATEGORIES.map((category) => (
              <Box key={category.title}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1 }}>
                  <Box sx={{ width: 8, height: 8, borderRadius: "50%", bgcolor: category.color }} />
                  <Typography
                    variant="caption"
                    sx={{ color: "text.secondary", fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.6 }}
                  >
                    {category.title}
                  </Typography>
                </Box>
                <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(150px, 190px))", gap: 1.5 }}>
                  {category.operations.map((op) => {
                    const selected = operationType === op.value;
                    return (
                      <Paper
                        key={op.value}
                        variant="outlined"
                        onClick={() => {
                          setOperationType(op.value);
                          setOperationParams({});
                          setAdditionalFiles([]);
                        }}
                        sx={{
                          position: "relative",
                          p: 1.75,
                          cursor: "pointer",
                          textAlign: "center",
                          borderRadius: 2.5,
                          borderWidth: selected ? 2 : 1,
                          borderColor: selected ? category.color : "divider",
                          bgcolor: selected ? `${category.color}0D` : "background.paper",
                          boxShadow: selected ? `0 8px 18px ${category.color}33` : "none",
                          transition: "transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease",
                          "&:hover": { borderColor: category.color, transform: "translateY(-2px)", boxShadow: `0 8px 18px ${category.color}2E` },
                        }}
                      >
                        {selected && (
                          <CheckCircleIcon sx={{ position: "absolute", top: 6, right: 6, fontSize: 16, color: category.color, bgcolor: "#fff", borderRadius: "50%" }} />
                        )}
                        <Box
                          sx={{
                            width: 38,
                            height: 38,
                            mx: "auto",
                            mb: 0.75,
                            borderRadius: "50%",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            background: `linear-gradient(135deg, ${category.color}CC 0%, ${category.color} 100%)`,
                            color: "#fff",
                            boxShadow: `0 3px 8px ${category.color}4D`,
                            "& svg": { fontSize: 19 },
                          }}
                        >
                          {op.icon}
                        </Box>
                        <Typography variant="body2" sx={{ fontWeight: selected ? 700 : 600, lineHeight: 1.3 }}>
                          {op.label}
                        </Typography>
                        {op.description && (
                          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.25, fontSize: 10.5, lineHeight: 1.2 }}>
                            {op.description}
                          </Typography>
                        )}
                      </Paper>
                    );
                  })}
                </Box>
              </Box>
            ))}
          </Stack>

          <Button component="label" variant="outlined" sx={{ mb: 2 }}>
            {file ? file.name : isMerge ? "Seleccionar primer documento" : "Seleccionar archivo"}
            <input type="file" hidden onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          </Button>

          {isMerge && (
            <Box sx={{ mb: 2 }}>
              <Button component="label" variant="outlined" sx={{ mb: 1 }}>
                Agregar más documentos a combinar
                <input
                  type="file"
                  hidden
                  multiple
                  onChange={(e) => setAdditionalFiles((prev) => [...prev, ...Array.from(e.target.files ?? [])])}
                />
              </Button>
              {additionalFiles.length > 0 && (
                <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                  {additionalFiles.map((f, i) => (
                    <Chip
                      key={`${f.name}-${i}`}
                      label={f.name}
                      onDelete={() => setAdditionalFiles((prev) => prev.filter((_, idx) => idx !== i))}
                    />
                  ))}
                </Stack>
              )}
              {!mergeReady && (
                <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
                  Agrega al menos un documento más para combinar.
                </Typography>
              )}
            </Box>
          )}

          {paramFields.length > 0 && (
            <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 2, mb: 2 }}>
              {paramFields.map((f) => (
                <TextField
                  key={f.key}
                  label={f.label}
                  required={f.required}
                  helperText={f.helperText}
                  value={operationParams[f.key] ?? ""}
                  onChange={(e) => setOperationParams((p) => ({ ...p, [f.key]: e.target.value }))}
                />
              ))}
            </Box>
          )}

          {convertMutation.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {convertMutation.error instanceof ApiError
                ? convertMutation.error.message
                : "No se pudo iniciar la conversión."}
            </Alert>
          )}

          <Box>
            <Button
              variant="contained"
              disabled={!canConvert || convertMutation.isPending}
              onClick={() => convertMutation.mutate()}
            >
              {convertMutation.isPending ? <CircularProgress size={20} /> : "Convertir"}
            </Button>
          </Box>
        </Paper>
      )}

      {activeStep === 1 && queuedIntentId && !documentId && (
        <Paper sx={{ p: 3 }}>
          <Typography variant="subtitle1" gutterBottom>
            Estado del proceso
          </Typography>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1 }}>
            <CloudOffOutlinedIcon sx={{ color: BRAND_COLORS.orange }} />
            <Typography sx={{ fontWeight: 600 }}>
              {queuedItem?.status === "failed"
                ? "No se pudo enviar"
                : queuedItem?.status === "needs-login"
                ? "Necesita iniciar sesión de nuevo"
                : "Se enviará automáticamente cuando vuelva la conexión"}
            </Typography>
          </Box>
          <Typography variant="body2" color="text.secondary">
            {queuedItem?.status === "failed" || queuedItem?.status === "needs-login"
              ? (queuedItem.lastError ?? "Quedó pendiente en la cola local de este equipo.")
              : "El archivo y la operación quedaron guardados en este equipo — no hace falta que hagas nada más."}
          </Typography>
          {(queuedItem?.status === "failed" || queuedItem?.status === "needs-login") && (
            <Button variant="outlined" sx={{ mt: 2 }} onClick={() => void processQueue()}>
              Reintentar
            </Button>
          )}
        </Paper>
      )}

      {activeStep === 1 && !(queuedIntentId && !documentId) && (
        <Paper sx={{ p: 3 }}>
          <Typography variant="subtitle1" gutterBottom>
            Estado del proceso
          </Typography>
          {!currentJob && <CircularProgress size={24} />}
          {currentJob && (
            <Box>
              <Typography>Estado: <strong>{translateJobStatus(currentJob.status)}</strong></Typography>

              {currentJob.status === "Completed" && currentJob.outputDocumentId && (
                <Alert
                  severity="success"
                  sx={{ mt: 2 }}
                  action={
                    <Button
                      color="inherit"
                      size="small"
                      onClick={() => downloadDocument(currentJob.outputDocumentId!, "documento-convertido")}
                    >
                      Descargar
                    </Button>
                  }
                >
                  Conversión completada.
                </Alert>
              )}
              {currentJob.status === "Failed" && (
                <Alert severity="error" sx={{ mt: 2 }}>
                  {currentJob.errorDetail ?? "El proceso falló."}
                </Alert>
              )}
              {["Completed", "Failed", "Rejected"].includes(currentJob.status) && (
                <Button
                  variant="outlined"
                  startIcon={<RestartAltIcon />}
                  sx={{ mt: 3 }}
                  onClick={resetWizard}
                >
                  Convertir otro documento
                </Button>
              )}
            </Box>
          )}
        </Paper>
      )}
    </Box>
  );
}
