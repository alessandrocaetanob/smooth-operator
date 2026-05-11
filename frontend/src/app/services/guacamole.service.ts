import { Injectable, NgZone, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import Guacamole from 'guacamole-common-js';

export type GuacState =
  | 'idle'
  | 'requesting-ticket'
  | 'connecting'
  | 'waiting'
  | 'connected'
  | 'disconnected'
  | 'error';

export interface GuacLogEntry {
  level: 'info' | 'ok' | 'warn' | 'error';
  message: string;
  timestamp: number;
}

// Maps Guacamole.Client state codes (per the source) to our domain state.
//   0=IDLE, 1=CONNECTING, 2=WAITING, 3=CONNECTED, 4=DISCONNECTING, 5=DISCONNECTED
const CLIENT_STATE_LABEL: Record<number, GuacState> = {
  0: 'idle',
  1: 'connecting',
  2: 'waiting',
  3: 'connected',
  4: 'disconnected',
  5: 'disconnected',
};

const PROGRESS_BY_STATE: Record<GuacState, number> = {
  idle: 0,
  'requesting-ticket': 15,
  connecting: 40,
  waiting: 70,
  connected: 100,
  disconnected: 0,
  error: 0,
};

// X11 keysyms used by the toolbar combos.
export const Keysyms = {
  Backspace: 0xff08,
  Tab: 0xff09,
  Return: 0xff0d,
  Escape: 0xff1b,
  Delete: 0xffff,
  ShiftLeft: 0xffe1,
  ControlLeft: 0xffe3,
  AltLeft: 0xffe9,
  SuperLeft: 0xffeb,
  Insert: 0xff63,
  Home: 0xff50,
  End: 0xff57,
  PageUp: 0xff55,
  PageDown: 0xff56,
  ArrowLeft: 0xff51,
  ArrowUp: 0xff52,
  ArrowRight: 0xff53,
  ArrowDown: 0xff54,
  F1: 0xffbe,
  F2: 0xffbf,
  F3: 0xffc0,
  F4: 0xffc1,
  F5: 0xffc2,
  F6: 0xffc3,
  F7: 0xffc4,
  F8: 0xffc5,
  F9: 0xffc6,
  F10: 0xffc7,
  F11: 0xffc8,
  F12: 0xffc9,
};

interface TicketResponse {
  ticket: string;
}

interface TicketRequest {
  terminalAppearance?: {
    colorScheme?: string;
    fontName?: string;
    fontSize?: number;
  };
}

/**
 * Per-connection session unit. Holds one Guacamole client + all its reactive
 * signals. Instantiated by GuacamoleSessionManagerService — not an Angular
 * injectable, so dependencies (HttpClient, NgZone) are passed via constructor.
 */
export class GuacamoleSession {
  private client: Guacamole.Client | null = null;
  private tunnel: Guacamole.Tunnel | null = null;
  private keyboard: Guacamole.Keyboard | null = null;
  private mouse: Guacamole.Mouse | null = null;
  private touch: Guacamole.Mouse.Touchscreen | null = null;
  private displayHost: HTMLElement | null = null;
  private resizeListener: (() => void) | null = null;
  private pasteListener: ((ev: ClipboardEvent) => void) | null = null;
  private timerInterval: ReturnType<typeof setInterval> | null = null;
  private connectPromise: Promise<void> | null = null;

  private readonly _state = signal<GuacState>('idle');
  private readonly _logs = signal<GuacLogEntry[]>([]);
  private readonly _error = signal<string | null>(null);
  private readonly _displayName = signal<string | null>(null);
  private readonly _hostClipboard = signal<string>('');
  private readonly _elapsedSeconds = signal(0);
  private readonly _connectedAt = signal<number | null>(null);

  /** True when the session is minimized (display detached but WS alive). */
  readonly minimized = signal(false);

  readonly state = this._state.asReadonly();
  readonly logs = this._logs.asReadonly();
  readonly error = this._error.asReadonly();
  readonly displayName = this._displayName.asReadonly();
  readonly hostClipboard = this._hostClipboard.asReadonly();
  readonly elapsedSeconds = this._elapsedSeconds.asReadonly();
  readonly connectedAt = this._connectedAt.asReadonly();
  readonly progress = computed(() => PROGRESS_BY_STATE[this._state()] ?? 0);
  readonly isConnected = computed(() => this._state() === 'connected');
  readonly formattedElapsed = computed(() => {
    const s = this._elapsedSeconds();
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const sec = s % 60;
    if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
    return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
  });

  constructor(
    readonly connectionId: string,
    private readonly http: HttpClient,
    private readonly zone: NgZone,
  ) {}

  /**
   * Idempotent connect: no-op when already connecting/connected.
   * Tears down any previous client before reconnecting.
   */
  async connect(): Promise<void> {
    const s = this._state();
    if (s === 'requesting-ticket' || s === 'connecting' || s === 'waiting' || s === 'connected') {
      return this.connectPromise ?? Promise.resolve();
    }
    if (this.client) {
      this.disconnect();
    }
    this._error.set(null);
    this._logs.set([]);
    this._displayName.set(null);
    this._hostClipboard.set('');
    this.minimized.set(false);

    this.setState('requesting-ticket');
    this.log('info', 'Requesting connection ticket...');

    this.connectPromise = this.doConnect();
    try {
      await this.connectPromise;
    } finally {
      this.connectPromise = null;
    }
  }

  private async doConnect(): Promise<void> {
    let ticket: string;
    try {
      const overrides = this.readSessionTerminalOverrides();
      const request: TicketRequest = overrides
        ? {
            terminalAppearance: {
              colorScheme: overrides['color-scheme'],
              fontName: overrides['font-name'],
              fontSize: Number(overrides['font-size']),
            },
          }
        : {};
      const res = await firstValueFrom(
        this.http.post<TicketResponse>(`/api/Guacamole/ticket/${this.connectionId}`, request),
      );
      ticket = res.ticket;
      this.log('ok', 'Ticket issued');
    } catch (err: unknown) {
      const msg = this.errorMessage(err);
      this.log('error', `Ticket request failed: ${msg}`);
      this._error.set(msg);
      this.setState('error');
      throw err;
    }

    this.log('info', 'Opening WebSocket tunnel to guacd...');
    const wsUrl = this.buildWebSocketUrl();
    const tunnel = new Guacamole.WebSocketTunnel(wsUrl);
    const client = new Guacamole.Client(tunnel);
    this.tunnel = tunnel;
    this.client = client;

    tunnel.onstatechange = (state) => {
      this.zone.run(() => {
        if (state === Guacamole.Tunnel.State.OPEN) {
          this.log('ok', 'Tunnel established');
        } else if (state === Guacamole.Tunnel.State.UNSTABLE) {
          this.log('warn', 'Tunnel unstable');
        } else if (state === Guacamole.Tunnel.State.CLOSED) {
          this.log('info', 'Tunnel closed');
        }
      });
    };
    tunnel.onerror = (status) => {
      this.zone.run(() => {
        this.log('error', `Tunnel error: ${status?.message ?? 'unknown'}`);
        this._error.set(status?.message ?? 'Tunnel error');
        this.setState('error');
      });
    };

    client.onstatechange = (state) => {
      this.zone.run(() => {
        const next = CLIENT_STATE_LABEL[state] ?? 'idle';
        this.setState(next);
        if (next === 'connecting') this.log('info', 'Authenticating with remote host...');
        if (next === 'waiting') this.log('info', 'Waiting for remote display...');
        if (next === 'connected') {
          this.log('ok', 'Session ready');
          this.startTimer();
        }
        if (next === 'disconnected') {
          this.log('info', 'Session disconnected');
          this.stopTimer();
        }
      });
    };
    client.onerror = (status) => {
      this.zone.run(() => {
        this.log('error', `Client error: ${status?.message ?? 'unknown'} (code ${status?.code})`);
        this._error.set(status?.message ?? 'Client error');
        this.setState('error');
        this.stopTimer();
      });
    };
    client.onname = (name) => {
      this.zone.run(() => {
        this._displayName.set(name);
      });
    };
    client.onclipboard = (stream, mimetype) => {
      if (!mimetype.startsWith('text/')) return;
      const reader = new Guacamole.StringReader(stream);
      let buffer = '';
      reader.ontext = (text) => {
        buffer += text;
      };
      reader.onend = () => {
        this.zone.run(() => {
          this._hostClipboard.set(buffer);
        });
        if (
          typeof navigator !== 'undefined' &&
          navigator.clipboard &&
          'writeText' in navigator.clipboard
        ) {
          navigator.clipboard.writeText(buffer).catch(() => {
            // Browser denied access (no gesture); modal fallback exposes hostClipboard().
          });
        }
      };
    };

    this.setState('connecting');
    this.log('info', 'Sending connect request...');
    try {
      // Guacamole's WebSocketTunnel appends `?<data>` to the tunnel URL when
      // connect(data) is called. We pass the ticket here so the final WS URL
      // is `/api/Guacamole/connect/{id}?ticket=...` — passing `client.connect()`
      // with no data would otherwise produce `?undefined` and a malformed URL.
      client.connect(`ticket=${encodeURIComponent(ticket)}`);
    } catch (err) {
      const msg = this.errorMessage(err);
      this.log('error', `Connect failed: ${msg}`);
      this._error.set(msg);
      this.setState('error');
      throw err;
    }
  }

  attachDisplay(host: HTMLElement): void {
    if (!this.client) return;

    // Idempotent: if we're already bound to this exact host, do nothing.
    if (this.displayHost === host && this.keyboard) {
      return;
    }

    // Re-attaching to a different host: neutralize old bindings first.
    this.detachDisplay();

    this.minimized.set(false);
    this.displayHost = host;
    const display = this.client.getDisplay();
    const el = display.getElement();

    while (host.firstChild) host.firstChild.remove();
    host.appendChild(el);

    const mouse = new Guacamole.Mouse(el);
    mouse.onmousedown =
      mouse.onmouseup =
      mouse.onmousemove =
        (state) => {
          if (this.client) this.client.sendMouseState(state);
        };
    this.mouse = mouse;

    const touch = new Guacamole.Mouse.Touchscreen(el);
    touch.onmousedown =
      touch.onmouseup =
      touch.onmousemove =
        (state) => {
          if (this.client) this.client.sendMouseState(state);
        };
    this.touch = touch;

    // Keyboard is bound to the document so the canvas need not be focused.
    // Skip events when the user is typing into a real input or a modal is open.
    const keyboard = new Guacamole.Keyboard(document);
    const isTypingInForm = (): boolean => {
      const ae = document.activeElement as HTMLElement | null;
      if (!ae) return false;
      const tag = ae.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return true;
      if ((ae as HTMLElement).isContentEditable) return true;
      if (document.querySelector('[data-guac-modal-open="true"]')) return true;
      return false;
    };
    keyboard.onkeydown = (keysym) => {
      if (!this.client) return false;
      if (isTypingInForm()) return true;
      this.client.sendKeyEvent(1, keysym);
      return false;
    };
    keyboard.onkeyup = (keysym) => {
      if (!this.client) return;
      if (isTypingInForm()) return;
      this.client.sendKeyEvent(0, keysym);
    };
    this.keyboard = keyboard;

    try {
      host.focus({ preventScroll: true });
    } catch {
      /* ignore focus errors */
    }

    const doResize = () => this.resizeToHost();
    this.resizeListener = doResize;
    globalThis.addEventListener('resize', doResize);
    display.onresize = () => doResize();
    queueMicrotask(doResize);

    const onPaste = (ev: ClipboardEvent) => {
      const target = ev.target as HTMLElement | null;
      if (target) {
        const tag = target.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA' || target.isContentEditable) return;
      }
      const text = ev.clipboardData?.getData('text/plain');
      if (text) this.pasteToHost(text);
    };
    this.pasteListener = onPaste;
    document.addEventListener('paste', onPaste);
  }

  /**
   * Tear down display-side bindings (keyboard/mouse/listeners) without
   * disconnecting the underlying client/tunnel. Safe to call when minimizing.
   *
   * Note: Guacamole.Keyboard exposes no public listener-removal API, so we
   * neutralize it by nulling its callbacks and resetting state. The orphaned
   * document listener will continue to receive events but dispatches nothing.
   */
  detachDisplay(): void {
    if (this.keyboard) {
      try {
        this.keyboard.onkeydown = null;
        this.keyboard.onkeyup = null;
        this.keyboard.reset();
      } catch {
        /* swallow */
      }
      this.keyboard = null;
    }
    if (this.mouse) {
      try {
        this.mouse.onmousedown = null;
        this.mouse.onmouseup = null;
        this.mouse.onmousemove = null;
      } catch {
        /* swallow */
      }
      this.mouse = null;
    }
    if (this.touch) {
      try {
        this.touch.onmousedown = null;
        this.touch.onmouseup = null;
        this.touch.onmousemove = null;
      } catch {
        /* swallow */
      }
      this.touch = null;
    }
    if (this.resizeListener) {
      globalThis.removeEventListener('resize', this.resizeListener);
      this.resizeListener = null;
    }
    if (this.pasteListener) {
      document.removeEventListener('paste', this.pasteListener);
      this.pasteListener = null;
    }
    if (this.displayHost) {
      while (this.displayHost.firstChild) {
        this.displayHost.firstChild.remove();
      }
    }
    this.displayHost = null;
  }

  disconnect(): void {
    this.stopTimer();
    if (this.client) {
      try {
        this.client.disconnect();
      } catch {
        /* swallow */
      }
    }
    this.detachDisplay();
    this.client = null;
    this.tunnel = null;
    this.setState('disconnected');
  }

  reset(): void {
    this._state.set('idle');
    this._logs.set([]);
    this._error.set(null);
    this._displayName.set(null);
    this._hostClipboard.set('');
    this._elapsedSeconds.set(0);
    this._connectedAt.set(null);
    this.minimized.set(false);
  }

  resizeToHost(): void {
    if (!this.client || !this.displayHost) return;
    const w = Math.max(320, Math.floor(this.displayHost.clientWidth));
    const h = Math.max(240, Math.floor(this.displayHost.clientHeight));
    const display = this.client.getDisplay();
    // Reset scale to 1 — CSS scaling causes blurry text in SSH terminals.
    display.scale(1);
    try {
      this.client.sendSize(w, h);
    } catch {
      // sendSize fails before the connect handshake completes; safe to ignore.
    }
  }

  sendKey(keysym: number, pressed: boolean): void {
    if (!this.client) return;
    this.client.sendKeyEvent(pressed ? 1 : 0, keysym);
  }

  // Press all keysyms, then release in reverse order (Ctrl+Alt+Delete etc.)
  sendKeyCombo(keysyms: number[]): void {
    if (!this.client || keysyms.length === 0) return;
    for (const k of keysyms) this.client.sendKeyEvent(1, k);
    for (let i = keysyms.length - 1; i >= 0; i--) this.client.sendKeyEvent(0, keysyms[i]);
  }

  pasteToHost(text: string): void {
    if (!this.client) return;
    try {
      const stream = this.client.createClipboardStream('text/plain');
      const writer = new Guacamole.StringWriter(stream);
      writer.sendText(text);
      writer.sendEnd();
    } catch {
      /* clipboard stream may not be supported on every protocol */
    }
    this.typeText(text);
  }

  typeText(text: string): void {
    if (!this.client || !text) return;
    for (const ch of text) {
      const code = ch.codePointAt(0);
      if (code === undefined) continue;
      const keysym = this.charToKeysym(ch, code);
      this.client.sendKeyEvent(1, keysym);
      this.client.sendKeyEvent(0, keysym);
    }
  }

  captureScreenshot(): string | null {
    const display = this.displayHost?.querySelector('canvas') as HTMLCanvasElement | null;
    if (!display) return null;
    try {
      return display.toDataURL('image/png');
    } catch {
      return null;
    }
  }

  private startTimer(): void {
    this.stopTimer();
    const start = Date.now();
    this._connectedAt.set(start);
    this._elapsedSeconds.set(0);
    this.timerInterval = setInterval(() => {
      this.zone.run(() => {
        this._elapsedSeconds.set(Math.floor((Date.now() - start) / 1000));
      });
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
  }

  private buildWebSocketUrl(): string {
    const proto = globalThis.location.protocol === 'https:' ? 'wss' : 'ws';
    const host = globalThis.location.host;
    return `${proto}://${host}/api/Guacamole/connect/${this.connectionId}`;
  }

  private errorMessage(err: unknown): string {
    if (!err) return 'Unknown error';
    if (typeof err === 'string') return err;
    const e = err as { message?: string; statusText?: string; status?: number };
    return e.message || e.statusText || (e.status ? `HTTP ${e.status}` : 'Unknown error');
  }

  private setState(next: GuacState): void {
    this._state.set(next);
  }

  private log(level: GuacLogEntry['level'], message: string): void {
    this._logs.update((entries) => {
      const next = entries.concat({ level, message, timestamp: Date.now() });
      return next.length > 200 ? next.slice(-200) : next;
    });
  }

  private charToKeysym(ch: string, code: number): number {
    switch (ch) {
      case '\n':
      case '\r':
        return 0xff0d;
      case '\t':
        return 0xff09;
      case '\b':
        return 0xff08;
    }
    if (code >= 0x20 && code <= 0x7e) return code;
    return 0x01000000 | code;
  }

  private readSessionTerminalOverrides(): Record<string, string> | null {
    if (typeof sessionStorage === 'undefined') return null;
    const key = `smooth-operator.session-terminal.${this.connectionId}`;
    const raw = sessionStorage.getItem(key);
    if (!raw) return null;

    try {
      const parsed = JSON.parse(raw) as {
        colorScheme?: string;
        fontName?: string;
        fontSize?: number;
      };
      const result: Record<string, string> = {};
      if (parsed.colorScheme?.trim()) result['color-scheme'] = parsed.colorScheme.trim();
      if (parsed.fontName?.trim()) result['font-name'] = parsed.fontName.trim();
      if (Number.isFinite(parsed.fontSize)) result['font-size'] = String(parsed.fontSize);
      return Object.keys(result).length > 0 ? result : null;
    } catch {
      return null;
    }
  }
}

/**
 * Root singleton that manages all active GuacamoleSessions.
 * Replaces the old single-instance GuacamoleClientService.
 */
@Injectable({ providedIn: 'root' })
export class GuacamoleSessionManagerService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);

  private readonly _sessions = signal<Map<string, GuacamoleSession>>(new Map());

  /** Reactive map of all active sessions keyed by connectionId. */
  readonly sessions = this._sessions.asReadonly();

  /** Sessions currently minimized (WS alive, display detached). */
  readonly minimizedSessions = computed(() =>
    Array.from(this._sessions().values()).filter((s) => s.minimized()),
  );

  /** Get existing session or create a new one. Never call from a computed(). */
  getOrCreate(connectionId: string): GuacamoleSession {
    const existing = this._sessions().get(connectionId);
    if (existing) return existing;
    const session = new GuacamoleSession(connectionId, this.http, this.zone);
    this._sessions.update((m) => new Map(m).set(connectionId, session));
    return session;
  }

  get(connectionId: string): GuacamoleSession | undefined {
    return this._sessions().get(connectionId);
  }

  /** Detach display and mark minimized; WS connection stays alive. */
  minimize(connectionId: string): void {
    const session = this._sessions().get(connectionId);
    if (session) {
      session.detachDisplay();
      session.minimized.set(true);
    }
  }

  /** Disconnect and remove session from the manager. */
  destroy(connectionId: string): void {
    const session = this._sessions().get(connectionId);
    if (session) {
      session.disconnect();
    }
    this._sessions.update((m) => {
      const next = new Map(m);
      next.delete(connectionId);
      return next;
    });
  }
}

// Backward-compat alias so any remaining imports of GuacamoleClientService
// still compile while the migration to GuacamoleSessionManagerService proceeds.
export { GuacamoleSessionManagerService as GuacamoleClientService };
