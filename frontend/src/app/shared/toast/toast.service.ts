import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
  ttl: number;
  startedAt: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly _toasts = signal<Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();

  success(message: string): void {
    this.push('success', message, 3500);
  }

  error(message: string): void {
    this.push('error', message, 6000);
  }

  info(message: string): void {
    this.push('info', message, 3500);
  }

  warning(message: string): void {
    this.push('warning', message, 5000);
  }

  dismiss(id: number): void {
    this._toasts.update((arr) => arr.filter((t) => t.id !== id));
  }

  private push(kind: ToastKind, message: string, ttl: number): void {
    const id = this.nextId++;
    const startedAt = Date.now();
    this._toasts.update((arr) => [...arr, { id, kind, message, ttl, startedAt }]);
    setTimeout(() => this.dismiss(id), ttl);
  }
}
