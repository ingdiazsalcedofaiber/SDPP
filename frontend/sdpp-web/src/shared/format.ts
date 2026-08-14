export function formatBytes(bytes: number): string {
  if (bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  const value = bytes / 1024 ** exponent;
  return `${value.toFixed(exponent === 0 ? 0 : 1)} ${units[exponent]}`;
}

export function formatPeriodLabel(isoDate: string, bucket: "day" | "week" | "month"): string {
  const date = new Date(isoDate);
  if (bucket === "month") return date.toLocaleDateString("es-ES", { month: "short", year: "2-digit" });
  return date.toLocaleDateString("es-ES", { day: "2-digit", month: "short" });
}

// Forces América/Bogotá regardless of the viewing device's own OS timezone — plain toLocaleString()
// silently converts using whatever timezone the viewer's machine happens to be set to, which for an
// audit/evidence platform can read hours off from the real event time. Use this instead of a bare
// `new Date(x).toLocaleString()` anywhere a UTC timestamp from the backend is displayed.
export function formatBogotaDateTime(isoUtc: string | null | undefined): string {
  if (!isoUtc) return "—";
  return new Date(isoUtc).toLocaleString("es-CO", { dateStyle: "medium", timeStyle: "short", timeZone: "America/Bogota" });
}
