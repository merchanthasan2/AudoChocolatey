<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { Job } from '../ipc/commands';
  import AppIcon from './AppIcon.svelte';
  import { jobButtonState, type ButtonMode } from '../logic/jobButton';
  import { formatBytes } from '../logic/format';

  let {
    name,
    meta,
    job,
    mode,
    iconKey,
    lead,
    action,
    cells,
    bar = false,
    density = 'list',
  }: {
    name: string;
    meta: string;
    job?: Job;
    mode?: ButtonMode;
    iconKey?: string;
    lead?: Snippet;
    action?: Snippet;
    /** Table cells between the name and the action (list views with columns). */
    cells?: Snippet;
    bar?: boolean;
    density?: 'list' | 'compact';
  } = $props();

  const ui = $derived(job && mode ? jobButtonState(job, mode) : null);
  const running = $derived(ui?.kind === 'progress' || ui?.kind === 'working' || ui?.kind === 'queued');

  // While a job runs, the meta line becomes a live status ("Downloading · 1.9 MB · 42%").
  const status = $derived.by(() => {
    if (!job || !ui) return null;
    if (ui.kind === 'idle' || ui.kind === 'done' || ui.kind === 'skipped') return null;
    if (ui.kind === 'failed') return job.error ?? 'Something went wrong';
    const size = job.bytesTotal ? ` · ${formatBytes(job.bytesTotal)}` : '';
    const pct = ui.percent != null ? ` · ${Math.round(ui.percent)}%` : '';
    const retry = job.attempt > 0 ? 'Trying another way · ' : '';
    return `${retry}${ui.label}${size}${pct}`;
  });
</script>

<div class="row" class:active={running} class:compact={density === 'compact'}>
  {#if lead}{@render lead()}{/if}
  <AppIcon {name} {iconKey} size={density === 'compact' ? 26 : 40} />
  <div class="text">
    <div class="name truncate" title={name}>{name}</div>
    {#if density === 'list' || status}
      <div class="meta truncate" class:live={status && ui?.kind !== 'failed'} class:bad={ui?.kind === 'failed'}>{status ?? meta}</div>
    {/if}
    {#if bar && running}
      <div class="bar" class:indeterminate={ui?.percent == null} role="presentation">
        <i style="width:{ui?.percent ?? 30}%"></i>
      </div>
    {/if}
  </div>
  {#if cells}{@render cells()}{/if}
  {#if action}<div class="action">{@render action()}</div>{/if}
</div>

<style>
  .row {
    display: flex; align-items: center; gap: 14px;
    padding: 12px 16px; min-height: 64px;
    content-visibility: auto; contain-intrinsic-size: auto 64px;
    transition: background-color 200ms ease;
  }
  .row.compact { min-height: 44px; padding: 6px 16px; gap: 12px; contain-intrinsic-size: auto 44px; position: relative; }
  .row.compact .name { font-size: 13.5px; }
  .row.compact .bar { position: absolute; left: 0; right: 0; bottom: 0; margin: 0; max-width: none; height: 3px; border-radius: 0; }
  .action { display: flex; justify-content: flex-end; flex-shrink: 0; }
  .row.active { background: color-mix(in srgb, var(--accent-soft) 45%, transparent); }
  :global(.row-wrap + .row-wrap) .row, .row + :global(.row) { border-top: 1px solid var(--hairline); }
  .text { flex: 1; min-width: 0; }
  .name { font-weight: 600; font-size: 14px; }
  .meta { color: var(--muted); font-size: 12.5px; margin-top: 1px; }
  .meta.live { color: var(--accent); }
  .meta.bad { color: var(--danger); }
  .bar { height: 4px; border-radius: 4px; background: var(--hairline-strong); margin-top: 7px; overflow: hidden; max-width: 360px; animation: barIn 240ms var(--ease-out); }
  .bar i { display: block; height: 100%; border-radius: 4px; background: var(--accent); transition: width 200ms linear; }
  .bar.indeterminate i { width: 30% !important; animation: slide 1.2s var(--ease-out) infinite; }
  @keyframes slide { from { transform: translateX(-100%); } to { transform: translateX(340%); } }
  @keyframes barIn { from { opacity: 0; transform: scaleX(0.9); } to { opacity: 1; transform: none; } }
  :global(:root[data-motion='reduced']) .bar.indeterminate i { animation-duration: 3s; }
</style>
