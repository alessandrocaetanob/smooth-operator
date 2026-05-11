import { Injectable, signal, effect } from '@angular/core';

const STORAGE_KEY = 'smooth-operator.sidebar.collapsed';

@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly _collapsed = signal<boolean>(this.readInitial());
  private readonly _mobileNavOpen = signal(false);
  readonly collapsed = this._collapsed.asReadonly();
  readonly mobileNavOpen = this._mobileNavOpen.asReadonly();

  constructor() {
    effect(() => {
      const value = this._collapsed();
      try {
        localStorage.setItem(STORAGE_KEY, value ? '1' : '0');
      } catch {
        // localStorage unavailable; ignore
      }
    });
  }

  toggle(): void {
    this._collapsed.update((v) => !v);
  }

  setCollapsed(value: boolean): void {
    this._collapsed.set(value);
  }

  toggleMobileNav(): void {
    this._mobileNavOpen.update((v) => !v);
  }

  openMobileNav(): void {
    this._mobileNavOpen.set(true);
  }

  closeMobileNav(): void {
    this._mobileNavOpen.set(false);
  }

  private readInitial(): boolean {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  }
}
