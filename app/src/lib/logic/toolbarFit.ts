/** How many toolbar actions fit before the rest move into the "More" menu. */
export function fitActions(widths: number[], available: number, overflowW: number, gap: number): number {
  const total = widths.reduce((s, w) => s + w, 0) + gap * Math.max(0, widths.length - 1);
  if (total <= available) return widths.length;
  let used = overflowW;
  let n = 0;
  for (const w of widths) {
    if (used + gap + w > available) break;
    used += gap + w;
    n++;
  }
  return n;
}
