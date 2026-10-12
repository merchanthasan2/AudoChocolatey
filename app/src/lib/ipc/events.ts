import { listen, type UnlistenFn } from '@tauri-apps/api/event';
import type { Job } from './bindings/Job';

// Typed wrappers for the Rust queue's events. Each returns an unsubscribe function.
export const onJobChanged = (cb: (job: Job) => void): Promise<UnlistenFn> =>
  listen<Job>('job://changed', (e) => cb(e.payload));

export const onJobProgress = (cb: (job: Job) => void): Promise<UnlistenFn> =>
  listen<Job>('job://progress', (e) => cb(e.payload));

export const onQueueChanged = (cb: (jobs: Job[]) => void): Promise<UnlistenFn> =>
  listen<Job[]>('queue://changed', (e) => cb(e.payload));
