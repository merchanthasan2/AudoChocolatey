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
export const listUpdates = () => invoke<UpdatesListing>('list_updates');
export const setRule = (target: SourceRef, name: string, kind: RuleKind) => invoke<UpdatesListing>('set_rule', { target, name, kind });
export const removeRule = (key: string) => invoke<UpdatesListing>('remove_rule', { key });
export const search = (query: string) => invoke<Listing<AppEntry>>('search', { query });
export const getSettings = () => invoke<Settings>('get_settings');
export const updateSettings = (patch: SettingsPatch) => invoke<Settings>('update_settings', { patch });

// Marketplace (curated catalog)
import type { AppEntry } from './bindings/AppEntry';
import type { Home } from './bindings/Home';
import type { AppDetails } from './bindings/AppDetails';
import type { BundleView } from './bindings/BundleView';
export type { AppEntry, Home, AppDetails, BundleView };
export const marketplaceHome = () => invoke<Home>('marketplace_home');
export const marketplaceCategory = (id: string) => invoke<AppEntry[]>('marketplace_category', { id });
export const getApp = (entry: AppEntry) => invoke<AppDetails>('get_app', { entry });

// Install queue
import type { Job } from './bindings/Job';
import type { QueueSnapshot } from './bindings/QueueSnapshot';
import type { EnqueueItem } from './bindings/EnqueueItem';
import type { OpRequest } from './bindings/OpRequest';
export type { Job, EnqueueItem, OpRequest, QueueSnapshot };
import type { UpdatesListing } from './bindings/UpdatesListing';
import type { RuleKind } from './bindings/RuleKind';
import type { Rule } from './bindings/Rule';
import type { HiddenUpdate } from './bindings/HiddenUpdate';
import type { SourceRef } from './bindings/SourceRef';
export type { UpdatesListing, RuleKind, Rule, HiddenUpdate, SourceRef };
export type { JobPhase } from './bindings/JobPhase';

export const enqueue = (ops: EnqueueItem[]) => invoke<string[]>('enqueue', { ops });
export const updateAll = () => invoke<string[]>('update_all');
export const getQueue = () => invoke<QueueSnapshot>('get_queue');
export const pauseQueue = () => invoke<void>('pause_queue');
export const resumeQueue = () => invoke<void>('resume_queue');
export const quitAnyway = () => invoke<void>('quit_anyway');
export const cancelJob = (id: string) => invoke<void>('cancel_job', { id });
export const retryJob = (id: string) => invoke<string>('retry_job', { id });
export const clearFinished = () => invoke<void>('clear_finished');
export const getJobLog = (id: string) => invoke<string[]>('get_job_log', { id });
