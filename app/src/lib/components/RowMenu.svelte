<script lang="ts">
  import { fade } from 'svelte/transition';
  import Icon from './Icon.svelte';
  let { label, items }: { label: string; items: { text: string; run: () => void }[] } = $props();
  let open = $state(false);
  let root = $state<HTMLElement>();
  let btn = $state<HTMLButtonElement>();
  let menu = $state<HTMLElement>();
  // Fixed to the window, so list cards (which clip their rows) can't cut the menu off.
  let pos = $state({ top: 0, right: 0 });

  // Rows sit inside transformed (animated) containers, where position: fixed stops meaning the
  // window; the open menu is moved to <body> so it always lines up with its button.
  function portal(node: HTMLElement) {
    document.body.appendChild(node);
    return { destroy: () => node.remove() };
  }

  function toggle() {
    if (!open && btn) {
      const r = btn.getBoundingClientRect();
      pos = { top: r.bottom + 6, right: window.innerWidth - r.right };
    }
    open = !open;
  }

  function choose(run: () => void) {
    open = false;
    run();
  }
</script>

<svelte:window
  onkeydown={(e) => { if (open && e.key === 'Escape') open = false; }}
  onpointerdown={(e) => { if (open && root && !root.contains(e.target as Node) && !menu?.contains(e.target as Node)) open = false; }}
  onscrollcapture={() => (open = false)}
  onresize={() => (open = false)} />

<div class="wrap" bind:this={root}>
  <button class="dots" bind:this={btn} aria-label={label} aria-haspopup="menu" aria-expanded={open} onclick={toggle}>
    <Icon name="more" size={18} stroke={3} />
  </button>
  {#if open}
    <div class="menu card" role="menu" use:portal bind:this={menu} style="top:{pos.top}px;right:{pos.right}px" transition:fade={{ duration: 120 }}>
      {#each items as it (it.text)}
        <button role="menuitem" onclick={() => choose(it.run)}>{it.text}</button>
      {/each}
    </div>
  {/if}
</div>

<style>
  .wrap { position: relative; }
  .dots { width: 32px; height: 32px; border-radius: 50%; display: grid; place-items: center; color: var(--muted); }
  .dots:hover, .dots[aria-expanded='true'] { background: var(--pill); color: var(--ink); }
  .menu {
    position: fixed; z-index: 30; min-width: 190px; padding: 6px;
    box-shadow: var(--shadow-raised); transform-origin: top right;
  }
  .menu button { display: block; width: 100%; text-align: left; padding: 8px 10px; border-radius: 8px; font-size: 13px; color: var(--ink-2); }
  .menu button:hover, .menu button:focus-visible { background: var(--pill); color: var(--ink); }
</style>
