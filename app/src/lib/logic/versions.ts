// Port of src-tauri/src/versions.rs `compare`: numeric segments compare numerically, text
// case-insensitively, missing segments count as zero, and a text (pre-release) segment sorts
// below nothing (2.0.0-beta < 2.0.0). Shares its test table.

const isNum = (s: string) => s.length > 0 && /^\d+$/.test(s);

function segment(x: string, y: string): number {
  const nx = isNum(x), ny = isNum(y);
  if (nx && ny) {
    const a = x.replace(/^0+/, ''), b = y.replace(/^0+/, '');
    return a.length !== b.length ? Math.sign(a.length - b.length) : a < b ? -1 : a > b ? 1 : 0;
  }
  if (nx) return 1;
  if (ny) return -1;
  return x < y ? -1 : x > y ? 1 : 0;
}

function againstMissing(x: string): number {
  if (isNum(x)) return x.replace(/^0+/, '') === '' ? 0 : 1;
  return -1;
}

export function compareVersions(a: string, b: string): number {
  const split = (s: string) => s.split(/[.\-+_]/).filter((p) => p !== '').map((p) => p.toLowerCase());
  const pa = split(a), pb = split(b);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const x = pa[i], y = pb[i];
    const ord = x !== undefined && y !== undefined ? segment(x, y) : x !== undefined ? againstMissing(x) : -againstMissing(y!);
    if (ord !== 0) return ord;
  }
  return 0;
}

/** A version we can order: starts with a digit (not "Unknown" or "< 2.0"). */
export const knownVersion = (v: string | undefined | null): v is string => !!v && /^\d/.test(v.trim());
