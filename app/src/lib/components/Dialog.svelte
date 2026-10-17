<script lang="ts">
  import type { Snippet } from 'svelte';
  import { fade, scale } from 'svelte/transition';

  // One modal shell: scrim, centred card, Escape closes, focus moves inside on open.
  let { title, onclose, children, buttons, wide = false }:
    { title: string; onclose: () => void; children: Snippet; buttons?: Snippet; wide?: boolean } = $props();
  let box = $state<HTMLElement>();
  const id = `dlg-${Math.random().toString(36).slice(2, 8)}`;
  $effect(() => { box?.querySelector<HTMLElement>('input, button.btn-primary, button')?.focus(); });
</script>

<svelte:window onkeydown={(e) => { if (e.key === 'Escape') onclose(); }} />

<div class="scrim" transition:fade={{ duration: 160 }} onpointerdown={onclose} role="presentation"></div>
<div class="dialog card" class:wide role="dialog" aria-modal="true" aria-labelledby={id} bind:this={box}
  transition:scale={{ start: 0.96, duration: 180 }}>
  <h2 {id}>{title}</h2>
  <div class="body">{@render children()}</div>
  {#if buttons}<div class="buttons">{@render buttons()}</div>{/if}
</div>

<style>
  .scrim { position: fixed; inset: 0; background: var(--scrim); z-index: 60; }
  .dialog {
    position: fixed; z-index: 61; left: 50%; top: 50%; translate: -50% -50%;
    width: min(440px, calc(100vw - 32px)); max-height: calc(100vh - 64px);
    display: flex; flex-direction: column; padding: 22px 22px 18px;
  }
  .dialog.wide { width: min(600px, calc(100vw - 32px)); }
  h2 { font-size: 17px; margin: 0 0 10px; }
  .body { overflow-y: auto; min-height: 0; }
  .buttons { display: flex; justify-content: flex-end; gap: 8px; margin-top: 18px; flex-wrap: wrap; }
</style>
