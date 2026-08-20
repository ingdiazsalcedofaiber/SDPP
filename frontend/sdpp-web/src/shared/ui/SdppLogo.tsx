import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import { BRAND_COLORS } from "../theme";

/** Brand mark: the real TICs corporate logo file (public/logo-tics.png), same asset used on the
 * login page — never a redrawn approximation. */
export function SdppLogo({ withTagline = true, height = 56 }: { withTagline?: boolean; height?: number }) {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: { xs: 1, sm: 2 } }}>
      <Box
        component="img" src="/logo-tics.png" alt="Tecnologías de Información y Comunicaciones"
        sx={{ height: { xs: Math.min(height, 36), sm: height }, width: "auto" }}
      />
      <Box>
        <Typography sx={{ fontWeight: 800, fontSize: { xs: 19, sm: 26 }, lineHeight: 1, letterSpacing: 0.5, color: BRAND_COLORS.textDark }}>
          SDPP
        </Typography>
        {/* The tagline's own text is long enough (and deliberately non-wrapping — see below) that
            keeping it on a phone-width topbar would force the whole header to overflow or crowd out
            the nav/user controls next to it; hidden below "sm" rather than passing withTagline down
            as a prop, since every caller (AppShell, LoginPage) wants the same breakpoint, not a
            per-caller decision. */}
        {withTagline && (
          <Box sx={{ display: { xs: "none", sm: "flex" }, alignItems: "center", gap: 0.75, mt: "7px" }}>
            <Box sx={{ width: 12, height: 2, borderRadius: 1, bgcolor: BRAND_COLORS.teal, flexShrink: 0 }} />
            <Typography
              sx={{
                fontSize: 10,
                fontWeight: 600,
                color: BRAND_COLORS.teal,
                letterSpacing: 0.6,
                textTransform: "uppercase",
                whiteSpace: "nowrap",
              }}
            >
              Secure Document Processing Platform
            </Typography>
          </Box>
        )}
      </Box>
    </Box>
  );
}
