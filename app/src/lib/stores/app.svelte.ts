// Application state: one reactive store per concern, all backed by the typed IPC layer.
import * as api from '../ipc/commands';
import type {
  Overview, Listing, InstalledPackage, UpgradeCandidate, Settings, SettingsPatch, Job, AppEntry, Home,
} from '../ipc/commands';
import { onJobChanged, onJobProgress, onQueueChanged } from '../ipc/events';
import { latestOnly, STALE } from '../logic/latestOnly';
import { jobFor } from '../logic/jobFor';
import { tidyResults } from '../logic/tidyResults';
import { missingFrom, installedSet, withJobs, needsChoco } from '../logic/entries';
import { resolveMotion, resolveTheme } from '../logic/theme';
import type { SourceKind } from '../ipc/bindings/SourceKind';
import type { OpKind } from '../ipc/bindings/OpKind';

const message = (e: unknown) => (e as { message?: string })?.message ?? String(e);

/* ---------------------------------------------------------------- router */
export type View = 'marketplace' | 'updates' | 'installed' | 'settings';
class Router {
  view = $state<View>('updates');
  go(v: View) { this.view = v; }
}
export const router = new Router();

/* ---------------------------------------------------------------- toasts */
export interface Toast { id: number; text: string; tone: 'ok' | 'error'; action?: { label: string; run: () => void } }
class Toasts {
  list = $state<Toast[]>([]);
  #next = 1;
  #timers = new Map<number, { left: number; started: number; handle?: ReturnType<typeof setTimeout> }>();
  push(t: Omit<Toast, 'id'>, ms = 5000) {
    const id = this.#next++;
    this.list = [...this.list, { ...t, id }].slice(-4);
    this.#timers.set(id, { left: ms, started: 0 });
    this.resume(id);
  }
  /** Hovering or focusing a toast stops its countdown (people need time to read and click). */
  pause(id: number) {
    const t = this.#timers.get(id);
    if (!t?.handle) return;
    clearTimeout(t.handle);
    t.handle = undefined;
    t.left -= Date.now() - t.started;
  }
  resume(id: number) {
    const t = this.#timers.get(id);
    if (!t || t.handle) return;
    t.started = Date.now();
    t.handle = setTimeout(() => this.dismiss(id), Math.max(1500, t.left));
  }
  dismiss(id: number) {
    const t = this.#timers.get(id);
    if (t?.handle) clearTimeout(t.handle);
    this.#timers.delete(id);
    this.list = this.list.filter((x) => x.id !== id);
  }
}
export const toasts = new Toasts();

/* ---------------------------------------------------------------- overview */
class OverviewStore {
  value = $state<Overview | null>(null);
  error = $state<string | null>(null);
  /** Chocolatey is installed and working, and the user hasn't switched it off in Settings. */
  chocoReady = $derived(
    (this.value?.sources.some((s) => s.kind === 'choco' && s.state === 'ready') ?? false) && settings.value?.choco.enabled !== false,
  );
  async load() {
    try { this.value = await api.getOverview(); this.error = null; } catch (e) { this.error = message(e); }
  }
}
export const overview = new OverviewStore();

/* ---------------------------------------------------------------- settings */
class SettingsStore {
  value = $state<Settings | null>(null);
  #osReduced = typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  async load() {
    try { this.value = await api.getSettings(); } catch { /* defaults stay in CSS */ }
    this.apply();
  }
  async update(patch: SettingsPatch) {
    try { this.value = await api.updateSettings(patch); this.apply(); }
    catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  apply() {
    const root = document.documentElement;
    const theme = resolveTheme(this.value?.theme ?? 'system');
    if (theme) root.dataset.theme = theme; else delete root.dataset.theme;
    root.dataset.motion = resolveMotion(this.value?.motion ?? 'system', this.#osReduced);
  }
}
export const settings = new SettingsStore();

/* ---------------------------------------------------------------- library */
class Section<T> {
  items = $state<T[]>([]);
  sourceErrors = $state<Listing<T>['sourceErrors']>([]);
  loading = $state(false);
  loaded = $state(false);
  loadedAt = $state<number | null>(null);
  error = $state<string | null>(null);
  constructor(private fetcher: () => Promise<Listing<T>>) {}
  async refresh() {
    if (this.loading) return;
    this.loading = true;
    try {
      const l = await this.fetcher();
      this.items = l.items;
      this.sourceErrors = l.sourceErrors;
      this.error = null;
      this.loadedAt = Date.now();
    } catch (e) {
      this.error = message(e);
    } finally {
      this.loading = false;
      this.loaded = true;
    }
  }
}
export const installed = new Section<InstalledPackage>(api.listInstalled);
export const updates = new Section<UpgradeCandidate>(api.listUpdates);

/* ---------------------------------------------------------------- queue */
const FINISHED = new Set(['succeeded', 'failed', 'cancelled', 'skipped']);
class QueueStore {
  jobs = $state<Job[]>([]);
  active = $derived(this.jobs.filter((j) => !FINISHED.has(j.phase)));
  current = $derived(this.active.find((j) => j.phase !== 'queued'));
  #started = false;
  #refreshTimer: ReturnType<typeof setTimeout> | undefined;

  async init() {
    if (this.#started) return;
    this.#started = true;
    await onJobChanged((j) => this.#upsert(j, true));
    await onJobProgress((j) => this.#upsert(j, false));
    await onQueueChanged((all) => (this.jobs = all));
    try { this.jobs = await api.getQueue(); } catch { /* empty queue */ }
  }

  #upsert(job: Job, announce: boolean) {
    const prev = this.jobs.find((j) => j.id === job.id);
    this.jobs = prev ? this.jobs.map((j) => (j.id === job.id ? job : j)) : [...this.jobs, job];
    if (!announce || prev?.phase === job.phase) return;
    if (job.phase === 'succeeded') {
      const verb = job.op.kind === 'uninstall' ? 'removed' : job.op.kind === 'upgrade' ? 'updated' : 'installed';
      toasts.push({ text: `${job.displayName} ${verb}${job.rebootRequired ? ' — restart to finish' : ''}`, tone: 'ok' });
      this.#scheduleRefresh();
    } else if (job.phase === 'failed') {
      toasts.push(
        { text: `Couldn't finish ${job.displayName}: ${job.error ?? 'unknown error'}`, tone: 'error', action: { label: 'Retry', run: () => this.retry(job.id) } },
        8000,
      );
    }
  }

  /** Installed and update lists change after any successful job; refresh once things settle. */
  #scheduleRefresh() {
    clearTimeout(this.#refreshTimer);
    this.#refreshTimer = setTimeout(() => { installed.refresh(); updates.refresh(); }, 1200);
  }

  jobFor(source: SourceKind, id: string, kind?: OpKind) { return jobFor(this.jobs, source, id, kind); }

  async #enqueue(kind: OpKind, source: SourceKind, id: string, name: string) {
    try {
      await api.enqueue([{ op: { kind, target: { source, id }, version: null }, displayName: name }]);
    } catch (e) {
      toasts.push({ text: message(e), tone: 'error' });
    }
  }
  install(p: { source: SourceKind; id: string; name: string }) { return this.#enqueue('install', p.source, p.id, p.name); }
  /** Install a catalog/search entry through its best source. */
  installEntry(e: AppEntry) {
    const id = e.best === 'winget' ? e.winget : e.choco;
    return id ? this.#enqueue('install', e.best, id, e.name) : Promise.resolve();
  }
  /** What a bundle would queue now: not installed, not already queued, installable. */
  bundleMissing(entries: AppEntry[]) {
    const present = withJobs(installedSet(installed.items), this.jobs);
    return missingFrom(entries, present).filter((e) => !needsChoco(e, overview.chocoReady));
  }
  /** Queue every app of a bundle that isn't there yet; returns how many were queued. Waits for
   *  a good installed list, so apps already on the PC are never reinstalled. */
  async installMany(entries: AppEntry[]) {
    if (!installed.loaded || installed.error) return 0;
    const missing = this.bundleMissing(entries);
    const ops = missing.flatMap((e) => {
      const id = e.best === 'winget' ? e.winget : e.choco;
      return id ? [{ op: { kind: 'install' as const, target: { source: e.best, id }, version: null }, displayName: e.name }] : [];
    });
    if (ops.length === 0) return 0;
    // Not `this.batch`: that tracks update runs for the Updates header.
    try { await api.enqueue(ops); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); return 0; }
    return ops.length;
  }
  update(c: UpgradeCandidate) { return this.#enqueue('upgrade', c.source, c.id, c.name); }
  uninstall(p: { source: SourceKind; id: string; name: string }) { return this.#enqueue('uninstall', p.source, p.id, p.name); }
  /** Job ids of the last "Update all/selected" run, for the "Updating 2 of 5" header. */
  batch = $state<string[]>([]);
  batchJobs = $derived(this.batch.map((id) => this.jobs.find((j) => j.id === id)).filter((j): j is Job => !!j));
  batchDone = $derived(this.batchJobs.filter((j) => FINISHED.has(j.phase)).length);
  batchRunning = $derived(this.batchJobs.length > 0 && this.batchDone < this.batchJobs.length);

  async updateAll() {
    try { this.batch = await api.updateAll(); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async updateMany(list: UpgradeCandidate[]) {
    if (list.length === 0) return;
    try {
      this.batch = await api.enqueue(
        list.map((c) => ({ op: { kind: 'upgrade', target: { source: c.source, id: c.id }, version: null }, displayName: c.name })),
      );
    } catch (e) {
      toasts.push({ text: message(e), tone: 'error' });
    }
  }
  async cancel(id: string) {
    try { await api.cancelJob(id); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async retry(id: string) {
    try { await api.retryJob(id); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async clearFinished() { await api.clearFinished(); }
}
export const queue = new QueueStore();

/* ---------------------------------------------------------------- search */
class SearchStore {
  query = $state('');
  results = $state<AppEntry[]>([]);
  sourceErrors = $state<Listing<AppEntry>['sourceErrors']>([]);
  loading = $state(false);
  error = $state<string | null>(null);
  searched = $state('');
  #guard = latestOnly<Listing<AppEntry>>();
  #timer: ReturnType<typeof setTimeout> | undefined;

  set(q: string) {
    this.query = q;
    clearTimeout(this.#timer);
    const trimmed = q.trim();
    if (trimmed.length < 2) {
      this.#guard.invalidate();
      this.results = []; this.sourceErrors = []; this.loading = false; this.error = null; this.searched = '';
      return;
    }
    this.loading = true;
    this.#timer = setTimeout(() => this.#run(trimmed), 300);
  }

  async #run(q: string) {
    try {
      const r = await this.#guard.run(api.search(q));
      if (r === STALE) return;
      // Rank by name and fold language editions; the merged entry is kept whole.
      const tidy = tidyResults(r.items.map((e) => ({ id: e.winget ?? e.choco ?? e.key, name: e.name, source: e.best, entry: e })), q);
      this.results = tidy.map((t) => t.entry);
      this.sourceErrors = r.sourceErrors;
      this.error = null;
      this.searched = q;
    } catch (e) {
      this.error = message(e);
    } finally {
      if (this.query.trim() === q) this.loading = false;
    }
  }
}
export const search = new SearchStore();

/* ---------------------------------------------------------------- marketplace */
class MarketStore {
  home = $state<Home | null>(null);
  category = $state<string | null>(null);
  categoryApps = $state<AppEntry[]>([]);
  detail = $state<AppEntry | null>(null);
  async load() {
    if (this.home) return;
    try { this.home = await api.marketplaceHome(); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async openCategory(id: string | null) {
    this.category = id;
    this.categoryApps = id ? await api.marketplaceCategory(id) : [];
  }
}
export const market = new MarketStore();
