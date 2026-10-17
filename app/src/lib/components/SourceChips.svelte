<script lang="ts">
  import type { SourceLabel } from '../ipc/bindings/SourceLabel';
  import { LABEL_ORDER, SOURCE_LABELS } from '../logic/sources';
  // Where apps come from, with counts. One choice at a time; empty sources are left out unless
  // chosen, so the row never offers a filter that shows nothing.
  let { value, counts, onchange }:
    { value: SourceLabel | 'all'; counts: Record<SourceLabel | 'all', number>; onchange: (v: SourceLabel | 'all') => void } = $props();
  const shown = $derived(LABEL_ORDER.filter((l) => counts[l] > 0 || value === l));
</script>

<div class="chips" role="radiogroup" aria-label="Where apps come from">
  <button role="radio" aria-checked={value === 'all'} class:on={value === 'all'} onclick={() => onchange('all')}>
    All <span class="n">{counts.all}</span>
  </button>
  {#each shown as l (l)}
    <button role="radio" aria-checked={value === l} class:on={value === l} onclick={() => onchange(l)}>
      {SOURCE_LABELS[l]} <span class="n">{counts[l]}</span>
    </button>
  {/each}
</div>

<style>
  .chips { display: flex; flex-wrap: wrap; gap: 6px; }
  button {
    height: 30px; padding: 0 12px; border-radius: 999px; font-size: 12.5px; font-weight: 600;
    color: var(--ink-2); background: var(--card); box-shadow: inset 0 0 0 1px var(--hairline-strong);
    display: inline-flex; align-items: center; gap: 6px; white-space: nowrap;
    transition: background-color var(--dur-quick) ease, color var(--dur-quick) ease;
  }
  button:hover { color: var(--ink); }
  button.on { background: var(--ink); color: var(--canvas); box-shadow: none; }
  .n { font-weight: 500; opacity: 0.7; font-variant-numeric: tabular-nums; }
</style>
