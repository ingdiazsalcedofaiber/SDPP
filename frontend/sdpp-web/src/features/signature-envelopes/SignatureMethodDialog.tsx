import { useEffect, useRef, useState } from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import Tab from "@mui/material/Tab";
import Tabs from "@mui/material/Tabs";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import CheckIcon from "@mui/icons-material/Check";
import { useQuery } from "@tanstack/react-query";
import { listSavedSignatures } from "../../shared/api/signature";
import type { SignatureMethodUsed } from "../../shared/api/signature";
import { BRAND_COLORS } from "../../shared/theme";

const CANVAS_WIDTH = 600;
const CANVAS_HEIGHT = 200;
const CROP_PADDING_PX = 8;

/** Same ink-bounding-box crop as the original single-signer module — see SignatureCanvas.tsx's own
 * doc comment for why this matters (the placement box must equal the visible ink, not a padded
 * transparent rectangle). Shared here because both the "Dibujar" and "Escribir" tabs produce a raw
 * canvas that needs the same treatment before becoming the final PNG. */
function cropToInk(source: HTMLCanvasElement): { canvas: HTMLCanvasElement; aspectRatio: number } {
  const ctx = source.getContext("2d");
  if (!ctx) return { canvas: source, aspectRatio: CANVAS_WIDTH / CANVAS_HEIGHT };

  const { data, width, height } = ctx.getImageData(0, 0, source.width, source.height);
  let minX = width, minY = height, maxX = -1, maxY = -1;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      if (data[(y * width + x) * 4 + 3] > 0) {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }
  }
  if (maxX < 0) return { canvas: source, aspectRatio: CANVAS_WIDTH / CANVAS_HEIGHT };

  const x = Math.max(0, minX - CROP_PADDING_PX);
  const y = Math.max(0, minY - CROP_PADDING_PX);
  const cropWidth = Math.min(width, maxX + CROP_PADDING_PX) - x;
  const cropHeight = Math.min(height, maxY + CROP_PADDING_PX) - y;
  const cropped = document.createElement("canvas");
  cropped.width = cropWidth;
  cropped.height = cropHeight;
  const croppedCtx = cropped.getContext("2d")!;
  // Keep the crop transparent — PdfSharp 6.1.1's cross-platform PNG decoder didn't honor alpha
  // (a transparent PNG rendered as an opaque black box), which used to force flattening onto white
  // here. Fixed at the source by upgrading to PdfSharp 6.2.4 (confirmed via an isolated probe), so
  // the signature can sit over existing text/lines/images without hiding them, matching how a real
  // ink signature behaves.
  croppedCtx.drawImage(source, x, y, cropWidth, cropHeight, 0, 0, cropWidth, cropHeight);
  return { canvas: cropped, aspectRatio: cropWidth / cropHeight };
}

interface SignatureMethodDialogProps {
  open: boolean;
  defaultName?: string;
  onCancel: () => void;
  onConfirm: (blob: Blob, aspectRatio: number, method: SignatureMethodUsed) => void;
}

/**
 * Every signature-image capture method the spec asks for (punto 4): dibujar a mano, escribir el
 * nombre y convertirlo en firma manuscrita, subir una imagen PNG, o reutilizar una firma guardada.
 * Whichever tab is used, the result normalizes to the same (Blob, aspectRatio, method) shape the
 * signing flow already expects — see EnvelopeSigningPage's onConfirm handler.
 */
export function SignatureMethodDialog({ open, defaultName, onCancel, onConfirm }: SignatureMethodDialogProps) {
  const [tab, setTab] = useState<"draw" | "type" | "upload" | "saved">("draw");
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const drawingRef = useRef(false);
  const [hasStrokes, setHasStrokes] = useState(false);
  const [typedName, setTypedName] = useState(defaultName ?? "");
  const [uploadPreview, setUploadPreview] = useState<{ blob: Blob; url: string; aspectRatio: number } | null>(null);

  // retry:false — an external recipient with no SDPP session always 401s here (saved signatures
  // are an internal-account feature); that's expected, not worth retrying 3x with backoff.
  const savedSignaturesQuery = useQuery({ queryKey: ["saved-signatures"], queryFn: listSavedSignatures, enabled: open, retry: false });

  useEffect(() => {
    if (open) {
      setTab("draw");
      setHasStrokes(false);
      setUploadPreview(null);
    }
  }, [open]);

  const getContext = () => canvasRef.current?.getContext("2d") ?? null;
  const pointFromEvent = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    return { x: ((e.clientX - rect.left) / rect.width) * CANVAS_WIDTH, y: ((e.clientY - rect.top) / rect.height) * CANVAS_HEIGHT };
  };
  const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
    e.currentTarget.setPointerCapture(e.pointerId);
    const ctx = getContext();
    if (!ctx) return;
    drawingRef.current = true;
    const { x, y } = pointFromEvent(e);
    ctx.beginPath();
    ctx.moveTo(x, y);
  };
  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current) return;
    const ctx = getContext();
    if (!ctx) return;
    const { x, y } = pointFromEvent(e);
    ctx.lineWidth = 2.5;
    ctx.lineCap = "round";
    ctx.strokeStyle = BRAND_COLORS.textDark;
    ctx.lineTo(x, y);
    ctx.stroke();
    setHasStrokes(true);
  };
  const handlePointerUp = () => { drawingRef.current = false; };
  const handleClear = () => {
    getContext()?.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    setHasStrokes(false);
  };

  const renderTypedName = () => {
    const canvas = document.createElement("canvas");
    canvas.width = CANVAS_WIDTH;
    canvas.height = CANVAS_HEIGHT;
    const ctx = canvas.getContext("2d")!;
    ctx.font = "64px cursive";
    ctx.fillStyle = BRAND_COLORS.textDark;
    ctx.textBaseline = "middle";
    ctx.fillText(typedName.trim(), 20, CANVAS_HEIGHT / 2, CANVAS_WIDTH - 40);
    return canvas;
  };

  const handleUploadFile = (file: File) => {
    const img = new Image();
    img.onload = () => {
      // No white-flattening needed (see cropToInk's doc comment) — draw as-is, preserving
      // whatever transparency the uploaded PNG already has.
      const canvas = document.createElement("canvas");
      canvas.width = img.width;
      canvas.height = img.height;
      const ctx = canvas.getContext("2d")!;
      ctx.drawImage(img, 0, 0);
      canvas.toBlob((blob) => {
        if (blob) setUploadPreview({ blob, url: URL.createObjectURL(blob), aspectRatio: img.width / img.height });
      }, "image/png");
    };
    img.src = URL.createObjectURL(file);
  };

  const confirmDraw = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const cropped = cropToInk(canvas);
    cropped.canvas.toBlob((blob) => blob && onConfirm(blob, cropped.aspectRatio, "Drawn"), "image/png");
  };

  const confirmTyped = () => {
    const cropped = cropToInk(renderTypedName());
    cropped.canvas.toBlob((blob) => blob && onConfirm(blob, cropped.aspectRatio, "Typed"), "image/png");
  };

  const confirmUpload = () => {
    if (uploadPreview) onConfirm(uploadPreview.blob, uploadPreview.aspectRatio, "Uploaded");
  };

  const confirmSaved = (imageUrl: string, aspectRatio: number) => {
    fetch(imageUrl).then((r) => r.blob()).then((blob) => onConfirm(blob, aspectRatio, "Reused"));
  };

  return (
    <Dialog open={open} onClose={onCancel} maxWidth="md" fullWidth>
      <DialogTitle>Capturar firma</DialogTitle>
      <Tabs value={tab} onChange={(_e, v) => setTab(v)} sx={{ px: 3 }}>
        <Tab value="draw" label="Dibujar" />
        <Tab value="type" label="Escribir mi nombre" />
        <Tab value="upload" label="Subir imagen" />
        {!!savedSignaturesQuery.data?.length && <Tab value="saved" label="Firmas guardadas" />}
      </Tabs>
      <DialogContent>
        {tab === "draw" && (
          <>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Firme con el dedo o un lápiz digital dentro del recuadro.
            </Typography>
            <Box sx={{ border: "2px dashed rgba(15,40,38,0.2)", borderRadius: 2, bgcolor: "#FBFDFC", touchAction: "none", width: "100%", aspectRatio: `${CANVAS_WIDTH} / ${CANVAS_HEIGHT}` }}>
              <canvas
                ref={canvasRef} width={CANVAS_WIDTH} height={CANVAS_HEIGHT}
                style={{ width: "100%", height: "100%", touchAction: "none", cursor: "crosshair" }}
                onPointerDown={handlePointerDown} onPointerMove={handlePointerMove} onPointerUp={handlePointerUp} onPointerLeave={handlePointerUp}
              />
            </Box>
            <Button size="small" startIcon={<RestartAltIcon />} onClick={handleClear} disabled={!hasStrokes} sx={{ mt: 1 }}>
              Limpiar
            </Button>
          </>
        )}

        {tab === "type" && (
          <>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Escriba su nombre; se convertirá automáticamente en una firma manuscrita.
            </Typography>
            <TextField fullWidth label="Su nombre" value={typedName} onChange={(e) => setTypedName(e.target.value)} sx={{ mb: 2 }} />
            {typedName.trim() && (
              <Box sx={{ border: "1px solid rgba(15,40,38,0.15)", borderRadius: 2, p: 2, textAlign: "center" }}>
                <Typography sx={{ fontFamily: "cursive", fontSize: 40, color: BRAND_COLORS.textDark }}>{typedName}</Typography>
              </Box>
            )}
          </>
        )}

        {tab === "upload" && (
          <>
            <Button component="label" variant="outlined" startIcon={<CloudUploadIcon />} sx={{ mb: 2 }}>
              Seleccionar imagen PNG
              <input type="file" accept="image/png,image/jpeg" hidden onChange={(e) => e.target.files?.[0] && handleUploadFile(e.target.files[0])} />
            </Button>
            {uploadPreview && (
              <Box sx={{ border: "1px solid rgba(15,40,38,0.15)", borderRadius: 2, p: 2, textAlign: "center" }}>
                <img src={uploadPreview.url} alt="Vista previa" style={{ maxWidth: "100%", maxHeight: 150 }} />
              </Box>
            )}
          </>
        )}

        {tab === "saved" && (
          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2 }}>
            {savedSignaturesQuery.data?.map((s) => (
              <Box
                key={s.id}
                onClick={() => confirmSaved(`/api/v1/signature/saved-signatures/${s.id}/image`, s.aspectRatio)}
                sx={{ border: "1px solid rgba(15,40,38,0.15)", borderRadius: 2, p: 1.5, cursor: "pointer", width: 180, textAlign: "center", "&:hover": { borderColor: BRAND_COLORS.teal } }}
              >
                <Typography variant="caption" color="text.secondary">{s.label}</Typography>
              </Box>
            ))}
          </Box>
        )}
      </DialogContent>
      <DialogActions sx={{ p: 2 }}>
        <Button size="large" variant="outlined" onClick={onCancel}>Cancelar</Button>
        {tab === "draw" && (
          <Button size="large" variant="contained" startIcon={<CheckIcon />} onClick={confirmDraw} disabled={!hasStrokes}>Confirmar firma</Button>
        )}
        {tab === "type" && (
          <Button size="large" variant="contained" startIcon={<CheckIcon />} onClick={confirmTyped} disabled={!typedName.trim()}>Confirmar firma</Button>
        )}
        {tab === "upload" && (
          <Button size="large" variant="contained" startIcon={<CheckIcon />} onClick={confirmUpload} disabled={!uploadPreview}>Confirmar firma</Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
