import type { SourceLabel } from '../ipc/bindings/SourceLabel';
import { exportCsv } from '../ipc/commands';
import { SOURCE_LABELS } from './sources';
import { toasts } from '../stores/app.svelte';

export type CsvInput = { name: string; id: string; version: string; newVersion?: string; label: SourceLabel; released?: string | null };

/** The shown rows (filtered and sorted) with friendly source names. */
export function csvRows(rows: CsvInput[]) {
  return rows.map((r) => ({
    name: r.name, id: r.id, version: r.version, newVersion: r.newVersion, source: SOURCE_LABELS[r.label], released: r.released ?? undefined,
  }));
}

export async function exportListCsv(page: 'updates' | 'installed', rows: CsvInput[]) {
  try {
    const n = await exportCsv(page, csvRows(rows));
    if (n != null) toasts.push({ text: `Saved ${n} ${n === 1 ? 'app' : 'apps'} to a spreadsheet`, tone: 'ok' });
  } catch (e) {
    toasts.push({ text: (e as { message?: string })?.message ?? String(e), tone: 'error' });
  }
}
