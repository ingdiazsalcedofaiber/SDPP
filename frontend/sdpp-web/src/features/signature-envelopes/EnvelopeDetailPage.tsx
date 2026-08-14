import { useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import CancelIcon from "@mui/icons-material/Cancel";
import DrawIcon from "@mui/icons-material/Draw";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import DownloadIcon from "@mui/icons-material/Download";
import RefreshIcon from "@mui/icons-material/Refresh";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import RadioButtonUncheckedIcon from "@mui/icons-material/RadioButtonUnchecked";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router-dom";
import VerifiedIcon from "@mui/icons-material/Verified";
import { cancelEnvelope, downloadEnvelopeCertificate, getEnvelope, resendRecipientAccess } from "../../shared/api/signature";
import type { RecipientStatus } from "../../shared/api/signature";
import { ENVELOPE_STATUS_LABELS, RECIPIENT_STATUS_LABELS } from "../../shared/api/signature";
import { downloadDocument } from "../../shared/api/documents";
import { ApiError } from "../../shared/api/client";
import { BRAND_COLORS } from "../../shared/theme";
import { useAuthStore } from "../../app/auth";

// Forces América/Bogotá regardless of the viewing device's own OS timezone — without this,
// toLocaleString silently converts using whatever timezone the viewer's machine happens to be set
// to, which can read hours off from the real Colombian time these evidentiary timestamps must
// reflect (same reasoning as the certificate PDF's FormatCertificateTime on the backend).
function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString("es-CO", { dateStyle: "medium", timeStyle: "short", timeZone: "America/Bogota" }) : "—";
}

function recipientIcon(status: RecipientStatus) {
  if (status === "Signed") return <CheckCircleIcon sx={{ color: BRAND_COLORS.teal }} />;
  if (status === "Declined" || status === "Expired") return <HighlightOffIcon sx={{ color: "#B23A2E" }} />;
  return <RadioButtonUncheckedIcon sx={{ color: BRAND_COLORS.textLight }} />;
}

export function EnvelopeDetailPage() {
  const { envelopeId } = useParams<{ envelopeId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const currentUserId = useAuthStore((s) => s.user?.id);
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [resentLink, setResentLink] = useState<{ recipientId: string; url: string } | null>(null);

  const query = useQuery({ queryKey: ["signature-envelope", envelopeId], queryFn: () => getEnvelope(envelopeId!), enabled: !!envelopeId });

  const cancelMutation = useMutation({
    mutationFn: () => cancelEnvelope(envelopeId!),
    onSuccess: () => {
      setCancelDialogOpen(false);
      queryClient.invalidateQueries({ queryKey: ["signature-envelope", envelopeId] });
    },
  });

  const resendMutation = useMutation({
    mutationFn: (recipientId: string) => resendRecipientAccess(envelopeId!, recipientId),
    onSuccess: (result, recipientId) => setResentLink({ recipientId, url: `${window.location.origin}/firmar/publico/${result.accessToken}` }),
  });

  const signNowMutation = useMutation({
    mutationFn: (recipientId: string) => resendRecipientAccess(envelopeId!, recipientId),
    onSuccess: (result) => navigate(`/firmar/publico/${result.accessToken}`),
  });

  if (query.isPending) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (query.isError || !query.data) {
    return <Alert severity="error">No se pudo cargar el sobre.</Alert>;
  }

  const envelope = query.data;
  const isCreator = envelope.createdByUserId === currentUserId;
  const isTerminal = ["Completed", "Declined", "Cancelled", "Expired"].includes(envelope.status);
  const myTurnRecipient = envelope.recipients.find(
    (r) => r.matchedUserId === currentUserId && (r.status === "Sent" || r.status === "Viewed"),
  );
  const fieldsFor = (recipientId: string) => envelope.fields.filter((f) => f.recipientId === recipientId);

  return (
    <Box sx={{ maxWidth: 900, mx: "auto" }}>
      <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/firmar")} sx={{ mb: 2 }}>
        Volver a la bandeja
      </Button>

      <Paper sx={{ p: 3, mb: 3 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 2 }}>
          <Box>
            <Typography variant="h5" gutterBottom>{envelope.title}</Typography>
            {envelope.message && <Typography variant="body2" color="text.secondary">{envelope.message}</Typography>}
          </Box>
          <Chip label={ENVELOPE_STATUS_LABELS[envelope.status]} sx={{ fontWeight: 700, fontSize: 13, px: 1 }} />
        </Box>

        <Divider sx={{ my: 2 }} />

        <Box sx={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
          <Box>
            <Typography variant="caption" color="text.secondary">Modo</Typography>
            <Typography variant="body2">{envelope.signingMode === "Sequential" ? "Secuencial" : "Simultánea"}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">Creado</Typography>
            <Typography variant="body2">{formatDateTime(envelope.createdAtUtc)}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">Fecha límite</Typography>
            <Typography variant="body2">{formatDateTime(envelope.dueDateUtc)}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">Hash SHA-256 original</Typography>
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: 12 }}>{envelope.originalSha256Hash.slice(0, 24)}…</Typography>
          </Box>
        </Box>

        <Box sx={{ display: "flex", gap: 1.5, mt: 3, flexWrap: "wrap" }}>
          {myTurnRecipient && (
            <Button
              variant="contained" color="secondary" startIcon={<DrawIcon />}
              disabled={signNowMutation.isPending}
              onClick={() => signNowMutation.mutate(myTurnRecipient.recipientId)}
            >
              Firmar ahora
            </Button>
          )}
          <Button variant="outlined" startIcon={<DownloadIcon />} onClick={() => downloadDocument(envelope.sourceDocumentId, "documento-original.pdf")}>
            Descargar original
          </Button>
          {envelope.finalDocumentId && (
            <Button variant="contained" startIcon={<DownloadIcon />} onClick={() => downloadDocument(envelope.finalDocumentId!, "documento-firmado.pdf")}>
              Descargar documento firmado
            </Button>
          )}
          {envelope.status === "Completed" && (
            <>
              <Button variant="outlined" startIcon={<DownloadIcon />} onClick={() => downloadEnvelopeCertificate(envelope.envelopeId)}>
                Descargar certificado
              </Button>
              <Button
                variant="outlined" startIcon={<VerifiedIcon />}
                onClick={() => window.open(`/firmar/verificar/${envelope.envelopeId}`, "_blank", "noopener")}
              >
                Verificar documento
              </Button>
            </>
          )}
          {isCreator && !isTerminal && envelope.status !== "Draft" && (
            <Button variant="outlined" color="error" startIcon={<CancelIcon />} onClick={() => setCancelDialogOpen(true)}>
              Cancelar sobre
            </Button>
          )}
        </Box>
      </Paper>

      <Paper sx={{ p: 3 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>Firmantes</Typography>
        {envelope.recipients
          .slice()
          .sort((a, b) => a.order - b.order)
          .map((r) => (
            <Box key={r.recipientId} sx={{ display: "flex", alignItems: "flex-start", gap: 1.5, py: 1.5, borderBottom: "1px solid rgba(15,40,38,0.06)" }}>
              {recipientIcon(r.status)}
              <Box sx={{ flexGrow: 1 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {envelope.signingMode === "Sequential" && `${r.order}. `}{r.fullName} <Typography component="span" variant="caption" color="text.secondary">({r.email})</Typography>
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {RECIPIENT_STATUS_LABELS[r.status]}
                  {r.signedAtUtc && ` — firmó el ${formatDateTime(r.signedAtUtc)}`}
                  {r.viewedAtUtc && !r.signedAtUtc && ` — visto el ${formatDateTime(r.viewedAtUtc)}`}
                  {r.declinedAtUtc && ` — rechazó el ${formatDateTime(r.declinedAtUtc)}: ${r.declineReason}`}
                  {" · "}{fieldsFor(r.recipientId).length} campo{fieldsFor(r.recipientId).length === 1 ? "" : "s"}
                </Typography>
                {resentLink?.recipientId === r.recipientId && (
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1, mt: 0.5 }}>
                    <Typography variant="caption" sx={{ wordBreak: "break-all", color: BRAND_COLORS.teal }}>{resentLink.url}</Typography>
                    <IconButton size="small" onClick={() => navigator.clipboard.writeText(resentLink.url)}>
                      <ContentCopyIcon sx={{ fontSize: 14 }} />
                    </IconButton>
                  </Box>
                )}
              </Box>
              {isCreator && (r.status === "Sent" || r.status === "Viewed") && (
                <Button size="small" startIcon={<RefreshIcon />} onClick={() => resendMutation.mutate(r.recipientId)} disabled={resendMutation.isPending}>
                  Reenviar enlace
                </Button>
              )}
            </Box>
          ))}
      </Paper>

      <Dialog open={cancelDialogOpen} onClose={() => setCancelDialogOpen(false)}>
        <DialogTitle>¿Cancelar este sobre?</DialogTitle>
        <DialogContent>
          <DialogContentText>Ningún firmante podrá continuar firmando. Esta acción no se puede deshacer.</DialogContentText>
          {cancelMutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {cancelMutation.error instanceof ApiError ? cancelMutation.error.message : "No se pudo cancelar el sobre."}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCancelDialogOpen(false)}>Volver</Button>
          <Button color="error" variant="contained" disabled={cancelMutation.isPending} onClick={() => cancelMutation.mutate()}>
            Sí, cancelar sobre
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
