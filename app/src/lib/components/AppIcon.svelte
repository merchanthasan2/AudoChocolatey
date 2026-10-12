<script lang="ts">
  import { monogram } from '../logic/monogram';
  let { name, size = 40 }: { name: string; size?: number } = $props();
  const m = $derived(monogram(name));
</script>

<!-- Colour tile until real icons arrive (P5). Hue is stable per app name. -->
<div class="tile" style="--h:{m.hue};--s:{size}px" aria-hidden="true">
  <span>{m.letters}</span>
</div>

<style>
  .tile {
    width: var(--s); height: var(--s); flex: none;
    border-radius: var(--radius-icon);
    display: grid; place-items: center;
    background: linear-gradient(160deg, hsl(var(--h) 62% 92%), hsl(var(--h) 52% 84%));
    box-shadow: inset 0 0 0 1px hsl(var(--h) 40% 50% / 0.12);
    color: hsl(var(--h) 48% 28%);
  }
  span { font-family: var(--font-display); font-weight: 700; font-size: calc(var(--s) * 0.36); letter-spacing: -0.02em; }
  :global(:root[data-theme='dark']) .tile {
    background: linear-gradient(160deg, hsl(var(--h) 30% 30%), hsl(var(--h) 34% 22%));
    color: hsl(var(--h) 60% 86%);
    box-shadow: inset 0 0 0 1px hsl(var(--h) 50% 80% / 0.1);
  }
  @media (prefers-color-scheme: dark) {
    :global(:root:not([data-theme='light'])) .tile {
      background: linear-gradient(160deg, hsl(var(--h) 30% 30%), hsl(var(--h) 34% 22%));
      color: hsl(var(--h) 60% 86%);
      box-shadow: inset 0 0 0 1px hsl(var(--h) 50% 80% / 0.1);
    }
  }
</style>
