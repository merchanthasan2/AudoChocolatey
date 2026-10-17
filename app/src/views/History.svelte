<script lang="ts">
  import { queue, toasts } from '../lib/stores/app.svelte';
  import { getHistory, rollback, type HistoryEntry } from '../lib/ipc/commands';
  import Icon from '../lib/components/Icon.svelte';
  import AppIcon from '../lib/components/AppIcon.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';
  import { historyLine, historyTone, signerNote } from '../lib/logic/history';
  import { relativeTime } from '../lib/logic/format';

  let list = $state<HistoryEntry[] | null>(null);
  let error = $state<string | null>(null);
  async function load() {
    try { list = await getHistory(); error = null; }
    catch (e) { error = (e as { message?: string })?.message ?? String(e); list ??= []; }
  }
  // Reload whenever another change finishes.
  const finished = $derived(queue.jobs.filter((j) => ['succeeded', 'failed', 'cancelled', 'skipped'].includes(j.phase)).length);
  $effect(() => { void finished; load(); });

  // One check at a time; the Windows dialog asks before anything changes.
  let checking = $state<string | null>(null);
  async function goBack(id: string) {
    checking = id;
    try {
      if (await rollback(id)) toasts.push({ text: 'Going back to the earlier version. You can follow it in Activity.', tone: 'ok' });
    } catch (e) {
      toasts.push({ text: (e as { message?: string })?.message ?? String(e), tone: 'error' }, 8000);
    } finally { checking = null; }
  }

  let now = $state(Date.now());
  $effect(() => {
    const t = setInterval(() => (now = Date.now()), 30_000);
    return () => clearInterval(t);
  });
</script>

<header class="head">
  <h1>History</h1>
  <p class="muted">The last 200 installs, updates and removals on this PC.</p>
</header>

{#if error}<p class="muted">{error}</p>{/if}

{#if list === null}
  <Skeleton rows={5} />
{:else if list.length === 0}
  <EmptyState icon="history" title="Nothing yet" body="Apps you install, update or remove here will be listed, so you can see what changed and when." />
{:else}
  <div class="card list">
    {#each list as e (e.job.id)}
      {@const j = e.job}
      <div class="row">
        <AppIcon name={j.displayName} size={36} iconKey="{j.op.target.source}:{j.op.target.id}" />
        <div class="text">
          <div class="name">{j.displayName}</div>
          <div class="meta">
            <span class={historyTone(j)}>{historyLine(j)}</span>
            {#if j.endedAt} · {relativeTime(j.endedAt, now)}{/if}
            {#if signerNote(j)} · <span class:warn={j.signer?.kind !== 'signed'}>{signerNote(j)}</span>{/if}
            {#if j.phase === 'failed' && j.error}<div class="err">{j.error}</div>{/if}
          </div>
        </div>
        {#if e.canGoBack}
          <button class="btn btn-quiet" disabled={checking !== null} onclick={() => goBack(j.id)}>
            <Icon name="undo" size={16} /> {checking === j.id ? 'Checking…' : `Go back to ${j.fromVersion}`}
          </button>
        {/if}
      </div>
    {/each}
  </div>
{/if}

<style>
  .head { margin-bottom: 18px; }
  .head p { margin-top: 4px; }
  .row { display: flex; align-items: center; gap: 14px; padding: 12px 16px; }
  .row + .row { border-top: 1px solid var(--hairline); }
  .text { min-width: 0; flex: 1; }
  .name { font-weight: 600; }
  .meta { font-size: 12.5px; color: var(--muted); }
  .ok { color: var(--ink-2); }
  .bad { color: var(--danger); font-weight: 600; }
  .err { margin-top: 2px; }
  .warn { color: var(--warn-ink); }
  .row .btn { gap: 6px; flex-shrink: 0; }
</style>
