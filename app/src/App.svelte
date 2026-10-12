<script lang="ts">
  // Temporary foundation screen; replaced by the full interface in P4.
  import { onMount } from 'svelte';
  import { fly } from 'svelte/transition';
  import { backOut } from 'svelte/easing';
  import { getOverview, listInstalled, listUpdates, type Overview, type SourceStatus } from './lib/ipc/commands';

  let overview = $state<Overview | null>(null);
  let error = $state<string | null>(null);
  let installed = $state<number | null>(null);
  let updates = $state<number | null>(null);

  const label = (s: SourceStatus) =>
    s.state === 'ready' ? s.version : s.state === 'missing' ? 'Not installed' : 'Needs attention';
  const name = (s: SourceStatus) => (s.kind === 'winget' ? 'winget' : 'Chocolatey');

  onMount(() => {
    getOverview().then((o) => (overview = o)).catch((e) => (error = String(e)));
    listInstalled().then((l) => (installed = l.items.length)).catch(() => (installed = -1));
    listUpdates().then((l) => (updates = l.items.length)).catch(() => (updates = -1));
  });

  const count = (n: number | null) => (n === null ? 'Checking…' : n < 0 ? 'Unavailable' : String(n));
</script>

<main>
  <section class="card" in:fly={{ y: 14, duration: 420, easing: backOut }}>
    <h1>Quantum Updater</h1>
    <p class="lede">Version 3 is being rebuilt. This is the foundation build.</p>
    {#if overview}
      <dl>
        <dt>Version</dt><dd>{overview.appVersion}</dd>
        <dt>Build</dt><dd class="mono">{overview.buildSha}</dd>
        <dt>Running as admin</dt><dd>{overview.isElevated ? 'Yes' : 'No (development build)'}</dd>
        {#each overview.sources as s (s.kind)}
          <dt>{name(s)}</dt><dd class:muted={s.state !== 'ready'}>{label(s)}</dd>
        {/each}
        <dt>Installed apps</dt><dd>{count(installed)}</dd>
        <dt>Updates available</dt><dd>{count(updates)}</dd>
      </dl>
    {:else if error}
      <p class="error">Could not reach the app core: {error}</p>
    {:else}
      <p class="muted">Connecting…</p>
    {/if}
  </section>
</main>

<style>
  main { min-height: 100%; display: grid; place-items: center; padding: 32px; }
  .card {
    background: var(--card);
    border-radius: var(--radius-card);
    box-shadow: 0 0 0 1px var(--hairline), 0 8px 24px -12px rgba(60, 40, 10, 0.18);
    padding: 28px 32px;
    width: min(440px, 100%);
  }
  h1 { font-family: var(--font-display); font-size: 28px; font-weight: 700; letter-spacing: -0.035em; margin: 0; }
  .lede { color: var(--muted); margin: 4px 0 20px; }
  dl { display: grid; grid-template-columns: auto 1fr; gap: 8px 16px; margin: 0; }
  dt { color: var(--muted); }
  dd { margin: 0; font-weight: 600; }
  .mono { font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace; }
  .muted { color: var(--muted); }
  .error { color: #b3261e; }
</style>
