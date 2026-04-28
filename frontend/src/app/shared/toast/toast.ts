import { Component, OnDestroy, inject } from '@angular/core';
import { Toast, ToastService } from './toast.service';
import { toastEnter } from '../animations';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  templateUrl: './toast.html',
  animations: [toastEnter],
})
export class ToastContainer implements OnDestroy {
  private readonly svc = inject(ToastService);
  readonly toasts = this.svc.toasts;

  private readonly tick = setInterval(() => {
    // Force change-detection each tick so drain bars stay live.
    // Signals already handle this; the interval just ensures sub-second repaint.
  }, 100);

  ngOnDestroy(): void {
    clearInterval(this.tick);
  }

  trackById(_: number, t: Toast): number {
    return t.id;
  }

  /** Returns CSS animation-duration string for the drain bar */
  drainDuration(t: Toast): string {
    return `${t.ttl}ms`;
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
      case 'warning':
        return 'border-amber-400/40 bg-amber-400/10 text-amber-300 dark:text-amber-200';
      default:
        return 'border-primary-container/40 bg-primary-container/15 text-on-primary-container';
    }
  }

  drainClass(kind: Toast['kind']): string {
    switch (kind) {
      case 'success':
        return 'bg-tertiary';
      case 'error':
        return 'bg-error';
      case 'warning':
        return 'bg-amber-400';
      default:
        return 'bg-primary';
    }
  }

  toneIcon(kind: Toast['kind']): string {
    switch (kind) {
      case 'success':
        return 'check_circle';
      case 'error':
        return 'error';
      case 'warning':
        return 'warning';
      default:
        return 'info';
    }
  }
}
