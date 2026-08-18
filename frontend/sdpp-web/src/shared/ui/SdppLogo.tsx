import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import { BRAND_COLORS } from "../theme";

/** Brand mark: the real TICs corporate logo file (public/logo-tics.png), same asset used on the
 * login page — never a redrawn approximation. */
export function SdppLogo({ withTagline = true, height = 56 }: { withTagline?: boolean; height?: number }) {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
      <Box component="img" src="/logo-tics.png" alt="Tecnologías de Información y Comunicaciones" sx={{ height, width: "auto" }} />
      <Box>
        <Typography sx={{ fontWeight: 800, fontSize: 26, lineHeight: 1, letterSpacing: 0.5, color: BRAND_COLORS.textDark }}>
          SDPP
        </Typography>
        {withTagline && (
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mt: "7px" }}>
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
