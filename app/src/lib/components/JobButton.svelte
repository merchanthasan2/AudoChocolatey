<script lang="ts">
  import type { Job } from '../ipc/commands';
  import { jobButtonState, type ButtonMode } from '../logic/jobButton';
  import ProgressRing from './ProgressRing.svelte';
  import { clickGuard } from '../logic/clickGuard';

  let {
    job,
    mode,
    name,
    onstart,
    oncancel,
    onretry,
  }: {
    job: Job | undefined;
    mode: ButtonMode;
    name: string;
    onstart: () => void;
    oncancel: (id: string) => void;
    onretry: (id: string) => void;
  } = $props();

  const ui = $derived(jobButtonState(job, mode));

  // The check mark is a brief moment, then the button settles into a quiet "Done" pill.
  // `kind` is a primitive, so this effect re-runs only when the state really changes
  // (job objects are replaced on every queue event).
  const kind = $derived(ui.kind);
  let celebrating = $state(false);
  let lastKind = '';
  const guard = clickGuard(500);
  $effect(() => {
    const k = kind;
    const from = lastKind;
    lastKind = k;
    guard(); // a state change also opens a short no-click window
    if (k === 'done' && from && from !== 'done' && from !== 'idle') {
      celebrating = true;
      const t = setTimeout(() => (celebrating = false), 1100);
      return () => {
        clearTimeout(t);
        celebrating = false;
      };
    }
  });

  const round = $derived(ui.kind === 'progress' || ui.kind === 'working' || celebrating);
  const width = $derived(round ? 30 : Math.max(84, Math.round(ui.label.length * 7.4 + 30)));

  function click() {
    if (!guard()) return;
    if (!job || ui.kind === 'idle') return onstart();
    if (ui.kind === 'failed') return onretry(job.id);
    if (ui.cancellable) return oncancel(job.id);
  }

  const title = $derived(
    ui.kind === 'progress'
      ? `${ui.label} ${Math.round(ui.percent ?? 0)}%${ui.cancellable ? ' · click to stop' : ''}`
      : ui.cancellable && ui.kind !== 'idle'
        ? `${ui.label} · click to stop`
        : ui.label,
  );
  const inert = $derived(
    !celebrating && (ui.kind === 'done' || ui.kind === 'skipped' || ((ui.kind === 'working' || ui.kind === 'progress') && !ui.cancellable)),
  );
</script>

<button
  class="jb"
  class:round
  data-kind={celebrating ? 'check' : ui.kind}
  style="width:{width}px"
  onclick={click}
  disabled={inert}
  {title}
  aria-label="{title}: {name}"
>
  {#if celebrating}
    <svg class="check" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 12.5l4 4L18 8" /></svg>
  {:else if ui.kind === 'progress' || ui.kind === 'working'}
    <span class="ring">
      <ProgressRing percent={ui.percent} size={30} label={ui.label} />
      {#if ui.cancellable}<span class="stop" aria-hidden="true"></span>{/if}
    </span>
  {:else}
    {#key ui.label}
      <span class="label">
        {#if ui.kind === 'queued'}<span class="dot" aria-hidden="true"></span>{/if}{ui.label}
      </span>
    {/key}
  {/if}
</button>

<style>
  .jb {
    position: relative; flex: none;
    height: 30px; border-radius: var(--radius-pill);
    display: inline-grid; place-items: center;
    font-weight: 600; font-size: 13px; white-space: nowrap;
    background: var(--pill); color: var(--ink);
    transition: width var(--dur-moment) var(--spring), background-color 160ms ease, transform var(--dur-press) var(--ease-out);
  }
  .jb:active:not(:disabled) { transform: scale(0.96); }
  .jb:disabled { cursor: default; }
  @media (hover: hover) and (pointer: fine) {
    .jb:hover:not(:disabled) { background: var(--pill-hover); }
    .jb.round:hover:not(:disabled) { background: transparent; }
  }
  .jb.round { background: transparent; }
  .jb[data-kind='queued'] { color: var(--muted); }
  .jb[data-kind='done'], .jb[data-kind='skipped'] { background: transparent; color: var(--muted); box-shadow: inset 0 0 0 1px var(--hairline-strong); }
  .jb[data-kind='failed'] { background: var(--danger-soft); color: var(--danger); animation: shake 380ms var(--ease-out); }
  .jb[data-kind='check'] { background: var(--accent); }

  .label { display: inline-flex; align-items: center; gap: 6px; animation: swap 180ms var(--ease-out); }
  .dot { width: 6px; height: 6px; border-radius: 50%; background: var(--accent); animation: breathe 1.4s ease-in-out infinite; }

  .ring { position: relative; display: grid; place-items: center; }
  .stop { position: absolute; width: 8px; height: 8px; border-radius: 2px; background: var(--accent); }

  .check { width: 18px; height: 18px; animation: pop var(--dur-moment) var(--spring); }
  .check path {
    fill: none; stroke: var(--accent-ink); stroke-width: 2.6; stroke-linecap: round; stroke-linejoin: round;
    stroke-dasharray: 20; stroke-dashoffset: 20; animation: draw 320ms var(--ease-out) 80ms forwards;
  }

  @keyframes swap { from { opacity: 0; filter: blur(2px); } to { opacity: 1; filter: blur(0); } }
  @keyframes draw { to { stroke-dashoffset: 0; } }
  @keyframes pop { from { transform: scale(0.6); opacity: 0; } to { transform: scale(1); opacity: 1; } }
  @keyframes breathe { 50% { opacity: 0.35; } }
  @keyframes shake { 20% { transform: translateX(-3px); } 40% { transform: translateX(3px); } 60% { transform: translateX(-2px); } 80% { transform: translateX(1px); } }

  :global(:root[data-motion='reduced']) .jb { transition: background-color 160ms ease; }
  :global(:root[data-motion='reduced']) .jb[data-kind='failed'],
  :global(:root[data-motion='reduced']) .check { animation: none; }
</style>
