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
    reason: null,
    verify: null,
    fromVersion: null,
    toVersion: null,
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

import { uniqueKeys } from './uniqueKeys';
describe('uniqueKeys', () => {
  it('keeps duplicates distinct (winget lists x86 and x64 copies under one id)', () => {
    const keys = uniqueKeys([{ id: 'Microsoft.VCLibs.14' }, { id: 'Git.Git' }, { id: 'Microsoft.VCLibs.14' }], (x) => x.id);
    expect(keys).toEqual(['Microsoft.VCLibs.14', 'Git.Git', 'Microsoft.VCLibs.14#2']);
    expect(new Set(keys).size).toBe(3);
  });
});

import { tidyResults } from './tidyResults';
describe('tidyResults', () => {
  const r = (id: string, name: string, source: 'winget' | 'choco' = 'winget') => ({ id, name, source });
  it('hides language editions when the main app is present', () => {
    const out = tidyResults(
      [r('Mozilla.Firefox', 'Mozilla Firefox (en-US)'), r('Mozilla.Firefox.ach', 'Mozilla Firefox (ach)'),
       r('Mozilla.Firefox.pt-BR', 'Mozilla Firefox (pt-BR)'), r('Mozilla.Firefox.MSIX', 'Mozilla Firefox (MSIX)')],
      'firefox',
    );
    expect(out.map((x) => x.id)).toEqual(['Mozilla.Firefox', 'Mozilla.Firefox.MSIX']);
  });
  it('keeps editions when the main app is missing', () => {
    expect(tidyResults([r('Foo.Bar.de', 'Foo (de)')], 'foo')).toHaveLength(1);
  });
  it('ranks exact and prefix name matches first, across sources', () => {
    const out = tidyResults(
      [r('Ghostery.Dawn', 'Ghostery Private Browser'), r('Mozilla.Firefox', 'Mozilla Firefox'), r('firefox', 'Firefox', 'choco')],
      'firefox',
    );
    expect(out.map((x) => x.id)).toEqual(['firefox', 'Mozilla.Firefox', 'Ghostery.Dawn']);
  });
});

import { clickGuard } from './clickGuard';
import { visibleUpdates } from './visibleUpdates';
describe('clickGuard', () => {
  it('ignores repeat clicks inside the window', () => {
    const g = clickGuard(500);
    expect(g(1000)).toBe(true);
    expect(g(1200)).toBe(false);
    expect(g(1499)).toBe(false);
    expect(g(1600)).toBe(true);
  });
});
describe('visibleUpdates', () => {
  const u = (id: string) => ({ source: 'winget' as const, id });
  it('hides a just-finished update only until the list is refreshed', () => {
    const jobs: Record<string, { phase: string; endedAt: number | null }> = { A: { phase: 'succeeded', endedAt: 200 } };
    const lookup = (x: { id: string }) => jobs[x.id];
    expect(visibleUpdates([u('A'), u('B')], lookup, 100).map((x) => x.id)).toEqual(['B']);
    expect(visibleUpdates([u('A'), u('B')], lookup, 300).map((x) => x.id)).toEqual(['A', 'B']);
  });
});
describe('latestOnly.invalidate', () => {
  it('turns an in-flight request stale', async () => {
    const guard = latestOnly<string>();
    let resolve!: (v: string) => void;
    const p = guard.run(new Promise<string>((r) => (resolve = r)));
    guard.invalidate();
    resolve('late');
    expect(await p).toBe(STALE);
  });
});

import { installedSet, isInstalled, missingFrom, withJobs, needsChoco } from './entries';
describe('entries', () => {
  const installed = [
    { id: 'Mozilla.Firefox', source: 'winget' as const },
    { id: 'vcredist140', source: 'choco' as const },
    { id: 'ARP\Machine\X64\Thing', source: null },
  ];
  const e = (winget: string | null, choco: string | null) => ({ winget, choco });
  it('an app counts as installed through either source, case-insensitively', () => {
    const set = installedSet(installed);
    expect(isInstalled(e('mozilla.firefox', 'firefox'), set)).toBe(true);
    expect(isInstalled(e(null, 'VCREDIST140'), set)).toBe(true);
    expect(isInstalled(e('Git.Git', 'git'), set)).toBe(false);
  });
  it('bundles only queue what is missing', () => {
    const set = installedSet(installed);
    const bundle = [e('Mozilla.Firefox', 'firefox'), e('Git.Git', 'git'), e(null, 'vcredist140')];
    expect(missingFrom(bundle, set)).toEqual([e('Git.Git', 'git')]);
  });
  const job = (source: 'winget' | 'choco', id: string, phase: string, kind = 'install') =>
    ({ op: { kind, target: { source, id } }, phase }) as never;
  it('apps queued, running or just installed are not queued again', () => {
    const set = withJobs(installedSet(installed), [
      job('winget', 'Git.Git', 'downloading'),
      job('winget', 'VideoLAN.VLC', 'succeeded'),
      job('winget', '7zip.7zip', 'failed'),
      job('winget', 'Notepad++.Notepad++', 'installing', 'uninstall'),
    ]);
    const bundle = [e('Git.Git', 'git'), e('VideoLAN.VLC', 'vlc'), e('7zip.7zip', '7zip'), e('Notepad++.Notepad++', 'notepadplusplus')];
    expect(missingFrom(bundle, set)).toEqual([e('7zip.7zip', '7zip'), e('Notepad++.Notepad++', 'notepadplusplus')]);
  });
  it('Chocolatey-only apps need Chocolatey switched on', () => {
    expect(needsChoco({ best: 'choco' }, false)).toBe(true);
    expect(needsChoco({ best: 'choco' }, true)).toBe(false);
    expect(needsChoco({ best: 'winget' }, false)).toBe(false);
  });
});

import { notStarted } from './queue';
describe('queue after a restart', () => {
  it('finds jobs that never started, newest app request once', () => {
    const list = [
      job('cancelled', { id: 'j1', detail: 'Not started' }),
      job('cancelled', { id: 'j2', detail: 'Stopped when the app closed' }),
      job('cancelled', { id: 'j3', detail: null }),
      job('queued', { id: 'j4', op: { kind: 'upgrade', target: { source: 'choco', id: 'git' }, version: null } }),
      job('cancelled', { id: 'j5', detail: 'Not started' }),
    ];
    expect(notStarted(list).map((j) => j.id)).toEqual(['j5']);
  });
  it('a job already retried no longer counts', () => {
    const other = { kind: 'install' as const, target: { source: 'winget' as const, id: 'Git.Git' }, version: null };
    const list = [
      job('cancelled', { id: 'j1', detail: 'Not started' }),
      job('cancelled', { id: 'j2', detail: 'Not started', op: other }),
      job('queued', { id: 'j3' }),
    ];
    expect(notStarted(list).map((j) => j.id)).toEqual(['j2']);
  });
});

import { doneToast } from './doneToast';
describe('finished-job toast', () => {
  it('confirms normally, and is honest when the version did not move', () => {
    expect(doneToast(job('succeeded', { op: { kind: 'upgrade', target: { source: 'winget', id: 'A' }, version: null }, verify: 'confirmed' })))
      .toEqual({ text: 'Firefox updated', tone: 'ok' });
    expect(doneToast(job('succeeded', { op: { kind: 'upgrade', target: { source: 'winget', id: 'A' }, version: null }, verify: 'unchanged', toVersion: '1.0' })))
      .toEqual({ text: 'Firefox finished, but still reports version 1.0. It may finish updating after you restart it.', tone: 'warn' });
    expect(doneToast(job('succeeded', { op: { kind: 'uninstall', target: { source: 'winget', id: 'A' }, version: null }, verify: 'unchanged' })))
      .toEqual({ text: 'Firefox finished, but still appears to be installed.', tone: 'warn' });
    expect(doneToast(job('succeeded', { rebootRequired: true }))).toEqual({ text: 'Firefox installed — restart to finish', tone: 'ok' });
  });
});

import { ruleLabel } from './rules';
describe('hidden update labels', () => {
  it('says why an update is hidden', () => {
    expect(ruleLabel({ kind: 'never' })).toBe('Never update');
    expect(ruleLabel({ kind: 'skipVersion', version: '2.0' })).toBe('Skipping version 2.0');
  });
});
