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
        return 'border-emerald-500/40 bg-emerald-950/80 text-emerald-100';
      case 'error':
        return 'border-red-500/40 bg-red-950/80 text-red-100';
      default:
        return 'border-blue-500/40 bg-blue-950/80 text-blue-100';
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
