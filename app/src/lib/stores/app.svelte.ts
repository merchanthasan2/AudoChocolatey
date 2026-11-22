// Application state: one reactive store per concern, all backed by the typed IPC layer.
import * as api from '../ipc/commands';
import type {
  Overview, Listing, InstalledPackage, UpgradeCandidate, Settings, SettingsPatch, Job, AppEntry, Home,
} from '../ipc/commands';
import { onJobChanged, onJobProgress, onQueueChanged } from '../ipc/events';
import { latestOnly, STALE } from '../logic/latestOnly';
import { jobFor } from '../logic/jobFor';
import { tidyResults } from '../logic/tidyResults';
import { missingFrom, installedSet, withJobs, needsSetup, bestId } from '../logic/entries';
import { notStarted } from '../logic/queue';
import { doneToast } from '../logic/doneToast';
import { resolveMotion, resolveTheme } from '../logic/theme';
import type { SourceKind } from '../ipc/bindings/SourceKind';
import type { OpKind } from '../ipc/bindings/OpKind';
import type { UpdatesListing, RuleKind } from '../ipc/commands';
import type { ListPref } from '../ipc/bindings/ListPref';
import type { ListSort } from '../ipc/bindings/ListSort';
import type { SourceLabel } from '../ipc/bindings/SourceLabel';

const message = (e: unknown) => (e as { message?: string })?.message ?? String(e);

/* ---------------------------------------------------------------- router */
export type View = 'marketplace' | 'updates' | 'installed' | 'history' | 'startup' | 'settings';
class Router {
  view = $state<View>('updates');
  go(v: View) { this.view = v; }
}
export const router = new Router();

/* ---------------------------------------------------------------- toasts */
export interface Toast { id: number; text: string; tone: 'ok' | 'warn' | 'error'; action?: { label: string; run: () => void } }
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
  /** Which optional sources can install right now. */
  on = $derived({ choco: this.chocoReady, store: settings.value?.store.enabled !== false });
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

/* ---------------------------------------------------------------- lists */
const DEFAULT_PREF: ListPref = { sort: 'suggested', dir: 'asc', view: 'list', hideComponents: true };
/** One page's list: what's typed and the source filter (this session only), and the sort,
 *  view and components switch (kept in Settings, saved shortly after a change). */
export class ListState {
  text = $state('');
  label = $state<SourceLabel | 'all'>('all');
  #local = $state<ListPref | null>(null);
  #timer: ReturnType<typeof setTimeout> | undefined;
  constructor(private page: 'updates' | 'installed') {}
  get pref(): ListPref { return this.#local ?? settings.value?.lists?.[this.page] ?? DEFAULT_PREF; }
  get filtered() { return this.text.trim() !== '' || this.label !== 'all'; }
  set(change: Partial<ListPref>) {
    this.#local = { ...this.pref, ...change };
    clearTimeout(this.#timer);
    this.#timer = setTimeout(() => {
      const lists = settings.value?.lists;
      if (lists && this.#local) settings.update({ lists: { ...lists, [this.page]: this.#local } });
    }, 400);
  }
  /** Click a column: the same one flips direction, a new one starts ascending. */
  sortBy(sort: ListSort) {
    this.set(sort === this.pref.sort ? { dir: this.pref.dir === 'asc' ? 'desc' : 'asc' } : { sort, dir: 'asc' });
  }
  clear() { this.text = ''; this.label = 'all'; }
}
export const lists = { updates: new ListState('updates'), installed: new ListState('installed') };

/* ---------------------------------------------------------------- library */
class Section<T, L extends Listing<T> = Listing<T>> {
  items = $state<T[]>([]);
  /** The whole last response (extra fields such as hidden updates). */
  last = $state<L | null>(null);
  sourceErrors = $state<Listing<T>['sourceErrors']>([]);
  loading = $state(false);
  loaded = $state(false);
  loadedAt = $state<number | null>(null);
  error = $state<string | null>(null);
  constructor(private fetcher: () => Promise<L>) {}
  /** Take a fresh response from another command (e.g. after changing a rule). */
  accept(l: L, freshCheck = false) {
    this.items = l.items;
    this.sourceErrors = l.sourceErrors;
    this.last = l;
    if (freshCheck) { this.loadedAt = Date.now(); this.loaded = true; this.error = null; }
  }
  async refresh() {
    if (this.loading) return;
    this.loading = true;
    try {
      const l = await this.fetcher();
      this.items = l.items;
      this.sourceErrors = l.sourceErrors;
      this.last = l;
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
class UpdatesSection extends Section<UpgradeCandidate, UpdatesListing> {
  hidden = $derived(this.last?.hidden ?? []);
  async setRule(c: UpgradeCandidate, kind: RuleKind) {
    try { this.accept(await api.setRule({ source: c.source, id: c.id }, c.name, kind)); }
    catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async restore(key: string) {
    try { this.accept(await api.removeRule(key)); }
    catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  /** Skips the shown version of several apps at once. */
  async skipMany(list: UpgradeCandidate[]) {
    if (list.length === 0) return;
    try {
      this.accept(await api.setRules(list.map((c) => ({ target: { source: c.source, id: c.id }, name: c.name, kind: { kind: 'skipVersion', version: c.availableVersion } }))));
      toasts.push({ text: `Skipping ${list.length === 1 ? 'that version' : `${list.length} updates`}. They're under Hidden updates.`, tone: 'ok' });
    } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  /** "What's new" and release dates, by `source:id`, for the exact shown version. */
  info = $state<Record<string, api.UpdateInfo>>({});
  takeInfo(list: api.UpdateInfo[]) {
    this.info = Object.fromEntries(list.map((i) => [`${i.source}:${i.id.toLowerCase()}`, i]));
  }
  infoFor(c: UpgradeCandidate): api.UpdateInfo | undefined {
    const i = this.info[`${c.source}:${c.id.toLowerCase()}`];
    return i && i.version === c.availableVersion ? i : undefined;
  }
  async loadInfo() {
    try { this.takeInfo(await api.getUpdateInfo()); } catch { /* none yet */ }
  }
  /** Apps installed directly that this app can update (never part of Update all). */
  linked = $state<api.LinkedUpdate[]>([]);
  async loadLinked() {
    try { this.linked = (await api.listLinkedUpdates()) ?? []; } catch { this.linked = []; }
  }
}
export const updates = new UpdatesSection(api.listUpdates);

/* ---------------------------------------------------------------- my bundles */
class MyBundlesStore {
  list = $state<api.MyBundleView[]>([]);
  /** The open import, waiting for "Install" or "Save as bundle". */
  importing = $state<api.ImportPreview | null>(null);
  /** Apps waiting for the "Add to bundle" dialog. */
  adding = $state<api.BundleAppIn[] | null>(null);
  async load() {
    try { this.list = await api.myBundles(); } catch { /* none yet */ }
  }
  async #run(f: () => Promise<api.MyBundleView[]>, done?: string) {
    try {
      this.list = await f();
      if (done) toasts.push({ text: done, tone: 'ok' });
      return true;
    } catch (e) { toasts.push({ text: message(e), tone: 'error' }); return false; }
  }
  add(bundle: string | null, name: string | null, apps: api.BundleAppIn[]) {
    const n = apps.length;
    const to = bundle ? this.list.find((b) => b.id === bundle)?.name : name;
    return this.#run(() => api.addToBundle(bundle, name, apps), `Added ${n} ${n === 1 ? 'app' : 'apps'} to ${to ?? 'your bundle'}`);
  }
  removeApp(bundle: string, key: string) { return this.#run(() => api.removeFromBundle(bundle, key)); }
  remove(bundle: string) { return this.#run(() => api.deleteBundle(bundle), 'Bundle deleted'); }
  async export(what: api.ExportWhat) {
    try {
      const n = await api.exportApps(what);
      if (n != null) toasts.push({ text: `Saved a list of ${n} ${n === 1 ? 'app' : 'apps'}`, tone: 'ok' });
    } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async pickImport() {
    try { this.importing = await api.importPick(); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async installImport(indices: number[]) {
    if (!this.importing) return;
    try {
      const ids = await api.importApply(this.importing.token, indices);
      toasts.push({ text: `Installing ${ids.length} ${ids.length === 1 ? 'app' : 'apps'}`, tone: 'ok' });
      this.importing = null;
    } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async keepImport(indices: number[], name: string) {
    if (!this.importing) return;
    const token = this.importing.token;
    if (await this.#run(() => api.importKeep(token, indices, name), 'Saved to My bundles')) this.importing = null;
  }
}
export const myBundles = new MyBundlesStore();

/* ---------------------------------------------------------------- queue */
const FINISHED = new Set(['succeeded', 'failed', 'cancelled', 'skipped']);
/** The answer to "VLC is open. Close it and update?" */
export type OpenAnswer = 'close' | 'later' | 'cancel';

class QueueStore {
  jobs = $state<Job[]>([]);
  paused = $state(false);
  /** About the current run of changes (the restore point). */
  note = $state<string | null>(null);
  /** Apps that need Windows to restart to finish. */
  restartNeeded = $state<string[]>([]);
  notStarted = $derived(notStarted(this.jobs));
  active = $derived(this.jobs.filter((j) => !FINISHED.has(j.phase)));
  current = $derived(this.active.find((j) => j.phase !== 'queued'));
  #started = false;
  #refreshTimer: ReturnType<typeof setTimeout> | undefined;

  async init() {
    if (this.#started) return;
    this.#started = true;
    await onJobChanged((j) => this.#upsert(j, true));
    await onJobProgress((j) => this.#upsert(j, false));
    await onQueueChanged((s) => this.#take(s));
    try { this.#take(await api.getQueue()); } catch { /* empty queue */ }
  }

  /** Jobs cleared from the panel stay in History, not here. */
  #take(s: api.QueueSnapshot) {
    this.jobs = s.jobs.filter((j) => !j.dismissed);
    this.paused = s.paused;
    this.note = s.note;
    this.restartNeeded = s.restartNeeded ?? [];
  }

  #upsert(job: Job, announce: boolean) {
    const prev = this.jobs.find((j) => j.id === job.id);
    this.jobs = prev ? this.jobs.map((j) => (j.id === job.id ? job : j)) : [...this.jobs, job];
    if (!announce || prev?.phase === job.phase) return;
    if (job.phase === 'succeeded') {
      toasts.push(doneToast(job), job.verify === 'unchanged' ? 8000 : undefined);
      this.#scheduleRefresh();
    } else if (job.phase === 'failed') {
      toasts.push(
        { text: job.error ?? `Couldn't finish ${job.displayName}.`, tone: 'error', action: { label: 'Retry', run: () => this.retry(job.id) } },
        8000,
      );
    }
  }

  /** Installed and update lists change after any successful job; refresh once things settle. */
  #scheduleRefresh() {
    clearTimeout(this.#refreshTimer);
    this.#refreshTimer = setTimeout(() => { installed.refresh().then(() => updates.loadLinked()); updates.refresh(); }, 1200);
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
    const id = bestId(e);
    return id ? this.#enqueue('install', e.best, id, e.name) : Promise.resolve();
  }
  /** What a bundle would queue now: not installed, not already queued, installable. */
  bundleMissing(entries: AppEntry[]) {
    const present = withJobs(installedSet(installed.items), this.jobs);
    return missingFrom(entries, present).filter((e) => !needsSetup(e, overview.on));
  }
  /** Queue every app of a bundle that isn't there yet; returns how many were queued. Waits for
   *  a good installed list, so apps already on the PC are never reinstalled. */
  async installMany(entries: AppEntry[]) {
    if (!installed.loaded || installed.error) return 0;
    const missing = this.bundleMissing(entries);
    const ops = missing.flatMap((e) => {
      const id = bestId(e);
      return id ? [{ op: { kind: 'install' as const, target: { source: e.best, id }, version: null }, displayName: e.name }] : [];
    });
    if (ops.length === 0) return 0;
    // Not `this.batch`: that tracks update runs for the Updates header.
    try { await api.enqueue(ops); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); return 0; }
    return ops.length;
  }
  /** "VLC is open. Close it and update?" Set while asking; the dialog answers through `answerOpen`. */
  openAsk = $state<{ apps: api.RunningApp[]; resolve: (a: OpenAnswer) => void } | null>(null);
  answerOpen(a: OpenAnswer) {
    const ask = this.openAsk;
    this.openAsk = null;
    ask?.resolve(a);
  }
  /** Splits updates into the ones to run now and the ones to leave for later, asking first when
   *  any app is open. Returns null when the person cancels. Never closes anything silently. */
  async #checkOpen(list: { source: SourceKind; id: string; name: string }[]): Promise<{ later: api.RunningApp[] } | null> {
    if (list.length === 0) return { later: [] };
    let open: api.RunningApp[] = [];
    try { open = await api.runningApps(list.map((c) => ({ source: c.source, id: c.id, name: c.name }))); } catch { return { later: [] }; }
    if (open.length === 0) return { later: [] };
    const answer = await new Promise<OpenAnswer>((resolve) => { this.openAsk = { apps: open, resolve }; });
    if (answer === 'cancel') return null;
    if (answer === 'later') return { later: open };
    const still = await api.closeApps(open.map((a) => ({ source: a.source, id: a.id, name: a.name }))).catch(() => open.map((a) => a.name));
    if (still.length > 0) {
      toasts.push({ text: `${still.join(', ')} ${still.length === 1 ? 'is' : 'are'} still open, so ${still.length === 1 ? 'it waits' : 'they wait'} for later.`, tone: 'warn' }, 8000);
    }
    return { later: open.filter((a) => still.includes(a.name)) };
  }
  async update(c: UpgradeCandidate) {
    const r = await this.#checkOpen([c]);
    if (!r || r.later.length > 0) return;
    return this.#enqueue('upgrade', c.source, c.id, c.name);
  }
  uninstall(p: { source: SourceKind; id: string; name: string }) { return this.#enqueue('uninstall', p.source, p.id, p.name); }
  /** Removes several apps in one go (the backend refuses more than 50, or a protected app). */
  async uninstallMany(list: { source: SourceKind; id: string; name: string }[]) {
    if (list.length === 0) return false;
    try {
      await api.enqueue(list.map((p) => ({ op: { kind: 'uninstall', target: { source: p.source, id: p.id }, version: null }, displayName: p.name })));
      return true;
    } catch (e) { toasts.push({ text: message(e), tone: 'error' }); return false; }
  }
  /** Job ids of the last "Update all/selected" run, for the "Updating 2 of 5" header. */
  batch = $state<string[]>([]);
  batchJobs = $derived(this.batch.map((id) => this.jobs.find((j) => j.id === id)).filter((j): j is Job => !!j));
  batchDone = $derived(this.batchJobs.filter((j) => FINISHED.has(j.phase)).length);
  batchRunning = $derived(this.batchJobs.length > 0 && this.batchDone < this.batchJobs.length);

  async updateAll() {
    const r = await this.#checkOpen(updates.items);
    if (!r) return;
    const skip = r.later.map((a) => ({ source: a.source, id: a.id }));
    try { this.batch = await api.updateAll(skip); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  /** `mode`: silent (normal), with the installer's own window, or reinstall the same version. */
  async updateMany(list: UpgradeCandidate[], mode: api.OpMode = 'normal') {
    if (list.length === 0) return;
    const r = await this.#checkOpen(list);
    if (!r) return;
    list = list.filter((c) => !r.later.some((a) => a.source === c.source && a.id === c.id));
    if (list.length === 0) return;
    try {
      this.batch = await api.enqueue(
        list.map((c) => ({ op: { kind: 'upgrade', target: { source: c.source, id: c.id }, version: null, mode }, displayName: c.name })),
      );
    } catch (e) {
      toasts.push({ text: message(e), tone: 'error' });
    }
  }
  async cancel(id: string) {
    try { await api.cancelJob(id); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async retry(id: string, mode?: api.OpMode) {
    try { await api.retryJob(id, mode); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  async clearFinished() { await api.clearFinished(); }
  async setPaused(on: boolean) {
    try { await (on ? api.pauseQueue() : api.resumeQueue()); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  /** Re-queue what an app restart stopped before it started. */
  async retryNotStarted() {
    for (const j of this.notStarted) await this.retry(j.id);
  }
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
  /** Live extras below the hand-checked apps (not checked by us). */
  more = $state<AppEntry[]>([]);
  moreLoading = $state(false);
  detail = $state<AppEntry | null>(null);
  #catGuard = latestOnly<AppEntry[]>();
  #moreGuard = latestOnly<AppEntry[]>();
  async load() {
    if (this.home) return;
    try { this.home = await api.marketplaceHome(); } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
  }
  /** Switching quickly never shows one category's apps under another's name. */
  async openCategory(id: string | null) {
    this.category = id;
    this.categoryApps = [];
    this.more = [];
    if (!id) { this.#catGuard.invalidate(); this.#moreGuard.invalidate(); return; }
    try {
      const apps = await this.#catGuard.run(api.marketplaceCategory(id));
      if (apps !== STALE) this.categoryApps = apps;
    } catch (e) { toasts.push({ text: message(e), tone: 'error' }); }
    this.moreLoading = true;
    try {
      const more = await this.#moreGuard.run(api.marketplaceCategoryMore(id));
      if (more !== STALE) { this.more = more; this.moreLoading = false; }
    } catch { this.moreLoading = false; }
  }
}
export const market = new MarketStore();
