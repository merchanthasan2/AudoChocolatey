import { describe, expect, it } from 'vitest';
import { compareVersions } from './versions';
import { applyQuery, labelCounts, matchesText, type ListItem, type ListQuery } from './listView';
import { keepSelected, headerState, rangeKeys } from './selection';
import { fitActions } from './toolbarFit';
import { primaryAction } from './primaryAction';

describe('version order (same table as Rust)', () => {
  it('compares like people expect', () => {
    expect(compareVersions('1.95.2', '1.95.10')).toBe(-1);
    expect(compareVersions('9.9.2', '9.9.3')).toBe(-1);
    expect(compareVersions('1.0', '1.0.0')).toBe(0);
    expect(compareVersions('2.0.0-beta', '2.0.0')).toBe(-1);
    expect(compareVersions('117.14.19', '117.14.41')).toBe(-1);
    expect(compareVersions('26.002.21931', '26.1.0')).toBe(1);
    expect(compareVersions('0.0.309', '0.0.311')).toBe(-1);
    expect(compareVersions('14.51.36247', '14.44.35211')).toBe(1);
  });
});

describe('list view', () => {
  const item = (name: string, over: Partial<ListItem> = {}): ListItem => ({
    key: name, name, id: name.replace(/ /g, '.'), label: 'microsoftCatalog', component: false, version: '1.0', ...over,
  });
  const items = [
    item('Zoom', { security: true, version: '6.0', released: '2026-10-01' }),
    item('Ärger App', { label: 'installedDirectly', version: 'Unknown' }),
    item('VC Runtime', { component: true, label: 'installedDirectly' }),
    item('Audacity', { label: 'communityCatalog', version: '3.6', released: null }),
    item('Firefox', { security: true, version: '131.0', released: '2026-09-01' }),
  ];
  const q = (over: Partial<ListQuery> = {}): ListQuery => ({ text: '', label: 'all', hideComponents: true, sort: 'name', dir: 'asc', ...over });
  const names = (o: Partial<ListQuery>) => applyQuery(items, (x) => x, q(o)).map((x) => x.name);

  it('searches names and ids, every word, ignoring accents', () => {
    expect(matchesText(items[1], 'arger')).toBe(true);
    expect(matchesText(items[0], 'zoom 7')).toBe(false);
    expect(matchesText(item('Mozilla Firefox', { id: 'Mozilla.Firefox' }), 'moz fire')).toBe(true);
  });
  it('hides components and filters by source', () => {
    expect(names({})).toEqual(['Ärger App', 'Audacity', 'Firefox', 'Zoom']);
    expect(names({ hideComponents: false })).toContain('VC Runtime');
    expect(names({ label: 'communityCatalog' })).toEqual(['Audacity']);
  });
  it('sorts, with unknown versions and missing dates last either way', () => {
    expect(names({ sort: 'suggested' })).toEqual(['Firefox', 'Zoom', 'Ärger App', 'Audacity']);
    expect(names({ sort: 'version' })).toEqual(['Audacity', 'Zoom', 'Firefox', 'Ärger App']);
    expect(names({ sort: 'version', dir: 'desc' })).toEqual(['Firefox', 'Zoom', 'Audacity', 'Ärger App']);
    const released = names({ sort: 'released', dir: 'desc' });
    expect(released.slice(0, 2)).toEqual(['Zoom', 'Firefox']);
    expect(names({ sort: 'released', dir: 'asc' }).slice(0, 2)).toEqual(['Firefox', 'Zoom']);
    expect(names({ sort: 'source' }).at(-1)).toBe('Ärger App');
  });
  it('counts per source after search and the components switch', () => {
    const c = labelCounts(items, { text: '', hideComponents: true });
    expect(c.all).toBe(4);
    expect(c.installedDirectly).toBe(1);
    expect(labelCounts(items, { text: '', hideComponents: false }).installedDirectly).toBe(2);
    expect(labelCounts(items, { text: 'zoom', hideComponents: true }).all).toBe(1);
  });
});

describe('selection, toolbar and the main button', () => {
  it('prunes selection to what is shown and selectable', () => {
    expect(keepSelected(['a', 'b', 'c'], new Set(['a', 'c']))).toEqual(['a', 'c']);
    expect(headerState(0, 0)).toEqual({ checked: false, indeterminate: false, disabled: true });
    expect(headerState(2, 3)).toEqual({ checked: false, indeterminate: true, disabled: false });
    expect(headerState(3, 3).checked).toBe(true);
  });
  it('selects ranges either way', () => {
    expect(rangeKeys(['a', 'b', 'c', 'd'], 'b', 'd')).toEqual(['b', 'c', 'd']);
    expect(rangeKeys(['a', 'b', 'c', 'd'], 'd', 'b')).toEqual(['b', 'c', 'd']);
    expect(rangeKeys(['a', 'b'], 'gone', 'b')).toEqual(['b']);
  });
  it('fits toolbar actions', () => {
    expect(fitActions([100, 100, 100], 320, 40, 8)).toBe(3);
    expect(fitActions([100, 100, 100], 300, 40, 8)).toBe(2);
    expect(fitActions([100, 100, 100], 100, 40, 8)).toBe(0);
  });
  it('labels the main button', () => {
    expect(primaryAction(3, 5, false)).toEqual({ label: 'Update 3 selected', scope: 'selected' });
    expect(primaryAction(0, 5, false)).toEqual({ label: 'Update all', scope: 'all' });
    expect(primaryAction(0, 4, true)).toEqual({ label: 'Update 4 shown', scope: 'shown' });
    expect(primaryAction(0, 0, false)).toBeNull();
  });
});
