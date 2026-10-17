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
    <h2>Background</h2>
    <div class="row">
      <div><div class="label">Start at sign-in</div><div class="hint">Starts quietly in the tray when you sign in, so checks keep running.</div></div>
      <Toggle label="Start at sign-in" checked={s.startAtLogin} onchange={(v) => settings.update({ startAtLogin: v, backgroundPrompted: true })} />
    </div>
    <div class="row">
      <div><div class="label">Start in the tray</div><div class="hint">At sign-in, stay in the tray instead of opening the window.</div></div>
      <Toggle label="Start in the tray" checked={s.startInTray} onchange={(v) => settings.update({ startInTray: v })} />
    </div>
    <div class="row">
      <div><div class="label">Closing keeps it running</div><div class="hint">Closing the window leaves Quantum Updater in the tray. Quit from the tray icon.</div></div>
      <Toggle label="Closing keeps it running" checked={s.closeToTray} onchange={(v) => settings.update({ closeToTray: v })} />
    </div>
    <div class="row">
      <div><div class="label">Notify me about new updates</div><div class="hint">A Windows notification when new updates appear while the window is closed.</div></div>
      <Toggle label="Notify me about new updates" checked={s.notifyUpdates} onchange={(v) => settings.update({ notifyUpdates: v })} />
    </div>
    <div class="row">
      <div><div class="label">Check every</div><div class="hint">How often to look for updates in the background.</div></div>
      <Segmented label="Check every" value={String(s.checkIntervalHours)} onchange={(v) => settings.update({ checkIntervalHours: Number(v) })}
        options={[{ value: '3', label: '3 h' }, { value: '6', label: '6 h' }, { value: '12', label: '12 h' }, { value: '24', label: 'Daily' }]} />
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
    <div class="row">
      <div><div class="label">Microsoft Store apps</div><div class="hint">Find and install Store apps too. They install into your own Windows account.</div></div>
      <Toggle label="Use Microsoft Store apps" checked={s.store.enabled} onchange={(v) => settings.update({ storeEnabled: v })} />
    </div>
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
