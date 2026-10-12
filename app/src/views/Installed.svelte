<script lang="ts">
  import { installed, queue } from '../lib/stores/app.svelte';
  import AppRow from '../lib/components/AppRow.svelte';
  import JobButton from '../lib/components/JobButton.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import SearchField from '../lib/components/SearchField.svelte';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';

  let filter = $state('');
  const shown = $derived.by(() => {
    const f = filter.trim().toLowerCase();
    const list = f ? installed.items.filter((p) => p.name.toLowerCase().includes(f) || p.id.toLowerCase().includes(f)) : installed.items;
    return [...list].sort((a, b) => a.name.localeCompare(b.name));
  });
  const keys = $derived(uniqueKeys(shown, (p) => `${p.source ?? 'local'}:${p.id}`));
  const managed = $derived(installed.items.filter((p) => p.source).length);
  const sourceText = (s: string | null) => (s === 'winget' ? 'winget' : s === 'choco' ? 'Chocolatey' : 'Managed by Windows');
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

{#each installed.sourceErrors as e (e.source)}
  <div class="gap"><Banner text={`Couldn't read ${sourceText(e.source)} apps: ${e.message}`} action={{ label: 'Try again', run: () => installed.refresh() }} /></div>
{/each}

{#if !installed.loaded}
  <Skeleton rows={7} />
{:else if shown.length === 0}
  <EmptyState icon="search" title="No apps match" body={filter ? `Nothing installed matches "${filter}".` : 'No apps found.'} />
{:else}
  <div class="card list">
    {#each shown as p, i (keys[i])}
      {@const job = p.source ? queue.jobFor(p.source, p.id, 'uninstall') : undefined}
      <AppRow name={p.name} meta="{p.version} · {sourceText(p.source)}" {job} mode="uninstall">
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

<style>
  .head { display: flex; align-items: flex-end; justify-content: space-between; gap: 24px; margin-bottom: 22px; }
  .head p { margin-top: 4px; }
  .filter { width: min(320px, 40%); }
  .gap { margin-bottom: 12px; }
</style>
