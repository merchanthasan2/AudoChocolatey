import type { Overview } from '../ipc/commands';

export type AdminBanner = { text: string; askForAdmin: boolean };

/** The one banner about admin rights the app shows, if any. */
export function bannerFor(o: Overview | null): AdminBanner | null {
  if (!o) return null;
  if (!o.isElevated) {
    return {
      text: 'Quantum Updater needs administrator access to install and update apps. Windows will ask you to confirm.',
      askForAdmin: true,
    };
  }
  if (o.sameUser === false) {
    return {
      text: "Quantum Updater is running as a different administrator than the person signed in. Updates still work, but Microsoft Store apps and automatic updates are paused.",
      askForAdmin: false,
    };
  }
  return null;
}
