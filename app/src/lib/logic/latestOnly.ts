export const STALE: unique symbol = Symbol('stale');

/**
 * Wraps async requests so only the most recent one's result is delivered; older ones resolve
 * to STALE. `invalidate()` makes everything in flight stale (e.g. when the search is cleared).
 */
export function latestOnly<T>() {
  let seq = 0;
  return {
    async run(p: Promise<T>): Promise<T | typeof STALE> {
      const mine = ++seq;
      try {
        const value = await p;
        return mine === seq ? value : STALE;
      } catch (e) {
        if (mine !== seq) return STALE;
        throw e;
      }
    },
    invalidate() {
      seq++;
    },
  };
}
