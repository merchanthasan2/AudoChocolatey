<script lang="ts">
  let { checked, indeterminate = false, onchange, label, disabled = false }:
    { checked: boolean; indeterminate?: boolean; onchange: (v: boolean) => void; label: string; disabled?: boolean } = $props();
</script>

<button class="cb" role="checkbox" aria-checked={indeterminate ? 'mixed' : checked} aria-label={label}
  class:on={checked || indeterminate} {disabled} onclick={() => onchange(!checked)}>
  {#if indeterminate}
    <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 8h8" /></svg>
  {:else if checked}
    <svg viewBox="0 0 16 16" aria-hidden="true"><path class="tick" d="M3.5 8.5l3 3 6-7" /></svg>
  {/if}
</button>

<style>
  .cb {
    width: 20px; height: 20px; flex: none; border-radius: 6px;
    display: grid; place-items: center;
    box-shadow: inset 0 0 0 1.5px var(--hairline-strong);
    background: var(--card);
    transition: background-color 140ms ease, box-shadow 140ms ease, transform var(--dur-press) var(--ease-out);
  }
  .cb:active:not(:disabled) { transform: scale(0.9); }
  .cb:disabled { opacity: 0.35; cursor: default; }
  .cb.on { background: var(--accent); box-shadow: none; }
  @media (hover: hover) and (pointer: fine) {
    .cb:not(.on):hover:not(:disabled) { box-shadow: inset 0 0 0 1.5px var(--accent); }
  }
  svg { width: 14px; height: 14px; }
  path { fill: none; stroke: var(--accent-ink); stroke-width: 2.2; stroke-linecap: round; stroke-linejoin: round; }
  .tick { stroke-dasharray: 14; stroke-dashoffset: 14; animation: draw 220ms var(--ease-out) forwards; }
  @keyframes draw { to { stroke-dashoffset: 0; } }
  :global(:root[data-motion='reduced']) .tick { animation: none; stroke-dashoffset: 0; }
</style>
