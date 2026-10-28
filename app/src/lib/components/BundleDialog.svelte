<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';
  import type { AppEntry, BundleView, MyBundleView } from '../ipc/commands';
  import { installed, updates, queue, overview, myBundles, toasts } from '../stores/app.svelte';
  import { installedSet, bestId, withJobs } from '../logic/entries';
  import { bundleRows } from '../logic/bundleView';
  import Dialog from './Dialog.svelte';
  import Checkbox from './Checkbox.svelte';
  import AppIcon from './AppIcon.svelte';

  let { bundle, mine = false, onclose }: { bundle: BundleView | MyBundleView; mine?: boolean; onclose: () => void } = $props();

  const blurb = $derived('blurb' in bundle ? bundle.blurb : 'Your own list');
  const rows = $derived(bundleRows(
    bundle.apps,
    withJobs(installedSet(installed.items), queue.jobs),
    updates.items,
    overview.on,
  ));
  const picked = new SvelteSet<string>();
  const decided = new SvelteSet<string>();
  // Tick each app once it can be chosen. An untick stays unticked, and an app whose source
  // is still loading gets ticked when that source comes online.
  $effect(() => {
    for (const r of rows) {
      if (r.state === 'off' || decided.has(r.entry.key)) continue;
      decided.add(r.entry.key);
      picked.add(r.entry.key);
    }
  });

  const chosen = $derived(rows.filter((r) => picked.has(r.entry.key)));
  const toInstall = $derived(chosen.filter((r) => r.state === 'missing').map((r) => r.entry));
  const toUpdate = $derived(chosen.flatMap((r) => (r.update ? [r.update] : [])));
  const dropped = $derived(mine ? bundle.apps.filter((a) => !picked.has(a.key)) : []);
  const choosable = $derived(rows.filter((r) => r.state !== 'off'));
  const allOn = $derived(choosable.length > 0 && choosable.every((r) => picked.has(r.entry.key)));
  const someOn = $derived(choosable.some((r) => picked.has(r.entry.key)));

  const note: Record<string, string> = {
    off: 'Its source is turned off in Settings',
    missing: 'Not installed',
    update: 'Update available',
    installed: 'Installed',
  };
  const label = (a: AppEntry) => (a.curated ? a.name : `${a.name} (${a.winget ?? a.choco ?? a.msstore})`);

  function toggleAll(on: boolean) {
    picked.clear();
    if (on) for (const r of choosable) picked.add(r.entry.key);
  }

  async function install() {
    const n = await queue.installMany(toInstall);
    if (n > 0) toasts.push({ text: `Installing ${n} ${n === 1 ? 'app' : 'apps'} from ${bundle.name}`, tone: 'ok' });
  }
  async function update() {
    await queue.updateMany(toUpdate);
  }
  async function saveMine() {
    const drop = dropped.slice();
    let n = 0;
    for (const a of drop) if (await myBundles.removeApp(bundle.id, a.key)) n += 1;
    if (n > 0) toasts.push({ text: `Removed ${n} from ${bundle.name}`, tone: 'ok' });
  }
  async function saveCopy() {
    const apps = chosen.flatMap((r) => {
      const id = bestId(r.entry);
      return id ? [{ source: r.entry.best, id, name: r.entry.name }] : [];
    });
    if (apps.length === 0) return;
    if (await myBundles.add(null, bundle.name, apps)) onclose();
  }
</script>

<Dialog title={bundle.name} wide {onclose}>
  <p class="muted intro">{blurb}. Untick anything you don't want. Install adds what's missing. Update brings the rest up to date.</p>
  <div class="all">
    <Checkbox label="Select all apps" checked={allOn} indeterminate={!allOn && someOn} disabled={choosable.length === 0} onchange={toggleAll} />
    <span>{chosen.length} of {rows.length} selected</span>
  </div>
  <div class="rows">
    {#each rows as r (r.entry.key)}
      <div class="row">
        <Checkbox label="Include {r.entry.name}" checked={picked.has(r.entry.key)} disabled={r.state === 'off'}
          onchange={(v) => (v ? picked.add(r.entry.key) : picked.delete(r.entry.key))} />
        <AppIcon name={r.entry.name} size={28} iconKey={r.entry.iconKey} />
        <div class="text">
          <div class="name">{label(r.entry)}</div>
          <div class="meta">{note[r.state]}</div>
        </div>
      </div>
    {/each}
  </div>
  {#snippet buttons()}
    <button class="btn btn-quiet" onclick={onclose}>Close</button>
    {#if mine}
      <button class="btn btn-quiet" disabled={dropped.length === 0 || dropped.length === bundle.apps.length} onclick={saveMine}>
        Remove {dropped.length} from this bundle
      </button>
    {:else}
      <button class="btn btn-quiet" disabled={chosen.length === 0} onclick={saveCopy}>Save as my bundle</button>
    {/if}
    <button class="btn" disabled={toUpdate.length === 0} onclick={update}>Update {toUpdate.length}</button>
    <button class="btn btn-primary" disabled={toInstall.length === 0} onclick={install}>
      Install {toInstall.length}
    </button>
  {/snippet}
</Dialog>

<style>
  .intro { font-size: 13px; margin-bottom: 12px; }
  .all { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; font-size: 13px; font-weight: 600; color: var(--ink-2); }
  .rows { display: grid; gap: 2px; max-height: 46vh; overflow-y: auto; border: 1px solid var(--hairline); border-radius: 10px; padding: 4px; }
  .row { display: flex; align-items: center; gap: 12px; padding: 8px 10px; border-radius: 8px; }
  .row:hover { background: var(--pill); }
  .text { min-width: 0; }
  .name { font-weight: 600; }
  .meta { font-size: 12px; color: var(--muted); }
</style>
