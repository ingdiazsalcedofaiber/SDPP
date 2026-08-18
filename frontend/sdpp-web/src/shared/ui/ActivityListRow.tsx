import type { ReactNode } from "react";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";

/** One row of a "feed"-style list (recent documents, recent activity) — a leading colored icon,
 * a primary/secondary text stack, and an optional trailing chip plus right-aligned timestamp.
 * Replaces a dense MUI Table for these two panels: a spreadsheet grid suits browsing structured
 * data, but a short "what happened recently" list reads better as a feed. */
export function ActivityListRow({
  icon, iconColor, primary, secondary, trailing, meta,
}: {
  icon: ReactNode; iconColor: string; primary: string; secondary?: string; trailing?: ReactNode; meta?: string;
}) {
  return (
    <Box
      sx={{
        display: "flex", alignItems: "center", gap: 1.75, px: 2.5, py: 1.5,
        borderBottom: "1px solid rgba(15, 40, 38, 0.06)",
        transition: "background-color 0.15s ease",
        "&:last-of-type": { borderBottom: "none" },
        "&:hover": { bgcolor: "rgba(20, 168, 156, 0.05)" },
      }}
    >
      <Box
        sx={{
          width: 36, height: 36, borderRadius: "50%", flexShrink: 0,
          display: "flex", alignItems: "center", justifyContent: "center",
          bgcolor: `${iconColor}1F`, color: iconColor,
          "& svg": { fontSize: 18 },
        }}
      >
        {icon}
      </Box>
      <Box sx={{ minWidth: 0, flexGrow: 1 }}>
        <Typography variant="body2" sx={{ fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
          {primary}
        </Typography>
        {secondary && (
          <Typography variant="caption" color="text.secondary">{secondary}</Typography>
        )}
      </Box>
      <Box sx={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 0.5, flexShrink: 0 }}>
        {trailing}
        {meta && <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: "nowrap" }}>{meta}</Typography>}
      </Box>
    </Box>
  );
}
