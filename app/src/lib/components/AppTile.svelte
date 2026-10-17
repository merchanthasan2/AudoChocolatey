<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { Job } from '../ipc/commands';
  import AppIcon from './AppIcon.svelte';
  import { jobButtonState, type ButtonMode } from '../logic/jobButton';

  // One app in the grid view.
  let { name, meta, note, iconKey, job, mode, lead, action }:
    { name: string; meta: string; note?: string; iconKey?: string; job?: Job; mode?: ButtonMode; lead?: Snippet; action?: Snippet } = $props();
  const ui = $derived(job && mode ? jobButtonState(job, mode) : null);
  const running = $derived(ui?.kind === 'progress' || ui?.kind === 'working' || ui?.kind === 'queued');
</script>

<article class="tile card" class:active={running}>
  {#if lead}<div class="lead">{@render lead()}</div>{/if}
  <AppIcon {name} {iconKey} size={48} />
  <div class="name truncate" title={name}>{name}</div>
  <div class="meta truncate">{meta}</div>
  {#if note}<div class="note truncate" title={note}>{note}</div>{/if}
  {#if action}<div class="action">{@render action()}</div>{/if}
  {#if running}
    <div class="bar" class:indeterminate={ui?.percent == null} role="presentation"><i style="width:{ui?.percent ?? 30}%"></i></div>
  {/if}
</article>

<style>
  .tile { position: relative; display: grid; justify-items: start; gap: 4px; padding: 16px; min-width: 0; overflow: hidden; transition: background-color 200ms ease; }
  .tile.active { background: color-mix(in srgb, var(--accent-soft) 45%, var(--card)); }
  .lead { position: absolute; top: 12px; right: 12px; }
  .name { font-weight: 650; margin-top: 8px; max-width: 100%; }
  .meta { color: var(--muted); font-size: 12.5px; max-width: 100%; }
  .note { color: var(--ink-2); font-size: 12px; max-width: 100%; }
  .action { margin-top: 10px; display: flex; gap: 4px; align-items: center; }
  .bar { position: absolute; left: 0; right: 0; bottom: 0; height: 3px; background: var(--hairline-strong); overflow: hidden; }
  .bar i { display: block; height: 100%; background: var(--accent); transition: width 200ms linear; }
  .bar.indeterminate i { width: 30% !important; animation: slide 1.2s var(--ease-out) infinite; }
  @keyframes slide { from { transform: translateX(-100%); } to { transform: translateX(340%); } }
</style>
