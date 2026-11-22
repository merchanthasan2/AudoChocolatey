<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';
  import { installed, queue, myBundles, lists, toasts } from '../lib/stores/app.svelte';
  import { removeDirect, type InstalledPackage } from '../lib/ipc/commands';
  import AppRow from '../lib/components/AppRow.svelte';
  import AppTile from '../lib/components/AppTile.svelte';
  import AppList from '../lib/components/AppList.svelte';
  import ListHeader, { type Column } from '../lib/components/ListHeader.svelte';
  import ListControls from '../lib/components/ListControls.svelte';
  import ListToolbar, { type ToolAction } from '../lib/components/ListToolbar.svelte';
  import JobButton from '../lib/components/JobButton.svelte';
  import Checkbox from '../lib/components/Checkbox.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import Dialog from '../lib/components/Dialog.svelte';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';
  import { applyQuery, labelCounts, type ListItem } from '../lib/logic/listView';
  import { keepSelected, headerState, rangeKeys } from '../lib/logic/selection';
  import { SOURCE_LABELS } from '../lib/logic/sources';
  import { exportListCsv } from '../lib/logic/csvRows';

  const MAX_REMOVE = 50;
  const list = lists.installed;
  const view = (p: InstalledPackage): ListItem => ({
    key: `${p.source ?? 'local'}:${p.id}`, name: p.name, id: p.id, label: p.label, component: p.component, version: p.version,
  });
  const counts = $derived(labelCounts(installed.items.map(view), { text: list.text, hideComponents: list.pref.hideComponents }));
  const componentCount = $derived(installed.items.filter((p) => p.component).length);
  const shown = $derived(applyQuery(installed.items, view, { text: list.text, label: list.label, hideComponents: list.pref.hideComponents, sort: list.pref.sort === 'suggested' ? 'name' : list.pref.sort, dir: list.pref.dir }));
  const keys = $derived(uniqueKeys(shown, (p) => `${p.source ?? 'local'}:${p.id}`));
  const managed = $derived(installed.items.filter((p) => p.source).length);
  const note = (p: InstalledPackage) =>
    p.component
      ? 'Windows component'
      : p.source || p.linked
        ? 'Kept up to date here'
        : p.removable
          ? p.label === 'microsoftStore'
            ? 'Uninstall uses Windows'
            : 'Uninstall opens its own uninstaller'
          : 'Updates handled by Windows';

  const jobOf = (p: InstalledPackage) => (p.source ? queue.jobFor(p.source, p.id, 'uninstall') : undefined);
  const busy = (p: InstalledPackage) => {
    const j = jobOf(p);
    return !!j && !['succeeded', 'failed', 'cancelled', 'skipped'].includes(j.phase);
  };
  // Only apps a source manages can be removed or saved to a bundle.
  const selectable = (p: InstalledPackage) => !!p.source && !busy(p);

  const selected = new SvelteSet<string>();
  const selectableKeys = $derived(new Set(shown.flatMap((p, i) => (selectable(p) ? [keys[i]] : []))));
  // Never act on rows that are filtered away or busy.
  $effect(() => {
    const keep = new Set(keepSelected(selected, selectableKeys));
    for (const k of [...selected]) if (!keep.has(k)) selected.delete(k);
  });
  const picked = $derived(shown.filter((_, i) => selected.has(keys[i])));
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

  let confirming = $state(false);
  let direct = $state<InstalledPackage | null>(null);
  let directBusy = $state<string | null>(null);
  async function removeOne() {
    const p = direct;
    if (!p || directBusy) return;
    direct = null;
    directBusy = p.id;
    try {
      const result = await removeDirect(p.id);
      toasts.push({
        text: result.opened ? `${p.name}'s uninstaller is open. Finish it there.` : `${p.name} was uninstalled.`,
        tone: 'ok',
      });
      await installed.refresh();
    } catch (e) {
      const err = e as { code?: string; message?: string };
      toasts.push({ text: err.message ?? "Couldn't open its uninstaller.", tone: err.code === 'cancelled' ? 'warn' : 'error' });
    } finally {
      directBusy = null;
    }
  }
  async function removeSelected() {
    const items = picked.map((p) => ({ source: p.source!, id: p.id, name: p.name }));
    confirming = false;
    if (await queue.uninstallMany(items)) selected.clear();
  }

  const columns: Column[] = [
    { label: 'App', sort: 'name', cls: 'c-app' },
    { label: 'Version', sort: 'version', cls: 'c-ver' },
    { label: 'Source', sort: 'source', cls: 'c-src' },
    { label: '', cls: 'c-act' },
  ];
  const actions = $derived<ToolAction[]>([
    ...(picked.length > 0
      ? [
          { key: 'remove', label: `Remove ${picked.length}`, icon: 'trash', danger: true, disabled: picked.length > MAX_REMOVE, run: () => (confirming = true) },
          { key: 'bundle', label: 'Add to bundle', icon: 'plus', run: () => (myBundles.adding = picked.map((p) => ({ source: p.source!, id: p.id, name: p.name }))) },
        ]
      : []),
    { key: 'check', label: 'Check again', icon: 'refresh', disabled: installed.loading, run: () => installed.refresh() },
    { key: 'export', label: 'Export list', icon: 'download', disabled: !installed.loaded, run: () => myBundles.export({ kind: 'installed' }) },
    { key: 'import', label: 'Import list', icon: 'upload', run: () => myBundles.pickImport() },
    { key: 'csv', label: 'Save as spreadsheet', icon: 'download', disabled: shown.length === 0, run: () => exportListCsv('installed', shown.map((p) => ({ name: p.name, id: p.id, version: p.version, label: p.label }))) },
  ]);
</script>

<svelte:window onkeydown={(e) => (shift = e.shiftKey)} onkeyup={(e) => (shift = e.shiftKey)} />

<header class="head">
  <div>
    <h1>Installed</h1>
    <p class="muted">
      {#if installed.loaded}{installed.items.length} apps · {managed} can be managed here{:else}Looking at your PC…{/if}
    </p>
  </div>
</header>

<ListControls {list} {counts} components={componentCount} placeholder="Search installed apps" />

<div class="gap"><ListToolbar {actions} /></div>

{#each installed.sourceErrors as e (e.source)}
  <div class="gap"><Banner text="Some installed apps couldn't be read. The list may be incomplete." action={{ label: 'Try again', run: () => installed.refresh() }} /></div>
{/each}

<AppList items={shown} keyOf={(_, i) => keys[i]} view={list.pref.view} loaded={installed.loaded} error={installed.error}
  filtered={list.filtered} onclear={() => list.clear()} actW={110}
  empty={{ icon: 'search', title: 'No apps found', body: 'Nothing installed could be read.' }}>
  {#snippet header()}
    <ListHeader {columns} sort={list.pref.sort} dir={list.pref.dir} onsort={(s) => list.sortBy(s)}>
      {#snippet lead()}
        <Checkbox label="Select all apps shown" checked={head.checked} indeterminate={head.indeterminate} disabled={head.disabled} onchange={toggleAll} />
      {/snippet}
    </ListHeader>
  {/snippet}
  {#snippet row(p, i)}
    {@const job = jobOf(p)}
    <AppRow name={p.name} meta={note(p)} {job} mode="uninstall" iconKey="{p.source ?? 'local'}:{p.id}" density={list.pref.view === 'compact' ? 'compact' : 'list'}>
      {#snippet lead()}
        <Checkbox label="Select {p.name}" checked={selected.has(keys[i])} disabled={!selectable(p)} onchange={(v) => pick(keys[i], v)} />
      {/snippet}
      {#snippet cells()}
        <span class="cell c-ver truncate" title={p.version}>{p.version}</span>
        <span class="cell c-src">{SOURCE_LABELS[p.label]}</span>
      {/snippet}
      {#snippet action()}
        {#if p.source}
          <JobButton {job} mode="uninstall" name={p.name}
            onstart={() => queue.uninstall({ source: p.source!, id: p.id, name: p.name })}
            oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
        {:else if p.removable}
          <JobButton job={undefined} mode="uninstall" name={p.name}
            onstart={() => { if (directBusy !== p.id) direct = p; }}
            oncancel={() => {}} onretry={() => {}} />
        {/if}
      {/snippet}
    </AppRow>
  {/snippet}
  {#snippet tile(p, i)}
    {@const job = jobOf(p)}
    <AppTile name={p.name} meta="{p.version} · {SOURCE_LABELS[p.label]}" note={note(p)} {job} mode="uninstall" iconKey="{p.source ?? 'local'}:{p.id}">
      {#snippet lead()}
        <Checkbox label="Select {p.name}" checked={selected.has(keys[i])} disabled={!selectable(p)} onchange={(v) => pick(keys[i], v)} />
      {/snippet}
      {#snippet action()}
        {#if p.source}
          <JobButton {job} mode="uninstall" name={p.name}
            onstart={() => queue.uninstall({ source: p.source!, id: p.id, name: p.name })}
            oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
        {:else if p.removable}
          <JobButton job={undefined} mode="uninstall" name={p.name}
            onstart={() => { if (directBusy !== p.id) direct = p; }}
            oncancel={() => {}} onretry={() => {}} />
        {/if}
      {/snippet}
    </AppTile>
  {/snippet}
</AppList>

{#if direct}
  <Dialog title="Uninstall {direct.name}?" onclose={() => (direct = null)}>
    <p class="muted">{direct.label === 'microsoftStore' ? 'Windows will uninstall it.' : 'Its own uninstaller will open. Nothing is removed until you finish that window.'}</p>
    {#snippet buttons()}
      <button class="btn btn-primary" onclick={() => (direct = null)}>Keep it</button>
      <button class="btn btn-quiet danger" onclick={removeOne}>Uninstall</button>
    {/snippet}
  </Dialog>
{/if}

{#if confirming}
  <Dialog title="Remove {picked.length} {picked.length === 1 ? 'app' : 'apps'}?" onclose={() => (confirming = false)}>
    <p class="muted">These apps will be uninstalled one after another. Their settings may be lost.</p>
    <ul class="names">
      {#each picked as p (p.source + p.id)}<li>{p.name}</li>{/each}
    </ul>
    {#snippet buttons()}
      <button class="btn btn-primary" onclick={() => (confirming = false)}>Keep them</button>
      <button class="btn btn-quiet danger" onclick={removeSelected}>Remove {picked.length}</button>
    {/snippet}
  </Dialog>
{/if}

<style>
  .head { margin-bottom: 16px; }
  .head p { margin-top: 4px; }
  .gap { margin-bottom: 12px; }
  .danger { color: var(--danger); }
  .names { margin: 12px 0 0; padding: 10px 14px 10px 30px; max-height: 40vh; overflow-y: auto; background: var(--pill); border-radius: 10px; font-size: 13px; }
  .names li + li { margin-top: 3px; }
</style>
