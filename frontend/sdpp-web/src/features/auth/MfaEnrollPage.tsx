import { useState } from "react";
import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import TextField from "@mui/material/TextField";
import Chip from "@mui/material/Chip";
import Checkbox from "@mui/material/Checkbox";
import FormControlLabel from "@mui/material/FormControlLabel";
import ShieldOutlinedIcon from "@mui/icons-material/ShieldOutlined";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { confirmMfaEnrollment } from "../../app/auth";
import { ApiError } from "../../shared/api/client";

const LOGIN_ACCENT = "#6A1B9A";

interface EnrollLocationState {
  qrCodeDataUri: string;
  manualEntryKey: string;
}

/**
 * First-ever login for any user (MFA is mandatory from the start, see docs on the auth design) —
 * shows the QR generated server-side by AuthenticateFirstFactorAsync, then, once the code is
 * confirmed, forces the user to acknowledge their one-time backup codes before entering the app.
 */
export function MfaEnrollPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as EnrollLocationState | null;

  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [backupCodes, setBackupCodes] = useState<string[] | null>(null);
  const [savedCodesAck, setSavedCodesAck] = useState(false);

  if (!state?.qrCodeDataUri) {
    return <Navigate to="/login" replace />;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      const result = await confirmMfaEnrollment(code);
      setBackupCodes(result.backupCodes);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "No se pudo activar el MFA. Intenta de nuevo.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        background: "linear-gradient(135deg, #FCEFE0 0%, #F3F6E4 25%, #F1EDF7 50%, #FBEAF1 75%, #E9F5F3 100%)",
        p: 2,
      }}
    >
      <Paper elevation={4} sx={{ maxWidth: 480, width: "100%", borderRadius: 4, p: { xs: 3, sm: 5 } }}>
        <Box component="img" src="/logo-tics.png" alt="Tecnologías de Información y Comunicaciones" sx={{ height: 72, width: "auto", mb: 3, display: "block", mx: "auto" }} />

        {!backupCodes ? (
          <>
            <Chip
              icon={<ShieldOutlinedIcon sx={{ color: `${LOGIN_ACCENT} !important` }} />}
              label="ACTIVAR VERIFICACIÓN EN DOS PASOS"
              size="small"
              variant="outlined"
              sx={{ mb: 2, color: LOGIN_ACCENT, borderColor: LOGIN_ACCENT, fontWeight: 700 }}
            />
            <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
              Configura tu app autenticadora
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
              SDPP requiere un segundo factor de autenticación desde el primer inicio de sesión.
              Escanea este código con Google Authenticator, Microsoft Authenticator o una app
              similar.
            </Typography>

            <Box sx={{ display: "flex", justifyContent: "center", mb: 2 }}>
              <Box
                component="img"
                src={state.qrCodeDataUri}
                alt="Código QR para configurar MFA"
                sx={{ width: 220, height: 220, border: "1px solid", borderColor: "divider", borderRadius: 2 }}
              />
            </Box>

            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 0.5 }}>
              ¿No puedes escanear? Ingresa esta clave manualmente:
            </Typography>
            <Typography
              variant="body2"
              sx={{ fontFamily: "monospace", fontWeight: 700, letterSpacing: 1, mb: 3, wordBreak: "break-all" }}
            >
              {state.manualEntryKey}
            </Typography>

            {error && (
              <Alert severity="error" sx={{ mb: 2, textAlign: "left" }}>
                {error}
              </Alert>
            )}

            <Box component="form" onSubmit={handleSubmit}>
              <TextField
                label="Código de 6 dígitos"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                fullWidth
                autoFocus
                slotProps={{ htmlInput: { inputMode: "numeric", maxLength: 6 } }}
                sx={{ mb: 2 }}
              />
              <Button
                type="submit"
                variant="contained"
                fullWidth
                disabled={isSubmitting || code.length === 0}
                sx={{ bgcolor: LOGIN_ACCENT, color: "#fff", "&:hover": { bgcolor: "#57157A" } }}
              >
                Activar
              </Button>
            </Box>
          </>
        ) : (
          <>
            <Chip label="GUARDA TUS CÓDIGOS DE RESPALDO" size="small" color="warning" sx={{ mb: 2, fontWeight: 700 }} />
            <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
              MFA activado
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Guarda estos 10 códigos en un lugar seguro. Cada uno funciona una sola vez y son la
              única forma de entrar si pierdes tu dispositivo — no volverán a mostrarse.
            </Typography>

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: "1fr 1fr",
                gap: 1,
                p: 2,
                mb: 2,
                bgcolor: "action.hover",
                borderRadius: 2,
                fontFamily: "monospace",
              }}
            >
              {backupCodes.map((backupCode) => (
                <Typography key={backupCode} variant="body2" sx={{ fontFamily: "monospace" }}>
                  {backupCode}
                </Typography>
              ))}
            </Box>

            <FormControlLabel
              control={<Checkbox checked={savedCodesAck} onChange={(e) => setSavedCodesAck(e.target.checked)} />}
              label="Ya guardé mis códigos de respaldo en un lugar seguro"
              sx={{ mb: 2 }}
            />

            <Button
              variant="contained"
              fullWidth
              disabled={!savedCodesAck}
              onClick={() => navigate("/", { replace: true })}
              sx={{ bgcolor: LOGIN_ACCENT, color: "#fff", "&:hover": { bgcolor: "#57157A" } }}
            >
              Continuar
            </Button>
          </>
        )}
      </Paper>
    </Box>
  );
}
