<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';
  import { installed, queue, myBundles } from '../lib/stores/app.svelte';
  import type { InstalledPackage } from '../lib/ipc/commands';
  import AppRow from '../lib/components/AppRow.svelte';
  import JobButton from '../lib/components/JobButton.svelte';
  import Checkbox from '../lib/components/Checkbox.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import SearchField from '../lib/components/SearchField.svelte';
  import Dialog from '../lib/components/Dialog.svelte';
  import Icon from '../lib/components/Icon.svelte';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';

  const MAX_REMOVE = 50;
  let filter = $state('');
  const shown = $derived.by(() => {
    const f = filter.trim().toLowerCase();
    const list = f ? installed.items.filter((p) => p.name.toLowerCase().includes(f) || p.id.toLowerCase().includes(f)) : installed.items;
    return [...list].sort((a, b) => a.name.localeCompare(b.name));
  });
  const keys = $derived(uniqueKeys(shown, (p) => `${p.source ?? 'local'}:${p.id}`));
  const managed = $derived(installed.items.filter((p) => p.source).length);
  const sourceText = (s: string | null) => (s ? 'Kept up to date here' : 'Updates handled by Windows');

  const jobOf = (p: InstalledPackage) => (p.source ? queue.jobFor(p.source, p.id, 'uninstall') : undefined);
  const busy = (p: InstalledPackage) => {
    const j = jobOf(p);
    return !!j && !['succeeded', 'failed', 'cancelled', 'skipped'].includes(j.phase);
  };
  // Only apps a source manages can be removed or saved to a bundle.
  const selectable = (p: InstalledPackage) => !!p.source && !busy(p);

  const selected = new SvelteSet<string>();
  $effect(() => {
    const live = new Set(shown.map((p, i) => (selectable(p) ? keys[i] : '')));
    for (const k of [...selected]) if (!live.has(k)) selected.delete(k);
  });
  const picked = $derived(shown.filter((_, i) => selected.has(keys[i])));
  const pickable = $derived(shown.filter(selectable));
  const allPicked = $derived(pickable.length > 0 && picked.length === pickable.length);
  function toggleAll(on: boolean) {
    selected.clear();
    if (on) shown.forEach((p, i) => { if (selectable(p)) selected.add(keys[i]); });
  }

  let confirming = $state(false);
  async function removeSelected() {
    const list = picked.map((p) => ({ source: p.source!, id: p.id, name: p.name }));
    confirming = false;
    if (await queue.uninstallMany(list)) selected.clear();
  }
  function addSelected() {
    myBundles.adding = picked.map((p) => ({ source: p.source!, id: p.id, name: p.name }));
  }
</script>

<header class="head">
  <div>
    <h1>Installed</h1>
    <p class="muted">
      {#if installed.loaded}{installed.items.length} apps · {managed} can be managed here{:else}Looking at your PC…{/if}
    </p>
  </div>
  <div class="filter"><SearchField value={filter} oninput={(v) => (filter = v)} placeholder="Filter installed apps" hint={false} /></div>
</header>

<div class="toolbar">
  {#if picked.length > 0}
    <span class="count">{picked.length} selected</span>
    <button class="btn btn-quiet danger" disabled={picked.length > MAX_REMOVE} title={picked.length > MAX_REMOVE ? 'Remove at most 50 apps at a time' : undefined}
      onclick={() => (confirming = true)}><Icon name="trash" size={16} /> Remove selected</button>
    <button class="btn btn-quiet" onclick={addSelected}><Icon name="plus" size={16} /> Add to bundle</button>
  {/if}
  <span class="spacer"></span>
  <button class="btn btn-quiet" onclick={() => installed.refresh()} disabled={installed.loading}><Icon name="refresh" size={16} /> Check again</button>
  <button class="btn btn-quiet" onclick={() => myBundles.export({ kind: 'installed' })} disabled={!installed.loaded}><Icon name="download" size={16} /> Export list</button>
  <button class="btn btn-quiet" onclick={() => myBundles.pickImport()}><Icon name="upload" size={16} /> Import list</button>
</div>

{#each installed.sourceErrors as e (e.source)}
  <div class="gap"><Banner text="Some installed apps couldn't be read. The list may be incomplete." action={{ label: 'Try again', run: () => installed.refresh() }} /></div>
{/each}

{#if !installed.loaded}
  <Skeleton rows={7} />
{:else if shown.length === 0}
  <EmptyState icon="search" title="No apps match" body={filter ? `Nothing installed matches "${filter}".` : 'No apps found.'} />
{:else}
  <div class="card list">
    <div class="list-head">
      <Checkbox label="Select all apps" checked={allPicked} indeterminate={picked.length > 0 && !allPicked}
        disabled={pickable.length === 0} onchange={(v) => toggleAll(v)} />
      <span class="muted">{picked.length > 0 ? `${picked.length} selected` : 'Select apps to remove them or save them to a bundle'}</span>
    </div>
    {#each shown as p, i (keys[i])}
      {@const job = jobOf(p)}
      <AppRow name={p.name} meta="{p.version} · {sourceText(p.source)}" {job} mode="uninstall" iconKey="{p.source ?? 'local'}:{p.id}">
        {#snippet lead()}
          <Checkbox label="Select {p.name}" checked={selected.has(keys[i])} disabled={!selectable(p)}
            onchange={(v) => (v ? selected.add(keys[i]) : selected.delete(keys[i]))} />
        {/snippet}
        {#snippet action()}
          {#if p.source}
            <JobButton {job} mode="uninstall" name={p.name}
              onstart={() => queue.uninstall({ source: p.source!, id: p.id, name: p.name })}
              oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
          {/if}
        {/snippet}
      </AppRow>
    {/each}
  </div>
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
  .head { display: flex; align-items: flex-end; justify-content: space-between; gap: 24px; margin-bottom: 14px; }
  .head p { margin-top: 4px; }
  .filter { width: min(320px, 40%); }
  .gap { margin-bottom: 12px; }
  .toolbar { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; margin-bottom: 12px; min-height: 36px; }
  .toolbar .btn { gap: 6px; }
  .count { font-weight: 600; font-size: 13px; margin-right: 6px; }
  .spacer { flex: 1; }
  .danger { color: var(--danger); }
  .list-head { display: flex; align-items: center; gap: 14px; padding: 10px 16px; border-bottom: 1px solid var(--hairline); font-size: 12.5px; }
  .names { margin: 12px 0 0; padding: 10px 14px 10px 30px; max-height: 40vh; overflow-y: auto; background: var(--pill); border-radius: 10px; font-size: 13px; }
  .names li + li { margin-top: 3px; }
</style>
