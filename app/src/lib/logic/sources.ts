// Package-tool names, for the details panel's Advanced block and Settings only (see policy.mjs).
const NAMES: Record<string, string> = { winget: 'winget', choco: 'Chocolatey', msstore: 'Microsoft Store' };

export const sourceName = (kind: string): string => NAMES[kind] ?? kind;
