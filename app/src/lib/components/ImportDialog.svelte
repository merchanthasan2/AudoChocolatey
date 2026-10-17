<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';
  import { myBundles, installed, overview } from '../stores/app.svelte';
  import { installedSet } from '../logic/entries';
  import { importRows } from '../logic/importRows';
  import Dialog from './Dialog.svelte';
  import Checkbox from './Checkbox.svelte';
  import AppIcon from './AppIcon.svelte';

  const p = $derived(myBundles.importing);
  const rows = $derived(p ? importRows(p, installedSet(installed.items), overview.on) : []);
  const picked = new SvelteSet<number>();
  // Fresh ticks for each opened file.
  let token = '';
  $effect(() => {
    if (!p || p.token === token) return;
    token = p.token;
    picked.clear();
    for (const r of rows) if (r.ticked) picked.add(r.index);
  });
  let name = $state('');
  const chosen = $derived(rows.filter((r) => picked.has(r.index)).map((r) => r.index));
  const note = { installed: 'Already installed', off: 'Its source is turned off in Settings', ready: '' };
</script>

{#if p}
  <Dialog title={p.name ? `Import "${p.name}"` : 'Import an app list'} wide onclose={() => (myBundles.importing = null)}>
    <p class="muted intro">
      Pick the apps to install. Apps we don't recognise show their package name and start unticked; only tick them if you know them.
      {#if p.skipped > 0}{p.skipped} {p.skipped === 1 ? 'entry was' : 'entries were'} left out because {p.skipped === 1 ? 'it' : 'they'} couldn't be read.{/if}
    </p>
    <div class="rows">
      {#each rows as r (r.index)}
        <div class="row">
          <Checkbox label="Install {r.entry.name}" checked={picked.has(r.index)} disabled={r.state !== 'ready'}
            onchange={(v) => (v ? picked.add(r.index) : picked.delete(r.index))} />
          <AppIcon name={r.entry.name} size={28} iconKey={r.entry.iconKey} />
          <div class="text">
            <div class="name">{r.entry.name}</div>
            <div class="meta">
              {#if !r.entry.curated}<span class="id">{r.entry.winget ?? r.entry.choco ?? r.entry.msstore}</span> · Not checked by us{/if}
              {#if r.state !== 'ready'}{r.entry.curated ? '' : ' · '}{note[r.state]}{/if}
            </div>
          </div>
        </div>
      {/each}
    </div>
    <label class="keep">
      <span>Save as a bundle named</span>
      <input bind:value={name} placeholder={p.name ?? 'Imported apps'} maxlength="60" />
    </label>
    {#snippet buttons()}
      <button class="btn btn-quiet" onclick={() => (myBundles.importing = null)}>Cancel</button>
      <button class="btn btn-quiet" disabled={chosen.length === 0} onclick={() => myBundles.keepImport(chosen, name)}>Save as bundle</button>
      <button class="btn btn-primary" disabled={chosen.length === 0} onclick={() => myBundles.installImport(chosen)}>
        Install {chosen.length} {chosen.length === 1 ? 'app' : 'apps'}
      </button>
    {/snippet}
  </Dialog>
{/if}

<style>
  .intro { font-size: 13px; margin-bottom: 12px; }
  .rows { display: grid; gap: 2px; max-height: 46vh; overflow-y: auto; border: 1px solid var(--hairline); border-radius: 10px; padding: 4px; }
  .row { display: flex; align-items: center; gap: 12px; padding: 8px 10px; border-radius: 8px; }
  .row:hover { background: var(--pill); }
  .text { min-width: 0; }
  .name { font-weight: 600; }
  .meta { font-size: 12px; color: var(--muted); }
  .id { font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace; color: var(--ink-2); }
  .keep { display: flex; align-items: center; gap: 10px; margin-top: 14px; font-size: 13px; color: var(--muted); }
  .keep input { flex: 1; height: 32px; padding: 0 10px; border-radius: 8px; border: 1px solid var(--hairline-strong); background: var(--card); color: var(--ink); font: inherit; }
</style>
