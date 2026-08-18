import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";

/** A track ring (light tint) plus a determinate progress ring (brand color) stacked on top, with
 * the percentage centered inside — the "stat ring" language used throughout this dashboard for
 * any metric that's genuinely a share of something (completion rate, storage used, error rate),
 * never a decorative percentage invented just to fill a ring. */
export function RingIndicator({ percent, color, size = 64 }: { percent: number; color: string; size?: number }) {
  const clamped = Math.max(0, Math.min(100, percent));
  return (
    <Box sx={{ position: "relative", width: size, height: size, flexShrink: 0 }}>
      <CircularProgress variant="determinate" value={100} size={size} thickness={4} sx={{ color: `${color}26`, position: "absolute" }} />
      <CircularProgress variant="determinate" value={clamped} size={size} thickness={4} sx={{ color }} />
      <Box sx={{ position: "absolute", inset: 0, display: "flex", alignItems: "center", justifyContent: "center" }}>
        <Typography sx={{ fontSize: size * 0.22, fontWeight: 700, color }}>{Math.round(clamped)}%</Typography>
      </Box>
    </Box>
  );
}
