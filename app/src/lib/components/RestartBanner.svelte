<script lang="ts">
  import { onMount } from 'svelte';
  import { listen } from '@tauri-apps/api/event';
  import { queue, toasts } from '../stores/app.svelte';
  import { restartNow } from '../ipc/commands';
  import Banner from './Banner.svelte';
  import Dialog from './Dialog.svelte';

  // Updates that finished but need Windows to restart. Restarting always asks first.
  const names = $derived(queue.restartNeeded);
  let asking = $state(false);
  onMount(() => {
    const off = listen('app://restart-requested', () => (asking = true));
    return () => { off.then((f) => f()); };
  });
  const list = $derived(names.length <= 3 ? names.join(', ') : `${names.slice(0, 3).join(', ')} and ${names.length - 3} more`);
  async function restart() {
    asking = false;
    try {
      await restartNow();
      toasts.push({ text: 'Windows will restart in 30 seconds. Save your work.', tone: 'warn' }, 30000);
    } catch (e) {
      toasts.push({ text: (e as { message?: string })?.message ?? String(e), tone: 'error' });
    }
  }
</script>

{#if names.length > 0}
  <div class="restart">
    <Banner icon="refresh" text="Restart your PC to finish {names.length === 1 ? 'updating' : `${names.length} updates:`} {list}."
      action={{ label: 'Restart…', run: () => (asking = true) }} />
  </div>
{/if}

{#if asking}
  <Dialog title="Restart now?" onclose={() => (asking = false)}>
    <p class="muted">Save your work and close your apps first. Windows restarts 30 seconds after you choose Restart.</p>
    {#snippet buttons()}
      <button class="btn btn-quiet" onclick={() => (asking = false)}>Not now</button>
      <button class="btn btn-primary" onclick={restart}>Restart</button>
    {/snippet}
  </Dialog>
{/if}

<style>
  .restart { margin-bottom: 16px; }
</style>
