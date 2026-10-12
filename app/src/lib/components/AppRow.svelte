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
    action,
  }: { name: string; meta: string; job?: Job; mode?: ButtonMode; action?: Snippet } = $props();

  // While a job runs, the meta line becomes a live status ("Downloading · 1.9 MB").
  const status = $derived.by(() => {
    if (!job || !mode) return null;
    const s = jobButtonState(job, mode);
    if (s.kind === 'idle' || s.kind === 'done' || s.kind === 'skipped') return null;
    if (s.kind === 'failed') return job.error ?? 'Something went wrong';
    const size = job.bytesTotal ? ` · ${formatBytes(job.bytesTotal)}` : '';
    const pct = s.percent != null ? ` · ${Math.round(s.percent)}%` : '';
    return `${s.label}${size}${pct}`;
  });
</script>

<div class="row">
  <AppIcon {name} />
  <div class="text">
    <div class="name truncate" title={name}>{name}</div>
    <div class="meta truncate" class:live={status} class:bad={job?.phase === 'failed'}>{status ?? meta}</div>
  </div>
  {#if action}{@render action()}{/if}
</div>

<style>
  .row {
    display: flex; align-items: center; gap: 14px;
    padding: 12px 16px; min-height: 64px;
    content-visibility: auto; contain-intrinsic-size: auto 64px;
  }
  .row + :global(.row) { border-top: 1px solid var(--hairline); }
  .text { flex: 1; min-width: 0; }
  .name { font-weight: 600; font-size: 14px; }
  .meta { color: var(--muted); font-size: 12.5px; margin-top: 1px; }
  .meta.live { color: var(--accent); }
  .meta.bad { color: var(--danger); }
</style>
