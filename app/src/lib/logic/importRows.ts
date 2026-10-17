import type { ImportPreview } from '../ipc/commands';
import { isInstalled, needsSetup } from './entries';

export type RowState = 'ready' | 'installed' | 'off';

/** What each imported app would do, and whether it starts ticked: only apps we know (from the
 *  catalog) that aren't installed yet. Apps we don't know show their id and start unticked. */
export function importRows(p: ImportPreview, installed: Set<string>, on: { choco: boolean; store: boolean }) {
  return p.rows.map((r) => {
    const state: RowState = isInstalled(r.entry, installed) ? 'installed' : needsSetup(r.entry, on) ? 'off' : 'ready';
    return { ...r, state, ticked: state === 'ready' && r.entry.curated };
  });
}
