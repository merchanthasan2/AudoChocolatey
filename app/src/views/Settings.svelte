<script lang="ts">
  import { settings, overview, myBundles, toasts } from '../lib/stores/app.svelte';
  import { getDiagnostics, getAutoStatus, pickBackupFolder, backupNow, type Diagnostics, type AutoStatus } from '../lib/ipc/commands';
  import Icon from '../lib/components/Icon.svelte';
  import SearchField from '../lib/components/SearchField.svelte';
  import QuantumStepLogo from '../lib/components/QuantumStepLogo.svelte';
  import type { AutoRules } from '../lib/ipc/bindings/AutoRules';
  import { clock } from '../lib/logic/rules';
  import Segmented from '../lib/components/Segmented.svelte';
  import Toggle from '../lib/components/Toggle.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import { onMount } from 'svelte';

  let diag = $state<Diagnostics | null>(null);
  onMount(() => { getDiagnostics().then((d) => (diag = d)).catch(() => {}); });
  const s = $derived(settings.value);

  // Search: a matching section shows whole; otherwise only its matching rows show.
  let q = $state('');
  let root = $state<HTMLElement>();
  let none = $state(false);
  $effect(() => {
    void s;
    const t = q.trim().toLowerCase();
    if (!root) return;
    let anyShown = false;
    for (const sec of root.querySelectorAll<HTMLElement>('section.group')) {
      const title = (sec.querySelector('h2')?.textContent ?? '').toLowerCase();
      const whole = !t || title.includes(t);
      let shown = whole;
      for (const el of sec.querySelectorAll<HTMLElement>(':scope > .row, :scope > dl, :scope > p, :scope > .about')) {
        const hit = whole || (el.textContent ?? '').toLowerCase().includes(t);
        el.style.display = hit ? '' : 'none';
        shown ||= hit;
      }
      sec.style.display = shown ? '' : 'none';
      anyShown ||= shown;
    }
    none = !anyShown;
  });
  const sourceLabel = (k: string) => (k === 'winget' ? 'winget' : 'Chocolatey');

  // Re-read whenever the automatic choices change, so the "Waiting: …" line stays true.
  let auto = $state<AutoStatus | null>(null);
  $effect(() => {
    void settings.value?.autoRules;
    getAutoStatus().then((a) => (auto = a)).catch(() => (auto = null));
  });
  const rulesPatch = (r: AutoRules, change: Partial<AutoRules>) => settings.update({ autoRules: { ...r, ...change } });
  const hours = Array.from({ length: 24 }, (_, h) => h * 60);

  const err = (e: unknown) => (e as { message?: string })?.message ?? String(e);
  async function chooseFolder() {
    try {
      const next = await pickBackupFolder();
      if (next) settings.value = next;
    } catch (e) { toasts.push({ text: err(e), tone: 'error' }); }
  }
  let backingUp = $state(false);
  async function backup() {
    backingUp = true;
    try { toasts.push({ text: `Saved ${await backupNow()}`, tone: 'ok' }); }
    catch (e) { toasts.push({ text: err(e), tone: 'error' }); }
    finally { backingUp = false; }
  }
</script>

<header class="head">
  <h1>Settings</h1>
  <div class="find"><SearchField value={q} oninput={(v) => (q = v)} placeholder="Search settings" hint={false} /></div>
</header>

{#if s}
<div bind:this={root}>
  {#if none}<p class="muted none">No settings match "{q}".</p>{/if}
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
      <div><div class="label">Notify me when updates are ready</div><div class="hint">A Windows notification when new updates appear while the window is closed.</div></div>
      <Toggle label="Notify me when updates are ready" checked={s.notifyUpdates} onchange={(v) => settings.update({ notifyUpdates: v })} />
    </div>
    <div class="row">
      <div><div class="label">Notify me when changes finish</div><div class="hint">One notification when a batch of updates, installs or removals is done.</div></div>
      <Toggle label="Notify me when changes finish" checked={s.notifyFinished} onchange={(v) => settings.update({ notifyFinished: v })} />
    </div>
    <div class="row">
      <div><div class="label">Notify me when something fails</div><div class="hint">Tells you when an app couldn't be updated, installed or removed, so you can try again.</div></div>
      <Toggle label="Notify me when something fails" checked={s.notifyFailed} onchange={(v) => settings.update({ notifyFailed: v })} />
    </div>
    <div class="row">
      <div><div class="label">Check every</div><div class="hint">How often to look for updates in the background.</div></div>
      <Segmented label="Check every" value={String(s.checkIntervalHours)} onchange={(v) => settings.update({ checkIntervalHours: Number(v) })}
        options={[{ value: '3', label: '3 h' }, { value: '6', label: '6 h' }, { value: '12', label: '12 h' }, { value: '24', label: 'Daily' }]} />
    </div>
  </section>

  <section class="card group">
    <h2>Automatic updates</h2>
    <div class="row">
      <div>
        <div class="label">When updates are ready</div>
        <div class="hint">Automatic installs updates for you. Each app can choose for itself from its menu on the Updates page.</div>
      </div>
      <Segmented label="When updates are ready" value={s.autoUpdate} onchange={(v) => settings.update({ autoUpdate: v })}
        options={[{ value: 'notify', label: 'Ask me first' }, { value: 'automatic', label: 'Automatic' }]} />
    </div>
    {#if auto?.message}<p class="hint wait">{auto.message} Updates you start yourself still run.</p>{/if}
    <div class="row">
      <div><div class="label">Wait after a new version comes out</div><div class="hint">Automatic updates wait a little, so early problems get fixed first. Updates you start yourself never wait.</div></div>
      <Segmented label="Wait after a new version comes out" value={String(s.autoRules.minAgeDays)} onchange={(v) => rulesPatch(s.autoRules, { minAgeDays: Number(v) })}
        options={[{ value: '0', label: 'No wait' }, { value: '1', label: '1 day' }, { value: '2', label: '2 days' }, { value: '7', label: '1 week' }]} />
    </div>
    <div class="row">
      <div><div class="label">Not on battery</div><div class="hint">Wait until this PC is plugged in.</div></div>
      <Toggle label="Not on battery" checked={s.autoRules.skipOnBattery} onchange={(v) => rulesPatch(s.autoRules, { skipOnBattery: v })} />
    </div>
    <div class="row">
      <div><div class="label">Not on metered internet</div><div class="hint">Wait when the connection is a phone hotspot or has a data limit.</div></div>
      <Toggle label="Not on metered internet" checked={s.autoRules.skipOnMetered} onchange={(v) => rulesPatch(s.autoRules, { skipOnMetered: v })} />
    </div>
    <div class="row">
      <div>
        <div class="label">Quiet hours</div>
        <div class="hint">No automatic updates between these times.</div>
        {#if s.autoRules.quietHours.enabled}
          <div class="times">
            <select aria-label="Quiet hours start" value={s.autoRules.quietHours.start}
              onchange={(e) => rulesPatch(s.autoRules, { quietHours: { ...s.autoRules.quietHours, start: Number(e.currentTarget.value) } })}>
              {#each hours as m (m)}<option value={m}>{clock(m)}</option>{/each}
            </select>
            <span class="muted">to</span>
            <select aria-label="Quiet hours end" value={s.autoRules.quietHours.end}
              onchange={(e) => rulesPatch(s.autoRules, { quietHours: { ...s.autoRules.quietHours, end: Number(e.currentTarget.value) } })}>
              {#each hours as m (m)}<option value={m}>{clock(m)}</option>{/each}
            </select>
          </div>
        {/if}
      </div>
      <Toggle label="Quiet hours" checked={s.autoRules.quietHours.enabled}
        onchange={(v) => rulesPatch(s.autoRules, { quietHours: { ...s.autoRules.quietHours, enabled: v } })} />
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
    <h2>Safety</h2>
    <div class="row">
      <div><div class="label">Make a restore point first</div><div class="hint">Before a run of changes, ask Windows for a restore point, so you can undo them from System Restore. Windows makes at most one a day.</div></div>
      <Toggle label="Make a restore point first" checked={s.restorePoint} onchange={(v) => settings.update({ restorePoint: v })} />
    </div>
  </section>

  <section class="card group">
    <h2>Your apps</h2>
    <div class="row">
      <div><div class="label">Save or restore your app list</div><div class="hint">A small file naming your apps, to set up another PC or this one again. No settings or personal files are included.</div></div>
      <div class="pair">
        <button class="btn btn-quiet" onclick={() => myBundles.export({ kind: 'installed' })}><Icon name="download" size={16} /> Export</button>
        <button class="btn btn-quiet" onclick={() => myBundles.pickImport()}><Icon name="upload" size={16} /> Import</button>
      </div>
    </div>
    <div class="row">
      <div>
        <div class="label">Back up the list automatically</div>
        <div class="hint">
          {#if s.backup.folder}Saves to <span class="mono">{s.backup.folder}</span>{:else}Pick a folder, for example one that OneDrive keeps in sync.{/if}
        </div>
      </div>
      {#if s.backup.folder}
        <Toggle label="Back up the list automatically" checked={s.backup.enabled} onchange={(v) => settings.update({ backupEnabled: v })} />
      {:else}
        <button class="btn btn-quiet" onclick={chooseFolder}><Icon name="folder" size={16} /> Choose folder</button>
      {/if}
    </div>
    {#if s.backup.folder}
      <div class="row">
        <div><div class="label">How often</div><div class="hint">Keeps the newest {s.backup.keep} backups and removes older ones it made.</div></div>
        <div class="pair">
          <Segmented label="How often" value={s.backup.every} onchange={(v) => settings.update({ backupEvery: v })}
            options={[{ value: 'daily', label: 'Daily' }, { value: 'weekly', label: 'Weekly' }]} />
          <select aria-label="Backups to keep" value={s.backup.keep} onchange={(e) => settings.update({ backupKeep: Number(e.currentTarget.value) })}>
            {#each [3, 5, 10, 30] as k (k)}<option value={k}>Keep {k}</option>{/each}
          </select>
        </div>
      </div>
      <div class="row">
        <div><div class="label">Backup folder</div><div class="hint">Change where backups go, or save one now.</div></div>
        <div class="pair">
          <button class="btn btn-quiet" onclick={chooseFolder}><Icon name="folder" size={16} /> Change</button>
          <button class="btn btn-quiet" disabled={!s.backup.enabled || backingUp} onclick={backup}>{backingUp ? 'Saving…' : 'Back up now'}</button>
        </div>
      </div>
    {/if}
  </section>

  <section class="card group">
    <h2>App updates</h2>
    <div class="row">
      <div><div class="label">Updates to Quantum Updater</div><div class="hint">Quantum Updater will update itself once its releases are signed. Until then, install new versions from the website.</div></div>
      <span class="badge">Coming soon</span>
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
    <div class="about">
      <dl>
        <dt>Version</dt><dd>{diag?.appVersion ?? overview.value?.appVersion ?? '…'}</dd>
        <dt>Build</dt><dd class="mono">{overview.value?.buildSha ?? '…'}</dd>
        <dt>Running as admin</dt><dd>{overview.value ? (overview.value.isElevated ? 'Yes' : 'No (development build)') : '…'}</dd>
        <dt>Settings folder</dt><dd class="mono truncate" title={diag?.configDir}>{diag?.configDir ?? '…'}</dd>
      </dl>
      <QuantumStepLogo size={112} />
    </div>
    <p class="hint small">Quantum Updater by Quantum Step Creation. Free to use; all rights reserved.</p>
  </section>
</div>
{/if}

<style>
  .head { margin-bottom: 22px; display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; max-width: 760px; }
  .find { width: min(300px, 50%); }
  .none { padding: 12px 2px; }
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
  .about { display: flex; align-items: center; gap: 24px; }
  .about dl { flex: 1; min-width: 0; }
  .about :global(svg) { flex-shrink: 0; }
  .pair { display: flex; align-items: center; gap: 6px; flex-shrink: 0; }
  .pair .btn { gap: 6px; }
  .wait { margin: 0 0 4px; padding: 8px 12px; border-radius: 10px; background: var(--pill); color: var(--ink-2); }
  .times { display: flex; align-items: center; gap: 8px; margin-top: 8px; }
  select { height: 30px; padding: 0 8px; border-radius: 8px; border: 1px solid var(--hairline-strong); background: var(--card); color: var(--ink); font: inherit; font-size: 13px; }
  .mono { font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace; font-size: 12.5px; }
</style>
