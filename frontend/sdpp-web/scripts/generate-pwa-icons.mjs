// One-off generator: public/logo-tics.png is 666x375 (not square), but a PWA manifest needs
// square icons. Run manually with `npm run generate:icons` whenever the logo changes — not part
// of `npm run build`, since it needs the sharp native binary and the source logo essentially
// never changes.
import sharp from "sharp";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const publicDir = join(__dirname, "..", "public");
const source = join(publicDir, "logo-tics.png");

// White, not brand-teal: the logo's own leftmost arc is teal, so a teal background made that
// segment disappear into the background (the exact "no se ve bien" the user flagged) — white gives
// every color in the mark (teal/orange/magenta/yellow) equal, clean contrast, and matches how the
// real in-app header already presents the logo (white AppBar, see app/AppShell.tsx).
const ICON_BACKGROUND = "#FFFFFF";

async function makeIcon(outFile, size, logoScale) {
  const logoWidth = Math.round(size * logoScale);
  const logo = await sharp(source).resize({ width: logoWidth, fit: "inside" }).toBuffer();
  const logoMeta = await sharp(logo).metadata();

  await sharp({
    create: { width: size, height: size, channels: 4, background: ICON_BACKGROUND },
  })
    .composite([{ input: logo, left: Math.round((size - logoMeta.width) / 2), top: Math.round((size - logoMeta.height) / 2) }])
    .png()
    .toFile(join(publicDir, outFile));

  console.log(`wrote ${outFile}`);
}

// Regular icons: logo fills ~70% of the square, centered on a solid white background.
await makeIcon("pwa-192.png", 192, 0.7);
await makeIcon("pwa-512.png", 512, 0.7);
// Maskable icon: Android/Chrome can crop to a circle/squircle, so the logo must sit inside the
// ~80% "safe zone" — use a smaller scale so nothing meaningful gets clipped.
await makeIcon("pwa-maskable-512.png", 512, 0.5);
