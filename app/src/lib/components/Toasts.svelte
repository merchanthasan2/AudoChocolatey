<script lang="ts">
  import { toasts } from '../stores/app.svelte';
  import Icon from './Icon.svelte';
  import { rise } from '../motion';
  import { fade } from 'svelte/transition';
</script>

<div class="stack" aria-live="polite">
  {#each toasts.list as t (t.id)}
    <div class="toast" class:error={t.tone === 'error'} in:rise={{ y: 14 }} out:fade={{ duration: 160 }}>
      <span class="ic"><Icon name={t.tone === 'error' ? 'alert' : 'check'} size={15} stroke={2.2} /></span>
      <span class="text">{t.text}</span>
      {#if t.action}
        <button class="act" onclick={() => { t.action?.run(); toasts.dismiss(t.id); }}>{t.action.label}</button>
      {/if}
      <button class="close" aria-label="Dismiss" onclick={() => toasts.dismiss(t.id)}><Icon name="x" size={13} /></button>
    </div>
  {/each}
</div>

<style>
  .stack { position: fixed; right: 20px; bottom: 20px; display: grid; gap: 8px; z-index: 50; width: min(380px, calc(100vw - 40px)); }
  .toast {
    display: flex; align-items: center; gap: 10px;
    padding: 11px 12px 11px 14px; border-radius: 12px;
    background: var(--ink); color: var(--canvas); box-shadow: var(--shadow-raised); font-size: 13px;
  }
  .ic { display: grid; place-items: center; width: 22px; height: 22px; border-radius: 50%; background: var(--accent); color: var(--accent-ink); flex: none; }
  .error .ic { background: var(--danger); color: #fff; }
  .text { flex: 1; min-width: 0; }
  .act { font-weight: 700; color: var(--canvas); text-decoration: underline; text-underline-offset: 3px; }
  .close { opacity: 0.6; display: grid; place-items: center; width: 22px; height: 22px; border-radius: 50%; color: var(--canvas); }
  .close:hover { opacity: 1; }
</style>
