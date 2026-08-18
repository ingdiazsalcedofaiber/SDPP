import Box from "@mui/material/Box";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import type { ReportingBucket } from "../../shared/api/reporting";
import { BRAND_COLORS } from "../../shared/theme";

export interface DateRangeValue {
  presetDays: 7 | 30 | 90;
  bucket: ReportingBucket;
}

export const DEFAULT_DATE_RANGE: DateRangeValue = { presetDays: 30, bucket: "day" };

export function resolveDateRange(value: DateRangeValue): { from: Date; to: Date; bucket: ReportingBucket } {
  const to = new Date();
  const from = new Date(to.getTime() - value.presetDays * 24 * 60 * 60 * 1000);
  return { from, to, bucket: value.bucket };
}

interface DateRangeFilterProps {
  value: DateRangeValue;
  onChange: (value: DateRangeValue) => void;
}

// Pill-shaped segmented control — replaces MUI's default outlined/boxy ToggleButtonGroup look
// (square corners, gray borders, all-caps text) with the same rounded, colored language used
// everywhere else in the app (sidebar nav pills, StatCard accents). Each group gets its own accent
// so "rango" and "agrupación" read as two distinct controls, not one continuous row of buttons.
function segmentedGroupSx(accent: string) {
  return {
    bgcolor: "rgba(15, 40, 38, 0.05)",
    borderRadius: 999,
    p: 0.5,
    gap: 0.25,
    "& .MuiToggleButtonGroup-grouped": {
      border: 0,
      borderRadius: "999px !important",
      textTransform: "none",
      fontSize: 13,
      fontWeight: 600,
      color: BRAND_COLORS.textLight,
      px: 1.75,
      py: 0.5,
      minWidth: 0,
      transition: "background-color 0.15s ease, color 0.15s ease",
      "&:hover": { bgcolor: "rgba(15, 40, 38, 0.06)" },
      "&.Mui-selected": {
        bgcolor: accent,
        color: "#fff",
        "&:hover": { bgcolor: accent },
      },
    },
  } as const;
}

export function DateRangeFilter({ value, onChange }: DateRangeFilterProps) {
  return (
    <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap" }}>
      <ToggleButtonGroup
        size="small"
        value={value.presetDays}
        exclusive
        onChange={(_, presetDays: 7 | 30 | 90 | null) => presetDays && onChange({ ...value, presetDays })}
        sx={segmentedGroupSx(BRAND_COLORS.teal)}
      >
        <ToggleButton value={7}>7 días</ToggleButton>
        <ToggleButton value={30}>30 días</ToggleButton>
        <ToggleButton value={90}>90 días</ToggleButton>
      </ToggleButtonGroup>
      <ToggleButtonGroup
        size="small"
        value={value.bucket}
        exclusive
        onChange={(_, bucket: ReportingBucket | null) => bucket && onChange({ ...value, bucket })}
        sx={segmentedGroupSx(BRAND_COLORS.orange)}
      >
        <ToggleButton value="day">Día</ToggleButton>
        <ToggleButton value="week">Semana</ToggleButton>
        <ToggleButton value="month">Mes</ToggleButton>
      </ToggleButtonGroup>
    </Box>
  );
}
