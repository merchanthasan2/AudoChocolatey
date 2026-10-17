<script lang="ts" generics="T">
  import type { Snippet } from 'svelte';
  import type { ListView } from '../ipc/bindings/ListView';
  import Skeleton from './Skeleton.svelte';
  import EmptyState from './EmptyState.svelte';

  // The frame shared by Updates and Installed: loading, error and empty states, then rows (list
  // and compact, under a sortable header) or tiles (grid). Pages keep their own rows and actions.
  let {
    items, keyOf, view, loaded, error, filtered, onclear, empty, header, row, tile, actW = 140,
  }: {
    items: T[];
    keyOf: (t: T, i: number) => string;
    view: ListView;
    loaded: boolean;
    error: string | null;
    /** Search or a filter is active. */
    filtered: boolean;
    onclear: () => void;
    empty: { icon: string; title: string; body: string; tone?: 'good' };
    header: Snippet;
    row: Snippet<[T, number]>;
    tile: Snippet<[T, number]>;
    /** Width of the action column, so header and rows line up. */
    actW?: number;
  } = $props();
</script>

{#if !loaded}
  <Skeleton rows={view === 'compact' ? 9 : 6} />
{:else if items.length === 0 && error}
  <EmptyState icon="alert" title="Couldn't read your apps" body={error} />
{:else if items.length === 0 && filtered}
  <div class="none">
    <EmptyState icon="search" title="No apps match" body="Try other words, or show every source." />
    <button class="btn btn-quiet" onclick={onclear}>Clear filters</button>
  </div>
{:else if items.length === 0}
  <EmptyState icon={empty.icon} tone={empty.tone} title={empty.title} body={empty.body} />
{:else if view === 'grid'}
  <div class="grid">
    {#each items as t, i (keyOf(t, i))}{@render tile(t, i)}{/each}
  </div>
{:else}
  <div class="card list applist" style="--act-w:{actW}px" role="table">
    {@render header()}
    {#each items as t, i (keyOf(t, i))}{@render row(t, i)}{/each}
  </div>
{/if}

<style>
  .none { display: grid; justify-items: center; gap: 4px; }
  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 12px; }
  .applist { container-type: inline-size; }
  /* Columns shared by the header and the rows. */
  .applist :global(.cell) { flex-shrink: 0; font-size: 12.5px; color: var(--ink-2); font-variant-numeric: tabular-nums; }
  .applist :global(.c-app) { flex: 1; min-width: 0; }
  .applist :global(.c-ver) { width: 104px; }
  .applist :global(.c-new) { width: 124px; }
  .applist :global(.c-src) { width: 136px; }
  .applist :global(.c-date) { width: 92px; }
  .applist :global(.c-act) { width: var(--act-w); }
  .applist :global(.action) { min-width: var(--act-w); }
  /* The header's App column also covers the icon in each row. */
  .applist :global(.list-header .c-app) { padding-left: 0; }
  @container (max-width: 760px) { .applist :global(.c-date) { display: none; } }
  @container (max-width: 640px) { .applist :global(.c-src) { display: none; } }
</style>
