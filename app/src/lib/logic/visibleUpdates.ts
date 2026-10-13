/**
 * Updates to show: a row whose upgrade just succeeded is hidden until the list is refreshed
 * after that job ended. If the refreshed list still reports it (winget sometimes does), it
 * comes back, so the page always agrees with the sidebar count.
 */
export function visibleUpdates<T>(
  items: T[],
  jobOf: (item: T) => { phase: string; endedAt: number | null } | undefined,
  loadedAt: number | null,
): T[] {
  return items.filter((item) => {
    const j = jobOf(item);
    if (!j || j.phase !== 'succeeded') return true;
    return loadedAt != null && j.endedAt != null && loadedAt > j.endedAt;
  });
}
