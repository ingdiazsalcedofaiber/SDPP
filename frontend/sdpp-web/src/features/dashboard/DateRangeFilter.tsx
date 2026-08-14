import Box from "@mui/material/Box";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import type { ReportingBucket } from "../../shared/api/reporting";

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

export function DateRangeFilter({ value, onChange }: DateRangeFilterProps) {
  return (
    <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap" }}>
      <ToggleButtonGroup
        size="small"
        value={value.presetDays}
        exclusive
        onChange={(_, presetDays: 7 | 30 | 90 | null) => presetDays && onChange({ ...value, presetDays })}
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
      >
        <ToggleButton value="day">Día</ToggleButton>
        <ToggleButton value="week">Semana</ToggleButton>
        <ToggleButton value="month">Mes</ToggleButton>
      </ToggleButtonGroup>
    </Box>
  );
}
