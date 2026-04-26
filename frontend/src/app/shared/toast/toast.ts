import { Component, inject } from '@angular/core';
import { Toast, ToastService } from './toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  templateUrl: './toast.html',
})
export class ToastContainer {
  private readonly svc = inject(ToastService);
  readonly toasts = this.svc.toasts;

  trackById(_: number, t: Toast): number {
    return t.id;
  }

  dismiss(id: number): void {
    this.svc.dismiss(id);
  }

  toneClass(kind: Toast['kind']): string {
    switch (kind) {
      case 'success':
        return 'border-tertiary/40 bg-tertiary-container/20 text-on-tertiary-container';
      case 'error':
        return 'border-error/40 bg-error-container/20 text-on-error-container';
      default:
        return 'border-primary-container/40 bg-primary-container/15 text-on-primary-container';
    }
  }

  toneIcon(kind: Toast['kind']): string {
    switch (kind) {
      case 'success':
        return 'check_circle';
      case 'error':
        return 'error';
      default:
        return 'info';
    }
  }
}
