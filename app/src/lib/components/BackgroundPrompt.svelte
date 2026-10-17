<script lang="ts">
  import { settings, overview } from '../stores/app.svelte';
  import { fade } from 'svelte/transition';

  // Asked once: start at sign-in is never set up without a yes.
  const show = $derived(!!settings.value && !settings.value.backgroundPrompted && !!overview.value?.isElevated);
  let busy = $state(false);

  async function answer(yes: boolean) {
    busy = true;
    if (yes) await settings.update({ startAtLogin: true, backgroundPrompted: true });
    // If Windows refused (the reason was shown), still record that we asked.
    if (!yes || !settings.value?.backgroundPrompted) await settings.update({ startAtLogin: false, backgroundPrompted: true });
    busy = false;
  }
</script>

{#if show}
  <div class="prompt card" transition:fade={{ duration: 160 }}>
    <div>
      <h3>Keep your apps updated in the background?</h3>
      <p class="muted">Quantum Updater starts quietly in the tray when you sign in and tells you when updates are ready. You can change this in Settings.</p>
    </div>
    <div class="buttons">
      <button class="btn btn-quiet" disabled={busy} onclick={() => answer(false)}>Not now</button>
      <button class="btn btn-primary" disabled={busy} onclick={() => answer(true)}>Yes, keep them updated</button>
    </div>
  </div>
{/if}

<style>
  .prompt { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 16px 18px; margin-bottom: 16px; }
  h3 { font-family: var(--font-display); font-size: 15px; font-weight: 650; margin: 0 0 4px; }
  p { font-size: 13px; }
  .buttons { display: flex; gap: 8px; flex: none; }
</style>
