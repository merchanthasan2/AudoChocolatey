import type { Job } from '../ipc/commands';

// Details the backend gives jobs stopped by an app restart (src-tauri/src/jobs/persist.rs).
const RESTART_DETAILS = new Set(['Not started', 'Stopped when the app closed']);

const sameOp = (a: Job, b: Job) =>
  a.op.kind === b.op.kind && a.op.target.source === b.op.target.source && a.op.target.id === b.op.target.id;

/** Jobs a restart stopped before anything was installed, and not asked for again since; the
 *  drawer offers to retry them. */
export function notStarted(jobs: Job[]): Job[] {
  return jobs.filter(
    (j, i) =>
      j.phase === 'cancelled' && RESTART_DETAILS.has(j.detail ?? '') && !jobs.slice(i + 1).some((k) => sameOp(j, k)),
  );
}
