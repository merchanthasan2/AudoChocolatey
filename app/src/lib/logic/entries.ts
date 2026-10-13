// Helpers for catalog/search entries against what's installed on this PC.

type Installed = { id: string; source: 'winget' | 'choco' | null };
type Ids = { winget: string | null; choco: string | null };

/** `winget:<id>` / `choco:<id>` keys (lowercase) for everything a source manages. */
export function installedSet(items: Installed[]): Set<string> {
  return new Set(items.filter((p) => p.source).map((p) => `${p.source}:${p.id.toLowerCase()}`));
}

export function isInstalled(entry: Ids, set: Set<string>): boolean {
  return (
    (!!entry.winget && set.has(`winget:${entry.winget.toLowerCase()}`)) ||
    (!!entry.choco && set.has(`choco:${entry.choco.toLowerCase()}`))
  );
}

/** Entries not installed yet (what a bundle should actually queue). */
export function missingFrom<T extends Ids>(entries: T[], set: Set<string>): T[] {
  return entries.filter((e) => !isInstalled(e, set));
}

type JobLike = { op: { kind: string; target: { source: 'winget' | 'choco'; id: string } }; phase: string };
const GONE = new Set(['failed', 'cancelled']);

/** Adds apps with a queued, running or finished-ok job, so a bundle never queues them twice
 *  (the installed list lags a second or two behind a finished install). */
export function withJobs(set: Set<string>, jobs: JobLike[]): Set<string> {
  const out = new Set(set);
  for (const j of jobs) {
    if (j.op.kind !== 'uninstall' && !GONE.has(j.phase)) out.add(`${j.op.target.source}:${j.op.target.id.toLowerCase()}`);
  }
  return out;
}

/** True when the app installs through Chocolatey but Chocolatey is off or missing. */
export function needsChoco(entry: { best: 'winget' | 'choco' }, chocoReady: boolean): boolean {
  return entry.best === 'choco' && !chocoReady;
}
