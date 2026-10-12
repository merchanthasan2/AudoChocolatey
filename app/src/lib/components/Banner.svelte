<script lang="ts">
  import Icon from './Icon.svelte';
  let { text, icon = 'alert', action }: { text: string; icon?: string; action?: { label: string; run: () => void } } = $props();
</script>

<div class="banner" role="status">
  <Icon name={icon} size={16} />
  <span class="text">{text}</span>
  {#if action}<button class="link" onclick={action.run}>{action.label}</button>{/if}
</div>

<style>
  .banner {
    display: flex; align-items: center; gap: 10px;
    padding: 10px 14px; border-radius: 10px;
    background: var(--warn-soft); color: var(--warn-ink); font-size: 13px;
  }
  .text { flex: 1; }
  .link { font-weight: 600; text-decoration: underline; text-underline-offset: 3px; }
</style>
