<script lang="ts">
  import { monogram } from '../logic/monogram';
  // `lookup` is the name Windows knows the app by (helps find its installed icon); defaults to `name`.
  let { name, size = 40, iconKey, lookup }: { name: string; size?: number; iconKey?: string; lookup?: string } = $props();
  const m = $derived(monogram(name));
  // Real icon from the app itself (served by the Rust icon cache); the tile shows until it loads
  // and stays if no original icon exists.
  let failed = $state(false);
  let loaded = $state(false);
  $effect(() => { void iconKey; failed = false; loaded = false; });
  const src = $derived(iconKey ? `http://icon.localhost/${encodeURIComponent(iconKey)}?name=${encodeURIComponent(lookup ?? name)}` : null);
</script>

<div class="tile" class:has-img={loaded} style="--h:{m.hue};--s:{size}px" aria-hidden="true">
  {#if src && !failed}
    <img {src} alt="" draggable="false" loading="lazy" decoding="async"
      onload={() => (loaded = true)} onerror={() => (failed = true)} class:show={loaded} />
  {/if}
  {#if !loaded}<span>{m.letters}</span>{/if}
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
  .tile { position: relative; overflow: hidden; }
  .tile.has-img { background: transparent !important; box-shadow: none !important; border-radius: 0; }
  img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; opacity: 0; transition: opacity 180ms ease; }
  img.show { opacity: 1; }
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
