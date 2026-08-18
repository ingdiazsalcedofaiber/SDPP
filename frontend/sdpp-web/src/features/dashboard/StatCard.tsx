import type { ReactNode } from "react";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Skeleton from "@mui/material/Skeleton";
import Typography from "@mui/material/Typography";

interface StatCardProps {
  label: string;
  value: string | number;
  icon: ReactNode;
  color: string;
  loading?: boolean;
  subtitle?: string;
}

// Same circular-icon card language established for the placeholder dashboard and reused across
// this app (sidebar nav, conversion-picker tiles) — now fed with real values instead of "—". The
// gradient badge + colored glow (instead of a flat pale-tint icon and a plain gray shadow) is
// what gives each card its own visual weight — a page full of these should never read as a plain
// data grid.
export function StatCard({ label, value, icon, color, loading, subtitle }: StatCardProps) {
  return (
    <Card sx={{ height: "100%", boxShadow: `0 10px 24px ${color}26`, "&:hover": { boxShadow: `0 14px 30px ${color}3D` } }}>
      <CardContent>
        <Box
          sx={{
            width: 44,
            height: 44,
            borderRadius: "50%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            background: `linear-gradient(135deg, ${color}CC 0%, ${color} 100%)`,
            color: "#fff",
            boxShadow: `0 4px 10px ${color}55`,
            mb: 1.5,
          }}
        >
          {icon}
        </Box>
        <Typography variant="overline" color="text.secondary" sx={{ display: "block", lineHeight: 1.3 }}>
          {label}
        </Typography>
        {loading ? (
          <Skeleton variant="text" width={90} height={44} />
        ) : (
          <Typography variant="h4" sx={{ fontWeight: 700 }}>{value}</Typography>
        )}
        {subtitle && (
          <Typography variant="caption" color="text.secondary">{subtitle}</Typography>
        )}
      </CardContent>
    </Card>
  );
}
