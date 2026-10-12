<script lang="ts">
  // Determinate only when a real percentage exists; otherwise an honest spinning arc.
  let { percent = null, size = 28, label = 'Progress' }: { percent?: number | null; size?: number; label?: string } = $props();
  const r = 11;
  const c = 2 * Math.PI * r;
</script>

<svg width={size} height={size} viewBox="0 0 28 28" class:spin={percent == null} role="progressbar"
  aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent ?? undefined}>
  <circle cx="14" cy="14" {r} class="track" />
  <circle cx="14" cy="14" {r} class="arc" stroke-dasharray={c}
    stroke-dashoffset={percent == null ? c * 0.78 : c * (1 - Math.min(100, Math.max(0, percent)) / 100)} />
</svg>

<style>
  svg { display: block; }
  circle { fill: none; stroke-width: 2.4; }
  .track { stroke: var(--hairline-strong); }
  .arc {
    stroke: var(--accent); stroke-linecap: round;
    transform: rotate(-90deg); transform-origin: 50% 50%;
    transition: stroke-dashoffset 160ms linear;
  }
  .spin .arc { animation: spin 0.85s linear infinite; }
  @keyframes spin { to { transform: rotate(270deg); } }
  :global(:root[data-motion='reduced']) .spin .arc { animation-duration: 2.4s; }
</style>
