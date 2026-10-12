// Search results made readable: language editions fold into the main app, best name matches first.
// (P2's catalog merge does the cross-source dedupe; this is presentation only.)

const EDITION = /^(.+)\.([a-z]{2,3}(?:-[A-Za-z]{2,4})?)$/;

function score(name: string, q: string): number {
  const n = name.toLowerCase().replace(/\s*\(.*?\)\s*/g, ' ').trim();
  if (n === q) return 0;
  if (n.startsWith(q)) return 1;
  if (n.split(/\s+/).some((w) => w === q)) return 2;
  if (n.includes(q)) return 3;
  return 4;
}

export function tidyResults<T extends { id: string; name: string; source: string }>(items: T[], query: string): T[] {
  const ids = new Set(items.map((i) => `${i.source}:${i.id}`));
  const kept = items.filter((i) => {
    const m = EDITION.exec(i.id);
    return !(m && ids.has(`${i.source}:${m[1]}`));
  });
  const q = query.trim().toLowerCase();
  return kept
    .map((item, index) => ({ item, index, s: score(item.name, q) }))
    .sort((a, b) => a.s - b.s || a.index - b.index)
    .map((x) => x.item);
}
