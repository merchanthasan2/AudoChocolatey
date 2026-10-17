/** Keeps only selected keys that are still shown and selectable, so nothing hidden is acted on. */
export function keepSelected(selected: Iterable<string>, selectableVisible: ReadonlySet<string>): string[] {
  return [...selected].filter((k) => selectableVisible.has(k));
}

export function headerState(sel: number, selectable: number): { checked: boolean; indeterminate: boolean; disabled: boolean } {
  return { checked: selectable > 0 && sel === selectable, indeterminate: sel > 0 && sel < selectable, disabled: selectable === 0 };
}

/** The keys from `anchor` to `target` in shown order (shift-click). */
export function rangeKeys(order: string[], anchor: string, target: string): string[] {
  const a = order.indexOf(anchor), b = order.indexOf(target);
  if (a < 0 || b < 0) return b < 0 ? [] : [target];
  const [lo, hi] = a < b ? [a, b] : [b, a];
  return order.slice(lo, hi + 1);
}
