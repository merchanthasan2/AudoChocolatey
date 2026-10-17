<script lang="ts">
  import type { SourceLabel } from '../ipc/bindings/SourceLabel';
  import type { ListView } from '../ipc/bindings/ListView';
  import type { ListState } from '../stores/app.svelte';
  import SearchField from './SearchField.svelte';
  import SourceChips from './SourceChips.svelte';
  import Segmented from './Segmented.svelte';
  import Toggle from './Toggle.svelte';

  // Search, source filter, view, and (Installed) the Windows components switch.
  let { list, counts, components, placeholder }:
    { list: ListState; counts: Record<SourceLabel | 'all', number>; components?: number; placeholder: string } = $props();
</script>

<div class="controls">
  <div class="top">
    <div class="search"><SearchField value={list.text} oninput={(v) => (list.text = v)} {placeholder} hint={false} /></div>
    <Segmented label="View" value={list.pref.view} onchange={(v: ListView) => list.set({ view: v })}
      options={[{ value: 'list', label: 'List' }, { value: 'compact', label: 'Compact' }, { value: 'grid', label: 'Grid' }]} />
  </div>
  <div class="bottom">
    <SourceChips value={list.label} {counts} onchange={(v) => (list.label = v)} />
    {#if components !== undefined}
      <label class="comp">
        <Toggle label="Hide Windows components" checked={list.pref.hideComponents} onchange={(v) => list.set({ hideComponents: v })} />
        <span>Hide Windows components{#if list.pref.hideComponents && components > 0}<span class="muted">&nbsp;· {components} hidden</span>{/if}</span>
      </label>
    {/if}
  </div>
</div>

<style>
  .controls { display: grid; gap: 10px; margin-bottom: 12px; }
  .top { display: flex; align-items: center; gap: 12px; }
  .search { flex: 1; min-width: 0; max-width: 420px; }
  .top :global(.seg) { margin-left: auto; }
  .bottom { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
  .comp { display: inline-flex; align-items: center; gap: 8px; font-size: 12.5px; font-weight: 600; color: var(--ink-2); cursor: pointer; }
</style>
