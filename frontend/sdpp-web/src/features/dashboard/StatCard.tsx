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
// this app (sidebar nav, conversion-picker tiles) — now fed with real values instead of "—".
export function StatCard({ label, value, icon, color, loading, subtitle }: StatCardProps) {
  return (
    <Card sx={{ borderTop: `3px solid ${color}`, height: "100%" }}>
      <CardContent>
        <Box
          sx={{
            width: 40,
            height: 40,
            borderRadius: "50%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            bgcolor: `${color}1F`,
            color,
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
