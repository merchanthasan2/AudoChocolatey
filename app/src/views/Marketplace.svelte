<script lang="ts">
  import { search, queue, installed, overview } from '../lib/stores/app.svelte';
  import { POPULAR } from '../lib/catalog/popular';
  import SearchField from '../lib/components/SearchField.svelte';
  import AppRow from '../lib/components/AppRow.svelte';
  import AppIcon from '../lib/components/AppIcon.svelte';
  import JobButton from '../lib/components/JobButton.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import { rise } from '../lib/motion';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';

  let { input = $bindable() }: { input?: HTMLInputElement } = $props();

  const installedIds = $derived(new Set(installed.items.filter((p) => p.source).map((p) => `${p.source}:${p.id.toLowerCase()}`)));
  const isInstalled = (source: string, id: string) => installedIds.has(`${source}:${id.toLowerCase()}`);
  const keys = $derived(uniqueKeys(search.results, (p) => `${p.source}:${p.id}`));
  const searching = $derived(search.query.trim().length >= 2);
  const sourceText = (s: string) => (s === 'winget' ? 'winget' : 'Chocolatey');
</script>

<header class="head">
  <h1>Find apps</h1>
  <p class="muted">Search winget and Chocolatey together. Everything installs quietly in the background.</p>
  <div class="search"><SearchField value={search.query} oninput={(v) => search.set(v)} bind:input /></div>
</header>

{#if searching}
  {#each search.sourceErrors as e (e.source)}
    <div class="banner-gap">
      <Banner text={e.code === 'network' ? "Couldn't reach the Chocolatey catalog. Showing winget results." : `${sourceText(e.source)} search didn't finish: ${e.message}`} icon={e.code === 'network' ? 'offline' : 'alert'} />
    </div>
  {/each}
  {#if search.loading && search.results.length === 0}
    <Skeleton rows={6} />
  {:else if search.error}
    <EmptyState icon="alert" title="Search didn't work" body={search.error} />
  {:else if search.results.length === 0 && search.searched}
    <EmptyState icon="search" title="No matches" body={`Nothing found for "${search.searched}". Try a shorter name.`} />
  {:else}
    <div class="card list" class:dim={search.loading}>
      {#each search.results as p, i (keys[i])}
        {@const done = isInstalled(p.source, p.id)}
        {@const needsChoco = p.source === 'choco' && !overview.chocoReady}
        <div in:rise={{ delay: Math.min(i, 8) * 50 }}>
          <AppRow name={p.name} meta={[p.version, sourceText(p.source), needsChoco ? 'Requires Chocolatey' : '', p.description ?? ''].filter(Boolean).join(' · ')}
            job={queue.jobFor(p.source, p.id, 'install')} mode="install">
            {#snippet action()}
              {#if done}
                <span class="badge">Installed</span>
              {:else if needsChoco}
                <span class="badge" title="Install Chocolatey from Settings to use these apps">Needs Chocolatey</span>
              {:else}
                <JobButton job={queue.jobFor(p.source, p.id, 'install')} mode="install" name={p.name}
                  onstart={() => queue.install({ source: p.source, id: p.id, name: p.name })}
                  oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
              {/if}
            {/snippet}
          </AppRow>
        </div>
      {/each}
    </div>
  {/if}
{:else}
  <section>
    <div class="section-head"><h2>Popular apps</h2><span class="muted">{POPULAR.length} picks</span></div>
    <div class="grid">
      {#each POPULAR as app, i (app.id)}
        {@const job = queue.jobFor('winget', app.id, 'install')}
        <article class="tile card" in:rise={{ delay: Math.min(i, 12) * 30, y: 8 }}>
          <div class="tile-top">
            <AppIcon name={app.name} size={48} />
            {#if isInstalled('winget', app.id)}
              <span class="badge">Installed</span>
            {:else}
              <JobButton {job} mode="install" name={app.name}
                onstart={() => queue.install({ source: 'winget', id: app.id, name: app.name })}
                oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
            {/if}
          </div>
          <h3 class="truncate">{app.name}</h3>
          <p class="pub truncate">{app.publisher} · {app.category}</p>
          <p class="blurb">{app.blurb}</p>
        </article>
      {/each}
    </div>
  </section>
{/if}

<style>
  .head { display: grid; gap: 6px; margin-bottom: 26px; }
  .search { margin-top: 12px; max-width: 640px; }
  .banner-gap { margin-bottom: 12px; }
  .list.dim { opacity: 0.6; transition: opacity var(--dur-quick) ease; }
  .section-head { display: flex; align-items: baseline; gap: 10px; margin-bottom: 14px; }
  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 14px; }
  .tile { padding: 16px; display: grid; gap: 4px; align-content: start; transition: transform var(--dur-quick) var(--ease-out), box-shadow var(--dur-quick) ease; }
  @media (hover: hover) and (pointer: fine) {
    .tile:hover { box-shadow: var(--shadow-raised); transform: translateY(-1px); }
  }
  .tile-top { display: flex; align-items: flex-start; justify-content: space-between; margin-bottom: 10px; }
  h3 { font-family: var(--font-display); font-size: 15px; font-weight: 650; letter-spacing: -0.02em; margin: 0; }
  .pub { font-size: 12px; color: var(--muted); }
  .blurb { font-size: 12.5px; color: var(--ink-2); margin-top: 4px; }
</style>
