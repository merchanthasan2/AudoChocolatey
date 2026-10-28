<script lang="ts">
  import { onMount } from 'svelte';
  import { overview, router, toasts } from '../lib/stores/app.svelte';
  import { listStartup, setStartup, type StartupItem, type StartupRef } from '../lib/ipc/commands';
  import type { StartupLock } from '../lib/ipc/bindings/StartupLock';
  import AppIcon from '../lib/components/AppIcon.svelte';
  import SearchField from '../lib/components/SearchField.svelte';
  import Toggle from '../lib/components/Toggle.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';

  let items = $state<StartupItem[] | null>(null);
  let warning = $state<string | null>(null);
  let perUser = $state(true);
  let error = $state<string | null>(null);
  let q = $state('');
  let busy = $state<string | null>(null);

  const note: Record<StartupLock, string> = {
    windows: 'Windows manages this',
    security: 'Security software stays on',
    settings: 'Change this in Settings',
    policy: 'Managed by policy',
    unchecked: 'Left as it is',
  };

  const keyOf = (it: StartupRef) => `${it.place}\u0000${it.folder}\u0000${it.name}`;
  const shown = $derived.by(() => {
    const all = items ?? [];
    const t = q.trim().toLowerCase();
    if (!t) return all;
    return all.filter((it) => `${it.label} ${it.detail}`.toLowerCase().includes(t));
  });

  async function load() {
    try {
      const list = await listStartup();
      items = list.items;
      warning = list.warning;
      perUser = list.perUser;
      error = null;
    } catch (e) {
      error = (e as { message?: string })?.message ?? String(e);
      items ??= [];
    }
  }
  onMount(load);

  async function toggle(it: StartupItem, enabled: boolean) {
    if (it.lock || busy) return;
    const id = keyOf(it);
    busy = id;
    const prior = items;
    items = (items ?? []).map((x) => (keyOf(x) === id ? { ...x, enabled } : x));
    try {
      const next = await setStartup({ place: it.place, name: it.name, folder: it.folder }, enabled);
      items = (items ?? []).map((x) => (keyOf(x) === id ? next : x));
    } catch (e) {
      items = prior;
      toasts.push({ text: (e as { message?: string })?.message ?? String(e), tone: 'error' }, 8000);
    } finally {
      busy = null;
    }
  }
</script>

<header class="head">
  <div>
    <h1>Startup</h1>
    <p class="muted">Apps that open when you sign in. Turning one off stops it from opening. Nothing is removed.</p>
  </div>
  <div class="find">
    <SearchField value={q} oninput={(v) => (q = v)} placeholder="Search startup" hint={false} />
  </div>
</header>

{#if !perUser}
  <div class="gap"><Banner icon="alert" text="You're running as a different administrator, so only startup items for everyone on this PC are shown." /></div>
{/if}
{#if warning}
  <div class="gap"><Banner text={warning} action={{ label: 'Try again', run: load }} /></div>
{/if}
{#if error}
  <div class="gap"><Banner text={error} action={{ label: 'Try again', run: load }} /></div>
{/if}

{#if items === null}
  <Skeleton rows={6} />
{:else if items.length === 0}
  <EmptyState icon="power" title="Nothing starts with Windows" body="Apps set to open when you sign in will be listed here." />
{:else if shown.length === 0}
  <p class="muted none">No startup items match "{q}".</p>
{:else}
  <div class="card list">
    {#each shown as it (keyOf(it))}
      <div class="row">
        <AppIcon name={it.label} size={36} />
        <div class="text">
          <div class="name">{it.label}</div>
          <div class="meta">
            {it.detail}
            {#if it.lock === 'settings'}
              · <button class="why" onclick={() => router.go('settings')}>{note.settings}</button>
            {:else if it.lock}
              · {note[it.lock]}
            {/if}
          </div>
        </div>
        {#if it.lock}
          <span class="held">{it.enabled ? 'On' : 'Off'}</span>
        {:else}
          <Toggle label="{it.enabled ? 'On' : 'Off'} for {it.label}" checked={it.enabled} onchange={(v) => toggle(it, v)} />
        {/if}
      </div>
    {/each}
  </div>
{/if}

<style>
  .head { margin-bottom: 18px; display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; }
  .head p { margin-top: 4px; max-width: 560px; }
  .find { width: min(280px, 46%); flex: none; }
  .gap { margin-bottom: 12px; }
  .none { padding: 8px 2px; }
  .row { display: flex; align-items: center; gap: 14px; padding: 12px 16px; }
  .row + .row { border-top: 1px solid var(--hairline); }
  .text { min-width: 0; flex: 1; }
  .name { font-weight: 600; }
  .meta { font-size: 12.5px; color: var(--muted); }
  .why { color: inherit; font: inherit; text-align: left; }
  .why:hover { text-decoration: underline; text-underline-offset: 3px; }
  .held { font-size: 12px; font-weight: 650; color: var(--faint); min-width: 40px; text-align: right; }
</style>
