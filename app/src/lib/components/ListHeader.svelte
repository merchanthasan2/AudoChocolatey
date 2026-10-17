<script lang="ts" module>
  import type { ListSort } from '../ipc/bindings/ListSort';
  /** One column: `sort` makes it clickable; `cls` sets its width and when it hides. */
  export type Column = { label: string; sort?: ListSort; cls: string };
</script>

<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { SortDir } from '../ipc/bindings/SortDir';
  import Icon from './Icon.svelte';
  let { columns, sort, dir, onsort, lead }:
    { columns: Column[]; sort: ListSort; dir: SortDir; onsort: (s: ListSort) => void; lead?: Snippet } = $props();
</script>

<div class="list-header" role="row">
  {#if lead}<span class="lead">{@render lead()}</span>{/if}
  {#each columns as c (c.label)}
    <span class="cell {c.cls}" role="columnheader" aria-sort={c.sort && c.sort === sort ? (dir === 'asc' ? 'ascending' : 'descending') : undefined}>
      {#if c.sort}
        <button class:on={c.sort === sort} onclick={() => onsort(c.sort!)}>
          {c.label}
          {#if c.sort === sort && c.sort !== 'suggested'}
            <span class="arrow" class:down={dir === 'desc'}><Icon name="chevron" size={12} stroke={2.5} /></span>
          {/if}
        </button>
      {:else}{c.label}{/if}
    </span>
  {/each}
</div>

<style>
  .list-header {
    display: flex; align-items: center; gap: 14px; padding: 9px 16px;
    border-bottom: 1px solid var(--hairline); font-size: 12px; color: var(--muted); font-weight: 600;
  }
  .lead { display: inline-flex; }
  button { display: inline-flex; align-items: center; gap: 4px; color: inherit; font: inherit; padding: 2px 0; }
  button:hover, button.on { color: var(--ink); }
  .arrow { display: inline-grid; transform: rotate(-90deg); transition: transform var(--dur-quick) var(--ease-out); }
  .arrow.down { transform: rotate(90deg); }
</style>
