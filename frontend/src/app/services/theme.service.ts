import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>('dark');

  /** Call once during app bootstrap. Reads localStorage; defaults to dark. */
  init(): void {
    const saved = localStorage.getItem('theme') as Theme | null;
    // Dark-first: only honour an explicit saved 'light' preference; everything
    // else (no saved value, or saved 'dark') defaults to dark mode.
    const resolved: Theme = saved === 'light' ? 'light' : 'dark';
    this.apply(resolved);

    // Keep signal in sync when the OS preference changes (only if no saved override)
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
      if (!localStorage.getItem('theme')) {
        this.apply(e.matches ? 'dark' : 'light');
      }
    });
  }

  toggle(): void {
    this.apply(this.theme() === 'dark' ? 'light' : 'dark');
  }

  private apply(t: Theme): void {
    this.theme.set(t);
    const root = document.documentElement;
    if (t === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
    localStorage.setItem('theme', t);
  }
}
