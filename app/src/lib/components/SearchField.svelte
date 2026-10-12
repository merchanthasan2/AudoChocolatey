<script lang="ts">
  import Icon from './Icon.svelte';
  let {
    value,
    oninput,
    placeholder = 'Search apps',
    hint = true,
    input = $bindable(),
  }: { value: string; oninput: (v: string) => void; placeholder?: string; hint?: boolean; input?: HTMLInputElement } = $props();
</script>

<label class="field">
  <Icon name="search" size={17} />
  <input bind:this={input} type="search" {placeholder} {value} spellcheck="false" autocomplete="off"
    oninput={(e) => oninput(e.currentTarget.value)}
    onkeydown={(e) => { if (e.key === 'Escape') { oninput(''); } }} />
  {#if value}
    <button class="clear" aria-label="Clear search" onclick={() => { oninput(''); input?.focus(); }}><Icon name="x" size={14} /></button>
  {:else if hint}
    <kbd>Ctrl K</kbd>
  {/if}
</label>

<style>
  .field {
    display: flex; align-items: center; gap: 10px;
    height: 46px; padding: 0 14px; border-radius: 12px;
    background: var(--card); box-shadow: var(--shadow-card); color: var(--muted);
    transition: box-shadow var(--dur-quick) ease;
  }
  .field:focus-within { box-shadow: 0 0 0 2px var(--accent), var(--shadow-card); }
  input { flex: 1; border: 0; outline: 0; background: transparent; font-size: 15px; color: var(--ink); min-width: 0; }
  input::placeholder { color: var(--faint); }
  input::-webkit-search-cancel-button { display: none; }
  kbd { font-family: inherit; font-size: 11px; font-weight: 600; color: var(--faint); border: 1px solid var(--hairline-strong); border-radius: 6px; padding: 2px 6px; }
  .clear { display: grid; place-items: center; width: 24px; height: 24px; border-radius: 50%; color: var(--muted); }
  .clear:hover { background: var(--pill); }
</style>
