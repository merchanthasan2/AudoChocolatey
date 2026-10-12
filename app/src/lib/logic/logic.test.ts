import { describe, expect, it } from 'vitest';
import type { Job } from '../ipc/bindings/Job';
import type { JobPhase } from '../ipc/bindings/JobPhase';
import { jobButtonState } from './jobButton';
import { latestOnly, STALE } from './latestOnly';
import { monogram } from './monogram';
import { formatBytes, pluralUpdates, relativeTime } from './format';
import { resolveTheme, resolveMotion } from './theme';
import { jobFor } from './jobFor';

function job(phase: JobPhase, extra: Partial<Job> = {}): Job {
  return {
    id: 'j1',
    op: { kind: 'install', target: { source: 'winget', id: 'Mozilla.Firefox' }, version: null },
    displayName: 'Firefox',
    phase,
    detail: null,
    percent: null,
    bytesDone: null,
    bytesTotal: null,
    cancellable: ['queued', 'preparing', 'waiting', 'downloading'].includes(phase),
    rebootRequired: false,
    error: null,
    startedAt: null,
    endedAt: null,
    ...extra,
  };
}

describe('jobButtonState', () => {
  it('idle labels by mode', () => {
    expect(jobButtonState(undefined, 'update')).toMatchObject({ kind: 'idle', label: 'Update' });
    expect(jobButtonState(undefined, 'install')).toMatchObject({ kind: 'idle', label: 'Get' });
    expect(jobButtonState(undefined, 'uninstall')).toMatchObject({ kind: 'idle', label: 'Uninstall' });
  });
  it('queued is cancellable', () => {
    expect(jobButtonState(job('queued'), 'install')).toMatchObject({ kind: 'queued', label: 'Queued', cancellable: true });
  });
  it('real percent is determinate, missing percent is working', () => {
    expect(jobButtonState(job('downloading', { percent: 42.5 }), 'install')).toMatchObject({ kind: 'progress', percent: 42.5 });
    expect(jobButtonState(job('downloading'), 'install')).toMatchObject({ kind: 'working', label: 'Downloading', percent: null });
  });
  it('installing cannot be cancelled', () => {
    expect(jobButtonState(job('installing'), 'install')).toMatchObject({ kind: 'working', label: 'Installing', cancellable: false });
    expect(jobButtonState(job('waiting'), 'install').label).toBe('Waiting for another installer');
  });
  it('done, restart and skipped', () => {
    expect(jobButtonState(job('succeeded'), 'install')).toMatchObject({ kind: 'done', label: 'Done' });
    expect(jobButtonState(job('succeeded', { rebootRequired: true }), 'update').label).toBe('Restart needed');
    expect(jobButtonState(job('skipped', { detail: 'No applicable update' }), 'update')).toMatchObject({ kind: 'skipped', label: 'Up to date' });
    expect(jobButtonState(job('skipped', { detail: 'Already installed' }), 'install').label).toBe('Installed');
  });
  it('failed and cancelled fall back to a retry state', () => {
    expect(jobButtonState(job('failed', { error: 'No space' }), 'install')).toMatchObject({ kind: 'failed', label: 'Retry' });
    expect(jobButtonState(job('cancelled'), 'install')).toMatchObject({ kind: 'idle', label: 'Get' });
  });
});

describe('latestOnly', () => {
  it('drops responses from older requests', async () => {
    const guard = latestOnly<string>();
    let resolveA!: (v: string) => void;
    const a = guard.run(new Promise<string>((r) => (resolveA = r)));
    const b = guard.run(Promise.resolve('B'));
    expect(await b).toBe('B');
    resolveA('A');
    expect(await a).toBe(STALE);
  });
});

describe('monogram', () => {
  it('is stable and readable', () => {
    expect(monogram('Visual Studio Code').letters).toBe('VS');
    expect(monogram('7-Zip').letters).toBe('7Z');
    expect(monogram('firefox').letters).toBe('F');
    expect(monogram('').letters).toBe('?');
    expect(monogram('Discord').hue).toBe(monogram('Discord').hue);
    expect(monogram('Discord').hue).toBeGreaterThanOrEqual(0);
    expect(monogram('Discord').hue).toBeLessThan(360);
  });
});

describe('format', () => {
  it('bytes and plurals', () => {
    expect(formatBytes(2009088)).toBe('1.9 MB');
    expect(formatBytes(512)).toBe('512 B');
    expect(formatBytes(93445848)).toBe('89 MB');
    expect(pluralUpdates(0)).toBe('Everything is up to date');
    expect(pluralUpdates(1)).toBe('1 update ready');
    expect(pluralUpdates(4)).toBe('4 updates ready');
  });
  it('relative time', () => {
    const now = 1_000_000_000;
    expect(relativeTime(now - 5_000, now)).toBe('just now');
    expect(relativeTime(now - 120_000, now)).toBe('2 minutes ago');
    expect(relativeTime(now - 3_600_000, now)).toBe('1 hour ago');
  });
});

describe('theme and motion', () => {
  it('resolves theme', () => {
    expect(resolveTheme('system')).toBeUndefined();
    expect(resolveTheme('dark')).toBe('dark');
    expect(resolveTheme('light')).toBe('light');
  });
  it('resolves motion', () => {
    expect(resolveMotion('system', true)).toBe('reduced');
    expect(resolveMotion('system', false)).toBe('full');
    expect(resolveMotion('full', true)).toBe('full');
    expect(resolveMotion('reduced', false)).toBe('reduced');
  });
});

describe('jobFor', () => {
  it('prefers active jobs, then newest', () => {
    const done = job('succeeded', { id: 'j1', endedAt: 1 });
    const active = job('downloading', { id: 'j3' });
    const newer = job('failed', { id: 'j2', endedAt: 2 });
    expect(jobFor([done, newer, active], 'winget', 'Mozilla.Firefox')?.id).toBe('j3');
    expect(jobFor([done, newer], 'winget', 'Mozilla.Firefox')?.id).toBe('j2');
    expect(jobFor([done], 'choco', 'firefox')).toBeUndefined();
    expect(jobFor([done], 'winget', 'Mozilla.Firefox', 'upgrade')).toBeUndefined();
  });
});
