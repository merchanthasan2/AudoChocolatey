// Helpers for catalog/search entries against what's installed on this PC.

type Installed = { id: string; source: 'winget' | 'choco' | null };
type Ids = { winget: string | null; choco: string | null };

/** `winget:<id>` / `choco:<id>` keys (lowercase) for everything a source manages. */
export function installedSet(items: Installed[]): Set<string> {
  return new Set(items.filter((p) => p.source).map((p) => `${p.source}:${p.id.toLowerCase()}`));
}

export function isInstalled(entry: Ids, set: Set<string>): boolean {
  return (
    (!!entry.winget && set.has(`winget:${entry.winget.toLowerCase()}`)) ||
    (!!entry.choco && set.has(`choco:${entry.choco.toLowerCase()}`))
  );
}

/** Entries not installed yet (what a bundle should actually queue). */
export function missingFrom<T extends Ids>(entries: T[], set: Set<string>): T[] {
  return entries.filter((e) => !isInstalled(e, set));
}
