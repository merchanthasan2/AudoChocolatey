import type { Theme } from '../ipc/bindings/Theme';
import type { Motion } from '../ipc/bindings/Motion';

/** `undefined` means "follow Windows" (CSS media query decides). */
export function resolveTheme(theme: Theme): 'light' | 'dark' | undefined {
  return theme === 'system' ? undefined : theme;
}

export function resolveMotion(motion: Motion, osReduced: boolean): 'full' | 'reduced' {
  if (motion === 'system') return osReduced ? 'reduced' : 'full';
  return motion;
}
