<script lang="ts">
  import { market, queue, installed, overview } from '../stores/app.svelte';
  import { getApp, type AppDetails } from '../ipc/commands';
  import { installedSet, isInstalled, needsChoco } from '../logic/entries';
  import AppIcon from './AppIcon.svelte';
  import JobButton from './JobButton.svelte';
  import Icon from './Icon.svelte';
  import { fade } from 'svelte/transition';

  const app = $derived(market.detail);
  let details = $state<AppDetails | null>(null);
  let loading = $state(false);

  $effect(() => {
    const a = app;
    details = null;
    if (!a) return;
    loading = true;
    let live = true;
    getApp(a).then((d) => { if (live) details = d; }).catch(() => {}).finally(() => { if (live) loading = false; });
    return () => { live = false; };
  });

  const done = $derived(app ? isInstalled(app, installedSet(installed.items)) : false);
  const bestId = $derived(app ? (app.best === 'winget' ? app.winget : app.choco) : null);
  const job = $derived(app && bestId ? queue.jobFor(app.best, bestId, 'install') : undefined);
  const host = $derived.by(() => {
    const h = details?.homepage;
    if (!h) return null;
    try { return new URL(h).host.replace(/^www\./, ''); } catch { return null; }
  });

  function slide(_n: Element) {
    const reduced = document.documentElement.dataset.motion === 'reduced';
    return reduced
      ? { duration: 140, css: (t: number) => `opacity:${t}` }
      : { duration: 300, easing: (t: number) => 1 - Math.pow(1 - t, 3), css: (t: number) => `transform:translateX(${(1 - t) * 100}%)` };
  }
</script>

<svelte:window onkeydown={(e) => { if (app && e.key === 'Escape') market.detail = null; }} />

{#if app}
  <button class="scrim" aria-label="Close details" transition:fade={{ duration: 200 }} onclick={() => (market.detail = null)}></button>
  <aside class="panel" transition:slide aria-label="{app.name} details">
    <button class="close" aria-label="Close" onclick={() => (market.detail = null)}><Icon name="x" size={16} /></button>
    <div class="head">
      <AppIcon name={app.name} size={72} iconKey={app.iconKey} />
      <div class="title">
        <h2>{app.name}</h2>
        <p class="muted">{details?.publisher ?? app.publisher ?? ''}</p>
      </div>
    </div>
    <div class="cta">
      {#if done}
        <span class="badge">Installed</span>
      {:else if needsChoco(app, overview.chocoReady)}
        <span class="badge">Needs setup</span>
        <span class="muted small">This app comes from a source that's turned off. Turn it on in Settings.</span>
      {:else}
        <JobButton {job} mode="install" name={app.name}
          onstart={() => queue.installEntry(app)} oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
      {/if}
      {#if details?.version ?? app.version}<span class="muted small">Version {details?.version ?? app.version}</span>{/if}
    </div>

    <section>
      <h3>About</h3>
      {#if details?.description}
        <p class="desc">{details.description}</p>
      {:else if loading}
        <div class="line"></div><div class="line short"></div>
      {:else}
        <p class="desc muted">{app.blurb ?? 'No description available.'}</p>
      {/if}
    </section>

    <section class="facts">
      {#if details?.publisher ?? app.publisher}<div><span class="k">Publisher</span><span class="v">{details?.publisher ?? app.publisher}</span></div>{/if}
      {#if host}<div><span class="k">Website</span><span class="v">{host}</span></div>{/if}
      {#if details?.license}<div><span class="k">License</span><span class="v">{details.license}</span></div>{/if}
    </section>

    <details class="advanced">
      <summary>Advanced</summary>
      <!-- advanced -->
      <div class="facts">
        <div><span class="k">Available from</span><span class="v">{[app.winget ? 'winget' : '', app.choco ? 'Chocolatey' : ''].filter(Boolean).join(' · ')}</span></div>
        <div><span class="k">Installs with</span><span class="v">{app.best === 'winget' ? 'winget' : 'Chocolatey'}</span></div>
        <div><span class="k">Package id</span><span class="v mono">{bestId}</span></div>
      </div>
      <!-- /advanced -->
    </details>
  </aside>
{/if}

<style>
  .scrim { position: fixed; inset: 0; background: var(--scrim); z-index: 40; cursor: default; }
  .panel {
    position: fixed; top: 0; right: 0; bottom: 0; width: min(460px, 92vw); z-index: 41;
    background: var(--canvas); box-shadow: var(--shadow-raised);
    overflow-y: auto; padding: 28px 28px 40px;
  }
  .close { position: absolute; top: 16px; right: 16px; width: 32px; height: 32px; border-radius: 50%; display: grid; place-items: center; color: var(--muted); }
  .close:hover { background: var(--pill); color: var(--ink); }
  .head { display: flex; gap: 16px; align-items: center; margin-top: 8px; }
  .title h2 { font-size: 22px; }
  .cta { display: flex; align-items: center; gap: 12px; margin: 18px 0 22px; }
  .small { font-size: 12.5px; }
  section { margin-bottom: 20px; }
  h3 { font-family: var(--font-display); font-size: 14px; font-weight: 650; margin: 0 0 6px; }
  .desc { color: var(--ink-2); line-height: 1.55; user-select: text; }
  .line { height: 11px; border-radius: 6px; background: var(--pill); margin: 8px 0; }
  .line.short { width: 60%; }
  .facts { display: grid; gap: 8px; padding: 14px 16px; border-radius: 12px; background: var(--card); box-shadow: var(--shadow-card); }
  .advanced { margin-top: 14px; }
  .advanced summary { cursor: pointer; font-size: 13px; font-weight: 600; color: var(--muted); padding: 4px 0; list-style-position: inside; }
  .advanced summary:hover { color: var(--ink); }
  .advanced .facts { margin-top: 8px; }
  .facts div { display: flex; justify-content: space-between; gap: 16px; font-size: 13px; }
  .k { color: var(--muted); }
  .v { font-weight: 550; text-align: right; min-width: 0; overflow: hidden; text-overflow: ellipsis; }
  .mono { font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace; font-size: 12px; }
</style>
