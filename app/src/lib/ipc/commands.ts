import { invoke } from '@tauri-apps/api/core';

// The only module allowed to call invoke(). P1 replaces these hand-written types with ts-rs bindings.
export type Overview = { appVersion: string; buildSha: string; isElevated: boolean };

export const getOverview = () => invoke<Overview>('get_overview');
