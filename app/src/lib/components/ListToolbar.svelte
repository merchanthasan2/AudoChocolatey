<script lang="ts" module>
  export type ToolAction = { key: string; label: string; icon?: string; run: () => void; disabled?: boolean; danger?: boolean };
</script>

<script lang="ts">
  import type { Snippet } from 'svelte';
  import Icon from './Icon.svelte';
  import MenuButton from './MenuButton.svelte';
  import { fitActions } from '../logic/toolbarFit';

  // Page actions. Those that don't fit the width move into a "More" menu, in order.
  let { actions, primary }: { actions: ToolAction[]; primary?: Snippet } = $props();
  let bar = $state<HTMLElement>();
  let measure = $state<HTMLElement>();
  let primaryBox = $state<HTMLElement>();
  let fit = $state(99);
  const GAP = 6, MORE_W = 44;

  function refit() {
    if (!bar || !measure) return;
    const widths = [...measure.children].map((c) => (c as HTMLElement).offsetWidth);
    const room = bar.clientWidth - (primaryBox?.offsetWidth ?? 0) - (primary ? GAP * 2 : 0);
    fit = fitActions(widths, room, MORE_W, GAP);
  }
  $effect(() => {
    void actions.map((a) => a.label + a.disabled).join();
    if (!bar) return;
    const ro = new ResizeObserver(() => refit());
    ro.observe(bar);
    refit();
    return () => ro.disconnect();
  });
  const visible = $derived(actions.slice(0, fit));
  const rest = $derived(actions.slice(fit));
</script>

<div class="toolbar" bind:this={bar}>
  <div class="measure" aria-hidden="true" bind:this={measure}>
    {#each actions as a (a.key)}<span class="btn btn-quiet">{#if a.icon}<Icon name={a.icon} size={16} />{/if} {a.label}</span>{/each}
  </div>
  {#each visible as a (a.key)}
    <button class="btn btn-quiet" class:danger={a.danger} disabled={a.disabled} onclick={a.run}>
      {#if a.icon}<Icon name={a.icon} size={16} />{/if} {a.label}
    </button>
  {/each}
  {#if rest.length > 0}
    <MenuButton label="More actions" align="left" triggerClass="btn btn-quiet more"
      items={rest.map((a) => ({ text: a.label, run: a.run, disabled: a.disabled, danger: a.danger }))}>
      {#snippet trigger()}<Icon name="more" size={18} stroke={3} />{/snippet}
    </MenuButton>
  {/if}
  {#if primary}<span class="spacer"></span><span class="primary" bind:this={primaryBox}>{@render primary()}</span>{/if}
</div>

<style>
  .toolbar { position: relative; display: flex; align-items: center; gap: 6px; min-height: 38px; min-width: 0; }
  .toolbar :global(.btn) { gap: 6px; white-space: nowrap; flex-shrink: 0; }
  .measure { position: absolute; visibility: hidden; pointer-events: none; display: flex; gap: 6px; left: 0; top: 0; }
  .spacer { flex: 1; }
  .primary { display: inline-flex; gap: 6px; flex-shrink: 0; }
  .danger { color: var(--danger); }
  :global(.btn.more) { width: 38px; padding: 0; justify-content: center; }
</style>
