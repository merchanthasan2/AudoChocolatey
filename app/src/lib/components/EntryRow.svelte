<script lang="ts">
  import type { AppEntry } from '../ipc/commands';
  import { queue, installed, overview, market } from '../stores/app.svelte';
  import { installedSet, isInstalled, needsSetup, bestId } from '../logic/entries';
  import AppRow from './AppRow.svelte';
  import JobButton from './JobButton.svelte';

  // One app in a list (search results, "More apps"): the row opens details, the button installs.
  let { e }: { e: AppEntry } = $props();
  const id = $derived(bestId(e));
  const job = $derived(id ? queue.jobFor(e.best, id, 'install') : undefined);
  const setup = $derived(needsSetup(e, overview.on));
  const done = $derived(isInstalled(e, installedSet(installed.items)));
</script>

<div class="row-wrap">
  <button class="row-hit" aria-label="About {e.name}" onclick={() => (market.detail = e)}></button>
  <AppRow name={e.name} meta={[e.version, e.blurb ?? ''].filter(Boolean).join(' · ')} {job} mode="install" iconKey={e.iconKey}>
    {#snippet action()}
      <span class="row-action">
        {#if done}
          <span class="badge">Installed</span>
        {:else if setup}
          <span class="badge" title="This app comes from a source that's turned off. Turn it on in Settings.">Needs setup</span>
        {:else}
          <JobButton {job} mode="install" name={e.name}
            onstart={() => queue.installEntry(e)} oncancel={(jid) => queue.cancel(jid)} onretry={(jid) => queue.retry(jid)} />
        {/if}
      </span>
    {/snippet}
  </AppRow>
</div>

<style>
  .row-wrap { position: relative; }
  .row-hit { position: absolute; inset: 0; z-index: 0; }
  .row-wrap :global(.row) { position: relative; pointer-events: none; }
  .row-action { pointer-events: auto; position: relative; z-index: 1; }
</style>
