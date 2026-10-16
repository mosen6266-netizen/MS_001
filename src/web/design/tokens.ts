/** Data mirrors the CSS primitives; contrast testing uses the exact color values. */
export const palette = {
  root: "#0d1014", nav: "#11151a", panel: "#191f26", raised: "#222a33",
  text: "#f1f5f9", muted: "#b6c3d0", subtle: "#96a8b9",
  success: "#62d49b", onSuccess: "#081e13", danger: "#ef8991", onDanger: "#2d080d",
  focus: "#add2ff", warning: "#f5cb83",
} as const;

function channel(hex: string): number {
  const n = Number.parseInt(hex, 16) / 255;
  return n <= 0.04045 ? n / 12.92 : Math.pow((n + 0.055) / 1.055, 2.4);
}
export function luminance(hex: string): number {
  if (!/^#[0-9a-fA-F]{6}$/.test(hex)) throw new Error("Only opaque RGB hex is supported");
  return 0.2126 * channel(hex.slice(1, 3)) + 0.7152 * channel(hex.slice(3, 5)) + 0.0722 * channel(hex.slice(5, 7));
}
export function contrastRatio(a: string, b: string): number {
  const x = luminance(a), y = luminance(b);
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
}
