import { listen, type UnlistenFn } from '@tauri-apps/api/event';
import type { Job } from './bindings/Job';
import type { QueueSnapshot } from './bindings/QueueSnapshot';

// Typed wrappers for the Rust queue's events. Each returns an unsubscribe function.
export const onJobChanged = (cb: (job: Job) => void): Promise<UnlistenFn> =>
  listen<Job>('job://changed', (e) => cb(e.payload));

export const onJobProgress = (cb: (job: Job) => void): Promise<UnlistenFn> =>
  listen<Job>('job://progress', (e) => cb(e.payload));

export const onQueueChanged = (cb: (snapshot: QueueSnapshot) => void): Promise<UnlistenFn> =>
  listen<QueueSnapshot>('queue://changed', (e) => cb(e.payload));

/** Closing was held because an installer is running; the UI asks the user. */
export const onCloseBlocked = (cb: () => void): Promise<UnlistenFn> => listen('app://close-blocked', () => cb());
