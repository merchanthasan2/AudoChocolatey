<script lang="ts">
  import { settings, overview } from '../lib/stores/app.svelte';
  import { getDiagnostics, type Diagnostics } from '../lib/ipc/commands';
  import Segmented from '../lib/components/Segmented.svelte';
  import Toggle from '../lib/components/Toggle.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import { onMount } from 'svelte';

  let diag = $state<Diagnostics | null>(null);
  onMount(() => { getDiagnostics().then((d) => (diag = d)).catch(() => {}); });
  const s = $derived(settings.value);
  const sourceLabel = (k: string) => (k === 'winget' ? 'winget' : 'Chocolatey');
</script>

<header class="head"><h1>Settings</h1></header>

{#if s}
  {#if diag?.settingsReadOnly}
    <div class="gap"><Banner text="Your settings were saved by a newer version, so changes can't be saved here." /></div>
  {/if}

  <section class="card group">
    <h2>Appearance</h2>
    <div class="row">
      <div><div class="label">Theme</div><div class="hint">Follow Windows, or pick one.</div></div>
      <Segmented label="Theme" value={s.theme} onchange={(v) => settings.update({ theme: v })}
        options={[{ value: 'system', label: 'System' }, { value: 'light', label: 'Light' }, { value: 'dark', label: 'Dark' }]} />
    </div>
    <div class="row">
      <div><div class="label">Motion</div><div class="hint">Reduced swaps movement for gentle fades.</div></div>
      <Segmented label="Motion" value={s.motion} onchange={(v) => settings.update({ motion: v })}
        options={[{ value: 'system', label: 'System' }, { value: 'full', label: 'Full' }, { value: 'reduced', label: 'Reduced' }]} />
    </div>
  </section>

  <section class="card group">
    <h2>Updates</h2>
    <div class="row">
      <div><div class="label">Show apps with unknown versions</div><div class="hint">Some apps don't report a version, so updates can't be confirmed.</div></div>
      <Toggle label="Show apps with unknown versions" checked={s.showUnknownVersions} onchange={(v) => settings.update({ showUnknownVersions: v })} />
    </div>
  </section>

  <section class="card group">
    <h2>Advanced: app sources</h2>
    <p class="hint">Where Quantum Updater finds and installs apps. Most people never need to change these.</p>
    {#each overview.value?.sources ?? [] as src (src.kind)}
      <div class="row">
        <div>
          <div class="label">{sourceLabel(src.kind)}</div>
          <div class="hint">
            {#if src.state === 'ready'}Version {src.version}{:else if src.state === 'missing'}Not installed{:else}Needs attention: {src.reason}{/if}
          </div>
        </div>
        {#if src.kind === 'choco'}
          <Toggle label="Use Chocolatey" checked={s.choco.enabled} onchange={(v) => settings.update({ chocoEnabled: v })} />
        {:else}
          <span class="badge">{src.state === 'ready' ? 'Ready' : 'Missing'}</span>
        {/if}
      </div>
    {/each}
  </section>

  <section class="card group">
    <h2>About</h2>
    <dl>
      <dt>Version</dt><dd>{diag?.appVersion ?? overview.value?.appVersion ?? '…'}</dd>
      <dt>Build</dt><dd class="mono">{overview.value?.buildSha ?? '…'}</dd>
      <dt>Running as admin</dt><dd>{overview.value ? (overview.value.isElevated ? 'Yes' : 'No (development build)') : '…'}</dd>
      <dt>Settings folder</dt><dd class="mono truncate" title={diag?.configDir}>{diag?.configDir ?? '…'}</dd>
    </dl>
    <p class="hint small">Quantum Updater by Quantum Step. Free to use; all rights reserved.</p>
  </section>
{/if}

<style>
  .head { margin-bottom: 22px; }
  .gap { margin-bottom: 12px; }
  .group { padding: 18px 20px; margin-bottom: 14px; display: grid; gap: 2px; max-width: 760px; }
  .group h2 { margin-bottom: 8px; }
  .row { display: flex; align-items: center; justify-content: space-between; gap: 24px; padding: 12px 0; }
  .row + .row { border-top: 1px solid var(--hairline); }
  .label { font-weight: 600; }
  .hint { color: var(--muted); font-size: 12.5px; margin-top: 1px; }
  .hint.small { margin-top: 12px; }
  dl { display: grid; grid-template-columns: 160px 1fr; gap: 8px 16px; margin: 4px 0 0; }
  dt { color: var(--muted); }
  dd { margin: 0; font-weight: 550; min-width: 0; }
  .mono { font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace; font-size: 12.5px; }
</style>
