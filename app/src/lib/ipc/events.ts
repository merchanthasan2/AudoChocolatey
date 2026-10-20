import { listen, type UnlistenFn } from '@tauri-apps/api/event';
import type { Job } from './bindings/Job';
import type { QueueSnapshot } from './bindings/QueueSnapshot';
import type { UpdatesListing } from './bindings/UpdatesListing';

// Typed wrappers for the Rust queue's events. Each returns an unsubscribe function.
export const onJobChanged = (cb: (job: Job) => void): Promise<UnlistenFn> =>
  listen<Job>('job://changed', (e) => cb(e.payload));

export const onJobProgress = (cb: (job: Job) => void): Promise<UnlistenFn> =>
  listen<Job>('job://progress', (e) => cb(e.payload));

export const onQueueChanged = (cb: (snapshot: QueueSnapshot) => void): Promise<UnlistenFn> =>
  listen<QueueSnapshot>('queue://changed', (e) => cb(e.payload));

/** A check finished outside the Updates page (tray "Check now", scheduler). */
export const onUpdatesChanged = (cb: (l: UpdatesListing) => void): Promise<UnlistenFn> =>
  listen<UpdatesListing>('updates://changed', (e) => cb(e.payload));

/** Closing was held because an installer is running; the UI asks the user. */
export const onCloseBlocked = (cb: () => void): Promise<UnlistenFn> => listen('app://close-blocked', () => cb());

/** "What's new" links and release dates arrived for the shown updates. */
export const onUpdateInfo = (cb: (info: UpdateInfo[]) => void): Promise<UnlistenFn> =>
  listen<UpdateInfo[]>('updates://info', (e) => cb(e.payload));
import type { UpdateInfo } from './bindings/UpdateInfo';

/** An install or update put new icons on the desktop. */
export const onNewShortcuts = (cb: (s: NewShortcuts) => void): Promise<UnlistenFn> =>
  listen<NewShortcuts>('job://shortcuts', (e) => cb(e.payload));
import type { NewShortcuts } from './bindings/NewShortcuts';
export type { NewShortcuts };
