// Package-tool names, for the details panel's Advanced block and Settings only (see policy.mjs).
const NAMES: Record<string, string> = { winget: 'winget', choco: 'Chocolatey', msstore: 'Microsoft Store' };

export const sourceName = (kind: string): string => NAMES[kind] ?? kind;

import type { SourceLabel } from '../ipc/bindings/SourceLabel';
import type { SourceKind } from '../ipc/bindings/SourceKind';

/** Friendly source names (ADR 028): what people see on lists and filters. */
export const SOURCE_LABELS: Record<SourceLabel, string> = {
  microsoftCatalog: 'Microsoft catalog',
  communityCatalog: 'Community catalog',
  microsoftStore: 'Microsoft Store',
  installedDirectly: 'Installed directly',
  steam: 'Steam',
};

/** Filter and sort order. */
export const LABEL_ORDER: SourceLabel[] = ['microsoftCatalog', 'communityCatalog', 'microsoftStore', 'installedDirectly', 'steam'];

/** An update's label: updates always come from a source. */
export function labelOf(source: SourceKind): SourceLabel {
  return source === 'winget' ? 'microsoftCatalog' : source === 'choco' ? 'communityCatalog' : 'microsoftStore';
}
