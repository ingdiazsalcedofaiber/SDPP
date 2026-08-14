import { useState } from "react";
import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import TextField from "@mui/material/TextField";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { useNavigate } from "react-router-dom";
import { verifyMfa } from "../../app/auth";
import { ApiError } from "../../shared/api/client";

const LOGIN_ACCENT = "#6A1B9A";

/** Second factor on every login after the first — accepts a live TOTP code or a backup code (see
 * VerifyMfaAsync on the backend, which tries both). No client-side way to tell whether a pending
 * challenge still exists (the cookie is HttpOnly), so an invalid/expired challenge just surfaces
 * as a clear error on submit instead of a pre-check. */
export function MfaVerifyPage() {
  const navigate = useNavigate();
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      await verifyMfa(code);
      navigate("/", { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "No se pudo verificar el código. Intenta de nuevo.");
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
      <Paper elevation={4} sx={{ maxWidth: 420, width: "100%", borderRadius: 4, p: { xs: 3, sm: 5 } }}>
        <Box component="img" src="/logo-tics.png" alt="Tecnologías de Información y Comunicaciones" sx={{ height: 72, width: "auto", mb: 3, display: "block", mx: "auto" }} />

        <Chip
          icon={<LockOutlinedIcon sx={{ color: `${LOGIN_ACCENT} !important` }} />}
          label="VERIFICACIÓN EN DOS PASOS"
          size="small"
          variant="outlined"
          sx={{ mb: 2, color: LOGIN_ACCENT, borderColor: LOGIN_ACCENT, fontWeight: 700 }}
        />
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
          Ingresa tu código
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Abre tu app autenticadora e ingresa el código de 6 dígitos, o usa uno de tus códigos de
          respaldo si no tienes acceso a tu dispositivo.
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2, textAlign: "left" }}>
            {error}
          </Alert>
        )}

        <Box component="form" onSubmit={handleSubmit}>
          <TextField
            label="Código de verificación"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            fullWidth
            autoFocus
            sx={{ mb: 2 }}
          />
          <Button
            type="submit"
            variant="contained"
            fullWidth
            disabled={isSubmitting || code.length === 0}
            sx={{ bgcolor: LOGIN_ACCENT, color: "#fff", mb: 2, "&:hover": { bgcolor: "#57157A" } }}
          >
            Verificar
          </Button>
        </Box>

        <Link component="button" variant="body2" onClick={() => navigate("/login", { replace: true })}>
          Volver a iniciar sesión
        </Link>
      </Paper>
    </Box>
  );
}
