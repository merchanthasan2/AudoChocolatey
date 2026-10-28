<script lang="ts">
  import type { BundleView, MyBundleView } from '../ipc/commands';
  import { queue, installed, updates, overview, toasts, myBundles } from '../stores/app.svelte';
  import { clickGuard } from '../logic/clickGuard';
  import { installedSet, withJobs } from '../logic/entries';
  import { bundleRows } from '../logic/bundleView';
  import AppIcon from './AppIcon.svelte';
  import RowMenu from './RowMenu.svelte';
  import BundleDialog from './BundleDialog.svelte';

  // `mine`: one of the person's own bundles (no blurb; can be exported or deleted).
  let { bundle, mine = false }: { bundle: BundleView | MyBundleView; mine?: boolean } = $props();
  const blurb = $derived('blurb' in bundle ? bundle.blurb : `${bundle.apps.length} ${bundle.apps.length === 1 ? 'app' : 'apps'}`);
  // Apps we don't know show their package name, so a list can't pass one app off as another.
  const label = (a: BundleView['apps'][number]) => (a.curated ? a.name : `${a.name} (${a.winget ?? a.choco ?? a.msstore})`);
  // Until the installed list is in, "missing" would mean "everything".
  const ready = $derived(installed.loaded && !installed.error);
  const missing = $derived(queue.bundleMissing(bundle.apps));
  const updateCount = $derived(
    bundleRows(bundle.apps, withJobs(installedSet(installed.items), queue.jobs), updates.items, overview.on)
      .filter((r) => r.state === 'update').length,
  );
  const guard = clickGuard(1000);
  let open = $state(false);

  async function installAll() {
    if (!guard()) return;
    const n = await queue.installMany(bundle.apps);
    if (n > 0) toasts.push({ text: `Installing ${n} ${n === 1 ? 'app' : 'apps'} from ${bundle.name}`, tone: 'ok' });
  }
</script>

<article class="bundle card">
  <button class="hit" aria-label="Open {bundle.name}" onclick={() => (open = true)}></button>
  <div class="icons" aria-hidden="true">
    {#each bundle.apps.slice(0, 6) as a (a.key)}
      <AppIcon name={a.name} size={34} iconKey={a.iconKey} />
    {/each}
  </div>
  <div class="title">
    <h3>{bundle.name}</h3>
    {#if mine}
      <RowMenu label="More options for {bundle.name}" items={[
        { text: 'Export to a file', run: () => myBundles.export({ kind: 'bundle', id: bundle.id }) },
        { text: 'Delete this bundle', run: () => myBundles.remove(bundle.id) },
      ]} />
    {/if}
  </div>
  <p class="muted">{blurb}</p>
  <p class="list">{bundle.apps.map(label).join(' · ')}</p>
  <div class="foot">
    {#if !ready}
      <button class="btn btn-primary" disabled>{installed.error ? "Couldn't check installed apps" : 'Checking your apps…'}</button>
    {:else}
      {#if updateCount > 0}<span class="badge">{updateCount} {updateCount === 1 ? 'update' : 'updates'}</span>{/if}
      {#if missing.length > 0}
        <button class="btn btn-primary" onclick={installAll}>
          {missing.length === bundle.apps.length ? `Install ${missing.length} apps` : `Install ${missing.length} missing`}
        </button>
      {:else if updateCount === 0}
        <span class="badge">All installed</span>
      {/if}
    {/if}
  </div>
  {#if open}<BundleDialog {bundle} {mine} onclose={() => (open = false)} />{/if}
</article>

<style>
  .bundle {
    position: relative; padding: 18px; display: grid; gap: 6px; align-content: start;
    transition: transform var(--dur-quick) var(--ease-out), box-shadow var(--dur-quick) ease;
  }
  @media (hover: hover) and (pointer: fine) {
    .bundle:hover { box-shadow: var(--shadow-raised); transform: translateY(-1px); }
  }
  .hit { position: absolute; inset: 0; border-radius: inherit; z-index: 0; }
  .icons { display: flex; gap: 6px; margin-bottom: 8px; pointer-events: none; }
  .title { position: relative; z-index: 1; display: flex; align-items: center; justify-content: space-between; gap: 8px; pointer-events: none; }
  .title :global(button) { pointer-events: auto; }
  h3 { font-family: var(--font-display); font-size: 16px; font-weight: 700; letter-spacing: -0.02em; margin: 0; pointer-events: none; }
  .muted, .list { pointer-events: none; }
  .list { font-size: 12px; color: var(--ink-2); }
  .foot { position: relative; z-index: 1; margin-top: 8px; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; pointer-events: none; }
  .foot :global(button) { pointer-events: auto; }
</style>
