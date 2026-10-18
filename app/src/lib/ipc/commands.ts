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
import type { AutoStatus } from './bindings/AutoStatus';
export type { AutoStatus };
/** Whether automatic updates could start right now, and why not. */
export const getAutoStatus = () => invoke<AutoStatus>('get_auto_status');

// Marketplace (curated catalog)
import type { AppEntry } from './bindings/AppEntry';
import type { Home } from './bindings/Home';
import type { AppDetails } from './bindings/AppDetails';
import type { BundleView } from './bindings/BundleView';
export type { AppEntry, Home, AppDetails, BundleView };
export const marketplaceHome = () => invoke<Home>('marketplace_home');
export const marketplaceCategory = (id: string) => invoke<AppEntry[]>('marketplace_category', { id });
export const marketplaceCategoryMore = (id: string) => invoke<AppEntry[]>('marketplace_category_more', { id });
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
/** `skip`: apps to update later (for example because they're open). It can only remove work. */
export const updateAll = (skip: SourceRef[] = []) => invoke<string[]>('update_all', { skip });
import type { RunningQuery } from './bindings/RunningQuery';
import type { RunningApp } from './bindings/RunningApp';
export type { RunningQuery, RunningApp };
/** Which of these apps are open right now (programs started from their own install folder). */
export const runningApps = (apps: RunningQuery[]) => invoke<RunningApp[]>('running_apps', { apps });
/** Asks them to close as their X button would, waits up to 15 s; returns names still open. */
export const closeApps = (apps: RunningQuery[]) => invoke<string[]>('close_apps', { apps });
export const getQueue = () => invoke<QueueSnapshot>('get_queue');
export const pauseQueue = () => invoke<void>('pause_queue');
export const resumeQueue = () => invoke<void>('resume_queue');
export const quitAnyway = () => invoke<void>('quit_anyway');
/** Restarts through the Windows admin prompt; rejects if the person says no. */
export const relaunchAsAdmin = () => invoke<void>('relaunch_as_admin');
export const cancelJob = (id: string) => invoke<void>('cancel_job', { id });
export const retryJob = (id: string, mode?: import('./bindings/OpMode').OpMode) => invoke<string>('retry_job', { id, mode: mode ?? null });
export const clearFinished = () => invoke<void>('clear_finished');
export const getJobLog = (id: string) => invoke<string[]>('get_job_log', { id });
import type { HistoryEntry } from './bindings/HistoryEntry';
export type { HistoryEntry };
/** Finished changes, newest first. */
export const getHistory = () => invoke<HistoryEntry[]>('get_history');
/** Re-checks, then asks in a Windows dialog. Resolves to the job id, or null if they said no. */
export const rollback = (jobId: string) => invoke<string | null>('rollback', { jobId });

// My bundles, export and import (files are picked by Windows dialogs in Rust)
import type { MyBundleView } from './bindings/MyBundleView';
import type { BundleAppIn } from './bindings/BundleAppIn';
import type { ExportWhat } from './bindings/ExportWhat';
import type { ImportPreview } from './bindings/ImportPreview';
export type { MyBundleView, BundleAppIn, ExportWhat, ImportPreview };
export const myBundles = () => invoke<MyBundleView[]>('my_bundles');
export const addToBundle = (bundle: string | null, name: string | null, apps: BundleAppIn[]) =>
  invoke<MyBundleView[]>('add_to_bundle', { bundle, name, apps });
export const removeFromBundle = (bundle: string, key: string) => invoke<MyBundleView[]>('remove_from_bundle', { bundle, key });
export const deleteBundle = (bundle: string) => invoke<MyBundleView[]>('delete_bundle', { bundle });
/** Resolves to the number of apps saved, or null if the person cancelled. */
export const exportApps = (what: ExportWhat) => invoke<number | null>('export_apps', { what });
/** Resolves to null if the person cancelled. */
export const importPick = () => invoke<ImportPreview | null>('import_pick');
export const importApply = (token: string, indices: number[]) => invoke<string[]>('import_apply', { token, indices });
export const importKeep = (token: string, indices: number[], name: string) => invoke<MyBundleView[]>('import_keep', { token, indices, name });
export const pickBackupFolder = () => invoke<Settings | null>('pick_backup_folder');
export const backupNow = () => invoke<string>('backup_now');

// Spreadsheet export of the shown list (save dialog in Rust)
import type { CsvRow } from './bindings/CsvRow';
import type { ListPage } from './bindings/ListPage';
/** Resolves to the number of rows saved, or null if the person cancelled. */
export const exportCsv = (page: ListPage, rows: CsvRow[]) => invoke<number | null>('export_csv', { page, rows });

// Updates page extras
import type { UpdateInfo } from './bindings/UpdateInfo';
import type { RuleRequest } from './bindings/RuleRequest';
import type { OpMode } from './bindings/OpMode';
export type { UpdateInfo, RuleRequest, OpMode };
export const setRules = (items: RuleRequest[]) => invoke<UpdatesListing>('set_rules', { items });
export const getUpdateInfo = () => invoke<UpdateInfo[]>('get_update_info');
/** Opens the release notes Rust has for this exact update, in the person's own browser. */
export const openReleaseNotes = (target: SourceRef, version: string) => invoke<void>('open_release_notes', { target, version });
import type { LinkedUpdate } from './bindings/LinkedUpdate';
export type { LinkedUpdate };
/** Updates for apps installed directly that are linked to a catalog app. */
export const listLinkedUpdates = () => invoke<LinkedUpdate[]>('list_linked_updates');
/** Restarts Windows in 30 seconds. Only after the person confirmed. */
export const restartNow = () => invoke<void>('restart_now');
