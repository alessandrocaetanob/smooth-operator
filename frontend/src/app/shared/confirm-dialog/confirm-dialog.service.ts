import { Injectable, signal } from '@angular/core';

export type ConfirmTone = 'default' | 'danger';

export interface ConfirmRequest {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: ConfirmTone;
}

interface ActiveRequest extends ConfirmRequest {
  resolve: (result: boolean) => void;
}

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly _active = signal<ActiveRequest | null>(null);
  readonly active = this._active.asReadonly();

  ask(request: ConfirmRequest): Promise<boolean> {
    if (this._active()) {
      this._active()!.resolve(false);
    }
    return new Promise<boolean>((resolve) => {
      this._active.set({ ...request, resolve });
    });
  }

  resolve(result: boolean): void {
    const current = this._active();
    if (!current) return;
    this._active.set(null);
    current.resolve(result);
  }
}
