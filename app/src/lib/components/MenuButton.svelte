<script lang="ts" module>
  export type MenuItem = { text: string; run: () => void; disabled?: boolean; danger?: boolean };
</script>

<script lang="ts">
  import type { Snippet } from 'svelte';
  import { fade } from 'svelte/transition';

  // A button that opens a menu. The menu is moved to <body> and fixed to the window, so list
  // cards (which clip their rows) and animated containers can't cut it off or misplace it.
  let { label, items, trigger, triggerClass = 'btn btn-quiet', align = 'right' }:
    { label: string; items: MenuItem[]; trigger: Snippet; triggerClass?: string; align?: 'left' | 'right' } = $props();
  let open = $state(false);
  let btn = $state<HTMLButtonElement>();
  let menu = $state<HTMLElement>();
  let pos = $state({ top: 0, x: 0 });

  function portal(node: HTMLElement) {
    document.body.appendChild(node);
    return { destroy: () => node.remove() };
  }

  function show() {
    if (!btn) return;
    const r = btn.getBoundingClientRect();
    pos = { top: r.bottom + 6, x: align === 'right' ? window.innerWidth - r.right : r.left };
    open = true;
    queueMicrotask(() => focusAt(0));
  }
  function close(refocus = false) {
    open = false;
    if (refocus) btn?.focus();
  }
  function choose(it: MenuItem) {
    if (it.disabled) return;
    close(true);
    it.run();
  }
  const buttons = () => [...(menu?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)') ?? [])];
  function focusAt(i: number) {
    const b = buttons();
    if (b.length) b[(i + b.length) % b.length].focus();
  }
  function keys(e: KeyboardEvent) {
    const b = buttons();
    const at = b.indexOf(document.activeElement as HTMLButtonElement);
    if (e.key === 'ArrowDown') { e.preventDefault(); focusAt(at + 1); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); focusAt(at - 1); }
    else if (e.key === 'Home') { e.preventDefault(); focusAt(0); }
    else if (e.key === 'End') { e.preventDefault(); focusAt(-1); }
    else if (e.key === 'Tab') close();
  }
</script>

<svelte:window
  onkeydown={(e) => { if (open && e.key === 'Escape') close(true); }}
  onpointerdown={(e) => { if (open && !btn?.contains(e.target as Node) && !menu?.contains(e.target as Node)) close(); }}
  onscrollcapture={(e) => { if (open && !menu?.contains(e.target as Node)) close(); }}
  onresize={() => close()} />

<button class={triggerClass} bind:this={btn} aria-label={label} aria-haspopup="menu" aria-expanded={open}
  onclick={() => (open ? close() : show())}
  onkeydown={(e) => { if (!open && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) { e.preventDefault(); show(); } }}>
  {@render trigger()}
</button>
{#if open}
  <div class="menu card" role="menu" tabindex="-1" aria-label={label} use:portal bind:this={menu} onkeydown={keys}
    style="top:{pos.top}px;{align === 'right' ? 'right' : 'left'}:{pos.x}px" transition:fade={{ duration: 120 }}>
    {#each items as it (it.text)}
      <button role="menuitem" class:danger={it.danger} disabled={it.disabled} onclick={() => choose(it)}>{it.text}</button>
    {/each}
  </div>
{/if}

<style>
  .menu { position: fixed; z-index: 30; min-width: 200px; max-width: 320px; padding: 6px; box-shadow: var(--shadow-raised); }
  .menu button { display: block; width: 100%; text-align: left; padding: 8px 10px; border-radius: 8px; font-size: 13px; color: var(--ink-2); }
  .menu button:hover:not(:disabled), .menu button:focus-visible { background: var(--pill); color: var(--ink); outline: none; }
  .menu button:disabled { opacity: 0.45; cursor: default; }
  .menu button.danger { color: var(--danger); }
</style>
