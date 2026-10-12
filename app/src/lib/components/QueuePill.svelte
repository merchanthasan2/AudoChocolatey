<script lang="ts">
  import { queue } from '../stores/app.svelte';
  import ProgressRing from './ProgressRing.svelte';
  import Icon from './Icon.svelte';
  import { jobButtonState } from '../logic/jobButton';
  let { onopen }: { onopen: () => void } = $props();

  const current = $derived(queue.current);
  const waiting = $derived(queue.active.length - (current ? 1 : 0));
  const state = $derived(current ? jobButtonState(current, 'install') : null);
  const finishedCount = $derived(queue.jobs.length - queue.active.length);
</script>

{#if current || waiting > 0 || finishedCount > 0}
  <button class="pill" onclick={onopen} aria-label="Open activity">
    {#if current && state}
      <ProgressRing percent={state.percent} size={22} label={state.label} />
      <span class="text">
        <span class="title truncate">{state.label} {current.displayName}</span>
        <span class="sub">{waiting > 0 ? `${waiting} more waiting` : 'Click to see details'}</span>
      </span>
    {:else if waiting > 0}
      <Icon name="queue" size={18} />
      <span class="text"><span class="title">{waiting} waiting</span><span class="sub">Starting soon</span></span>
    {:else}
      <Icon name="queue" size={18} />
      <span class="text"><span class="title">Activity</span><span class="sub">{finishedCount} finished</span></span>
    {/if}
  </button>
{/if}

<style>
  .pill {
    display: flex; align-items: center; gap: 10px; width: 100%;
    padding: 10px 12px; border-radius: 12px; text-align: left;
    background: var(--card); box-shadow: var(--shadow-card); color: var(--ink-2);
    animation: enter var(--dur-moment) var(--spring);
    transition: transform var(--dur-press) var(--ease-out);
  }
  .pill:active { transform: scale(0.98); }
  .text { display: grid; min-width: 0; flex: 1; }
  .title { font-weight: 600; font-size: 12.5px; color: var(--ink); }
  .sub { font-size: 11.5px; color: var(--muted); }
  @keyframes enter { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
</style>
