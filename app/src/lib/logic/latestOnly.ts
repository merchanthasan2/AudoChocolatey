export const STALE: unique symbol = Symbol('stale');

/** Wraps async requests so only the most recent one's result is delivered; older ones resolve to STALE. */
export function latestOnly<T>() {
  let seq = 0;
  return {
    async run(p: Promise<T>): Promise<T | typeof STALE> {
      const mine = ++seq;
      const value = await p;
      return mine === seq ? value : STALE;
    },
  };
}
