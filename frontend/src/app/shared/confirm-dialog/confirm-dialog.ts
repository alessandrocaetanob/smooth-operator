import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  ViewChild,
  computed,
  effect,
  inject,
} from '@angular/core';
import { ConfirmDialogService } from './confirm-dialog.service';
import { fadeIn, scaleIn } from '../animations';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  templateUrl: './confirm-dialog.html',
  animations: [fadeIn, scaleIn],
})
export class ConfirmDialog implements AfterViewInit {
  private readonly svc = inject(ConfirmDialogService);
  readonly active = this.svc.active;

  readonly tone = computed(() => this.active()?.tone ?? 'default');
  readonly confirmLabel = computed(() => this.active()?.confirmLabel ?? 'Confirm');
  readonly cancelLabel = computed(() => this.active()?.cancelLabel ?? 'Cancel');

  @ViewChild('confirmBtn') confirmBtn?: ElementRef<HTMLButtonElement>;

  constructor() {
    effect(() => {
      if (this.active()) {
        queueMicrotask(() => this.confirmBtn?.nativeElement?.focus());
      }
    });
  }

  ngAfterViewInit(): void {
    this.confirmBtn?.nativeElement?.focus();
  }

  cancel(): void {
    this.svc.resolve(false);
  }

  confirm(): void {
    this.svc.resolve(true);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.active()) this.cancel();
  }
}
