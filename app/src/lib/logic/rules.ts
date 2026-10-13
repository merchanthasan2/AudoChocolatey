import type { RuleKind } from '../ipc/commands';

/** Why an update is hidden, as shown in the Hidden list. */
export function ruleLabel(kind: RuleKind): string {
  return kind.kind === 'never' ? 'Never update' : `Skipping version ${kind.version}`;
}
