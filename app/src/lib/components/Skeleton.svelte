<script lang="ts">
  let { rows = 5 }: { rows?: number } = $props();
</script>

<div class="card list" aria-busy="true" aria-label="Loading">
  {#each Array(rows) as _, i (i)}
    <div class="row" style="--d:{i * 70}ms">
      <div class="block icon"></div>
      <div class="lines">
        <div class="block line" style="width:{38 + ((i * 17) % 30)}%"></div>
        <div class="block line small" style="width:{22 + ((i * 11) % 18)}%"></div>
      </div>
      <div class="block pill"></div>
    </div>
  {/each}
</div>

<style>
  .row { display: flex; align-items: center; gap: 14px; padding: 12px 16px; height: 64px; }
  .row + .row { border-top: 1px solid var(--hairline); }
  .lines { flex: 1; display: grid; gap: 7px; }
  .block {
    background: linear-gradient(90deg, var(--pill) 0%, var(--canvas-2) 50%, var(--pill) 100%);
    background-size: 200% 100%;
    animation: shimmer 1.3s linear infinite;
    animation-delay: var(--d);
    border-radius: 6px;
  }
  .icon { width: 40px; height: 40px; border-radius: var(--radius-icon); }
  .line { height: 11px; }
  .line.small { height: 9px; }
  .pill { width: 84px; height: 30px; border-radius: 999px; }
  @keyframes shimmer { from { background-position: 200% 0; } to { background-position: -200% 0; } }
  :global(:root[data-motion='reduced']) .block { animation: none; }
</style>
