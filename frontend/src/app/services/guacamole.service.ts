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

@Injectable({ providedIn: 'root' })
export class GuacamoleClientService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);

  private client: Guacamole.Client | null = null;
  private tunnel: Guacamole.Tunnel | null = null;
  private keyboard: Guacamole.Keyboard | null = null;
  private mouse: Guacamole.Mouse | null = null;
  private touch: Guacamole.Mouse.Touchscreen | null = null;
  private displayHost: HTMLElement | null = null;
  private resizeListener: (() => void) | null = null;
  private pasteListener: ((ev: ClipboardEvent) => void) | null = null;
  private currentConnectionId: string | null = null;

  private readonly _state = signal<GuacState>('idle');
  private readonly _logs = signal<GuacLogEntry[]>([]);
  private readonly _error = signal<string | null>(null);
  private readonly _displayName = signal<string | null>(null);
  private readonly _hostClipboard = signal<string>('');

  readonly state = this._state.asReadonly();
  readonly logs = this._logs.asReadonly();
  readonly error = this._error.asReadonly();
  readonly displayName = this._displayName.asReadonly();
  readonly hostClipboard = this._hostClipboard.asReadonly();
  readonly progress = computed(() => PROGRESS_BY_STATE[this._state()] ?? 0);
  readonly isConnected = computed(() => this._state() === 'connected');

  async connect(connectionId: string): Promise<void> {
    if (this.client) {
      this.disconnect();
    }
    this.currentConnectionId = connectionId;
    this._error.set(null);
    this._logs.set([]);
    this._displayName.set(null);
    this._hostClipboard.set('');

    this.setState('requesting-ticket');
    this.log('info', 'Requesting connection ticket...');

    let ticket: string;
    try {
      const res = await firstValueFrom(
        this.http.post<TicketResponse>(`/api/Guacamole/ticket/${connectionId}`, {}),
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
    const wsUrl = this.buildWebSocketUrl(connectionId);
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
        if (next === 'connected') this.log('ok', 'Session ready');
        if (next === 'disconnected') this.log('info', 'Session disconnected');
      });
    };
    client.onerror = (status) => {
      this.zone.run(() => {
        this.log('error', `Client error: ${status?.message ?? 'unknown'} (code ${status?.code})`);
        this._error.set(status?.message ?? 'Client error');
        this.setState('error');
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
    // The Angular effect that calls attachDisplay can re-fire on every
    // signal change; without this guard, multiple Guacamole.Keyboard
    // instances pile up on `document` and every keystroke is sent N times.
    if (this.displayHost === host && this.keyboard) {
      return;
    }

    // Re-attaching to a different host: neutralize the previous keyboard's
    // callbacks (the library exposes no public way to remove its document
    // listeners) and tear down auxiliary listeners.
    this.detachDisplay();

    this.displayHost = host;
    const display = this.client.getDisplay();
    const el = display.getElement();

    while (host.firstChild) host.removeChild(host.firstChild);
    host.appendChild(el);

    // Mouse + touch — both wired to the display element.
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
    // BUT we must skip events when the user is typing into a real input
    // (e.g. our clipboard/key-combo modals), otherwise every keystroke is
    // also forwarded to the remote host.
    const keyboard = new Guacamole.Keyboard(document);
    const isTypingInForm = (): boolean => {
      const ae = document.activeElement as HTMLElement | null;
      if (!ae) return false;
      const tag = ae.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return true;
      if ((ae as HTMLElement).isContentEditable) return true;
      // Also pause when any modal is open — the modal owns interaction.
      if (document.querySelector('[data-guac-modal-open="true"]')) return true;
      return false;
    };
    keyboard.onkeydown = (keysym) => {
      if (!this.client) return false;
      if (isTypingInForm()) return true; // let the browser handle it
      this.client.sendKeyEvent(1, keysym);
      return false;
    };
    keyboard.onkeyup = (keysym) => {
      if (!this.client) return;
      if (isTypingInForm()) return;
      this.client.sendKeyEvent(0, keysym);
    };
    this.keyboard = keyboard;

    // Focus the display so the browser's text caret doesn't drift onto
    // arbitrary header text when the user hasn't clicked anything yet.
    try {
      host.focus({ preventScroll: true });
    } catch {
      /* ignore focus errors */
    }

    // Auto-resize: send the host element size whenever it changes, and rescale
    // the canvas to fit.
    const doResize = () => this.resizeToHost();
    this.resizeListener = doResize;
    window.addEventListener('resize', doResize);
    display.onresize = () => doResize();
    queueMicrotask(doResize);

    // Browser → host clipboard sync via paste events on the document.
    // Skip pastes that happen inside form inputs (e.g. the clipboard modal's
    // textarea) — the user is editing locally and will push manually.
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

  resizeToHost(): void {
    if (!this.client || !this.displayHost) return;
    const w = Math.max(320, Math.floor(this.displayHost.clientWidth));
    const h = Math.max(240, Math.floor(this.displayHost.clientHeight));
    const display = this.client.getDisplay();
    // Ask guacd to render natively at the host's pixel dimensions. We
    // intentionally do NOT call display.scale() here — scaling the canvas
    // via CSS transforms causes blurry/washed-out text in SSH terminals.
    // guacd will re-render at the new size and our canvas stays 1:1.
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

  // Press all keysyms, then release in reverse order. Used for combos like
  // Ctrl+Alt+Delete that must be observed simultaneously by the host.
  sendKeyCombo(keysyms: number[]): void {
    if (!this.client || keysyms.length === 0) return;
    for (const k of keysyms) this.client.sendKeyEvent(1, k);
    for (let i = keysyms.length - 1; i >= 0; i--) this.client.sendKeyEvent(0, keysyms[i]);
  }

  pasteToHost(text: string): void {
    if (!this.client) return;

    // Sync the remote clipboard so paste-on-other-windows works (RDP/VNC).
    try {
      const stream = this.client.createClipboardStream('text/plain');
      const writer = new Guacamole.StringWriter(stream);
      writer.sendText(text);
      writer.sendEnd();
    } catch {
      /* clipboard stream may not be supported on every protocol */
    }

    // Also type the text as Unicode keystrokes. This is the only way to
    // inject content into an SSH terminal (guacd does not auto-paste from
    // the clipboard buffer) and works as expected on RDP/VNC too.
    this.typeText(text);
  }

  /** Sends `text` to the host as a sequence of Unicode keysym events. */
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

  private charToKeysym(ch: string, code: number): number {
    // Special keys mapped to X11 keysyms.
    switch (ch) {
      case '\n':
      case '\r':
        return 0xff0d; // Return
      case '\t':
        return 0xff09; // Tab
      case '\b':
        return 0xff08; // Backspace
    }
    // ASCII printable range maps directly to its codepoint as an X11 keysym.
    if (code >= 0x20 && code <= 0x7e) return code;
    // Everything else uses the Unicode keysym range (0x01000000 + codepoint).
    return 0x01000000 | code;
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

  /**
   * Tear down display-side bindings (keyboard/mouse/listeners) without
   * disconnecting the underlying client/tunnel. Called before re-attaching
   * to a different host element so we don't leak listeners on `document`.
   *
   * Note: Guacamole.Keyboard exposes no public listener-removal API, so we
   * neutralize it by nulling its callbacks and resetting state. The orphaned
   * document listener will continue to receive events but will not dispatch
   * anything to the host.
   */
  private detachDisplay(): void {
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
      window.removeEventListener('resize', this.resizeListener);
      this.resizeListener = null;
    }
    if (this.pasteListener) {
      document.removeEventListener('paste', this.pasteListener);
      this.pasteListener = null;
    }
    if (this.displayHost) {
      while (this.displayHost.firstChild) {
        this.displayHost.removeChild(this.displayHost.firstChild);
      }
    }
    this.displayHost = null;
  }

  disconnect(): void {
    if (this.client) {
      try {
        this.client.disconnect();
      } catch {
        /* swallow */
      }
    }
    if (this.keyboard) {
      try {
        this.keyboard.reset();
      } catch {
        /* swallow */
      }
    }
    if (this.resizeListener) {
      window.removeEventListener('resize', this.resizeListener);
      this.resizeListener = null;
    }
    if (this.pasteListener) {
      document.removeEventListener('paste', this.pasteListener);
      this.pasteListener = null;
    }
    if (this.displayHost) {
      while (this.displayHost.firstChild) this.displayHost.removeChild(this.displayHost.firstChild);
    }
    this.client = null;
    this.tunnel = null;
    this.keyboard = null;
    this.mouse = null;
    this.touch = null;
    this.displayHost = null;
    this.currentConnectionId = null;
    this.setState('disconnected');
  }

  reset(): void {
    this._state.set('idle');
    this._logs.set([]);
    this._error.set(null);
    this._displayName.set(null);
    this._hostClipboard.set('');
  }

  private buildWebSocketUrl(connectionId: string): string {
    const proto = window.location.protocol === 'https:' ? 'wss' : 'ws';
    const host = window.location.host;
    return `${proto}://${host}/api/Guacamole/connect/${connectionId}`;
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
      // Keep ring bounded.
      return next.length > 200 ? next.slice(next.length - 200) : next;
    });
  }
}
