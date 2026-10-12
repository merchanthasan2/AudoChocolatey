import type { Job } from '../ipc/bindings/Job';

export type ButtonMode = 'install' | 'update' | 'uninstall';
export type ButtonKind = 'idle' | 'queued' | 'progress' | 'working' | 'done' | 'failed' | 'skipped';

export interface ButtonState {
  kind: ButtonKind;
  percent: number | null;
  label: string;
  cancellable: boolean;
}

const IDLE: Record<ButtonMode, string> = { install: 'Get', update: 'Update', uninstall: 'Uninstall' };

const WORKING: Partial<Record<Job['phase'], string>> = {
  preparing: 'Getting ready',
  waiting: 'Waiting for another installer',
  downloading: 'Downloading',
  installing: 'Installing',
  verifying: 'Finishing',
};

/** Maps a queue job onto the one morphing button the UI shows (Get -> ring -> tick -> Done). */
export function jobButtonState(job: Job | undefined, mode: ButtonMode): ButtonState {
  const idle: ButtonState = { kind: 'idle', percent: null, label: IDLE[mode], cancellable: false };
  if (!job) return idle;
  switch (job.phase) {
    case 'queued':
      return { kind: 'queued', percent: null, label: 'Queued', cancellable: true };
    case 'downloading':
      if (job.percent != null) {
        return { kind: 'progress', percent: job.percent, label: 'Downloading', cancellable: job.cancellable };
      }
      return { kind: 'working', percent: null, label: 'Downloading', cancellable: job.cancellable };
    case 'preparing':
    case 'waiting':
    case 'installing':
    case 'verifying':
      return { kind: 'working', percent: null, label: WORKING[job.phase]!, cancellable: job.cancellable };
    case 'succeeded':
      return { kind: 'done', percent: null, label: job.rebootRequired ? 'Restart needed' : 'Done', cancellable: false };
    case 'skipped':
      return {
        kind: 'skipped',
        percent: null,
        label: job.detail === 'Already installed' ? 'Installed' : 'Up to date',
        cancellable: false,
      };
    case 'failed':
      return { kind: 'failed', percent: null, label: 'Retry', cancellable: false };
    case 'cancelled':
      return idle;
  }
}
