/**
 * Returns a gate that lets one click through, then ignores clicks for `ms`.
 * Stops double-clicks (or an impatient second click) from landing on whatever button
 * just moved into the same spot, e.g. "Update 3 selected" turning into "Update all".
 */
export function clickGuard(ms: number) {
  let last = -Infinity;
  return (now: number = performance.now()): boolean => {
    if (now - last < ms) return false;
    last = now;
    return true;
  };
}
