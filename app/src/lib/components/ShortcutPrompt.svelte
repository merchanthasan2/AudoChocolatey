<script lang="ts">
  import { onMount } from 'svelte';
  import { onNewShortcuts, type NewShortcuts } from '../ipc/events';
  import { removeShortcuts } from '../ipc/commands';
  import { toasts } from '../stores/app.svelte';
  import Dialog from './Dialog.svelte';
  import Checkbox from './Checkbox.svelte';

  // "Remove the new desktop icon?" Offers collect while the person is away, then show together.
  // Only icons that appeared during our own install or update are listed (Rust decides).
  let offers = $state<NewShortcuts[]>([]);
  let always = $state(false);
  onMount(() => {
    const off = onNewShortcuts((s) => { offers = [...offers.filter((o) => o.jobId !== s.jobId), s]; });
    return () => { off.then((f) => f()); };
  });

  const icons = $derived(offers.flatMap((o) => o.names));
  const title = $derived(icons.length === 1 ? 'Remove the new desktop icon?' : `Remove ${icons.length} new desktop icons?`);
  const apps = $derived(offers.map((o) => o.appName));

  function keep() { offers = []; always = false; }
  async function remove() {
    const list = offers;
    const remember = always;
    keep();
    let gone = 0;
    for (const o of list) {
      try { gone += await removeShortcuts(o.jobId, remember); } catch (e) { toasts.push({ text: (e as { message?: string })?.message ?? String(e), tone: 'error' }); }
    }
    if (gone > 0) toasts.push({ text: gone === 1 ? 'Desktop icon removed.' : `${gone} desktop icons removed.`, tone: 'ok' });
  }
</script>

{#if offers.length > 0}
  <Dialog {title} onclose={keep}>
    <p class="muted">{apps.length === 1 ? `${apps[0]} added` : 'These apps added'} {icons.length === 1 ? 'an icon' : 'icons'} to your desktop:</p>
    <ul>{#each icons as n, i (i)}<li>{n}</li>{/each}</ul>
    <label class="row">
      <Checkbox checked={always} onchange={(v) => (always = v)} label="Always remove new desktop icons for {apps.length === 1 ? 'this app' : 'these apps'}" />
      <span>Always remove new desktop icons for {apps.length === 1 ? 'this app' : 'these apps'}</span>
    </label>
    {#snippet buttons()}
      <button class="btn btn-quiet" onclick={keep}>Keep</button>
      <button class="btn btn-primary" onclick={remove}>Remove</button>
    {/snippet}
  </Dialog>
{/if}

<style>
  ul { margin: 8px 0 14px; padding-left: 18px; }
  li { margin: 2px 0; }
  .row { display: flex; align-items: center; gap: 10px; font-size: 13px; }
</style>
