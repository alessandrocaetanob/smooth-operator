import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ConfirmDialogService } from './confirm-dialog.service';
import { fadeIn, scaleIn } from '../animations';
import { Mascot } from '../mascot/mascot';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [Mascot],
  templateUrl: './confirm-dialog.html',
  animations: [fadeIn, scaleIn],
})
export class ConfirmDialog implements AfterViewInit, OnDestroy {
  private readonly svc = inject(ConfirmDialogService);
  readonly active = this.svc.active;

  readonly tone = computed(() => this.active()?.tone ?? 'default');
  readonly confirmLabel = computed(() => this.active()?.confirmLabel ?? 'Confirm');
  readonly cancelLabel = computed(() => this.active()?.cancelLabel ?? 'Cancel');

  /** Countdown state for danger dialogs (3 s lock) */
  readonly isCountingDown = signal(false);
  private countdownTimer?: ReturnType<typeof setTimeout>;

  @ViewChild('confirmBtn') confirmBtn?: ElementRef<HTMLButtonElement>;

  constructor() {
    effect(() => {
      const req = this.active();
      if (req) {
        queueMicrotask(() => this.confirmBtn?.nativeElement?.focus());
        // For danger tone: lock the confirm button for 3 seconds
        if (req.tone === 'danger') {
          this.isCountingDown.set(true);
          clearTimeout(this.countdownTimer);
          this.countdownTimer = setTimeout(() => this.isCountingDown.set(false), 3000);
        } else {
          this.isCountingDown.set(false);
        }
      } else {
        // Dialog closed — cancel any pending countdown
        clearTimeout(this.countdownTimer);
        this.isCountingDown.set(false);
      }
    });
  }

  ngAfterViewInit(): void {
    this.confirmBtn?.nativeElement?.focus();
  }

  ngOnDestroy(): void {
    clearTimeout(this.countdownTimer);
  }

  cancel(): void {
    this.isCountingDown.set(false);
    clearTimeout(this.countdownTimer);
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
