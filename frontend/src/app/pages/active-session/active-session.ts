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

interface TermColorScheme {
  value: string;
  label: string;
  preview: string;
}

const TERM_COLOR_SCHEMES: TermColorScheme[] = [
  { value: 'gray-black', label: 'Gray / Black', preview: '#111' },
  { value: 'green-black', label: 'Green / Black', preview: '#0d1a0d' },
  { value: 'white-black', label: 'White / Black', preview: '#1a1a1a' },
  { value: 'black-white', label: 'Black / White', preview: '#f5f5f5' },
  { value: 'solarized-dark', label: 'Solarized Dark', preview: '#002b36' },
  { value: 'solarized-light', label: 'Solarized Light', preview: '#fdf6e3' },
];

const TERM_FONT_NAMES = [
  'monospace',
  'Courier New',
  'Consolas',
  'DejaVu Sans Mono',
  'Source Code Pro',
  'Inconsolata',
];

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
  @ViewChild('hiddenKbd', { static: false }) hiddenKbdRef?: ElementRef<HTMLInputElement>;

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
  readonly showTerminalThemeModal = signal(false);
  readonly comboCtrl = signal(true);
  readonly comboAlt = signal(false);
  readonly comboShift = signal(false);
  readonly comboSuper = signal(false);
  readonly comboKey = signal<KeyOption>(COMBO_KEYS[0]);
  readonly clipboardDraft = signal('');

  // ── Mobile keyboard ────────────────────────────────────────────────────────
  // Touch devices have no physical keyboard, and Guacamole.Keyboard only listens
  // for physical key events. A hidden input summons the phone's native soft
  // keyboard for text; the special-key bar covers keys a soft keyboard lacks.
  readonly keyboardActive = signal(false);
  readonly kbdCtrl = signal(false);
  readonly kbdAlt = signal(false);
  /** Height of the on-screen keyboard, so the special-key bar sits just above it. */
  readonly kbdInset = signal(0);
  readonly specialKeys: KeyOption[] = [
    { label: 'Esc', keysym: Keysyms.Escape },
    { label: 'Tab', keysym: Keysyms.Tab },
    { label: '←', keysym: Keysyms.ArrowLeft },
    { label: '↑', keysym: Keysyms.ArrowUp },
    { label: '↓', keysym: Keysyms.ArrowDown },
    { label: '→', keysym: Keysyms.ArrowRight },
  ];
  private composing = false;
  private viewportListener: (() => void) | null = null;

  // Protocol badge
  readonly protocol = computed(() => (this.connection()?.protocol ?? '').toUpperCase());
  readonly isSshConnection = computed(
    () => (this.connection()?.protocol ?? '').toLowerCase() === 'ssh',
  );
  readonly termColorScheme = signal('gray-black');
  readonly termFontName = signal('monospace');
  readonly termFontSize = signal(12);
  readonly termColorSchemes = TERM_COLOR_SCHEMES;
  readonly termFontNames = TERM_FONT_NAMES;

  // Mascot sparkle flash on first connect (3 s)
  readonly mascotConnectedFlash = signal(false);

  // Toolbar auto-hide (fades after 3 s inactivity)
  readonly toolbarVisible = signal(true);
  readonly toolbarCollapsed = signal(false);

  // Display zoom (1 = fit-to-screen). Adjusted via the toolbar zoom buttons.
  readonly zoom = signal(1);
  readonly zoomPercent = computed(() => `${Math.round(this.zoom() * 100)}%`);

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
  private terminalThemeLoadedFor: string | null = null;

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

    effect(() => {
      const id = this.connectionId();
      const connection = this.connection();
      if (!id || connection?.protocol?.toLowerCase() !== 'ssh') return;
      if (this.terminalThemeLoadedFor === id) return;
      this.terminalThemeLoadedFor = id;
      this.loadSessionTerminalTheme(connection.settings || '{}');
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
    // Reflect any zoom already applied to this session (e.g. after re-opening
    // a minimized session) so the toolbar state matches the canvas.
    const existingZoom = this.session()?.getZoom();
    if (existingZoom) this.zoom.set(existingZoom);
    this.onCanvasMouseMove();
  }

  ngOnDestroy(): void {
    this.mounted = false;
    this.displayEffect?.destroy();
    this.displayEffect = null;
    if (this.toolbarHideTimer) clearTimeout(this.toolbarHideTimer);
    this.detachViewportListener();
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

  openTerminalThemeModal(): void {
    this.showTerminalThemeModal.set(true);
  }

  closeTerminalThemeModal(): void {
    this.showTerminalThemeModal.set(false);
  }

  saveTerminalThemeForSession(): void {
    const id = this.connectionId();
    if (!id || typeof sessionStorage === 'undefined') return;
    const payload = {
      colorScheme: this.termColorScheme(),
      fontName: this.termFontName(),
      fontSize: this.termFontSize(),
    };
    sessionStorage.setItem(`smooth-operator.session-terminal.${id}`, JSON.stringify(payload));
    this.closeTerminalThemeModal();
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
    if (document.fullscreenElement) {
      document.exitFullscreen?.().catch(() => {
        /* intentional no-op */
      });
    } else {
      el.requestFullscreen?.().catch(() => {
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

  // ── Display zoom ───────────────────────────────────────────────────────────
  zoomIn(): void {
    this.applyZoom(this.zoom() + 0.25);
  }

  zoomOut(): void {
    this.applyZoom(this.zoom() - 0.25);
  }

  /** Reset zoom back to fit-to-screen. */
  zoomReset(): void {
    this.applyZoom(1);
  }

  private applyZoom(value: number): void {
    const clamped = Math.min(3, Math.max(0.5, Math.round(value * 4) / 4));
    this.zoom.set(clamped);
    this.session()?.setZoom(clamped);
  }

  // ── Mobile keyboard ────────────────────────────────────────────────────────
  /** Show/hide the soft keyboard by focusing/blurring the hidden input. */
  toggleMobileKeyboard(): void {
    const next = !this.keyboardActive();
    this.keyboardActive.set(next);
    if (next) {
      this.attachViewportListener();
      // Focus synchronously inside the click gesture — iOS only opens the soft
      // keyboard when focus() runs within the originating user gesture.
      this.hiddenKbdRef?.nativeElement.focus();
    } else {
      this.clearKbdModifiers();
      this.hiddenKbdRef?.nativeElement.blur();
      this.detachViewportListener();
    }
  }

  /** The hidden input lost focus (user dismissed the keyboard) — hide the bar. */
  onKbdBlur(): void {
    this.keyboardActive.set(false);
    this.clearKbdModifiers();
    this.detachViewportListener();
  }

  /** Translate native soft-keyboard edits into remote key events. */
  onKbdBeforeInput(event: Event): void {
    const ev = event as InputEvent;
    const session = this.session();
    if (!session) return;
    switch (ev.inputType) {
      case 'insertText':
        if (this.composing) return; // committed text arrives via compositionend
        if (ev.data) this.sendKbdText(ev.data);
        break;
      case 'insertLineBreak':
      case 'insertParagraph':
        this.pressSpecialKey(Keysyms.Return);
        break;
      case 'deleteContentBackward':
        this.pressSpecialKey(Keysyms.Backspace);
        break;
      case 'deleteContentForward':
        this.pressSpecialKey(Keysyms.Delete);
        break;
      default:
        return; // composition / unsupported — leave the input untouched
    }
    // Keep the hidden input empty so it never accumulates state.
    ev.preventDefault();
  }

  onKbdCompositionStart(): void {
    this.composing = true;
  }

  onKbdCompositionEnd(event: Event): void {
    this.composing = false;
    const ev = event as CompositionEvent;
    if (ev.data) this.sendKbdText(ev.data);
    const el = this.hiddenKbdRef?.nativeElement;
    if (el) el.value = '';
  }

  /** Send a special key (Esc/Tab/arrows), applying any one-shot modifiers. */
  pressSpecialKey(keysym: number): void {
    this.session()?.sendKeyCombo([...this.activeKbdMods(), keysym]);
    this.clearKbdModifiers();
    // Buttons keep focus off the input via pointerdown preventDefault; re-assert.
    if (this.keyboardActive()) this.hiddenKbdRef?.nativeElement.focus();
  }

  toggleKbdModifier(name: 'ctrl' | 'alt'): void {
    if (name === 'ctrl') this.kbdCtrl.update((v) => !v);
    else this.kbdAlt.update((v) => !v);
  }

  private sendKbdText(text: string): void {
    const session = this.session();
    if (!session) return;
    const mods = this.activeKbdMods();
    if (mods.length === 0) {
      session.typeText(text);
      return;
    }
    // Modifier + character (e.g. Ctrl+C): code points 0x20–0x7e are their own keysym.
    for (const ch of text) {
      const code = ch.codePointAt(0);
      if (code === undefined) continue;
      const keysym = code >= 0x20 && code <= 0x7e ? code : 0x01000000 | code;
      session.sendKeyCombo([...mods, keysym]);
    }
    this.clearKbdModifiers();
  }

  private activeKbdMods(): number[] {
    const mods: number[] = [];
    if (this.kbdCtrl()) mods.push(Keysyms.ControlLeft);
    if (this.kbdAlt()) mods.push(Keysyms.AltLeft);
    return mods;
  }

  private clearKbdModifiers(): void {
    this.kbdCtrl.set(false);
    this.kbdAlt.set(false);
  }

  private attachViewportListener(): void {
    const vv = globalThis.visualViewport;
    if (!vv || this.viewportListener) return;
    const update = () => {
      const inset = Math.max(0, globalThis.innerHeight - vv.height - vv.offsetTop);
      this.kbdInset.set(inset);
    };
    this.viewportListener = update;
    vv.addEventListener('resize', update);
    vv.addEventListener('scroll', update);
    update();
  }

  private detachViewportListener(): void {
    const vv = globalThis.visualViewport;
    if (vv && this.viewportListener) {
      vv.removeEventListener('resize', this.viewportListener);
      vv.removeEventListener('scroll', this.viewportListener);
    }
    this.viewportListener = null;
    this.kbdInset.set(0);
  }

  private loadSessionTerminalTheme(connectionSettings: string): void {
    const fromConnection = this.parseSettings(connectionSettings);
    let colorScheme = fromConnection['color-scheme'] ?? 'gray-black';
    let fontName = fromConnection['font-name'] ?? 'monospace';
    let fontSize = this.parseFontSize(fromConnection['font-size']);

    const stored = this.getStoredTerminalTheme();
    if (stored) {
      colorScheme = stored.colorScheme ?? colorScheme;
      fontName = stored.fontName ?? fontName;
      fontSize = stored.fontSize ?? fontSize;
    }

    this.termColorScheme.set(colorScheme);
    this.termFontName.set(fontName);
    this.termFontSize.set(fontSize);
  }

  private parseFontSize(value: string | undefined): number {
    const size = Number(value);
    return Number.isFinite(size) ? size : 12;
  }

  private getStoredTerminalTheme(): {
    colorScheme?: string;
    fontName?: string;
    fontSize?: number;
  } | null {
    const id = this.connectionId();
    if (!id || typeof sessionStorage === 'undefined') return null;

    const raw = sessionStorage.getItem(`smooth-operator.session-terminal.${id}`);
    if (!raw) return null;

    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }

  private parseSettings(json: string): Record<string, string> {
    try {
      return JSON.parse(json) ?? {};
    } catch {
      return {};
    }
  }
}
