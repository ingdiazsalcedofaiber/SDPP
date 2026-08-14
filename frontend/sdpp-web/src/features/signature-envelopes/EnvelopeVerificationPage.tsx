import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Divider from "@mui/material/Divider";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { verifyEnvelope, ENVELOPE_STATUS_LABELS, RECIPIENT_STATUS_LABELS } from "../../shared/api/signature";
import { ApiError } from "../../shared/api/client";
import { BRAND_COLORS } from "../../shared/theme";
import { SdppLogo } from "../../shared/ui/SdppLogo";

// Forces América/Bogotá — see EnvelopeDetailPage.tsx's formatDateTime for why this can't rely on
// the viewing device's own OS timezone (this is the PUBLIC verifier, viewed by anyone, anywhere).
function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString("es-CO", { dateStyle: "medium", timeStyle: "short", timeZone: "America/Bogota" }) : "—";
}

function CheckRow({ ok, label }: { ok: boolean; label: string }) {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 0.5 }}>
      {ok ? <CheckCircleIcon sx={{ color: BRAND_COLORS.teal, fontSize: 20 }} /> : <HighlightOffIcon sx={{ color: "#B23A2E", fontSize: 20 }} />}
      <Typography variant="body2">{label}</Typography>
    </Box>
  );
}

/**
 * The one truly public page in this module — reached by scanning the QR code printed on a
 * completion certificate, or by pasting an envelope ID directly. No login, no recipient token.
 * Recomputes the final document's hash server-side (see VerifyEnvelopeQuery) so this reports real
 * integrity, not just "the database says it's fine".
 */
export function EnvelopeVerificationPage() {
  const { envelopeId } = useParams<{ envelopeId: string }>();
  const query = useQuery({
    queryKey: ["envelope-verify", envelopeId],
    queryFn: () => verifyEnvelope(envelopeId!),
    enabled: !!envelopeId,
    retry: false,
  });

  return (
    <Box sx={{ minHeight: "100vh", bgcolor: "#F5F8F7", py: 4, px: 2 }}>
      <Box sx={{ maxWidth: 560, mx: "auto" }}>
        <Box sx={{ display: "flex", justifyContent: "center", mb: 3 }}>
          <SdppLogo />
        </Box>

        {query.isPending && (
          <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
            <CircularProgress />
          </Box>
        )}

        {query.isError && (
          <Paper sx={{ p: 4, textAlign: "center" }}>
            <HighlightOffIcon sx={{ fontSize: 48, color: "#B23A2E", mb: 1 }} />
            <Typography variant="h6" gutterBottom>No se pudo verificar</Typography>
            <Typography variant="body2" color="text.secondary">
              {query.error instanceof ApiError ? query.error.message : "Este identificador de sobre no existe."}
            </Typography>
          </Paper>
        )}

        {query.data && (
          <Paper sx={{ p: 4 }}>
            <Typography variant="h6" gutterBottom>Verificación de documento</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{query.data.title}</Typography>

            {query.data.status !== "Completed" ? (
              <Alert severity="warning" icon={<WarningAmberIcon />}>
                Este sobre todavía no está completado (estado: {ENVELOPE_STATUS_LABELS[query.data.status]}). La
                integridad del documento solo puede verificarse una vez que todos los firmantes hayan firmado.
              </Alert>
            ) : query.data.isIntact ? (
              <Alert severity="success" icon={<CheckCircleIcon />} sx={{ mb: 2 }}>
                ✅ Documento íntegro — no se detectaron cambios desde su finalización.
              </Alert>
            ) : !query.data.auditTrailIntact ? (
              <Alert severity="error" icon={<WarningAmberIcon />} sx={{ mb: 2 }}>
                ❌ AUDITORÍA COMPROMETIDA — la bitácora de eventos de este sobre no supera la verificación de
                integridad (hashes encadenados).
              </Alert>
            ) : (
              <Alert severity="error" icon={<WarningAmberIcon />} sx={{ mb: 2 }}>
                ❌ DOCUMENTO ALTERADO — el contenido almacenado no coincide con el hash registrado al completarse.
              </Alert>
            )}

            {query.data.status === "Completed" && (
              <Box sx={{ mb: 2 }}>
                <CheckRow ok={query.data.documentHashMatches} label="Hash del documento final válido" />
                <CheckRow ok={query.data.certificateHashMatches} label="Hash del certificado válido" />
                <CheckRow ok={query.data.auditTrailIntact} label={`Auditoría íntegra (${query.data.auditRecordCount} eventos verificados)`} />
                <CheckRow ok={query.data.recipients.every((r) => r.status === "Signed")} label="Todos los firmantes completaron su firma" />
              </Box>
            )}

            <Divider sx={{ my: 2 }} />

            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>Firmantes</Typography>
            {query.data.recipients.map((r) => (
              <Box key={r.email} sx={{ display: "flex", justifyContent: "space-between", py: 0.5 }}>
                <Box>
                  <Typography variant="body2">{r.fullName}</Typography>
                  <Typography variant="caption" color="text.secondary">{r.email}</Typography>
                </Box>
                <Box sx={{ textAlign: "right" }}>
                  <Chip size="small" label={RECIPIENT_STATUS_LABELS[r.status]} />
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>{formatDateTime(r.signedAtUtc)}</Typography>
                </Box>
              </Box>
            ))}

            <Divider sx={{ my: 2 }} />

            <Typography variant="caption" color="text.secondary" component="div" sx={{ wordBreak: "break-all" }}>
              Hash original: {query.data.originalSha256Hash}
            </Typography>
            {query.data.finalSha256Hash && (
              <Typography variant="caption" color="text.secondary" component="div" sx={{ wordBreak: "break-all" }}>
                Hash final: {query.data.finalSha256Hash}
              </Typography>
            )}
            {query.data.envelopeHash && (
              <Typography variant="caption" color="text.secondary" component="div" sx={{ wordBreak: "break-all" }}>
                Hash del sobre: {query.data.envelopeHash}
              </Typography>
            )}
          </Paper>
        )}
      </Box>
    </Box>
  );
}
