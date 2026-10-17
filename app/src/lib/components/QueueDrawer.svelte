<script lang="ts">
  import { queue } from '../stores/app.svelte';
  import AppRow from './AppRow.svelte';
  import JobButton from './JobButton.svelte';
  import Icon from './Icon.svelte';
  import RowMenu from './RowMenu.svelte';
  import { fade } from 'svelte/transition';
  import type { ButtonMode } from '../logic/jobButton';
  import type { Job } from '../ipc/commands';
  import { getJobLog } from '../ipc/commands';
  let { open = $bindable(false) }: { open?: boolean } = $props();

  const verb: Record<string, ButtonMode> = { install: 'install', upgrade: 'update', uninstall: 'uninstall', rollback: 'update' };
  const kindText: Record<string, string> = { install: 'Install', upgrade: 'Update', uninstall: 'Uninstall', rollback: 'Go back to the earlier version' };
  const jobs = $derived([...queue.jobs].reverse());
  const hasFinished = $derived(queue.jobs.length > queue.active.length);

  // Other ways to try a failed change; the backend checks each is allowed.
  function retryItems(j: Job) {
    const items = [{ text: 'Try again', run: () => queue.retry(j.id) }];
    const tool = j.op.target.source !== 'msstore';
    if (tool && (j.op.kind === 'install' || j.op.kind === 'upgrade')) items.push({ text: 'Try with the installer window', run: () => queue.retry(j.id, 'interactive') });
    if (tool && j.op.kind === 'upgrade') items.push({ text: 'Reinstall', run: () => queue.retry(j.id, 'reinstall') });
    return items;
  }

  // The live output of one job (technical view: the tools' own words are shown as they are).
  let logFor = $state<string | null>(null);
  let lines = $state<string[]>([]);
  let logBox = $state<HTMLElement>();
  $effect(() => {
    const id = logFor;
    if (!id) return;
    let stop = false;
    const tick = async () => {
      try { lines = await getJobLog(id); } catch { lines = []; }
      queueMicrotask(() => logBox?.scrollTo({ top: logBox.scrollHeight }));
      const running = queue.jobs.some((j) => j.id === id && !['succeeded', 'failed', 'cancelled', 'skipped'].includes(j.phase));
      if (!stop && running) setTimeout(tick, 1000);
    };
    tick();
    return () => { stop = true; };
  });

  function slide(_node: Element) {
    const reduced = document.documentElement.dataset.motion === 'reduced';
    return reduced
      ? { duration: 140, css: (t: number) => `opacity:${t}` }
      : { duration: 300, easing: (t: number) => 1 - Math.pow(1 - t, 3), css: (t: number) => `transform:translateX(${(1 - t) * 100}%)` };
  }
</script>

<svelte:window onkeydown={(e) => { if (open && e.key === 'Escape') open = false; }} />

{#if open}
  <button class="scrim" aria-label="Close activity" transition:fade={{ duration: 200 }} onclick={() => (open = false)}></button>
  <aside class="drawer" transition:slide aria-label="Activity">
    <header>
      <h2>Activity</h2>
      <div class="actions">
        {#if queue.active.length > 0 || queue.paused}
          <button class="btn btn-quiet" onclick={() => queue.setPaused(!queue.paused)}>{queue.paused ? 'Resume' : 'Pause'}</button>
        {/if}
        {#if hasFinished}<button class="btn btn-quiet" onclick={() => queue.clearFinished()}>Clear finished</button>{/if}
        <button class="icon-btn" aria-label="Close" onclick={() => (open = false)}><Icon name="x" size={16} /></button>
      </div>
    </header>
    <div class="body">
      {#if queue.note}
        <p class="note" role="status">{queue.note}</p>
      {/if}
      {#if queue.paused}
        <p class="note">Paused. What's running now will finish; nothing new starts until you resume.</p>
      {/if}
      {#if queue.notStarted.length > 0}
        <div class="note row">
          <span>{queue.notStarted.length === 1 ? '1 job' : `${queue.notStarted.length} jobs`} didn't run because Quantum Updater closed.</span>
          <button class="btn btn-quiet" onclick={() => queue.retryNotStarted()}>Run {queue.notStarted.length === 1 ? 'it' : 'them'} now</button>
        </div>
      {/if}
      {#if jobs.length === 0}
        <p class="muted empty">Nothing running. Installs and updates will show up here.</p>
      {:else}
        <div class="card list">
          {#each jobs as j (j.id)}
            <AppRow name={j.displayName} meta={j.detail ?? kindText[j.op.kind]} job={j} mode={verb[j.op.kind]} iconKey="{j.op.target.source}:{j.op.target.id}">
              {#snippet action()}
                <span class="acts">
                  <JobButton job={j} mode={verb[j.op.kind]} name={j.displayName}
                    onstart={() => queue.retry(j.id)} oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
                  {#if j.phase === 'failed' || j.phase === 'cancelled'}
                    <RowMenu label="Other ways to try {j.displayName}" items={retryItems(j)} />
                  {/if}
                  <button class="log-btn" aria-expanded={logFor === j.id} title="Show what's happening"
                    onclick={() => (logFor = logFor === j.id ? null : j.id)}>Details</button>
                </span>
              {/snippet}
            </AppRow>
            {#if logFor === j.id}
              <pre class="log" bind:this={logBox} aria-label="Live output for {j.displayName}">{lines.length ? lines.join('\n') : 'Nothing yet.'}</pre>
            {/if}
          {/each}
        </div>
      {/if}
    </div>
  </aside>
{/if}

<style>
  .scrim { position: fixed; inset: 0; background: var(--scrim); z-index: 40; cursor: default; }
  .drawer {
    position: fixed; top: 0; right: 0; bottom: 0; width: min(440px, 92vw); z-index: 41;
    background: var(--canvas); box-shadow: var(--shadow-raised);
    display: flex; flex-direction: column;
  }
  header { display: flex; align-items: center; justify-content: space-between; padding: 22px 20px 14px 24px; }
  .actions { display: flex; align-items: center; gap: 6px; }
  .icon-btn { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 50%; color: var(--muted); }
  .icon-btn:hover { background: var(--pill); color: var(--ink); }
  .body { overflow: auto; padding: 0 20px 24px; }
  .empty { padding: 24px 4px; }
  .note { margin: 0 0 12px; padding: 10px 14px; border-radius: 12px; background: var(--card); box-shadow: var(--shadow-card); font-size: 13px; color: var(--ink-2); }
  .acts { display: inline-flex; align-items: center; gap: 4px; }
  .log-btn { font-size: 12px; font-weight: 600; color: var(--muted); padding: 4px 6px; border-radius: 6px; }
  .log-btn:hover, .log-btn[aria-expanded='true'] { color: var(--ink); background: var(--pill); }
  .log { margin: 0; padding: 10px 14px; max-height: 220px; overflow: auto; background: var(--canvas-2); border-top: 1px solid var(--hairline);
    font: 11.5px/1.45 ui-monospace, 'Cascadia Mono', Consolas, monospace; color: var(--ink-2); white-space: pre-wrap; word-break: break-word; }
  .note.row { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
</style>
