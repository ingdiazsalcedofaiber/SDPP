import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import { BRAND_COLORS } from "../theme";

/** Brand mark: the real TICs corporate logo file (public/logo-tics.png), same asset used on the
 * login page — never a redrawn approximation. */
export function SdppLogo({ withTagline = true, height = 56 }: { withTagline?: boolean; height?: number }) {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
      <Box component="img" src="/logo-tics.png" alt="Tecnologías de Información y Comunicaciones" sx={{ height, width: "auto" }} />
      <Box>
        <Typography sx={{ fontWeight: 800, fontSize: 26, lineHeight: 1, letterSpacing: 0.5 }}>
          <Box component="span" sx={{ fontWeight: 400, color: BRAND_COLORS.textLight }}>SD</Box>
          <Box component="span" sx={{ fontWeight: 800, color: BRAND_COLORS.textDark }}>PP</Box>
        </Typography>
        {withTagline && (
          <Typography sx={{ fontSize: 11, color: BRAND_COLORS.textLight, letterSpacing: 0.2, mt: "-2px" }}>
            Secure Document Processing Platform
          </Typography>
        )}
      </Box>
    </Box>
  );
}
