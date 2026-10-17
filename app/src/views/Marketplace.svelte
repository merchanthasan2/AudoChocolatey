<script lang="ts">
  import { onMount } from 'svelte';
  import { search, market, myBundles } from '../lib/stores/app.svelte';
  import SearchField from '../lib/components/SearchField.svelte';
  import EntryRow from '../lib/components/EntryRow.svelte';
  import AppCard from '../lib/components/AppCard.svelte';
  import BundleCard from '../lib/components/BundleCard.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import Icon from '../lib/components/Icon.svelte';
  import { rise } from '../lib/motion';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';

  let { input = $bindable() }: { input?: HTMLInputElement } = $props();
  onMount(() => market.load());

  const keys = $derived(uniqueKeys(search.results, (e) => e.key));
  const searching = $derived(search.query.trim().length >= 2);
  let moreShown = $state(30);
  $effect(() => { void market.category; moreShown = 30; });
  const categoryName = $derived(market.home?.categories.find((c) => c.id === market.category)?.name ?? '');
</script>

<header class="head">
  <h1>Find apps</h1>
  <p class="muted">Thousands of apps, installed quietly in the background.</p>
  <div class="search"><SearchField value={search.query} oninput={(v) => search.set(v)} bind:input /></div>
</header>

{#if searching}
  {#if search.sourceErrors.length > 0}
    {@const offline = search.sourceErrors.some((e) => e.code === 'network')}
    <div class="banner-gap">
      <Banner text={offline ? "Part of the catalog couldn't be reached. Showing what we found." : "Part of the search didn't finish. Showing what we found."} icon={offline ? 'offline' : 'alert'} />
    </div>
  {/if}
  {#if search.loading && search.results.length === 0}
    <Skeleton rows={6} />
  {:else if search.error}
    <EmptyState icon="alert" title="Search didn't work" body={search.error} />
  {:else if search.results.length === 0 && search.searched}
    <EmptyState icon="search" title="No matches" body={`Nothing found for "${search.searched}". Try a shorter name.`} />
  {:else}
    <div class="card list" class:dim={search.loading}>
      {#each search.results as e, i (keys[i])}
        <div in:rise={{ delay: Math.min(i, 8) * 50 }}><EntryRow {e} /></div>
      {/each}
    </div>
  {/if}
{:else if market.category}
  <div class="section-head">
    <button class="back" onclick={() => market.openCategory(null)}><span class="flip"><Icon name="chevron" size={16} /></span> All apps</button>
    <h2>{categoryName}</h2>
  </div>
  <div class="grid">
    {#each market.categoryApps as app, i (app.key)}
      <div in:rise={{ delay: Math.min(i, 12) * 30, y: 8 }}><AppCard {app} /></div>
    {/each}
  </div>
  {#if market.moreLoading || market.more.length > 0}
    <section class="more">
      <div class="section-head"><h2>More apps</h2><span class="muted">Found by tag. Not checked by us, so look at the details before installing.</span></div>
      {#if market.moreLoading}
        <Skeleton rows={4} />
      {:else}
        <div class="card list">
          {#each market.more.slice(0, moreShown) as e (e.key)}<EntryRow {e} />{/each}
        </div>
        {#if market.more.length > moreShown}
          <button class="btn btn-quiet show-more" onclick={() => (moreShown += 30)}>Show more ({market.more.length - moreShown} left)</button>
        {/if}
      {/if}
    </section>
  {/if}
{:else if market.home}
  <nav class="chips" aria-label="Categories">
    {#each market.home.categories as c (c.id)}
      <button class="chip" onclick={() => market.openCategory(c.id)}>{c.name}<span class="n">{c.count}</span></button>
    {/each}
  </nav>

  <section>
    <div class="section-head"><h2>Featured</h2></div>
    <div class="grid">
      {#each market.home.featured as app, i (app.key)}
        <div in:rise={{ delay: Math.min(i, 12) * 30, y: 8 }}><AppCard {app} /></div>
      {/each}
    </div>
  </section>

  <section>
    <div class="section-head">
      <h2>My bundles</h2><span class="muted">Your own app lists, to set up a PC the way you like</span>
      <button class="btn btn-quiet import" onclick={() => myBundles.pickImport()}><Icon name="upload" size={16} /> Import a list</button>
    </div>
    {#if myBundles.list.length === 0}
      <p class="muted empty-mine">Select apps on the Installed or Updates page and choose "Add to bundle", or import a list you exported on another PC.</p>
    {:else}
      <div class="bundles">
        {#each myBundles.list as b (b.id)}<BundleCard bundle={b} mine />{/each}
      </div>
    {/if}
  </section>

  <section>
    <div class="section-head"><h2>Bundles</h2><span class="muted">Set up a PC in one go</span></div>
    <div class="bundles">
      {#each market.home.bundles as b (b.id)}<BundleCard bundle={b} />{/each}
    </div>
  </section>
{:else}
  <Skeleton rows={4} />
{/if}

<style>
  .head { display: grid; gap: 6px; margin-bottom: 22px; }
  .search { margin-top: 12px; max-width: 640px; }
  .banner-gap { margin-bottom: 12px; }
  .list.dim { opacity: 0.6; transition: opacity var(--dur-quick) ease; }
  .chips { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 26px; }
  .chip {
    display: inline-flex; align-items: center; gap: 8px; height: 34px; padding: 0 14px; border-radius: 999px;
    background: var(--card); box-shadow: var(--shadow-card); font-weight: 600; font-size: 13px; color: var(--ink-2);
    transition: transform var(--dur-press) var(--ease-out), background-color var(--dur-quick) ease;
  }
  .chip:active { transform: scale(0.97); }
  .chip:hover { background: var(--card-hover); color: var(--ink); }
  .n { font-size: 11px; color: var(--faint); }
  section { margin-bottom: 32px; }
  .more .section-head { flex-wrap: wrap; }
  .show-more { margin-top: 12px; }
  .section-head { display: flex; align-items: baseline; gap: 10px; margin-bottom: 14px; }
  .back { display: inline-flex; align-items: center; gap: 4px; font-weight: 600; color: var(--muted); margin-right: 8px; }
  .back:hover { color: var(--ink); }
  .flip { display: inline-grid; transform: rotate(180deg); }
  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 14px; margin-bottom: 32px; }
  .import { margin-left: auto; gap: 6px; }
  .empty-mine { font-size: 13px; padding: 4px 2px 8px; }
  .bundles { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 14px; }
</style>
