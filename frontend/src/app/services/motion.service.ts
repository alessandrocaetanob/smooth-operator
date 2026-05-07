import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class MotionService {
  /** True when the user has requested reduced motion. */
  readonly reducedMotion = signal(false);

  /** Call once during app bootstrap. */
  init(): void {
    const mq = globalThis.matchMedia('(prefers-reduced-motion: reduce)');
    this.reducedMotion.set(mq.matches);
    mq.addEventListener('change', (e) => this.reducedMotion.set(e.matches));
  }
}
