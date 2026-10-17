<script lang="ts">
  import type { BundleView, MyBundleView } from '../ipc/commands';
  import { queue, installed, toasts, myBundles } from '../stores/app.svelte';
  import { clickGuard } from '../logic/clickGuard';
  import AppIcon from './AppIcon.svelte';
  import RowMenu from './RowMenu.svelte';

  // `mine`: one of the person's own bundles (no blurb; can be exported or deleted).
  let { bundle, mine = false }: { bundle: BundleView | MyBundleView; mine?: boolean } = $props();
  const blurb = $derived('blurb' in bundle ? bundle.blurb : `${bundle.apps.length} ${bundle.apps.length === 1 ? 'app' : 'apps'}`);
  // Apps we don't know show their package name, so a list can't pass one app off as another.
  const label = (a: BundleView['apps'][number]) => (a.curated ? a.name : `${a.name} (${a.winget ?? a.choco ?? a.msstore})`);
  // Until the installed list is in, "missing" would mean "everything".
  const ready = $derived(installed.loaded && !installed.error);
  const missing = $derived(queue.bundleMissing(bundle.apps));
  const guard = clickGuard(1000);

  async function installAll() {
    if (!guard()) return;
    const n = await queue.installMany(bundle.apps);
    if (n > 0) toasts.push({ text: `Installing ${n} ${n === 1 ? 'app' : 'apps'} from ${bundle.name}`, tone: 'ok' });
  }
</script>

<article class="bundle card">
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
    {:else if missing.length === 0}
      <span class="badge">All installed</span>
    {:else}
      <button class="btn btn-primary" onclick={installAll}>
        {missing.length === bundle.apps.length ? `Install ${missing.length} apps` : `Install ${missing.length} missing`}
      </button>
    {/if}
  </div>
</article>

<style>
  .bundle { padding: 18px; display: grid; gap: 6px; align-content: start; }
  .icons { display: flex; gap: 6px; margin-bottom: 8px; }
  .title { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
  h3 { font-family: var(--font-display); font-size: 16px; font-weight: 700; letter-spacing: -0.02em; margin: 0; }
  .list { font-size: 12px; color: var(--ink-2); }
  .foot { margin-top: 8px; }
</style>
