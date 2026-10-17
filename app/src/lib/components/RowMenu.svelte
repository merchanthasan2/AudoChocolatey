<script lang="ts">
  import Icon from './Icon.svelte';
  import MenuButton, { type MenuItem } from './MenuButton.svelte';
  // The "…" menu on a row.
  let { label, items }: { label: string; items: MenuItem[] } = $props();
</script>

<MenuButton {label} {items} triggerClass="dots">
  {#snippet trigger()}<Icon name="more" size={18} stroke={3} />{/snippet}
</MenuButton>

<style>
  :global(.dots) { width: 32px; height: 32px; border-radius: 50%; display: grid; place-items: center; color: var(--muted); flex-shrink: 0; }
  :global(.dots:hover), :global(.dots[aria-expanded='true']) { background: var(--pill); color: var(--ink); }
</style>
