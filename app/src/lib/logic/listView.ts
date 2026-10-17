import type { SourceLabel } from '../ipc/bindings/SourceLabel';
import type { ListSort } from '../ipc/bindings/ListSort';
import type { SortDir } from '../ipc/bindings/SortDir';
import { LABEL_ORDER } from './sources';
import { compareVersions, knownVersion } from './versions';

/** What the list logic needs to know about a row, whatever page it's on. */
export interface ListItem {
  key: string;
  name: string;
  id: string;
  label: SourceLabel;
  component: boolean;
  version: string;
  newVersion?: string;
  released?: string | null;
  security?: boolean;
}

export interface ListQuery {
  text: string;
  label: SourceLabel | 'all';
  hideComponents: boolean;
  sort: ListSort;
  dir: SortDir;
}

const fold = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

/** Every word typed must appear in the name or the package id (accents ignored). */
export function matchesText(i: ListItem, text: string): boolean {
  const terms = fold(text).split(/\s+/).filter(Boolean);
  if (terms.length === 0) return true;
  const hay = `${fold(i.name)} ${fold(i.id)}`;
  return terms.every((t) => hay.includes(t));
}

/** Counts for the source filter, after search and the components switch. */
export function labelCounts(items: ListItem[], q: Pick<ListQuery, 'text' | 'hideComponents'>): Record<SourceLabel | 'all', number> {
  const out = { all: 0, microsoftCatalog: 0, communityCatalog: 0, microsoftStore: 0, installedDirectly: 0, steam: 0 };
  for (const i of items) {
    if ((q.hideComponents && i.component) || !matchesText(i, q.text)) continue;
    out.all++;
    out[i.label]++;
  }
  return out;
}

const byName = (a: ListItem, b: ListItem) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });

/** Unknown versions and missing dates sort last whichever way. */
export function compareItems(a: ListItem, b: ListItem, sort: ListSort, dir: SortDir): number {
  const flip = dir === 'desc' ? -1 : 1;
  const lastIfMissing = (x: unknown, y: unknown) => (x && !y ? -1 : !x && y ? 1 : 0);
  switch (sort) {
    case 'suggested':
      return (a.security ? 0 : 1) - (b.security ? 0 : 1) || byName(a, b);
    case 'name':
      return flip * byName(a, b);
    case 'version': {
      const va = knownVersion(a.version) ? a.version : null;
      const vb = knownVersion(b.version) ? b.version : null;
      if (!va || !vb) return lastIfMissing(va, vb) || byName(a, b);
      return flip * compareVersions(va, vb) || byName(a, b);
    }
    case 'source':
      return flip * (LABEL_ORDER.indexOf(a.label) - LABEL_ORDER.indexOf(b.label)) || byName(a, b);
    case 'released': {
      if (!a.released || !b.released) return lastIfMissing(a.released, b.released) || byName(a, b);
      return flip * a.released.localeCompare(b.released) || byName(a, b);
    }
  }
}

export function applyQuery<T>(items: T[], view: (t: T) => ListItem, q: ListQuery): T[] {
  return items
    .map((t) => ({ t, v: view(t) }))
    .filter(({ v }) => !(q.hideComponents && v.component) && (q.label === 'all' || v.label === q.label) && matchesText(v, q.text))
    .sort((x, y) => compareItems(x.v, y.v, q.sort, q.dir))
    .map(({ t }) => t);
}
