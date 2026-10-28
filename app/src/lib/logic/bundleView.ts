// What each app in an opened bundle can do right now.

import type { AppEntry } from '../ipc/bindings/AppEntry';
import { findUpdate, isInstalled, needsSetup } from './entries';

export type BundleRowState = 'off' | 'missing' | 'update' | 'installed';

export type BundleRow<U extends { source: 'winget' | 'choco' | 'msstore'; id: string }> = {
  entry: AppEntry;
  state: BundleRowState;
  update: U | null;
};

/** One row per app. A turned-off source only blocks apps that aren't installed yet. */
export function bundleRows<U extends { source: 'winget' | 'choco' | 'msstore'; id: string }>(
  apps: AppEntry[],
  installed: Set<string>,
  updates: U[],
  on: { choco: boolean; store: boolean },
): BundleRow<U>[] {
  return apps.map((entry) => {
    const update = findUpdate(entry, updates);
    const present = isInstalled(entry, installed);
    const state: BundleRowState = !present && needsSetup(entry, on)
      ? 'off'
      : !present
        ? 'missing'
        : update
          ? 'update'
          : 'installed';
    return { entry, state, update: state === 'update' ? update : null };
  });
}
