<script lang="ts">
  import type { AppEntry } from '../ipc/commands';
  import { queue, installed, market, overview } from '../stores/app.svelte';
  import { installedSet, isInstalled, needsSetup, bestId as idOf } from '../logic/entries';
  import AppIcon from './AppIcon.svelte';
  import JobButton from './JobButton.svelte';

  let { app }: { app: AppEntry } = $props();

  const done = $derived(isInstalled(app, installedSet(installed.items)));
  const bestId = $derived(idOf(app));
  const job = $derived(bestId ? queue.jobFor(app.best, bestId, 'install') : undefined);
  // The name Windows uses for this app when installed, so the tile shows its exact local icon.
  const localName = $derived(
    installed.items.find((p) => (p.source === 'winget' && p.id.toLowerCase() === app.winget?.toLowerCase()) || (p.source === 'choco' && p.id.toLowerCase() === app.choco?.toLowerCase()))?.name,
  );
</script>

<!-- The whole card opens details; the button inside installs. -->
<article class="tile card">
  <button class="hit" aria-label="About {app.name}" onclick={() => (market.detail = app)}></button>
  <div class="top">
    <AppIcon name={app.name} size={48} iconKey={app.iconKey} lookup={localName} />
    <div class="action">
      {#if done}
        <span class="badge">Installed</span>
      {:else if needsSetup(app, overview.on)}
        <span class="badge" title="This app comes from a source that's turned off. Turn it on in Settings.">Needs setup</span>
      {:else}
        <JobButton {job} mode="install" name={app.name}
          onstart={() => queue.installEntry(app)} oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
      {/if}
    </div>
  </div>
  <h3 class="truncate">{app.name}</h3>
  <p class="pub truncate">{app.publisher ?? ''}</p>
  {#if app.blurb}<p class="blurb">{app.blurb}</p>{/if}
</article>

<style>
  .tile {
    position: relative; padding: 16px; display: grid; gap: 4px; align-content: start;
    transition: transform var(--dur-quick) var(--ease-out), box-shadow var(--dur-quick) ease;
  }
  @media (hover: hover) and (pointer: fine) {
    .tile:hover { box-shadow: var(--shadow-raised); transform: translateY(-1px); }
  }
  .hit { position: absolute; inset: 0; border-radius: var(--radius-card); z-index: 0; }
  .top { display: flex; align-items: flex-start; justify-content: space-between; margin-bottom: 10px; }
  .top :global(.tile), .action { position: relative; z-index: 1; }
  .action { position: relative; z-index: 1; }
  h3 { font-family: var(--font-display); font-size: 15px; font-weight: 650; letter-spacing: -0.02em; margin: 0; }
  .pub { font-size: 12px; color: var(--muted); }
  .blurb { font-size: 12.5px; color: var(--ink-2); margin-top: 4px; }
  h3, .pub, .blurb { pointer-events: none; position: relative; }
</style>
