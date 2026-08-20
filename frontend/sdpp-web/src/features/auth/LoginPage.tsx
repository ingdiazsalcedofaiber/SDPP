import { useEffect, useRef, useState } from "react";
import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import Alert from "@mui/material/Alert";
import CircularProgress from "@mui/material/CircularProgress";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import { useNavigate } from "react-router-dom";
import { BRAND_COLORS } from "../../shared/theme";
import { loginWithGoogle, useAuthStore } from "../../app/auth";
import { ApiError } from "../../shared/api/client";

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;
const isClientIdConfigured = !!GOOGLE_CLIENT_ID && !GOOGLE_CLIENT_ID.startsWith("__PENDIENTE__");

// One-off accent for this page only (matches the reference layout the company asked for) — not
// part of shared/theme.ts's BRAND_COLORS, which stay teal/orange/magenta for the rest of the app.
// Happens to equal CLASSIFICATION_COLORS.Secreta, reused deliberately instead of inventing a new hex.
const LOGIN_ACCENT = "#6A1B9A";

const VALUE_PROPS = [
  { icon: <DescriptionOutlinedIcon />, text: "Conversión y clasificación automática de documentos" },
  { icon: <VerifiedUserOutlinedIcon />, text: "Protección y marcas de agua dinámicas por sensibilidad" },
  { icon: <FactCheckOutlinedIcon />, text: "Auditoría y trazabilidad completa de cada acceso" },
];

/**
 * The only entry point into the app — no username/password fields exist anywhere here or
 * anywhere else in SDPP (see docs on autenticación con Google). Google Identity Services renders
 * its own branded button into `buttonRef`; SDPP never sees the user's Google password, only the
 * ID token GIS hands back, which the backend validates (see app/auth.ts's loginWithGoogle).
 */
export function LoginPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const buttonRef = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (user) {
      navigate("/", { replace: true });
    }
  }, [user, navigate]);

  useEffect(() => {
    if (!isClientIdConfigured) {
      return;
    }

    let cancelled = false;

    // The GIS script (index.html) loads async/defer — poll briefly instead of assuming it's
    // ready by the time this component mounts.
    const tryInitialize = () => {
      if (cancelled) {
        return;
      }
      if (!window.google) {
        setTimeout(tryInitialize, 100);
        return;
      }

      window.google.accounts.id.initialize({
        client_id: GOOGLE_CLIENT_ID!,
        callback: async (response) => {
          setIsLoading(true);
          setError(null);
          try {
            const result = await loginWithGoogle(response.credential);
            if (result.status === "MFA_ENROLLMENT_REQUIRED") {
              navigate("/login/mfa-setup", {
                state: { qrCodeDataUri: result.qrCodeDataUri, manualEntryKey: result.manualEntryKey },
              });
            } else {
              navigate("/login/mfa-verify");
            }
          } catch (err) {
            setError(err instanceof ApiError ? err.message : "No se pudo iniciar sesión. Intenta de nuevo.");
          } finally {
            setIsLoading(false);
          }
        },
      });

      if (buttonRef.current) {
        window.google.accounts.id.renderButton(buttonRef.current, {
          type: "standard",
          theme: "filled_blue",
          size: "large",
          text: "continue_with",
          shape: "pill",
          width: 320,
        });
      }
    };

    tryInitialize();
    return () => {
      cancelled = true;
    };
  }, [navigate]);

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        // Soft wash of the logo's own palette (orange/yellow-green/purple/magenta) over a light
        // base — same colors as the swoosh in logo-tics.png, just heavily lightened so the white
        // card up front still reads clearly.
        background:
          "linear-gradient(135deg, #FCEFE0 0%, #F3F6E4 25%, #F1EDF7 50%, #FBEAF1 75%, #E9F5F3 100%)",
        p: 2,
      }}
    >
      <Paper
        elevation={4}
        sx={{
          display: "flex",
          flexDirection: { xs: "column", md: "row" },
          maxWidth: 920,
          width: "100%",
          minHeight: { md: 640 },
          borderRadius: 4,
          overflow: "hidden",
        }}
      >
        {/* Left panel — brand/value-prop side, over the facility photo supplied for this page
            (public/fondo.jpg). The gradient stays as a tint overlay so the white text/icons on
            top remain legible regardless of how bright the underlying photo is. */}
        <Box
          sx={{
            flex: { md: "0 0 42%" },
            minHeight: { xs: 320, md: "auto" },
            p: { xs: 3, sm: 5 },
            display: "flex",
            flexDirection: "column",
            justifyContent: "flex-end",
            color: "#fff",
            position: "relative",
            // Two layers over the photo: a dark-to-transparent fade from the bottom (where the
            // text sits, since this panel is bottom-aligned) so the text always has real contrast,
            // plus a diagonal wash in the logo's own corporate palette (purple/magenta/teal/
            // orange) — "multiply" blend lets the photo's own light/dark areas show through the
            // color instead of flattening it into a uniform tint.
            backgroundImage:
              "linear-gradient(to top, rgba(10,5,20,0.88) 0%, rgba(10,5,20,0.25) 50%, rgba(10,5,20,0) 100%), " +
              "linear-gradient(135deg, rgba(106,27,154,0.6) 0%, rgba(194,24,91,0.45) 33%, rgba(0,150,136,0.4) 66%, rgba(249,168,37,0.35) 100%), " +
              "url(/fondo.jpg)",
            backgroundBlendMode: "normal, multiply, normal",
            backgroundSize: "cover",
            backgroundPosition: "center",
          }}
        >
          <Chip
            icon={<LockOutlinedIcon sx={{ color: "#fff !important" }} />}
            label="PLATAFORMA SDPP"
            size="small"
            sx={{
              alignSelf: "flex-start",
              mb: 2,
              bgcolor: "rgba(255,255,255,0.18)",
              color: "#fff",
              fontWeight: 600,
              letterSpacing: 0.5,
            }}
          />
          <Typography variant="h4" sx={{ fontWeight: 800, mb: 1, lineHeight: 1.15 }}>
            Secure Document
            <br />
            Processing Platform
          </Typography>
          <Stack spacing={1.25} sx={{ mt: 3 }}>
            {VALUE_PROPS.map((item) => (
              <Stack key={item.text} direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
                <Box sx={{ display: "flex", opacity: 0.9 }}>{item.icon}</Box>
                <Typography variant="body2" sx={{ opacity: 0.95 }}>
                  {item.text}
                </Typography>
              </Stack>
            ))}
          </Stack>
        </Box>

        {/* Right panel — login card */}
        <Box sx={{ flex: 1, p: { xs: 3, sm: 6 }, display: "flex", flexDirection: "column", justifyContent: "center" }}>
          <Box
            component="img"
            src="/logo-tics.png"
            alt="Tecnologías de Información y Comunicaciones"
            sx={{
              height: 180,
              width: "auto",
              mb: 4,
              alignSelf: "center",
              // Soft raised/embossed look — a dark shadow below-right for depth plus a faint
              // light highlight above-left, applied per-pixel via drop-shadow (unlike box-shadow,
              // which would just box the whole transparent PNG bounding rect).
              filter:
                "drop-shadow(0 10px 14px rgba(0,0,0,0.22)) drop-shadow(0 2px 4px rgba(0,0,0,0.18)) drop-shadow(0 -1px 1px rgba(255,255,255,0.7))",
            }}
          />

          <Chip
            label="PORTAL SDPP"
            size="small"
            variant="outlined"
            sx={{ alignSelf: "flex-start", mb: 2, color: LOGIN_ACCENT, borderColor: LOGIN_ACCENT, fontWeight: 700, letterSpacing: 0.3 }}
          />
          <Typography variant="h5" sx={{ fontWeight: 700, mb: 1.5 }}>
            Iniciar sesión
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 5 }}>
            Inicia sesión con tu cuenta corporativa de Google para acceder a la plataforma de
            conversión y protección de documentos.
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2, textAlign: "left" }}>
              {error}
            </Alert>
          )}

          {!isClientIdConfigured && (
            <Alert severity="warning" sx={{ mb: 2, textAlign: "left" }}>
              Falta configurar VITE_GOOGLE_CLIENT_ID — el botón de Google no puede iniciarse.
            </Alert>
          )}

          {isLoading && (
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.5,
                px: 2.5,
                py: 1.5,
                mb: 2,
                borderRadius: 999,
                width: "fit-content",
                bgcolor: `${BRAND_COLORS.teal}14`,
              }}
            >
              <CircularProgress size={20} thickness={4} sx={{ color: BRAND_COLORS.teal }} />
              <Typography variant="body2" sx={{ fontWeight: 600, color: BRAND_COLORS.teal }}>
                Verificando tu cuenta…
              </Typography>
            </Box>
          )}

          {/* display:none (not unmounting) while loading — GIS renders its button once into this
              node via renderButton in the effect below; unmounting the Box would destroy that
              rendered iframe for good, since renderButton never gets called again afterwards. */}
          <Box
            ref={buttonRef}
            sx={{
              display: isLoading ? "none" : "block",
              minHeight: 44,
              width: "fit-content",
              transformOrigin: "center",
              "@keyframes accordionSqueeze": {
                "0%": { transform: "scaleY(1)" },
                "30%": { transform: "scaleY(0.8)" },
                "55%": { transform: "scaleY(1.08)" },
                "75%": { transform: "scaleY(0.95)" },
                "100%": { transform: "scaleY(1)" },
              },
              // The Google button itself is an iframe (can't be restyled), but a transform on
              // this wrapper still visually squeezes the whole thing — same "acordeón" effect
              // works for both mouse hover and a tap on touch devices (:active as fallback).
              "&:hover": { animation: "accordionSqueeze 0.5s ease-in-out" },
              "&:active": { animation: "accordionSqueeze 0.4s ease-in-out" },
            }}
          />
        </Box>
      </Paper>

      <Typography variant="caption" sx={{ mt: 3, color: BRAND_COLORS.textLight }}>
        © {new Date().getFullYear()} Clinaltec S.A.S · SDPP
      </Typography>
    </Box>
  );
}
