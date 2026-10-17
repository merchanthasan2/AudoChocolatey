import type { RuleKind, Rule } from '../ipc/commands';
import type { AutoUpdate } from '../ipc/bindings/AutoUpdate';

/** Why an update is hidden, as shown in the Hidden list. */
export function ruleLabel(kind: RuleKind): string {
  return kind.kind === 'never' ? 'Never update' : kind.kind === 'skipVersion' ? `Skipping version ${kind.version}` : '';
}

export type Choice = 'auto' | 'ask';

/** The app's own choice, if it made one (a skip keeps the choice it had before). */
export function ownChoice(kind: RuleKind | undefined): Choice | null {
  if (!kind) return null;
  if (kind.kind === 'auto' || kind.kind === 'ask') return kind.kind;
  if (kind.kind === 'skipVersion') return kind.after ?? null;
  return null;
}

/** Mirrors Rust `rules::effective`: the app's choice wins over the Settings mode. */
export function effectiveChoice(mode: AutoUpdate, kind: RuleKind | undefined): Choice {
  if (kind?.kind === 'never') return 'ask';
  return ownChoice(kind) ?? (mode === 'automatic' ? 'auto' : 'ask');
}

/** The rule behind the update at `index` in the listing, found by its key. */
export function ruleAt(listing: { itemKeys: string[]; rules: Rule[] } | null, index: number): Rule | undefined {
  const key = listing?.itemKeys[index];
  return key ? listing!.rules.find((r) => r.key === key) : undefined;
}

/** "22:00" for minutes after midnight. */
export function clock(minutes: number): string {
  const h = Math.floor(minutes / 60) % 24;
  const m = minutes % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}
