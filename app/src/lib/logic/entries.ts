// Helpers for catalog/search entries against what's installed on this PC.

type Source = 'winget' | 'choco' | 'msstore';
type Installed = { id: string; source: Source | null };
type Ids = { winget: string | null; choco: string | null; msstore?: string | null };

const keyOf = (source: Source, id: string) => `${source}:${source === 'msstore' ? id.toUpperCase() : id.toLowerCase()}`;

/** `<source>:<id>` keys for everything a source manages (case-folded per source). */
export function installedSet(items: Installed[]): Set<string> {
  return new Set(items.filter((p) => p.source).map((p) => keyOf(p.source as Source, p.id)));
}

export function isInstalled(entry: Ids, set: Set<string>): boolean {
  return (
    (!!entry.winget && set.has(keyOf('winget', entry.winget))) ||
    (!!entry.choco && set.has(keyOf('choco', entry.choco))) ||
    (!!entry.msstore && set.has(keyOf('msstore', entry.msstore)))
  );
}

/** Entries not installed yet (what a bundle should actually queue). */
export function missingFrom<T extends Ids>(entries: T[], set: Set<string>): T[] {
  return entries.filter((e) => !isInstalled(e, set));
}

type JobLike = { op: { kind: string; target: { source: Source; id: string } }; phase: string };
const GONE = new Set(['failed', 'cancelled']);

/** Adds apps with a queued, running or finished-ok job, so a bundle never queues them twice
 *  (the installed list lags a second or two behind a finished install). */
export function withJobs(set: Set<string>, jobs: JobLike[]): Set<string> {
  const out = new Set(set);
  for (const j of jobs) {
    if (j.op.kind !== 'uninstall' && !GONE.has(j.phase)) out.add(keyOf(j.op.target.source, j.op.target.id));
  }
  return out;
}

/** The id an install uses, from the entry's chosen source. */
export function bestId(e: Ids & { best: Source }): string | null {
  return (e.best === 'winget' ? e.winget : e.best === 'choco' ? e.choco : e.msstore) ?? null;
}

/** True when the app installs from a source that's turned off or missing. */
export function needsSetup(entry: { best: Source }, on: { choco: boolean; store: boolean }): boolean {
  return (entry.best === 'choco' && !on.choco) || (entry.best === 'msstore' && !on.store);
}
