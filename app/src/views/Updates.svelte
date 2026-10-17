<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';
  import { updates, queue, settings, myBundles } from '../lib/stores/app.svelte';
  import type { UpgradeCandidate } from '../lib/ipc/commands';
  import AppRow from '../lib/components/AppRow.svelte';
  import JobButton from '../lib/components/JobButton.svelte';
  import Checkbox from '../lib/components/Checkbox.svelte';
  import Skeleton from '../lib/components/Skeleton.svelte';
  import EmptyState from '../lib/components/EmptyState.svelte';
  import Banner from '../lib/components/Banner.svelte';
  import CountRoll from '../lib/components/CountRoll.svelte';
  import Icon from '../lib/components/Icon.svelte';
  import { relativeTime } from '../lib/logic/format';
  import { collapse, rise } from '../lib/motion';
  import { uniqueKeys } from '../lib/logic/uniqueKeys';
  import { visibleUpdates } from '../lib/logic/visibleUpdates';
  import { clickGuard } from '../lib/logic/clickGuard';
  import { ruleLabel, ruleAt, ownChoice, effectiveChoice } from '../lib/logic/rules';
  import RowMenu from '../lib/components/RowMenu.svelte';

  let showHidden = $state(false);

  // Each app's Automatic / Ask first choice (rows keep their listing index through `items`).
  const mode = $derived(settings.value?.autoUpdate ?? 'notify');
  const ruleOf = (u: UpgradeCandidate) => ruleAt(updates.last, updates.items.indexOf(u));
  const isAuto = (u: UpgradeCandidate) => effectiveChoice(mode, ruleOf(u)?.kind) === 'auto';
  function menuFor(u: UpgradeCandidate) {
    const rule = ruleOf(u);
    const own = ownChoice(rule?.kind);
    const items = [
      isAuto(u)
        ? { text: 'Ask before updating this app', run: () => updates.setRule(u, { kind: 'ask' }) }
        : { text: 'Update this app automatically', run: () => updates.setRule(u, { kind: 'auto' }) },
    ];
    if (own && rule) items.push({ text: 'Use my Settings choice', run: () => updates.restore(rule.key) });
    items.push(
      { text: `Skip version ${u.availableVersion}`, run: () => updates.setRule(u, { kind: 'skipVersion', version: u.availableVersion }) },
      { text: 'Never update this app', run: () => updates.setRule(u, { kind: 'never' }) },
    );
    return items;
  }

  const jobOf = (u: UpgradeCandidate) => queue.jobFor(u.source, u.id, 'upgrade');
  const isBusy = (u: UpgradeCandidate) => {
    const j = jobOf(u);
    return !!j && !['succeeded', 'failed', 'cancelled', 'skipped'].includes(j.phase);
  };

  // Finished rows leave the list (with a collapse); everything else stays, including running ones.
  const rows = $derived(visibleUpdates(updates.items, jobOf, updates.loadedAt));
  const keys = $derived(uniqueKeys(rows, (u) => `${u.source}:${u.id}`));
  const idle = $derived(rows.filter((u) => !isBusy(u)));

  const selected = new SvelteSet<string>();
  // Drop selections for rows that disappeared or started running.
  $effect(() => {
    const live = new Set(rows.map((u, i) => (isBusy(u) ? '' : keys[i])));
    for (const k of [...selected]) if (!live.has(k)) selected.delete(k);
  });
  const selectedRows = $derived(rows.filter((_, i) => selected.has(keys[i])));
  const allSelected = $derived(idle.length > 0 && selectedRows.length === idle.length);

  function toggleAll(on: boolean) {
    selected.clear();
    if (on) rows.forEach((u, i) => { if (!isBusy(u)) selected.add(keys[i]); });
  }
  // One button whose label changes; a short guard stops a double-click from landing on
  // "Update all" right after "Update N selected" was pressed.
  const guard = clickGuard(800);
  const primaryLabel = $derived(selectedRows.length > 0 ? `Update ${selectedRows.length} selected` : 'Update all');
  function primary() {
    if (!guard()) return;
    const list = selectedRows.length > 0 ? selectedRows : idle;
    selected.clear();
    queue.updateMany(list);
  }

  let now = $state(Date.now());
  $effect(() => {
    const t = setInterval(() => (now = Date.now()), 30_000);
    return () => clearInterval(t);
  });
  const batchPct = $derived(queue.batchJobs.length ? (queue.batchDone / queue.batchJobs.length) * 100 : 0);
</script>

<header class="head">
  <div class="title">
    <h1>
      {#if !updates.loaded}Checking for updates…{:else if rows.length === 0}Everything is up to date{:else}<CountRoll value={rows.length} /> {rows.length === 1 ? 'update' : 'updates'} ready{/if}
    </h1>
    <p class="muted">
      {#if queue.batchRunning}
        Updating {Math.min(queue.batchDone + 1, queue.batchJobs.length)} of {queue.batchJobs.length}…
      {:else if updates.loading}Checking now…{:else if updates.loadedAt}Checked {relativeTime(updates.loadedAt, now)}{/if}
    </p>
  </div>
  <div class="actions">
    {#if selectedRows.length > 0}
      <button class="btn btn-quiet" onclick={() => (myBundles.adding = selectedRows.map((u) => ({ source: u.source, id: u.id, name: u.name })))}>
        <Icon name="plus" size={16} /> Add to bundle
      </button>
    {/if}
    <button class="btn btn-quiet" onclick={() => updates.refresh()} disabled={updates.loading}>
      <span class:spinning={updates.loading}><Icon name="refresh" size={16} /></span> Check again
    </button>
    {#if idle.length > 0 || selectedRows.length > 0}
      <button class="btn btn-primary" onclick={primary}>{primaryLabel}</button>
    {/if}
  </div>
</header>

<div class="batch" class:on={queue.batchRunning} aria-hidden={!queue.batchRunning}>
  <div class="batch-bar"><i style="width:{queue.batchRunning ? Math.max(4, batchPct) : 0}%"></i></div>
</div>

{#each updates.sourceErrors as e (e.source)}
  <div class="gap"><Banner text="Some apps couldn't be checked for updates. The list may be incomplete." action={{ label: 'Try again', run: () => updates.refresh() }} /></div>
{/each}
{#if updates.error}<div class="gap"><Banner text={updates.error} action={{ label: 'Try again', run: () => updates.refresh() }} /></div>{/if}

{#if !updates.loaded}
  <Skeleton rows={4} />
{:else if rows.length === 0}
  <EmptyState icon="check" tone="good" title="You're all set" body="Every app we can update is on its latest version. We'll keep checking in the background." />
{:else}
  <div class="card list">
    <div class="list-head">
      <Checkbox label="Select all updates" checked={allSelected} indeterminate={selectedRows.length > 0 && !allSelected}
        disabled={idle.length === 0} onchange={(v) => toggleAll(v)} />
      <span class="muted">{selectedRows.length > 0 ? `${selectedRows.length} selected` : 'Select apps to update, or update everything'}</span>
    </div>
    {#each rows as u, i (keys[i])}
      {@const job = jobOf(u)}
      <div class="row-wrap" in:rise={{ delay: Math.min(i, 8) * 40 }} out:collapse>
        <AppRow name={u.name} meta="{u.installedVersion} → {u.availableVersion}{isAuto(u) ? ' · Updates automatically' : ''}" {job} mode="update" bar
          iconKey="{u.source}:{u.id}">
          {#snippet lead()}
            <Checkbox label="Select {u.name}" checked={selected.has(keys[i])} disabled={isBusy(u)}
              onchange={(v) => (v ? selected.add(keys[i]) : selected.delete(keys[i]))} />
          {/snippet}
          {#snippet action()}
            <span class="row-actions">
              <JobButton {job} mode="update" name={u.name}
                onstart={() => queue.update(u)} oncancel={(id) => queue.cancel(id)} onretry={(id) => queue.retry(id)} />
              {#if !isBusy(u)}
                <RowMenu label="More options for {u.name}" items={menuFor(u)} />
              {/if}
            </span>
          {/snippet}
        </AppRow>
      </div>
    {/each}
  </div>
{/if}

{#if updates.hidden.length > 0}
  <section class="hidden-section">
    <button class="hidden-toggle" aria-expanded={showHidden} onclick={() => (showHidden = !showHidden)}>
      <Icon name="eyeOff" size={16} /> Hidden ({updates.hidden.length})
      <span class="chev" class:open={showHidden}><Icon name="chevron" size={14} /></span>
    </button>
    {#if showHidden}
      <div class="card list" transition:collapse>
        {#each updates.hidden as h (h.rule.key + h.candidate.id)}
          <AppRow name={h.candidate.name} meta="{h.candidate.installedVersion} → {h.candidate.availableVersion} · {ruleLabel(h.rule.kind)}"
            iconKey="{h.candidate.source}:{h.candidate.id}" mode="update">
            {#snippet action()}
              <button class="btn btn-quiet" onclick={() => updates.restore(h.rule.key)}>Show again</button>
            {/snippet}
          </AppRow>
        {/each}
      </div>
    {/if}
  </section>
{/if}

<style>
  .head { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; margin-bottom: 18px; }
  .title p { margin-top: 4px; }
  .actions { display: flex; gap: 8px; }
  .gap { margin-bottom: 12px; }
  .batch { margin: -6px 0 16px; opacity: 0; transition: opacity 200ms ease; }
  .batch.on { opacity: 1; }
  .batch-bar { height: 6px; border-radius: 6px; background: var(--hairline-strong); overflow: hidden; }
  .batch-bar i { display: block; height: 100%; border-radius: 6px; background: var(--accent); transition: width 500ms var(--spring); }
  .list-head { display: flex; align-items: center; gap: 14px; padding: 10px 16px; border-bottom: 1px solid var(--hairline); font-size: 12.5px; }
  .row-actions { display: inline-flex; align-items: center; gap: 4px; }
  .hidden-section { margin-top: 18px; }
  .hidden-toggle { display: inline-flex; align-items: center; gap: 8px; font-weight: 600; font-size: 13px; color: var(--muted); padding: 6px 4px; margin-bottom: 8px; }
  .hidden-toggle:hover { color: var(--ink); }
  .chev { display: inline-grid; transform: rotate(90deg); transition: transform var(--dur-quick) var(--ease-out); }
  .chev.open { transform: rotate(-90deg); }
  .spinning { display: inline-grid; animation: spin 0.9s linear infinite; }
  @keyframes spin { to { transform: rotate(360deg); } }
</style>
