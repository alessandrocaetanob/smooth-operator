import {
  Component,
  DestroyRef,
  ElementRef,
  Input,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';

export type MascotState =
  | 'idle'
  | 'typing'
  | 'password'
  | 'loading'
  | 'success'
  | 'error'
  | 'thinking'
  | 'wave'
  | 'sleep';

@Component({
  selector: 'app-mascot',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './mascot.html',
  styleUrl: './mascot.css',
})
export class Mascot implements OnInit {
  /** Primary state-driven API */
  @Input() state: MascotState = 'idle';

  /** When true, pupils follow the cursor. Enable only on the auth page. */
  @Input() trackPointer = false;

  /** @deprecated Use state="password" instead */
  @Input() isPasswordFocused = false;

  /** @deprecated Cursor tracking replaces typing-length math */
  @Input() typingLength = 0;

  @Input() isSmall = false;

  private readonly destroyRef = inject(DestroyRef);
  private readonly el = inject(ElementRef);

  readonly eyeOffsetX = signal(0);
  readonly eyeOffsetY = signal(0);

  /** Resolved state after applying deprecated-input shims */
  get effectiveState(): MascotState {
    if (this.isPasswordFocused) return 'password';
    return this.state;
  }

  ngOnInit(): void {
    if (!this.trackPointer) return;

    const handler = (e: MouseEvent) => {
      const rect = (this.el.nativeElement as HTMLElement).getBoundingClientRect();
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height * 0.42; // eyes are in upper half
      const dx = e.clientX - cx;
      const dy = e.clientY - cy;
      const maxOffset = 7;
      const dist = Math.sqrt(dx * dx + dy * dy);
      const scale = dist > 0 ? Math.min(maxOffset, dist) / dist : 0;
      this.eyeOffsetX.set(Math.round(dx * scale * 10) / 10);
      this.eyeOffsetY.set(Math.round(dy * scale * 10) / 10);
    };

    document.addEventListener('mousemove', handler, { passive: true });
    this.destroyRef.onDestroy(() => document.removeEventListener('mousemove', handler));
  }
}

