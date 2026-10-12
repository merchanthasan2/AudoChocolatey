<script lang="ts">
  import { updates, queue } from '../lib/stores/app.svelte';
  import AppRow from '../lib/components/AppRow.svelte';
  import JobButton from '../lib/components/JobButton.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import CountRoll from '../lib/components/CountRoll.svelte';
  import Icon from '../lib/components/Icon.svelte';
  import { relativeTime } from '../lib/logic/format';
  import { collapse, rise } from '../lib/motion';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';

  // Rows finish and leave; the list keeps pending updates plus anything still running.
  const rows = $derived(
    updates.items.filter((u) => {
      const j = queue.jobFor(u.source, u.id, 'upgrade');
      return !j || j.phase !== 'succeeded';
    }),
  );
  const pending = $derived(rows.filter((u) => !queue.jobFor(u.source, u.id, 'upgrade') || ['failed', 'cancelled'].includes(queue.jobFor(u.source, u.id, 'upgrade')!.phase)));
  const keys = $derived(uniqueKeys(rows, (u) => `${u.source}:${u.id}`));
  const busy = $derived(queue.active.some((j) => j.op.kind === 'upgrade'));

  let now = $state(Date.now());
  $effect(() => {
    const t = setInterval(() => (now = Date.now()), 30_000);
    return () => clearInterval(t);
  });
  const sourceText = (s: string) => (s === 'winget' ? 'winget' : 'Chocolatey');
</script>

<header class="head">
  <div>
    <h1>
      {#if !updates.loaded}Checking for updates…{:else if rows.length === 0}Everything is up to date{:else}<CountRoll value={rows.length} /> {rows.length === 1 ? 'update' : 'updates'} ready{/if}
    </h1>
    <p class="muted">
      {#if updates.loading}Checking now…{:else if updates.loadedAt}Checked {relativeTime(updates.loadedAt, now)}{/if}
    </p>
  </div>
  <div class="actions">
    <button class="btn btn-quiet" onclick={() => updates.refresh()} disabled={updates.loading} aria-label="Check again">
      <span class:spinning={updates.loading}><Icon name="refresh" size={16} /></span> Check again
    </button>
    {#if pending.length > 0}
      <button class="btn btn-primary" onclick={() => queue.updateAll()} disabled={busy && pending.length === 0}>
        Update all
      </button>
    {/if}
  </div>
</header>

{#each updates.sourceErrors as e (e.source)}
  <div class="gap"><Banner text={`Couldn't check ${sourceText(e.source)}: ${e.message}`} action={{ label: 'Try again', run: () => updates.refresh() }} /></div>
{/each}
{#if updates.error}<div class="gap"><Banner text={updates.error} action={{ label: 'Try again', run: () => updates.refresh() }} /></div>{/if}

{#if !updates.loaded}
  <Skeleton rows={4} />
{:else if rows.length === 0}
  <EmptyState icon="check" tone="good" title="You're all set" body="Every app we can update is on its latest version. We'll keep checking in the background." />
{:else}
  <div class="card list">
    {#each rows as u, i (keys[i])}
      {@const job = queue.jobFor(u.source, u.id, 'upgrade')}
      <div in:rise={{ delay: Math.min(i, 8) * 40 }} out:collapse>
        <AppRow name={u.name} meta="{u.installedVersion} → {u.availableVersion} · {sourceText(u.source)}" {job} mode="update">
          {#snippet action()}
            <JobButton {job} mode="update" name={u.name}
              onstart={() => queue.update(u)} oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
          {/snippet}
        </AppRow>
      </div>
    {/each}
  </div>
{/if}

<style>
  .head { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; margin-bottom: 22px; }
  .head p { margin-top: 4px; }
  .actions { display: flex; gap: 8px; }
  .gap { margin-bottom: 12px; }
  .spinning { display: inline-grid; animation: spin 0.9s linear infinite; }
  @keyframes spin { to { transform: rotate(360deg); } }
</style>
