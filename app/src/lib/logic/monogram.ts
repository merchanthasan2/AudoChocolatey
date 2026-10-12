/** Two-letter tile and a stable hue for apps without an icon (until real icons land in P5). */
export function monogram(name: string): { letters: string; hue: number } {
  const words = name.replace(/\(.*?\)/g, ' ').split(/[\s\-_.]+/).filter((w) => /[a-z0-9]/i.test(w));
  const letters =
    words.length === 0 ? '?' : (words.length === 1 ? words[0][0] : words[0][0] + words[1][0]).toUpperCase();
  let h = 0;
  for (const c of name) h = (h * 31 + c.charCodeAt(0)) >>> 0;
  return { letters, hue: h % 360 };
}
