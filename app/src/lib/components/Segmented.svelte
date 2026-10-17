<script lang="ts" generics="T extends string">
  let { value, options, onchange, label }:
    { value: T; options: { value: T; label: string }[]; onchange: (v: T) => void; label: string } = $props();
</script>

<div class="seg" role="radiogroup" aria-label={label}>
  {#each options as o (o.value)}
    <button role="radio" aria-checked={value === o.value} class:on={value === o.value} onclick={() => onchange(o.value)}>{o.label}</button>
  {/each}
</div>

<style>
  .seg { display: inline-flex; flex-shrink: 0; padding: 3px; border-radius: 10px; background: var(--pill); gap: 2px; }
  button { height: 28px; padding: 0 14px; white-space: nowrap; border-radius: 8px; font-weight: 600; font-size: 12.5px; color: var(--muted); transition: background-color var(--dur-quick) ease, color var(--dur-quick) ease; }
  button.on { background: var(--card); color: var(--ink); box-shadow: var(--shadow-card); }
  button:not(.on):hover { color: var(--ink); }
</style>
