import type { Job } from '../ipc/bindings/Job';
import type { OpKind } from '../ipc/bindings/OpKind';
import type { SourceKind } from '../ipc/bindings/SourceKind';

const FINISHED = new Set(['succeeded', 'failed', 'cancelled', 'skipped']);

/** The job a package's button should reflect: an active one if any, else the most recent. */
export function jobFor(jobs: Job[], source: SourceKind, id: string, kind?: OpKind): Job | undefined {
  const mine = jobs.filter(
    (j) => j.op.target.source === source && j.op.target.id === id && (!kind || j.op.kind === kind),
  );
  const active = mine.find((j) => !FINISHED.has(j.phase));
  if (active) return active;
  return mine.reduce<Job | undefined>((best, j) => (!best || (j.endedAt ?? 0) > (best.endedAt ?? 0) ? j : best), undefined);
}
