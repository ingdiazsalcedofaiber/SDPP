import { createTheme } from "@mui/material/styles";

// Paleta de marca tomada del logotipo SDPP (swoosh degradado teal → naranja → magenta, wordmark
// en gris carbón) — única fuente de verdad para el tema y para src/shared/ui/SdppLogo.tsx, de
// modo que el logo y el resto de la interfaz nunca diverjan.
export const BRAND_COLORS = {
  teal: "#14A89C",
  orange: "#F5821F",
  magenta: "#D6217A",
  textDark: "#3A3A3A",
  textLight: "#8A8A8A",
};

// SDPP design tokens. La paleta de clasificación (más abajo) es intencionalmente independiente
// de la de marca: codifica severidad (docs/05-security/classification-engine.md §1), no
// identidad visual, y no debe cambiar si el branding cambia.
export const sdppTheme = createTheme({
  palette: {
    mode: "light",
    primary: { main: BRAND_COLORS.teal },
    secondary: { main: BRAND_COLORS.magenta },
    warning: { main: BRAND_COLORS.orange },
    text: { primary: BRAND_COLORS.textDark, secondary: BRAND_COLORS.textLight },
    // Fondo con un tinte apenas perceptible (no blanco puro) para que Paper/Card/tablas —
    // blancas, con sombra — "floten" visiblemente sobre el canvas en vez de fundirse con él.
    background: { default: "#F5F8F7", paper: "#FFFFFF" },
  },
  shape: { borderRadius: 12 },
  typography: {
    fontFamily: '"Plus Jakarta Sans", "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
    // The variable font's own weight steps — MUI's defaults (300/400/500/700) skip 600 and 800,
    // both used deliberately across this redesign (StatCard values, sidebar active state, hero
    // card labels) for a stronger, less uniformly-medium hierarchy than the default scale gives.
    fontWeightLight: 400,
    fontWeightRegular: 500,
    fontWeightMedium: 600,
    fontWeightBold: 700,
    h4: { fontWeight: 700 },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 700 },
  },
  components: {
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: "none" },
        elevation1: { boxShadow: "0 2px 10px rgba(15, 40, 38, 0.07)" },
        outlined: { borderColor: "rgba(15, 40, 38, 0.08)" },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          boxShadow: "0 2px 10px rgba(15, 40, 38, 0.07)",
          transition: "box-shadow 0.2s ease, transform 0.2s ease",
          "&:hover": { boxShadow: "0 8px 24px rgba(15, 40, 38, 0.14)" },
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: { textTransform: "none", fontWeight: 600, borderRadius: 10 },
        contained: { boxShadow: "0 2px 8px rgba(15, 40, 38, 0.18)" },
      },
    },
    MuiChip: {
      styleOverrides: { root: { fontWeight: 600 } },
    },
    MuiAppBar: {
      styleOverrides: {
        root: { boxShadow: "0 2px 12px rgba(15, 40, 38, 0.08)" },
      },
    },
    MuiTableHead: {
      styleOverrides: {
        root: {
          backgroundColor: "#F0F5F4",
          "& .MuiTableCell-root": {
            fontWeight: 700,
            fontSize: 11.5,
            textTransform: "uppercase",
            letterSpacing: 0.5,
            color: BRAND_COLORS.textLight,
          },
        },
      },
    },
    MuiTableBody: {
      styleOverrides: {
        root: {
          "& .MuiTableRow-root:hover": { backgroundColor: "rgba(20, 168, 156, 0.06)" },
        },
      },
    },
  },
});

// See docs/05-security/classification-engine.md §1 — deliberately ordered from least to most
// alarming so the color intensity itself communicates the ordering of ClassificationLevel.
export const CLASSIFICATION_COLORS: Record<string, string> = {
  Publica: "#4CAF50",
  UsoInterno: "#8BC34A",
  Privada: "#FFC107",
  Confidencial: "#FF9800",
  Restringida: "#E53935",
  Secreta: "#6A1B9A",
};
