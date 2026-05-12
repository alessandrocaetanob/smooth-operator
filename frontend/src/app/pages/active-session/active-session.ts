import {
  AfterViewInit,
  Component,
  EffectRef,
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
import { map } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  GuacamoleSessionManagerService,
  GuacamoleSession,
  Keysyms,
} from '../../services/guacamole.service';
import { ConnectionsService, Connection } from '../../services/connections.service';
import { Mascot, MascotState } from '../../shared/mascot/mascot';
import { Spinner } from '../../shared/spinner/spinner';
import { ThemeToggle } from '../../shared/theme-toggle/theme-toggle';

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
  imports: [CommonModule, FormsModule, Mascot, Spinner, ThemeToggle],
  templateUrl: './active-session.html',
  styleUrl: './active-session.css',
})
export class ActiveSession implements OnInit, AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sessionManager = inject(GuacamoleSessionManagerService);
  private readonly connections = inject(ConnectionsService);

  @ViewChild('display', { static: false }) displayRef?: ElementRef<HTMLDivElement>;

  // Reactive route param — correctly handles navigating between session IDs.
  readonly connectionId = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: this.route.snapshot.paramMap.get('id'),
  });

  // Computed signals that proxy through the session for this connectionId.
  private readonly session = computed<GuacamoleSession | null>(() => {
    const id = this.connectionId();
    if (!id) return null;
    return this.sessionManager.sessions().get(id) ?? null;
  });

  readonly state = computed(() => this.session()?.state() ?? ('idle' as const));
  readonly progress = computed(() => this.session()?.progress() ?? 0);
  readonly errorMsg = computed(() => this.session()?.error() ?? null);
  readonly hostClipboard = computed(() => this.session()?.hostClipboard() ?? '');
  readonly remoteName = computed(() => this.session()?.displayName() ?? null);
  readonly formattedElapsed = computed(() => this.session()?.formattedElapsed() ?? '00:00');

  readonly connection = computed<Connection | null>(() => {
    const id = this.connectionId();
    if (!id) return null;
    return this.connections.listAsMap().get(id) ?? null;
  });

  readonly mascotState = computed<MascotState>(() => {
    switch (this.state()) {
      case 'connecting':
      case 'requesting-ticket':
      case 'waiting':
        return 'loading';
      case 'connected':
        return this.mascotConnectedFlash() ? 'connected' : 'idle';
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

  // Protocol badge
  readonly protocol = computed(() => (this.connection()?.protocol ?? '').toUpperCase());

  // Mascot sparkle flash on first connect (3 s)
  readonly mascotConnectedFlash = signal(false);

  // Toolbar auto-hide (fades after 3 s inactivity)
  readonly toolbarVisible = signal(true);
  readonly toolbarCollapsed = signal(false);

  // Modal feedback
  readonly clipboardPushed = signal(false);
  readonly comboSent = signal(false);

  // Live key combo preview
  readonly comboPreview = computed(() => {
    const mods: string[] = [];
    if (this.comboCtrl()) mods.push('Ctrl');
    if (this.comboAlt()) mods.push('Alt');
    if (this.comboShift()) mods.push('Shift');
    if (this.comboSuper()) mods.push('⊞ Win');
    return [...mods, this.comboKey().label].join(' + ');
  });

  private mounted = false;
  private displayEffect: EffectRef | null = null;
  private readonly injector = inject(Injector);
  private mascotFlashStarted = false;
  private toolbarHideTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // On error: go to connecting page so the user sees logs and can retry.
    // On clean disconnect (no error message): session ended normally — go back to vault.
    effect(() => {
      const s = this.state();
      const id = this.connectionId();
      if (!id) return;
      if (s === 'error') {
        if (this.mounted) {
          this.router.navigate(['/connecting', id]);
        }
      } else if (s === 'disconnected') {
        if (this.mounted) {
          const hasError = !!this.errorMsg();
          this.router.navigate(hasError ? ['/connecting', id] : ['/vault']);
        }
      }
    });

    // Mascot sparkle flash on first successful connect (3 s, UI only).
    effect(() => {
      const s = this.state();
      if (s === 'connected' && !this.mascotFlashStarted) {
        this.mascotFlashStarted = true;
        this.mascotConnectedFlash.set(true);
        setTimeout(() => this.mascotConnectedFlash.set(false), 3000);
      } else if (s === 'disconnected' || s === 'error') {
        this.mascotFlashStarted = false;
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
      this.connections.reload().subscribe({
        error: () => {
          /* intentional no-op */
        },
      });
    }
    // If user landed here via deep link without going through /connecting, kick
    // off the connect flow now.
    const s = this.state();
    if (s === 'idle' || s === 'disconnected') {
      this.router.navigate(['/connecting', id]);
    }
  }

  ngAfterViewInit(): void {
    this.mounted = true;
    const session = this.session();
    if (this.displayRef && session?.isConnected()) {
      session.attachDisplay(this.displayRef.nativeElement);
    } else if (this.displayRef) {
      runInInjectionContext(this.injector, () => {
        let attached = false;
        this.displayEffect = effect(
          () => {
            const s = this.session();
            if (!attached && s?.isConnected() && this.displayRef) {
              attached = true;
              s.attachDisplay(this.displayRef.nativeElement);
            }
          },
          { manualCleanup: true },
        );
      });
    }
    this.onCanvasMouseMove();
  }

  ngOnDestroy(): void {
    this.mounted = false;
    this.displayEffect?.destroy();
    this.displayEffect = null;
    if (this.toolbarHideTimer) clearTimeout(this.toolbarHideTimer);
    const id = this.connectionId();
    if (!id) return;
    const session = this.sessionManager.get(id);
    if (session?.minimized()) {
      // Detach display bindings only — session stays alive in the session bar.
      session.detachDisplay();
    } else {
      // User navigated away without minimizing — clean up the session.
      this.sessionManager.destroy(id);
    }
  }

  // Toolbar actions ----------------------------------------------------------
  sendCtrlAltDel(): void {
    this.session()?.sendKeyCombo([Keysyms.ControlLeft, Keysyms.AltLeft, Keysyms.Delete]);
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
    this.session()?.sendKeyCombo(syms);
    this.comboSent.set(true);
    setTimeout(() => {
      this.comboSent.set(false);
      this.closeKeyComboModal();
    }, 900);
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
    if (text) this.session()?.pasteToHost(text);
    this.clipboardPushed.set(true);
    setTimeout(() => {
      this.clipboardPushed.set(false);
      this.closeClipboardModal();
    }, 1200);
  }
  copyHostClipboardLocally(): void {
    const text = this.hostClipboard();
    if (!text) return;
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(text).catch(() => {
        /* intentional no-op */
      });
    }
  }

  takeScreenshot(): void {
    const url = this.session()?.captureScreenshot();
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
      el.requestFullscreen?.().catch(() => {
        /* intentional no-op */
      });
    } else {
      document.exitFullscreen?.().catch(() => {
        /* intentional no-op */
      });
    }
  }

  /** Minimize session: detach display, keep WS alive, go to vault. */
  minimize(): void {
    const id = this.connectionId();
    if (id) this.sessionManager.minimize(id);
    this.router.navigate(['/vault']);
  }

  terminate(): void {
    const id = this.connectionId();
    if (id) this.sessionManager.destroy(id);
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

  // ── Toolbar auto-hide ──────────────────────────────────────────────────────
  onCanvasMouseMove(): void {
    this.toolbarVisible.set(true);
    if (this.toolbarHideTimer) clearTimeout(this.toolbarHideTimer);
    this.toolbarHideTimer = setTimeout(() => this.toolbarVisible.set(false), 3000);
  }

  toggleToolbarCollapse(): void {
    this.toolbarCollapsed.update((v) => !v);
  }
}
