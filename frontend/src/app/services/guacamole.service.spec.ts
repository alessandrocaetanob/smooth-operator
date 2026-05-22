import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

// ── Mock guacamole-common-js so no real WebSocket / canvas wiring runs. ──
const fakeDisplay = {
  getElement: vi.fn(() => document.createElement('div')),
  scale: vi.fn(),
  onresize: null as null | (() => void),
};

interface FakeClient {
  onstatechange: ((s: number) => void) | null;
  onerror: ((status: { message?: string; code?: number }) => void) | null;
  onname: ((name: string) => void) | null;
  onclipboard: ((stream: unknown, mimetype: string) => void) | null;
  getDisplay: () => typeof fakeDisplay;
  sendKeyEvent: ReturnType<typeof vi.fn>;
  sendMouseState: ReturnType<typeof vi.fn>;
  sendSize: ReturnType<typeof vi.fn>;
  connect: ReturnType<typeof vi.fn>;
  disconnect: ReturnType<typeof vi.fn>;
  createClipboardStream: ReturnType<typeof vi.fn>;
}

interface FakeTunnel {
  onstatechange: ((s: number) => void) | null;
  onerror: ((status: { message?: string }) => void) | null;
}

const clientInstances: FakeClient[] = [];
const tunnelInstances: FakeTunnel[] = [];

class FakeClipboardStream {
  // marker class
}

vi.mock('guacamole-common-js', () => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const ClientCtor = vi.fn(function (this: any) {
    const inst: FakeClient = {
      onstatechange: null,
      onerror: null,
      onname: null,
      onclipboard: null,
      getDisplay: () => fakeDisplay,
      sendKeyEvent: vi.fn(),
      sendMouseState: vi.fn(),
      sendSize: vi.fn(),
      connect: vi.fn(),
      disconnect: vi.fn(),
      createClipboardStream: vi.fn(() => new FakeClipboardStream()),
    };
    Object.assign(this, inst);
    clientInstances.push(this);
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const TunnelCtor = vi.fn(function (this: any) {
    const inst: FakeTunnel = {
      onstatechange: null,
      onerror: null,
    };
    Object.assign(this, inst);
    tunnelInstances.push(this);
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const KeyboardCtor = vi.fn(function (this: any) {
    this.onkeydown = null;
    this.onkeyup = null;
    this.reset = vi.fn();
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const MouseCtor = vi.fn(function (this: any) {
    this.onmousedown = null;
    this.onmouseup = null;
    this.onmousemove = null;
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (MouseCtor as any).Touchscreen = vi.fn(function (this: any) {
    this.onmousedown = null;
    this.onmouseup = null;
    this.onmousemove = null;
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const StringReaderCtor = vi.fn(function (this: any) {
    this.ontext = null;
    this.onend = null;
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const StringWriterCtor = vi.fn(function (this: any) {
    this.sendText = vi.fn();
    this.sendEnd = vi.fn();
  });

  const Tunnel = { State: { OPEN: 1, UNSTABLE: 2, CLOSED: 3 } };

  return {
    default: {
      Client: ClientCtor,
      WebSocketTunnel: TunnelCtor,
      Tunnel,
      Keyboard: KeyboardCtor,
      Mouse: MouseCtor,
      StringReader: StringReaderCtor,
      StringWriter: StringWriterCtor,
    },
  };
});

import { GuacamoleSession, GuacamoleSessionManagerService, Keysyms } from './guacamole.service';

describe('GuacamoleSession', () => {
  let httpTesting: HttpTestingController;
  let session: GuacamoleSession;
  let manager: GuacamoleSessionManagerService;

  beforeEach(() => {
    clientInstances.length = 0;
    tunnelInstances.length = 0;
    sessionStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpTesting = TestBed.inject(HttpTestingController);
    manager = TestBed.inject(GuacamoleSessionManagerService);
    session = manager.getOrCreate('conn-1');
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('initial state is idle', () => {
    expect(session.state()).toBe('idle');
    expect(session.progress()).toBe(0);
    expect(session.isConnected()).toBe(false);
    expect(session.logs()).toEqual([]);
    expect(session.error()).toBeNull();
  });

  describe('connect()', () => {
    it('issues ticket POST and wires tunnel/client handlers', async () => {
      const promise = session.connect();
      const req = httpTesting.expectOne('/api/Guacamole/ticket/conn-1');
      expect(req.request.method).toBe('POST');
      req.flush({ ticket: 'tok-1' });
      await promise;
      expect(session.state()).toBe('connecting');
      expect(tunnelInstances.length).toBe(1);
      expect(clientInstances.length).toBe(1);
      const client = clientInstances[0];
      expect(client.connect).toHaveBeenCalledWith('ticket=tok-1');
    });

    it('forwards client state changes via setState mapping', async () => {
      const promise = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await promise;
      const client = clientInstances[0];
      // 3 = CONNECTED
      client.onstatechange?.(3);
      expect(session.state()).toBe('connected');
      expect(session.isConnected()).toBe(true);
      // 5 = DISCONNECTED
      client.onstatechange?.(5);
      expect(session.state()).toBe('disconnected');
    });

    it('sets error state on ticket failure', async () => {
      const promise = session.connect();
      httpTesting
        .expectOne('/api/Guacamole/ticket/conn-1')
        .flush({ message: 'nope' }, { status: 500, statusText: 'err' });
      await expect(promise).rejects.toBeDefined();
      expect(session.state()).toBe('error');
      expect(session.error()).toBeTruthy();
    });

    it('idempotent: second connect while requesting-ticket does not issue a 2nd ticket', async () => {
      const first = session.connect();
      // Second call should NOT fire another ticket request — only one is in flight.
      const second = session.connect();
      const req = httpTesting.expectOne('/api/Guacamole/ticket/conn-1');
      req.flush({ ticket: 't' });
      await first;
      await second;
      // Only one Guacamole client should have been constructed despite two connect() calls.
      expect(clientInstances).toHaveLength(1);
    });

    it('disconnect tears down client and detaches display', async () => {
      const promise = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await promise;
      session.disconnect();
      expect(session.state()).toBe('disconnected');
      expect(clientInstances[0].disconnect).toHaveBeenCalled();
    });

    it('tunnel state transitions append logs', async () => {
      const promise = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await promise;
      const tunnel = tunnelInstances[0];
      tunnel.onstatechange?.(1); // OPEN
      tunnel.onstatechange?.(2); // UNSTABLE
      tunnel.onstatechange?.(3); // CLOSED
      const messages = session.logs().map((l) => l.message);
      expect(messages.join(' ')).toContain('Tunnel established');
      expect(messages.join(' ')).toContain('unstable');
      expect(messages.join(' ')).toContain('closed');
    });

    it('tunnel onerror sets error state', async () => {
      const promise = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await promise;
      const tunnel = tunnelInstances[0];
      tunnel.onerror?.({ message: 'wsbroken' });
      expect(session.state()).toBe('error');
      expect(session.error()).toBe('wsbroken');
    });

    it('client onerror sets error state', async () => {
      const promise = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await promise;
      const client = clientInstances[0];
      client.onerror?.({ message: 'crash', code: 42 });
      expect(session.state()).toBe('error');
      expect(session.error()).toBe('crash');
    });

    it('client onname updates displayName', async () => {
      const promise = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await promise;
      clientInstances[0].onname?.('remote-host');
      expect(session.displayName()).toBe('remote-host');
    });

    it('reads terminal overrides from sessionStorage', () => {
      sessionStorage.setItem(
        'smooth-operator.session-terminal.conn-1',
        JSON.stringify({ colorScheme: 'solarized-dark', fontName: 'Consolas', fontSize: 14 }),
      );
      const promise = session.connect();
      const req = httpTesting.expectOne('/api/Guacamole/ticket/conn-1');
      expect(req.request.body).toEqual({
        terminalAppearance: {
          colorScheme: 'solarized-dark',
          fontName: 'Consolas',
          fontSize: 14,
        },
      });
      req.flush({ ticket: 't' });
      return promise;
    });

    it('ignores malformed sessionStorage payload', async () => {
      sessionStorage.setItem('smooth-operator.session-terminal.conn-1', 'not-json');
      const promise = session.connect();
      const req = httpTesting.expectOne('/api/Guacamole/ticket/conn-1');
      expect(req.request.body).toEqual({});
      req.flush({ ticket: 't' });
      await promise;
    });
  });

  describe('sendKey / sendKeyCombo / typeText / pasteToHost', () => {
    let client: FakeClient;

    beforeEach(async () => {
      const p = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await p;
      client = clientInstances[0];
    });

    it('sendKey down then up emits sendKeyEvent', () => {
      session.sendKey(0xff0d, true);
      session.sendKey(0xff0d, false);
      expect(client.sendKeyEvent).toHaveBeenCalledWith(1, 0xff0d);
      expect(client.sendKeyEvent).toHaveBeenCalledWith(0, 0xff0d);
    });

    it('sendKeyCombo presses then releases in reverse order', () => {
      session.sendKeyCombo([Keysyms.ControlLeft, Keysyms.AltLeft, Keysyms.Delete]);
      const calls = client.sendKeyEvent.mock.calls;
      // First three are 1 (press) in given order
      expect(calls[0]).toEqual([1, Keysyms.ControlLeft]);
      expect(calls[1]).toEqual([1, Keysyms.AltLeft]);
      expect(calls[2]).toEqual([1, Keysyms.Delete]);
      // Next three are 0 (release) in reverse order
      expect(calls[3]).toEqual([0, Keysyms.Delete]);
      expect(calls[4]).toEqual([0, Keysyms.AltLeft]);
      expect(calls[5]).toEqual([0, Keysyms.ControlLeft]);
    });

    it('sendKeyCombo no-ops with empty array', () => {
      session.sendKeyCombo([]);
      expect(client.sendKeyEvent).not.toHaveBeenCalled();
    });

    it('typeText emits press/release per character including specials', () => {
      session.typeText('a\n\t');
      const calls = client.sendKeyEvent.mock.calls;
      // 'a' → 0x61 down/up, '\n' → 0xff0d down/up, '\t' → 0xff09 down/up
      expect(calls).toEqual([
        [1, 0x61],
        [0, 0x61],
        [1, 0xff0d],
        [0, 0xff0d],
        [1, 0xff09],
        [0, 0xff09],
      ]);
    });

    it('typeText handles unicode codepoints above ASCII', () => {
      session.typeText('é');
      const cp = 'é'.codePointAt(0)!;
      expect(client.sendKeyEvent).toHaveBeenCalledWith(1, 0x01000000 | cp);
    });

    it('pasteToHost writes through clipboard stream + typeText fallback', () => {
      session.pasteToHost('hello');
      expect(client.createClipboardStream).toHaveBeenCalledWith('text/plain');
      // typeText is also invoked → at least one sendKeyEvent
      expect(client.sendKeyEvent).toHaveBeenCalled();
    });

    it('captureScreenshot returns null when no display host attached', () => {
      expect(session.captureScreenshot()).toBeNull();
    });
  });

  describe('attachDisplay / detachDisplay / resize', () => {
    beforeEach(async () => {
      const p = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await p;
    });

    it('attachDisplay mounts display element', () => {
      const host = document.createElement('div');
      session.attachDisplay(host);
      expect(host.children.length).toBeGreaterThan(0);
      session.detachDisplay();
      expect(host.children.length).toBe(0);
    });

    it('resizeToHost dispatches sendSize through client', () => {
      const host = document.createElement('div');
      Object.defineProperty(host, 'clientWidth', { value: 1024, configurable: true });
      Object.defineProperty(host, 'clientHeight', { value: 768, configurable: true });
      session.attachDisplay(host);
      session.resizeToHost();
      expect(clientInstances[0].sendSize).toHaveBeenCalledWith(1024, 768);
    });

    it('resizeToHost no-ops when no display host', () => {
      session.detachDisplay();
      session.resizeToHost();
      expect(clientInstances[0].sendSize).not.toHaveBeenCalled();
    });

    it('resizeToHost skips redundant sendSize when the size is unchanged', () => {
      const host = document.createElement('div');
      Object.defineProperty(host, 'clientWidth', { value: 1024, configurable: true });
      Object.defineProperty(host, 'clientHeight', { value: 768, configurable: true });
      session.attachDisplay(host);
      session.resizeToHost();
      session.resizeToHost();
      session.resizeToHost();
      // Only the first call reaches guacd — the rest are deduplicated, so guacd's
      // `size` reply can never drive an unbounded resize feedback loop.
      expect(clientInstances[0].sendSize).toHaveBeenCalledTimes(1);
    });

    it('resizeToHost re-sends the size after a detach/attach cycle', () => {
      const host = document.createElement('div');
      Object.defineProperty(host, 'clientWidth', { value: 1024, configurable: true });
      Object.defineProperty(host, 'clientHeight', { value: 768, configurable: true });
      session.attachDisplay(host);
      session.resizeToHost();
      session.detachDisplay();
      session.attachDisplay(host);
      session.resizeToHost();
      expect(clientInstances[0].sendSize).toHaveBeenCalledTimes(2);
    });
  });

  describe('reset()', () => {
    it('clears all state to defaults', () => {
      // populate some state
      session.disconnect();
      session.reset();
      expect(session.state()).toBe('idle');
      expect(session.error()).toBeNull();
      expect(session.elapsedSeconds()).toBe(0);
      expect(session.minimized()).toBe(false);
    });
  });

  describe('formattedElapsed', () => {
    it('formats sub-hour as mm:ss', () => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (session as any)._elapsedSeconds.set(125);
      expect(session.formattedElapsed()).toBe('02:05');
    });

    it('formats hours as h:mm:ss', () => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (session as any)._elapsedSeconds.set(3661);
      expect(session.formattedElapsed()).toBe('1:01:01');
    });
  });
});

describe('GuacamoleSessionManagerService', () => {
  let httpTesting: HttpTestingController;
  let manager: GuacamoleSessionManagerService;

  beforeEach(() => {
    clientInstances.length = 0;
    tunnelInstances.length = 0;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpTesting = TestBed.inject(HttpTestingController);
    manager = TestBed.inject(GuacamoleSessionManagerService);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('getOrCreate returns the same instance for repeated ids', () => {
    const a = manager.getOrCreate('x');
    const b = manager.getOrCreate('x');
    expect(a).toBe(b);
  });

  it('get returns the existing session', () => {
    const s = manager.getOrCreate('x');
    expect(manager.get('x')).toBe(s);
    expect(manager.get('missing')).toBeUndefined();
  });

  it('minimize marks the session and detaches the display', () => {
    const s = manager.getOrCreate('x');
    const detach = vi.spyOn(s, 'detachDisplay');
    manager.minimize('x');
    expect(s.minimized()).toBe(true);
    expect(detach).toHaveBeenCalled();
  });

  it('minimize is a no-op for unknown ids', () => {
    expect(() => manager.minimize('does-not-exist')).not.toThrow();
  });

  it('destroy disconnects and removes from map', () => {
    const s = manager.getOrCreate('x');
    const disc = vi.spyOn(s, 'disconnect');
    manager.destroy('x');
    expect(disc).toHaveBeenCalled();
    expect(manager.sessions().has('x')).toBe(false);
  });

  it('minimizedSessions filters by minimized() flag', () => {
    const a = manager.getOrCreate('a');
    const b = manager.getOrCreate('b');
    a.minimized.set(true);
    b.minimized.set(false);
    expect(manager.minimizedSessions().map((s) => s.connectionId)).toEqual(['a']);
  });
});
