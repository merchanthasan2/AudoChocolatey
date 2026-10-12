import { invoke } from '@tauri-apps/api/core';
import type { Overview } from './bindings/Overview';
import type { Diagnostics } from './bindings/Diagnostics';
import type { Listing } from './bindings/Listing';
import type { InstalledPackage } from './bindings/InstalledPackage';
import type { UpgradeCandidate } from './bindings/UpgradeCandidate';
import type { SourcePackage } from './bindings/SourcePackage';
import type { Settings } from './bindings/Settings';
import type { SettingsPatch } from './bindings/SettingsPatch';

// The only module allowed to call invoke(). Types are generated from Rust by ts-rs.
export type { Overview, Diagnostics, Listing, InstalledPackage, UpgradeCandidate, SourcePackage, Settings, SettingsPatch };
export type { SourceStatus } from './bindings/SourceStatus';
export type { AppError } from './bindings/AppError';

export const getOverview = () => invoke<Overview>('get_overview');
export const getDiagnostics = () => invoke<Diagnostics>('get_diagnostics');
export const listInstalled = () => invoke<Listing<InstalledPackage>>('list_installed');
export const listUpdates = () => invoke<Listing<UpgradeCandidate>>('list_updates');
export const search = (query: string) => invoke<Listing<SourcePackage>>('search', { query });
export const getSettings = () => invoke<Settings>('get_settings');
export const updateSettings = (patch: SettingsPatch) => invoke<Settings>('update_settings', { patch });

// Install queue
import type { Job } from './bindings/Job';
import type { EnqueueItem } from './bindings/EnqueueItem';
import type { OpRequest } from './bindings/OpRequest';
export type { Job, EnqueueItem, OpRequest };
export type { JobPhase } from './bindings/JobPhase';

export const enqueue = (ops: EnqueueItem[]) => invoke<string[]>('enqueue', { ops });
export const updateAll = () => invoke<string[]>('update_all');
export const getQueue = () => invoke<Job[]>('get_queue');
export const cancelJob = (id: string) => invoke<void>('cancel_job', { id });
export const retryJob = (id: string) => invoke<string>('retry_job', { id });
export const clearFinished = () => invoke<void>('clear_finished');
export const getJobLog = (id: string) => invoke<string[]>('get_job_log', { id });
