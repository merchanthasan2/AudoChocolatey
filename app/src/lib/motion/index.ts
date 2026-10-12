import { backOut, cubicOut } from 'svelte/easing';
import type { TransitionConfig } from 'svelte/transition';

/** True when Windows "reduce motion" is on or the user chose Reduced in Settings. */
export function reducedMotion(): boolean {
  return document.documentElement.dataset.motion === 'reduced';
}

/** Springy rise for occasional moments (results arriving, rows, empty states). */
export function rise(_node: Element, { delay = 0, y = 10 }: { delay?: number; y?: number } = {}): TransitionConfig {
  if (reducedMotion()) return { duration: 160, delay: 0, css: (t) => `opacity:${t}` };
  return {
    delay,
    duration: 420,
    easing: backOut,
    css: (t, u) => `opacity:${Math.min(1, t * 1.6)};transform:translateY(${u * y}px) scale(${0.97 + 0.03 * t})`,
  };
}

/** Pop for confirmations (check marks, "Everything is up to date"). Never from scale(0). */
export function pop(_node: Element, { delay = 0 }: { delay?: number } = {}): TransitionConfig {
  if (reducedMotion()) return { duration: 160, css: (t) => `opacity:${t}` };
  return { delay, duration: 460, easing: backOut, css: (t) => `opacity:${Math.min(1, t * 2)};transform:scale(${0.9 + 0.1 * t})` };
}

/** Rows leaving a list: fade and collapse quickly (exit is faster than enter). */
export function collapse(node: Element): TransitionConfig {
  const h = (node as HTMLElement).offsetHeight;
  if (reducedMotion()) return { duration: 120, css: (t) => `opacity:${t}` };
  return {
    duration: 260,
    easing: cubicOut,
    css: (t) => `opacity:${t};height:${t * h}px;transform:translateX(${(1 - t) * 16}px);overflow:hidden`,
  };
}

/** View switches happen many times a day: a short fade only, no movement. */
export function viewFade(_node: Element): TransitionConfig {
  return { duration: 140, css: (t) => `opacity:${t}` };
}
