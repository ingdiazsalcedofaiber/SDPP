import Box from "@mui/material/Box";
import Skeleton from "@mui/material/Skeleton";
import Typography from "@mui/material/Typography";

/** Big colorful headline-number tile. The wave in the background is pure decoration (an SVG
 * flourish echoing the brand swoosh) — never fed real numbers, so it's never mistaken for a chart
 * making a claim about trend data we don't actually have per-card. */
export function GradientHeroCard({
  label, value, gradient, loading,
}: {
  label: string; value: string | number; gradient: string; loading?: boolean;
}) {
  return (
    <Box
      sx={{
        position: "relative", overflow: "hidden", borderRadius: 3, p: 2.5, minHeight: 132,
        background: gradient, color: "#fff", display: "flex", flexDirection: "column", justifyContent: "space-between",
      }}
    >
      <Box sx={{ position: "relative", zIndex: 1 }}>
        {loading ? (
          <Skeleton variant="text" width={70} height={44} sx={{ bgcolor: "rgba(255,255,255,0.3)" }} />
        ) : (
          <Typography sx={{ fontSize: 30, fontWeight: 800, lineHeight: 1.1 }}>{value}</Typography>
        )}
        <Typography sx={{ fontSize: 13, fontWeight: 500, opacity: 0.9, mt: 0.5 }}>{label}</Typography>
      </Box>
      <Box
        component="svg"
        viewBox="0 0 200 60"
        preserveAspectRatio="none"
        sx={{ position: "absolute", left: 0, right: 0, bottom: 0, height: 50, width: "100%", opacity: 0.35 }}
      >
        <path d="M0,40 C30,10 50,55 80,30 C110,5 130,50 160,25 C180,12 190,30 200,20 L200,60 L0,60 Z" fill="rgba(255,255,255,0.6)" />
      </Box>
    </Box>
  );
}
