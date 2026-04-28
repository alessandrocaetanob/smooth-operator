import {
  AfterViewInit,
  Component,
  ElementRef,
  Injector,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  effect,
  inject,
  runInInjectionContext,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GuacamoleClientService, Keysyms } from '../../services/guacamole.service';
import { ConnectionsService, Connection } from '../../services/connections.service';
import { Mascot, MascotState } from '../../shared/mascot/mascot';

interface KeyOption {
  label: string;
  keysym: number;
}

const COMBO_KEYS: KeyOption[] = [
  { label: 'Delete', keysym: Keysyms.Delete },
  { label: 'Backspace', keysym: Keysyms.Backspace },
  { label: 'Enter', keysym: Keysyms.Return },
  { label: 'Escape', keysym: Keysyms.Escape },
  { label: 'Tab', keysym: Keysyms.Tab },
  { label: 'Insert', keysym: Keysyms.Insert },
  { label: 'Home', keysym: Keysyms.Home },
  { label: 'End', keysym: Keysyms.End },
  { label: 'PageUp', keysym: Keysyms.PageUp },
  { label: 'PageDown', keysym: Keysyms.PageDown },
  { label: '←', keysym: Keysyms.ArrowLeft },
  { label: '→', keysym: Keysyms.ArrowRight },
  { label: '↑', keysym: Keysyms.ArrowUp },
  { label: '↓', keysym: Keysyms.ArrowDown },
  { label: 'F1', keysym: Keysyms.F1 },
  { label: 'F2', keysym: Keysyms.F2 },
  { label: 'F3', keysym: Keysyms.F3 },
  { label: 'F4', keysym: Keysyms.F4 },
  { label: 'F5', keysym: Keysyms.F5 },
  { label: 'F6', keysym: Keysyms.F6 },
  { label: 'F7', keysym: Keysyms.F7 },
  { label: 'F8', keysym: Keysyms.F8 },
  { label: 'F9', keysym: Keysyms.F9 },
  { label: 'F10', keysym: Keysyms.F10 },
  { label: 'F11', keysym: Keysyms.F11 },
  { label: 'F12', keysym: Keysyms.F12 },
];

@Component({
  selector: 'app-active-session',
  imports: [CommonModule, FormsModule, Mascot],
  templateUrl: './active-session.html',
  styleUrl: './active-session.css',
})
export class ActiveSession implements OnInit, AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly guac = inject(GuacamoleClientService);
  private readonly connections = inject(ConnectionsService);

  @ViewChild('display', { static: false }) displayRef?: ElementRef<HTMLDivElement>;

  readonly state = this.guac.state;
  readonly progress = this.guac.progress;
  readonly errorMsg = this.guac.error;
  readonly hostClipboard = this.guac.hostClipboard;
  readonly remoteName = this.guac.displayName;

  readonly connectionId = computed(() => this.route.snapshot.paramMap.get('id'));
  readonly connection = computed<Connection | null>(() => {
    const id = this.connectionId();
    if (!id) return null;
    return this.connections.list().find((c) => c.id === id) ?? null;
  });

  readonly mascotState = computed<MascotState>(() => {
    switch (this.state()) {
      case 'connecting':
      case 'requesting-ticket':
      case 'waiting':
        return 'loading';
      case 'error':
      case 'disconnected':
        return 'error';
      default:
        return 'idle';
    }
  });

  readonly comboKeys = COMBO_KEYS;
  readonly showKeyComboModal = signal(false);
  readonly showClipboardModal = signal(false);
  readonly comboCtrl = signal(true);
  readonly comboAlt = signal(false);
  readonly comboShift = signal(false);
  readonly comboSuper = signal(false);
  readonly comboKey = signal<KeyOption>(COMBO_KEYS[0]);
  readonly clipboardDraft = signal('');

  private mounted = false;
  private readonly injector = inject(Injector);

  constructor() {
    // If the client gets disconnected (or errors after connection),
    // bounce the user back to the ConnectingState page so they see the log.
    effect(() => {
      const s = this.state();
      const id = this.connectionId();
      if (!id) return;
      if (s === 'error' || s === 'disconnected') {
        if (this.mounted) {
          this.router.navigate(['/connecting', id]);
        }
      }
    });
  }

  ngOnInit(): void {
    const id = this.connectionId();
    if (!id) {
      this.router.navigate(['/vault']);
      return;
    }
    if (this.connections.list().length === 0) {
      this.connections.reload().subscribe({ error: () => {} });
    }
    // If user landed here via deep link without going through /connecting, kick
    // off the connect flow now.
    if (this.state() === 'idle' || this.state() === 'disconnected') {
      this.router.navigate(['/connecting', id]);
      return;
    }
  }

  ngAfterViewInit(): void {
    this.mounted = true;
    if (this.displayRef && this.guac.isConnected()) {
      this.guac.attachDisplay(this.displayRef.nativeElement);
    } else if (this.displayRef) {
      // Wait until connected, then attach.
      runInInjectionContext(this.injector, () => {
        const stop = effect(
          (onCleanup) => {
            if (this.guac.isConnected() && this.displayRef) {
              this.guac.attachDisplay(this.displayRef.nativeElement);
              onCleanup(() => stop.destroy());
            }
          },
          { manualCleanup: true },
        );
      });
    }
  }

  ngOnDestroy(): void {
    this.mounted = false;
  }

  // Toolbar actions ----------------------------------------------------------
  sendCtrlAltDel(): void {
    this.guac.sendKeyCombo([Keysyms.ControlLeft, Keysyms.AltLeft, Keysyms.Delete]);
  }

  openKeyComboModal(): void {
    this.showKeyComboModal.set(true);
  }
  closeKeyComboModal(): void {
    this.showKeyComboModal.set(false);
  }

  sendKeyCombo(): void {
    const syms: number[] = [];
    if (this.comboCtrl()) syms.push(Keysyms.ControlLeft);
    if (this.comboAlt()) syms.push(Keysyms.AltLeft);
    if (this.comboShift()) syms.push(Keysyms.ShiftLeft);
    if (this.comboSuper()) syms.push(Keysyms.SuperLeft);
    syms.push(this.comboKey().keysym);
    this.guac.sendKeyCombo(syms);
    this.closeKeyComboModal();
  }

  selectComboKey(key: KeyOption): void {
    this.comboKey.set(key);
  }

  openClipboardModal(): void {
    this.clipboardDraft.set(this.hostClipboard() || '');
    this.showClipboardModal.set(true);
  }
  closeClipboardModal(): void {
    this.showClipboardModal.set(false);
  }
  pushClipboardToHost(): void {
    const text = this.clipboardDraft();
    if (text) this.guac.pasteToHost(text);
    this.closeClipboardModal();
  }
  copyHostClipboardLocally(): void {
    const text = this.hostClipboard();
    if (!text) return;
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(text).catch(() => {});
    }
  }

  takeScreenshot(): void {
    const url = this.guac.captureScreenshot();
    if (!url) return;
    const a = document.createElement('a');
    a.href = url;
    a.download = `screenshot-${Date.now()}.png`;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  toggleFullscreen(): void {
    const el = this.displayRef?.nativeElement;
    if (!el) return;
    if (!document.fullscreenElement) {
      el.requestFullscreen?.().catch(() => {});
    } else {
      document.exitFullscreen?.().catch(() => {});
    }
  }

  terminate(): void {
    this.guac.disconnect();
    this.guac.reset();
    this.router.navigate(['/vault']);
  }

  toggleModifier(name: 'ctrl' | 'alt' | 'shift' | 'super'): void {
    switch (name) {
      case 'ctrl':
        this.comboCtrl.update((v) => !v);
        break;
      case 'alt':
        this.comboAlt.update((v) => !v);
        break;
      case 'shift':
        this.comboShift.update((v) => !v);
        break;
      case 'super':
        this.comboSuper.update((v) => !v);
        break;
    }
  }
}
