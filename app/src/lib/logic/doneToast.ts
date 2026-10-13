import type { Job } from '../ipc/commands';

const VERB: Record<string, string> = { install: 'installed', upgrade: 'updated', uninstall: 'removed' };

/** What to say when a job succeeds; a version check that didn't move says so plainly. */
export function doneToast(job: Job): { text: string; tone: 'ok' | 'warn' } {
  const name = job.displayName;
  if (job.verify === 'unchanged') {
    return job.op.kind === 'uninstall'
      ? { text: `${name} finished, but still appears to be installed.`, tone: 'warn' }
      : {
          text: `${name} finished, but still reports version ${job.toVersion ?? 'the same version'}. It may finish updating after you restart it.`,
          tone: 'warn',
        };
  }
  return { text: `${name} ${VERB[job.op.kind]}${job.rebootRequired ? ' — restart to finish' : ''}`, tone: 'ok' };
}
