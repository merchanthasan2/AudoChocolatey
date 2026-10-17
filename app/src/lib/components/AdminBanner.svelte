<script lang="ts">
  import { overview, toasts } from '../stores/app.svelte';
  import { relaunchAsAdmin } from '../ipc/commands';
  import { bannerFor } from '../logic/adminBanner';
  import Banner from './Banner.svelte';

  const b = $derived(bannerFor(overview.value));
  // Admin rights come only from the person, through the normal Windows prompt.
  async function askForAdmin() {
    try { await relaunchAsAdmin(); }
    catch { toasts.push({ text: "Admin access wasn't granted, so nothing changed.", tone: 'warn' }); }
  }
</script>

{#if b}
  <div class="admin-banner">
    <Banner icon="shield" text={b.text} action={b.askForAdmin ? { label: 'Allow admin access', run: askForAdmin } : undefined} />
  </div>
{/if}

<style>
  .admin-banner { margin-bottom: 16px; }
</style>
