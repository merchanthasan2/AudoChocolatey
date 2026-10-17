<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';
  import { updates, queue, settings, myBundles, lists, toasts } from '../lib/stores/app.svelte';
  import { openReleaseNotes, type UpgradeCandidate, type OpMode } from '../lib/ipc/commands';
  import AppRow from '../lib/components/AppRow.svelte';
  import AppTile from '../lib/components/AppTile.svelte';
  import AppList from '../lib/components/AppList.svelte';
  import ListHeader, { type Column } from '../lib/components/ListHeader.svelte';
  import ListControls from '../lib/components/ListControls.svelte';
  import ListToolbar, { type ToolAction } from '../lib/components/ListToolbar.svelte';
  import MenuButton from '../lib/components/MenuButton.svelte';
  import JobButton from '../lib/components/JobButton.svelte';
  import Checkbox from '../lib/components/Checkbox.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import CountRoll from '../lib/components/CountRoll.svelte';
  import Dialog from '../lib/components/Dialog.svelte';
  import Icon from '../lib/components/Icon.svelte';
  import RowMenu from '../lib/components/RowMenu.svelte';
  import { relativeTime } from '../lib/logic/format';
  import { collapse, rise } from '../lib/motion';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';
  import { visibleUpdates } from '../lib/logic/visibleUpdates';
  import { clickGuard } from '../lib/logic/clickGuard';
  import { ruleLabel, ruleAt, ownChoice, effectiveChoice } from '../lib/logic/rules';
  import { applyQuery, labelCounts, type ListItem } from '../lib/logic/listView';
  import { keepSelected, headerState, rangeKeys } from '../lib/logic/selection';
  import { primaryAction } from '../lib/logic/primaryAction';
  import { SOURCE_LABELS, labelOf } from '../lib/logic/sources';
  import { exportListCsv } from '../lib/logic/csvRows';

  const MAX_REINSTALL = 20;
  const list = lists.updates;
  let hiddenBox = $state<HTMLElement>();
  let showHidden = $state(false);

  const indexOf = (u: UpgradeCandidate) => updates.items.indexOf(u);
  const infoAt = (u: UpgradeCandidate) => updates.last?.info[indexOf(u)];
  const mode = $derived(settings.value?.autoUpdate ?? 'notify');
  const ruleOf = (u: UpgradeCandidate) => ruleAt(updates.last, indexOf(u));
  const isAuto = (u: UpgradeCandidate) => effectiveChoice(mode, ruleOf(u)?.kind) === 'auto';
  const notes = (u: UpgradeCandidate) =>
    [
      infoAt(u)?.siteChanged ? 'Downloads from a different website than last time' : '',
      infoAt(u)?.important ? 'Important to keep current' : '',
      isAuto(u) ? 'Updates automatically' : '',
    ].filter(Boolean).join(' · ');

  const view = (u: UpgradeCandidate): ListItem => ({
    key: `${u.source}:${u.id}`, name: u.name, id: u.id, label: labelOf(u.source), component: false,
    version: u.installedVersion, newVersion: u.availableVersion, released: updates.infoFor(u)?.released ?? null,
    security: !!infoAt(u)?.important,
  });

  const jobOf = (u: UpgradeCandidate) => queue.jobFor(u.source, u.id, 'upgrade');
  const isBusy = (u: UpgradeCandidate) => {
    const j = jobOf(u);
    return !!j && !['succeeded', 'failed', 'cancelled', 'skipped'].includes(j.phase);
  };

  // Finished rows leave the list (with a collapse); everything else stays, including running ones.
  const visible = $derived(visibleUpdates(updates.items, jobOf, updates.loadedAt));
  const counts = $derived(labelCounts(visible.map(view), { text: list.text, hideComponents: false }));
  const rows = $derived(applyQuery(visible, view, { text: list.text, label: list.label, hideComponents: false, sort: list.pref.sort, dir: list.pref.dir }));
  const keys = $derived(uniqueKeys(rows, (u) => `${u.source}:${u.id}`));
  const idle = $derived(rows.filter((u) => !isBusy(u)));

  const selected = new SvelteSet<string>();
  const selectableKeys = $derived(new Set(rows.flatMap((u, i) => (isBusy(u) ? [] : [keys[i]]))));
  // Never act on rows that are filtered away or already running.
  $effect(() => {
    const keep = new Set(keepSelected(selected, selectableKeys));
    for (const k of [...selected]) if (!keep.has(k)) selected.delete(k);
  });
  const picked = $derived(rows.filter((_, i) => selected.has(keys[i])));
  const head = $derived(headerState(picked.length, selectableKeys.size));
  function toggleAll(on: boolean) {
    selected.clear();
    if (on) for (const k of selectableKeys) selected.add(k);
  }
  let anchor: string | null = null;
  let shift = false;
  function pick(key: string, on: boolean) {
    const range = shift && anchor ? rangeKeys(keys, anchor, key).filter((k) => selectableKeys.has(k)) : [key];
    for (const k of range) (on ? selected.add(k) : selected.delete(k));
    anchor = key;
  }

  // One main button whose label follows the selection; a short guard stops a double-click from
  // landing on "Update all" right after "Update N selected".
  const guard = clickGuard(800);
  const main = $derived(primaryAction(picked.length, idle.length, list.filtered));
  function runPrimary(m: OpMode = 'normal') {
    if (!guard() || !main) return;
    const target = main.scope === 'selected' ? picked : idle;
    selected.clear();
    queue.updateMany(target, m);
  }
  const hasStore = $derived(picked.some((u) => u.source === 'msstore'));
  let reinstalling = $state(false);

  function menuFor(u: UpgradeCandidate) {
    const rule = ruleOf(u);
    const own = ownChoice(rule?.kind);
    const items = [
      isAuto(u)
        ? { text: 'Ask before updating this app', run: () => updates.setRule(u, { kind: 'ask' }) }
        : { text: 'Update this app automatically', run: () => updates.setRule(u, { kind: 'auto' }) },
    ];
    if (own && rule) items.push({ text: 'Use my Settings choice', run: () => updates.restore(rule.key) });
    if (u.source !== 'msstore') items.push({ text: 'Update with the installer window', run: () => queue.updateMany([u], 'interactive') });
    items.push(
      { text: `Skip version ${u.availableVersion}`, run: () => updates.setRule(u, { kind: 'skipVersion', version: u.availableVersion }) },
      { text: 'Never update this app', run: () => updates.setRule(u, { kind: 'never' }) },
    );
    return items;
  }

  async function whatsNew(u: UpgradeCandidate) {
    try { await openReleaseNotes({ source: u.source, id: u.id }, u.availableVersion); }
    catch (e) { toasts.push({ text: (e as { message?: string })?.message ?? "Couldn't open the link.", tone: 'error' }); }
  }

  const columns: Column[] = [
    { label: 'App', sort: 'name', cls: 'c-app' },
    { label: 'Installed', sort: 'version', cls: 'c-ver' },
    { label: 'New version', cls: 'c-new' },
    { label: 'Source', sort: 'source', cls: 'c-src' },
    { label: 'Released', sort: 'released', cls: 'c-date' },
    { label: '', cls: 'c-act' },
  ];
  const actions = $derived<ToolAction[]>([
    ...(picked.length > 0
      ? [
          { key: 'skip', label: 'Skip selected', icon: 'eyeOff', run: () => { updates.skipMany(picked); selected.clear(); } },
          { key: 'bundle', label: 'Add to bundle', icon: 'plus', run: () => (myBundles.adding = picked.map((u) => ({ source: u.source, id: u.id, name: u.name }))) },
        ]
      : []),
    { key: 'check', label: updates.loading ? 'Checking…' : 'Check again', icon: 'refresh', disabled: updates.loading, run: () => updates.refresh() },
    ...(updates.hidden.length > 0
      ? [{ key: 'hidden', label: `Hidden updates (${updates.hidden.length})`, icon: 'eyeOff', run: () => { showHidden = true; queueMicrotask(() => hiddenBox?.scrollIntoView({ behavior: 'smooth' })); } }]
      : []),
    { key: 'csv', label: 'Save as spreadsheet', icon: 'download', disabled: rows.length === 0, run: () => exportListCsv('updates', rows.map((u) => ({ name: u.name, id: u.id, version: u.installedVersion, newVersion: u.availableVersion, label: labelOf(u.source), released: updates.infoFor(u)?.released }))) },
  ]);

  let now = $state(Date.now());
  $effect(() => {
    const t = setInterval(() => (now = Date.now()), 30_000);
    return () => clearInterval(t);
  });
  const batchPct = $derived(queue.batchJobs.length ? (queue.batchDone / queue.batchJobs.length) * 100 : 0);
</script>

<svelte:window onkeydown={(e) => (shift = e.shiftKey)} onkeyup={(e) => (shift = e.shiftKey)} />

<header class="head">
  <div class="title">
    <h1>
      {#if !updates.loaded}Checking for updates…{:else if visible.length === 0}Everything is up to date{:else}<CountRoll value={visible.length} /> {visible.length === 1 ? 'update' : 'updates'} ready{/if}
    </h1>
    <p class="muted">
      {#if queue.batchRunning}
        Updating {Math.min(queue.batchDone + 1, queue.batchJobs.length)} of {queue.batchJobs.length}…
      {:else if updates.loading}Checking now…{:else if updates.loadedAt}Checked {relativeTime(updates.loadedAt, now)}{/if}
    </p>
  </div>
</header>

<div class="batch" class:on={queue.batchRunning} aria-hidden={!queue.batchRunning}>
  <div class="batch-bar"><i style="width:{queue.batchRunning ? Math.max(4, batchPct) : 0}%"></i></div>
</div>

{#if visible.length > 0}
  <ListControls {list} {counts} placeholder="Search updates" />
{/if}

<div class="gap">
  <ListToolbar {actions}>
    {#snippet primary()}
      {#if main}
        {#if picked.length > 0}
          <MenuButton label="More ways to update the selected apps" triggerClass="btn btn-quiet split"
            items={[
              { text: `Update ${picked.length}`, run: () => runPrimary('normal') },
              { text: 'Update with the installer window', run: () => runPrimary('interactive'), disabled: hasStore },
              { text: `Reinstall ${picked.length}`, run: () => (reinstalling = true), disabled: hasStore || picked.length > MAX_REINSTALL },
            ]}>
            {#snippet trigger()}<span class="chev"><Icon name="chevron" size={14} stroke={2.5} /></span>{/snippet}
          </MenuButton>
        {/if}
        <button class="btn btn-primary" onclick={() => runPrimary()}>{main.label}</button>
      {/if}
    {/snippet}
  </ListToolbar>
</div>

{#each updates.sourceErrors as e (e.source)}
  <div class="gap"><Banner text="Some apps couldn't be checked for updates. The list may be incomplete." action={{ label: 'Try again', run: () => updates.refresh() }} /></div>
{/each}
{#if updates.error && visible.length > 0}<div class="gap"><Banner text={updates.error} action={{ label: 'Try again', run: () => updates.refresh() }} /></div>{/if}

<AppList items={rows} keyOf={(_, i) => keys[i]} view={list.pref.view} loaded={updates.loaded} error={updates.error}
  filtered={list.filtered} onclear={() => list.clear()} actW={136}
  empty={{ icon: 'check', tone: 'good', title: "You're all set", body: "Every app we can update is on its latest version. We'll keep checking in the background." }}>
  {#snippet header()}
    <ListHeader {columns} sort={list.pref.sort} dir={list.pref.dir} onsort={(s) => list.sortBy(s)}>
      {#snippet lead()}
        <Checkbox label="Select all updates shown" checked={head.checked} indeterminate={head.indeterminate} disabled={head.disabled} onchange={toggleAll} />
      {/snippet}
    </ListHeader>
  {/snippet}
  {#snippet row(u, i)}
    {@const job = jobOf(u)}
    {@const info = updates.infoFor(u)}
    <div class="row-wrap" in:rise={{ delay: Math.min(i, 8) * 40 }} out:collapse>
      <AppRow name={u.name} meta={notes(u) || `${u.installedVersion} → ${u.availableVersion}`} {job} mode="update" bar
        iconKey="{u.source}:{u.id}" density={list.pref.view === 'compact' ? 'compact' : 'list'}>
        {#snippet lead()}
          <Checkbox label="Select {u.name}" checked={selected.has(keys[i])} disabled={isBusy(u)} onchange={(v) => pick(keys[i], v)} />
        {/snippet}
        {#snippet cells()}
          <span class="cell c-ver truncate" title={u.installedVersion}>{u.installedVersion}</span>
          <span class="cell c-new">
            <span class="truncate new" title={u.availableVersion}>{u.availableVersion}</span>
            {#if info?.notesHost}<button class="notes" title="Release notes on {info.notesHost}" onclick={() => whatsNew(u)}>What's new</button>{/if}
          </span>
          <span class="cell c-src">{SOURCE_LABELS[labelOf(u.source)]}</span>
          <span class="cell c-date">{info?.released ?? ''}</span>
        {/snippet}
        {#snippet action()}
          <span class="row-actions">
            <JobButton {job} mode="update" name={u.name}
              onstart={() => queue.update(u)} oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
            {#if !isBusy(u)}<RowMenu label="More options for {u.name}" items={menuFor(u)} />{/if}
          </span>
        {/snippet}
      </AppRow>
    </div>
  {/snippet}
  {#snippet tile(u, i)}
    {@const job = jobOf(u)}
    <AppTile name={u.name} meta="{u.installedVersion} → {u.availableVersion} · {SOURCE_LABELS[labelOf(u.source)]}" note={notes(u)} {job} mode="update" iconKey="{u.source}:{u.id}">
      {#snippet lead()}
        <Checkbox label="Select {u.name}" checked={selected.has(keys[i])} disabled={isBusy(u)} onchange={(v) => pick(keys[i], v)} />
      {/snippet}
      {#snippet action()}
        <JobButton {job} mode="update" name={u.name}
          onstart={() => queue.update(u)} oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
        {#if !isBusy(u)}<RowMenu label="More options for {u.name}" items={menuFor(u)} />{/if}
      {/snippet}
    </AppTile>
  {/snippet}
</AppList>

{#if updates.hidden.length > 0}
  <section class="hidden-section" bind:this={hiddenBox}>
    <button class="hidden-toggle" aria-expanded={showHidden} onclick={() => (showHidden = !showHidden)}>
      <Icon name="eyeOff" size={16} /> Hidden updates ({updates.hidden.length})
      <span class="chev-h" class:open={showHidden}><Icon name="chevron" size={14} /></span>
    </button>
    {#if showHidden}
      <div class="card list" transition:collapse>
        {#each updates.hidden as h (h.rule.key + h.candidate.id)}
          <AppRow name={h.candidate.name} meta="{h.candidate.installedVersion} → {h.candidate.availableVersion} · {ruleLabel(h.rule.kind)}"
            iconKey="{h.candidate.source}:{h.candidate.id}" mode="update">
            {#snippet action()}
              <button class="btn btn-quiet" onclick={() => updates.restore(h.rule.key)}>Show again</button>
            {/snippet}
          </AppRow>
        {/each}
      </div>
    {/if}
  </section>
{/if}

{#if reinstalling}
  <Dialog title="Reinstall {picked.length} {picked.length === 1 ? 'app' : 'apps'}?" onclose={() => (reinstalling = false)}>
    <p class="muted">Each app's installer runs again over the copy you have, which can fix a broken install. Apps usually keep their settings, but some installers reset them.</p>
    <ul class="names">{#each picked as u (u.source + u.id)}<li>{u.name}</li>{/each}</ul>
    {#snippet buttons()}
      <button class="btn btn-quiet" onclick={() => (reinstalling = false)}>Cancel</button>
      <button class="btn btn-primary" onclick={() => { reinstalling = false; runPrimary('reinstall'); }}>Reinstall {picked.length}</button>
    {/snippet}
  </Dialog>
{/if}

<style>
  .head { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; margin-bottom: 14px; }
  .title p { margin-top: 4px; }
  .gap { margin-bottom: 12px; }
  .batch { margin: -6px 0 12px; opacity: 0; transition: opacity 200ms ease; }
  .batch.on { opacity: 1; }
  .batch-bar { height: 6px; border-radius: 6px; background: var(--hairline-strong); overflow: hidden; }
  .batch-bar i { display: block; height: 100%; border-radius: 6px; background: var(--accent); transition: width 500ms var(--spring); }
  .row-actions { display: inline-flex; align-items: center; gap: 4px; }
  .c-new { display: inline-flex; flex-direction: column; align-items: flex-start; }
  .new { max-width: 100%; color: var(--ink); font-weight: 600; }
  .notes { font-size: 11.5px; color: var(--accent); font-weight: 600; padding: 0; }
  .notes:hover { text-decoration: underline; text-underline-offset: 2px; }
  :global(.btn.split) { width: 34px; padding: 0; justify-content: center; }
  .chev { display: inline-grid; transform: rotate(90deg); }
  .hidden-section { margin-top: 18px; }
  .hidden-toggle { display: inline-flex; align-items: center; gap: 8px; font-weight: 600; font-size: 13px; color: var(--muted); padding: 6px 4px; margin-bottom: 8px; }
  .hidden-toggle:hover { color: var(--ink); }
  .chev-h { display: inline-grid; transform: rotate(90deg); transition: transform var(--dur-quick) var(--ease-out); }
  .chev-h.open { transform: rotate(-90deg); }
  .names { margin: 12px 0 0; padding: 10px 14px 10px 30px; max-height: 40vh; overflow-y: auto; background: var(--pill); border-radius: 10px; font-size: 13px; }
</style>
