<script lang="ts">
  import { router, updates, overview, type View } from '../stores/app.svelte';
  import Icon from './Icon.svelte';
  import BrandMark from './BrandMark.svelte';
  import QueuePill from './QueuePill.svelte';
  let { onqueue }: { onqueue: () => void } = $props();

  const items: { view: View; label: string; icon: string }[] = [
    { view: 'updates', label: 'Updates', icon: 'updates' },
    { view: 'marketplace', label: 'Marketplace', icon: 'bag' },
    { view: 'installed', label: 'Installed', icon: 'grid' },
    { view: 'settings', label: 'Settings', icon: 'sliders' },
  ];
</script>

<nav class="side" aria-label="Main">
  <div class="brand">
    <BrandMark size={28} />
    <span>Quantum Updater</span>
  </div>
  <ul>
    {#each items as it (it.view)}
      <li>
        <button class="nav" class:on={router.view === it.view} aria-current={router.view === it.view ? 'page' : undefined}
          onclick={() => router.go(it.view)}>
          <Icon name={it.icon} size={18} />
          <span class="label">{it.label}</span>
          {#if it.view === 'updates' && updates.items.length > 0}
            <span class="count">{updates.items.length}</span>
          {/if}
        </button>
      </li>
    {/each}
  </ul>
  <div class="foot">
    <QueuePill onopen={onqueue} />
    {#if overview.value}
      <p class="ver">v{overview.value.appVersion}{overview.value.isElevated ? '' : ' · not elevated'}</p>
    {/if}
  </div>
</nav>

<style>
  .side { width: 232px; flex: none; display: flex; flex-direction: column; padding: 20px 14px 16px; gap: 18px; }
  .brand { display: flex; align-items: center; gap: 10px; padding: 0 8px; font-family: var(--font-display); font-weight: 700; font-size: 15px; letter-spacing: -0.02em; }
  ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
  .nav {
    width: 100%; display: flex; align-items: center; gap: 12px;
    height: 38px; padding: 0 12px; border-radius: 10px;
    color: var(--ink-2); font-weight: 550; font-size: 13.5px;
    transition: background-color var(--dur-quick) ease, color var(--dur-quick) ease;
  }
  .nav:hover { background: var(--canvas-2); color: var(--ink); }
  .nav.on { background: var(--ink); color: var(--canvas); }
  .label { flex: 1; text-align: left; }
  .count { min-width: 20px; height: 20px; padding: 0 6px; border-radius: 999px; display: grid; place-items: center; font-size: 11px; font-weight: 700; background: var(--accent); color: var(--accent-ink); }
  .nav.on .count { background: var(--canvas); color: var(--ink); }
  .foot { margin-top: auto; display: grid; gap: 10px; }
  .ver { font-size: 11px; color: var(--faint); padding: 0 8px; }
</style>
