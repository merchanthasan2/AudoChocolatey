<script lang="ts">
  import { onMount } from 'svelte';
  import { fade, scale } from 'svelte/transition';
  import { onCloseBlocked } from '../ipc/events';
  import { quitAnyway } from '../ipc/commands';
  import { queue } from '../stores/app.svelte';

  // Closing kills running installers, so the backend holds the close and asks here first.
  let open = $state(false);
  let keep = $state<HTMLButtonElement>();
  const name = $derived(queue.current?.displayName ?? 'An app');

  onMount(() => {
    const off = onCloseBlocked(() => { open = true; });
    return () => { off.then((f) => f()); };
  });
  $effect(() => { if (open) keep?.focus(); });
</script>

<svelte:window onkeydown={(e) => { if (open && e.key === 'Escape') open = false; }} />

{#if open}
  <div class="scrim" transition:fade={{ duration: 160 }}></div>
  <div class="dialog card" role="alertdialog" aria-modal="true" aria-labelledby="cg-title" aria-describedby="cg-body"
    transition:scale={{ start: 0.96, duration: 180 }}>
    <h2 id="cg-title">Still working on {name}</h2>
    <p id="cg-body" class="muted">Closing now stops it, and an install stopped halfway can leave the app broken. It usually takes a minute or two.</p>
    <div class="buttons">
      <button class="btn btn-quiet danger" onclick={() => quitAnyway()}>Close anyway</button>
      <button class="btn btn-primary" bind:this={keep} onclick={() => (open = false)}>Keep it open</button>
    </div>
  </div>
{/if}

<style>
  .scrim { position: fixed; inset: 0; background: var(--scrim); z-index: 60; }
  .dialog {
    position: fixed; z-index: 61; left: 50%; top: 50%; translate: -50% -50%;
    width: min(420px, calc(100vw - 32px)); padding: 22px 22px 18px;
  }
  h2 { font-size: 17px; margin: 0 0 8px; }
  .buttons { display: flex; justify-content: flex-end; gap: 8px; margin-top: 18px; }
  .danger { color: var(--danger); }
</style>
