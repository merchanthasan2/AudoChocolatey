<script lang="ts">
  import { myBundles } from '../stores/app.svelte';
  import Dialog from './Dialog.svelte';

  const apps = $derived(myBundles.adding);
  // '' means "a new bundle".
  let target = $state('');
  let name = $state('');
  $effect(() => { if (apps) { target = myBundles.list[0]?.id ?? ''; name = ''; } });
  const canAdd = $derived(target !== '' || name.trim() !== '');

  async function add() {
    if (!apps || !canAdd) return;
    if (await myBundles.add(target || null, target ? null : name, apps)) myBundles.adding = null;
  }
</script>

{#if apps}
  <Dialog title="Add {apps.length} {apps.length === 1 ? 'app' : 'apps'} to a bundle" onclose={() => (myBundles.adding = null)}>
    <p class="muted intro">Bundles let you install the same apps again in one go, on this PC or another one.</p>
    <div class="choices" role="radiogroup" aria-label="Bundle">
      {#each myBundles.list as b (b.id)}
        <label class="choice"><input type="radio" bind:group={target} value={b.id} /> {b.name} <span class="muted">· {b.apps.length} apps</span></label>
      {/each}
      <label class="choice"><input type="radio" bind:group={target} value="" /> A new bundle</label>
      {#if target === ''}
        <input class="name" bind:value={name} placeholder="Name, for example Work PC" maxlength="60"
          onkeydown={(e) => { if (e.key === 'Enter') add(); }} />
      {/if}
    </div>
    {#snippet buttons()}
      <button class="btn btn-quiet" onclick={() => (myBundles.adding = null)}>Cancel</button>
      <button class="btn btn-primary" disabled={!canAdd} onclick={add}>Add</button>
    {/snippet}
  </Dialog>
{/if}

<style>
  .intro { font-size: 13px; margin-bottom: 12px; }
  .choices { display: grid; gap: 4px; }
  .choice { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border-radius: 8px; cursor: pointer; }
  .choice:hover { background: var(--pill); }
  .choice input { accent-color: var(--accent); }
  .name { height: 34px; margin: 4px 0 0 30px; padding: 0 10px; border-radius: 8px; border: 1px solid var(--hairline-strong); background: var(--card); color: var(--ink); font: inherit; }
</style>
