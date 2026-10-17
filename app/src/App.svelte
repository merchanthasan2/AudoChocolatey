<script lang="ts">
  import { onMount } from 'svelte';
  import { router, overview, settings, installed, updates, queue, toasts } from './lib/stores/app.svelte';
  import { relaunchAsAdmin } from './lib/ipc/commands';
  import { onUpdatesChanged } from './lib/ipc/events';
  import Banner from './lib/components/Banner.svelte';
  import BackgroundPrompt from './lib/components/BackgroundPrompt.svelte';
  import Sidebar from './lib/components/Sidebar.svelte';
  import Toasts from './lib/components/Toasts.svelte';
  import QueueDrawer from './lib/components/QueueDrawer.svelte';
  import AppDetail from './lib/components/AppDetail.svelte';
  import CloseGuard from './lib/components/CloseGuard.svelte';
  import Marketplace from './views/Marketplace.svelte';
  import Updates from './views/Updates.svelte';
  import Installed from './views/Installed.svelte';
  import Settings from './views/Settings.svelte';
  import { viewFade } from './lib/motion';

  let drawer = $state(false);

  // Admin rights come only from the person, through the normal Windows prompt.
  async function askForAdmin() {
    try { await relaunchAsAdmin(); }
    catch { toasts.push({ text: "Admin access wasn't granted, so nothing changed.", tone: 'warn' }); }
  }
  let searchInput = $state<HTMLInputElement>();
  let scroller = $state<HTMLElement>();

  onMount(() => {
    settings.load();
    overview.load();
    queue.init();
    installed.refresh();
    updates.refresh();
    onUpdatesChanged((l) => updates.accept(l, true));
  });

  // Each view starts at the top; switching is a quick fade, never a slide (it happens often).
  $effect(() => {
    void router.view;
    scroller?.scrollTo({ top: 0 });
  });

  function keys(e: KeyboardEvent) {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      router.go('marketplace');
      queueMicrotask(() => searchInput?.focus());
    }
  }
</script>

<svelte:window onkeydown={keys} />

<div class="shell">
  <Sidebar onqueue={() => (drawer = true)} />
  <main bind:this={scroller}>
    <div class="content">
      {#if overview.value && !overview.value.isElevated}
        <div class="admin-banner">
          <Banner icon="shield" text="Quantum Updater needs administrator access to install and update apps. Windows will ask you to confirm."
            action={{ label: 'Allow admin access', run: askForAdmin }} />
        </div>
      {/if}
      <BackgroundPrompt />
      {#key router.view}
        <div in:viewFade>
          {#if router.view === 'marketplace'}
            <Marketplace bind:input={searchInput} />
          {:else if router.view === 'updates'}
            <Updates />
          {:else if router.view === 'installed'}
            <Installed />
          {:else}
            <Settings />
          {/if}
        </div>
      {/key}
    </div>
  </main>
</div>

<QueueDrawer bind:open={drawer} />
<AppDetail />
<CloseGuard />
<Toasts />

<style>
  .shell { display: flex; height: 100%; }
  main {
    flex: 1; min-width: 0; overflow-y: auto;
    background: var(--canvas-2);
    border-top-left-radius: 16px; margin-top: 8px;
    box-shadow: inset 1px 1px 0 var(--hairline);
  }
  .content { max-width: 1040px; margin: 0 auto; padding: 34px 40px 64px; }
  .admin-banner { margin-bottom: 16px; }
</style>
