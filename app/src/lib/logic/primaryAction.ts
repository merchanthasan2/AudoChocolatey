/** The main Updates button: selected apps, else everything shown (when filtered), else all. */
export function primaryAction(selected: number, shownIdle: number, filtered: boolean): { label: string; scope: 'selected' | 'all' | 'shown' } | null {
  if (selected > 0) return { label: `Update ${selected} selected`, scope: 'selected' };
  if (shownIdle === 0) return null;
  if (filtered) return { label: `Update ${shownIdle} shown`, scope: 'shown' };
  return { label: 'Update all', scope: 'all' };
}
