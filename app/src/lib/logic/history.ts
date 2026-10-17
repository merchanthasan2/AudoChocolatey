import type { Job } from '../ipc/commands';

/** What a finished change did, in plain words. */
export function historyLine(j: Job): string {
  const kind = j.op.kind;
  if (j.phase === 'succeeded') {
    if (kind === 'install') return j.toVersion ? `Installed version ${j.toVersion}` : 'Installed';
    if (kind === 'uninstall') return 'Removed';
    if (kind === 'rollback') return j.toVersion ? `Went back to version ${j.toVersion}` : 'Went back to the earlier version';
    if (j.fromVersion && j.toVersion && j.fromVersion !== j.toVersion) return `Updated from ${j.fromVersion} to ${j.toVersion}`;
    return 'Updated';
  }
  if (j.phase === 'failed') {
    const what = { install: 'install', upgrade: 'update', uninstall: 'remove', rollback: 'go back to the earlier version' }[kind] ?? 'change';
    return `Couldn't ${what}`;
  }
  if (j.phase === 'cancelled') return j.detail ?? 'Stopped';
  if (j.phase === 'skipped') return j.detail ?? 'Nothing to do';
  return '';
}

export function historyTone(j: Job): 'ok' | 'bad' | 'quiet' {
  return j.phase === 'succeeded' ? 'ok' : j.phase === 'failed' ? 'bad' : 'quiet';
}
