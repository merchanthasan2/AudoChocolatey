<script lang="ts">
  import type { Snippet } from 'svelte';
  import Icon from './Icon.svelte';
  import { pop } from '../motion';
  let { icon, title, body, tone = 'neutral', children }:
    { icon: string; title: string; body?: string; tone?: 'neutral' | 'good'; children?: Snippet } = $props();
</script>

<div class="empty" in:pop>
  <div class="badge-icon" class:good={tone === 'good'}><Icon name={icon} size={22} /></div>
  <h2>{title}</h2>
  {#if body}<p class="muted">{body}</p>{/if}
  {#if children}<div class="actions">{@render children()}</div>{/if}
</div>

<style>
  .empty { display: grid; justify-items: center; text-align: center; gap: 8px; padding: 56px 24px; }
  .badge-icon { width: 52px; height: 52px; border-radius: 16px; display: grid; place-items: center; background: var(--pill); color: var(--ink-2); margin-bottom: 6px; }
  .badge-icon.good { background: var(--accent-soft); color: var(--accent); }
  p { max-width: 360px; }
  .actions { margin-top: 10px; display: flex; gap: 8px; }
</style>
