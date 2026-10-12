/** Stable, unique list keys: repeated base keys get "#2", "#3"… in order of appearance. */
export function uniqueKeys<T>(items: T[], key: (item: T) => string): string[] {
  const seen = new Map<string, number>();
  return items.map((item) => {
    const base = key(item);
    const n = (seen.get(base) ?? 0) + 1;
    seen.set(base, n);
    return n === 1 ? base : `${base}#${n}`;
  });
}
