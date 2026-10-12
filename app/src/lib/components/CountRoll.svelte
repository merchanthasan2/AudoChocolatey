<script lang="ts">
  // The number rolls when it changes (springy), so "Update all" visibly counts down.
  let { value }: { value: number } = $props();
</script>

<span class="roll" aria-live="polite">
  {#key value}
    <span class="digit">{value}</span>
  {/key}
</span>

<style>
  .roll { display: inline-grid; overflow: hidden; vertical-align: bottom; height: 1.15em; line-height: 1.15em; }
  .digit { grid-area: 1 / 1; animation: in var(--dur-moment) var(--spring); }
  @keyframes in { from { transform: translateY(80%); opacity: 0; } to { transform: none; opacity: 1; } }
  :global(:root[data-motion='reduced']) .digit { animation: none; }
</style>
