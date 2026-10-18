<script lang="ts">
  import { queue } from '../stores/app.svelte';
  import Dialog from './Dialog.svelte';

  // "VLC is open. Close it and update?" Closing is always the person's choice, and asks the
  // app to close the way its X button would (it can still ask to save work).
  const apps = $derived(queue.openAsk?.apps ?? []);
  const one = $derived(apps.length === 1);
  const title = $derived(one ? `${apps[0]?.name} is open` : `${apps.length} apps are open`);
</script>

{#if queue.openAsk}
  <Dialog {title} onclose={() => queue.answerOpen('cancel')}>
    {#if one}
      <p class="muted">Close it and update? It closes the same way as clicking its X, so it can still ask you to save your work.</p>
    {:else}
      <p class="muted">Close them and update? They close the same way as clicking their X, so they can still ask you to save your work.</p>
      <ul>
        {#each apps as a (a.source + a.id)}<li>{a.name}</li>{/each}
      </ul>
    {/if}
    {#snippet buttons()}
      <button class="btn btn-quiet" onclick={() => queue.answerOpen('cancel')}>Cancel</button>
      <button class="btn" onclick={() => queue.answerOpen('later')}>Update later</button>
      <button class="btn btn-primary" onclick={() => queue.answerOpen('close')}>{one ? 'Close and update' : 'Close them and update'}</button>
    {/snippet}
  </Dialog>
{/if}

<style>
  ul { margin: 10px 0 0; padding-left: 18px; }
  li { margin: 2px 0; }
</style>
