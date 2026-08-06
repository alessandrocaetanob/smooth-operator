import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

// The guacamole-common-js double lives in one shared module so the four specs that
// mock this package cannot install competing factories — see the module for details.
vi.mock(
  'guacamole-common-js',
  async () => (await import('../../testing/guacamole-common-js.mock')).guacamoleModuleMock,
);

import {
  ROOT_STREAM,
  STREAM_INDEX_MIMETYPE,
  blobReaderInstances,
  blobWriterInstances,
  clientInstances,
  fakeDisplay,
  resetGuacamoleMockInstances,
  stringReaderInstances,
  tunnelInstances,
} from '../../testing/guacamole-common-js.mock';
import type { FakeClient, FakeGuacObject } from '../../testing/guacamole-common-js.mock';

import {
  FILE_TRANSFER_TIMEOUT_MS,
  GuacamoleSession,
  GuacamoleSessionManagerService,
  Keysyms,
} from './guacamole.service';

describe('GuacamoleSession', () => {
  let httpTesting: HttpTestingController;
  let session: GuacamoleSession;
  let manager: GuacamoleSessionManagerService;

  beforeEach(() => {
    resetGuacamoleMockInstances();
    sessionStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
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

    it('setZoom clamps the multiplier and re-scales the display', () => {
      const host = document.createElement('div');
      Object.defineProperty(host, 'clientWidth', { value: 1024, configurable: true });
      Object.defineProperty(host, 'clientHeight', { value: 768, configurable: true });
      session.attachDisplay(host);
      session.setZoom(2);
      expect(session.getZoom()).toBe(2);
      // Display fits 1:1 (1024×768), so the canvas scale equals the zoom.
      expect(fakeDisplay.scale).toHaveBeenLastCalledWith(2);
      session.setZoom(99);
      expect(session.getZoom()).toBe(3); // clamped to ZOOM_MAX
    });

    it('resizeToHost requests a desktop-class width on narrow viewports', () => {
      const host = document.createElement('div');
      Object.defineProperty(host, 'clientWidth', { value: 390, configurable: true });
      Object.defineProperty(host, 'clientHeight', { value: 720, configurable: true });
      const innerOriginal = globalThis.innerWidth;
      Object.defineProperty(globalThis, 'innerWidth', { configurable: true, value: 390 });
      try {
        session.attachDisplay(host);
        session.resizeToHost();
        // Phone-width 1:1 would cramp a terminal — a 1024px remote is requested.
        expect(clientInstances[0].sendSize).toHaveBeenCalledWith(1024, expect.any(Number));
      } finally {
        Object.defineProperty(globalThis, 'innerWidth', {
          configurable: true,
          value: innerOriginal,
        });
      }
    });

    it('display.onresize re-fits the canvas', () => {
      const host = document.createElement('div');
      Object.defineProperty(host, 'clientWidth', { value: 1024, configurable: true });
      Object.defineProperty(host, 'clientHeight', { value: 768, configurable: true });
      session.attachDisplay(host);
      fakeDisplay.scale.mockClear();
      fakeDisplay.onresize?.();
      expect(fakeDisplay.scale).toHaveBeenCalled();
    });
  });

  describe('file transfer (onfilesystem / listDirectory / downloadFile / uploadFile)', () => {
    let client: FakeClient;

    beforeEach(async () => {
      const p = session.connect();
      httpTesting.expectOne('/api/Guacamole/ticket/conn-1').flush({ ticket: 't' });
      await p;
      client = clientInstances[0];
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    function attachFilesystem(overrides: Partial<FakeGuacObject> = {}): FakeGuacObject {
      const obj: FakeGuacObject = {
        index: 0,
        requestInputStream: vi.fn(),
        createOutputStream: vi.fn(() => ({})),
        ...overrides,
      };
      client.onfilesystem?.(obj, 'Smooth Operator');
      return obj;
    }

    /** Fake InputStream delivered to requestInputStream body callbacks. */
    function makeFakeInputStream(): { sendAck: ReturnType<typeof vi.fn> } {
      return { sendAck: vi.fn() };
    }

    it('fileTransferAvailable/fileSystemName default to unavailable', () => {
      expect(session.fileTransferAvailable()).toBe(false);
      expect(session.fileSystemName()).toBeNull();
    });

    it('onfilesystem marks the filesystem available', () => {
      attachFilesystem();
      expect(session.fileTransferAvailable()).toBe(true);
      expect(session.fileSystemName()).toBe('Smooth Operator');
    });

    describe('listDirectory', () => {
      it('rejects when no filesystem is available', async () => {
        await expect(session.listDirectory('/')).rejects.toThrow('No filesystem available');
      });

      it('resolves entries sorted directories-first then alphabetically, stripping the path prefix', async () => {
        const stream = makeFakeInputStream();
        attachFilesystem({
          requestInputStream: vi.fn(
            (_name: string, cb: (stream: unknown, mimetype: string) => void) => {
              cb(stream, STREAM_INDEX_MIMETYPE);
            },
          ),
        });

        const promise = session.listDirectory(ROOT_STREAM);
        // Regression: guacd streams the listing only after this initial ack —
        // omitting it is exactly the "spinner forever" bug.
        expect(stream.sendAck).toHaveBeenCalledWith('Ready', 0);
        const reader = stringReaderInstances[stringReaderInstances.length - 1];
        reader.ontext?.(
          JSON.stringify({
            '/b.txt': 'text/plain',
            '/a.txt': 'text/plain',
            '/sub': STREAM_INDEX_MIMETYPE,
          }),
        );
        reader.onend?.();

        await expect(promise).resolves.toEqual([
          {
            streamName: '/sub',
            displayName: 'sub',
            mimetype: STREAM_INDEX_MIMETYPE,
            isDirectory: true,
          },
          {
            streamName: '/a.txt',
            displayName: 'a.txt',
            mimetype: 'text/plain',
            isDirectory: false,
          },
          {
            streamName: '/b.txt',
            displayName: 'b.txt',
            mimetype: 'text/plain',
            isDirectory: false,
          },
        ]);
      });

      it('rejects when the requested stream is not a directory and aborts the stream', async () => {
        const stream = makeFakeInputStream();
        attachFilesystem({
          requestInputStream: vi.fn(
            (_name: string, cb: (stream: unknown, mimetype: string) => void) => {
              cb(stream, 'text/plain');
            },
          ),
        });
        await expect(session.listDirectory('/a.txt')).rejects.toThrow('Not a directory');
        // A non-zero ack tells guacd to abort the stream it opened for us.
        expect(stream.sendAck).toHaveBeenCalledWith('Not a directory', 0x0100);
      });

      it('rejects with a timeout error when guacd never responds', async () => {
        vi.useFakeTimers();
        // No callback invocation at all — simulates an unresponsive guacd/remote
        // (e.g. an SFTP subsystem that hangs on real operations after connecting).
        attachFilesystem({ requestInputStream: vi.fn() });

        const promise = session.listDirectory('/');
        const assertion = expect(promise).rejects.toThrow('No data received for 20s');
        await vi.advanceTimersByTimeAsync(FILE_TRANSFER_TIMEOUT_MS);
        await assertion;
      });

      it('is an INACTIVITY timeout: steady data flow re-arms it past the total duration', async () => {
        vi.useFakeTimers();
        const stream = makeFakeInputStream();
        attachFilesystem({
          requestInputStream: vi.fn(
            (_name: string, cb: (stream: unknown, mimetype: string) => void) => {
              cb(stream, STREAM_INDEX_MIMETYPE);
            },
          ),
        });

        const promise = session.listDirectory(ROOT_STREAM);
        const reader = stringReaderInstances[stringReaderInstances.length - 1];
        // Trickle data every 15s for 60s total — over 3× the 20s timeout, but
        // never 20s of silence. The old fixed-race timeout failed exactly here.
        for (let i = 0; i < 4; i++) {
          await vi.advanceTimersByTimeAsync(15000);
          reader.ontext?.(i === 0 ? '{"/a.txt":"text/plain"' : '');
        }
        reader.ontext?.('}');
        reader.onend?.();

        await expect(promise).resolves.toEqual([
          {
            streamName: '/a.txt',
            displayName: 'a.txt',
            mimetype: 'text/plain',
            isDirectory: false,
          },
        ]);
      });

      it('honors a custom (vault-configured) timeout', async () => {
        vi.useFakeTimers();
        attachFilesystem({ requestInputStream: vi.fn() });

        const promise = session.listDirectory('/', 60000);
        const assertion = expect(promise).rejects.toThrow('No data received for 60s');
        // Still pending at the default 20s mark…
        await vi.advanceTimersByTimeAsync(20000);
        await vi.advanceTimersByTimeAsync(40000);
        await assertion;
      });
    });

    describe('downloadFile', () => {
      const entry = {
        streamName: '/a.txt',
        displayName: 'a.txt',
        mimetype: 'text/plain',
        isDirectory: false,
      };

      it('rejects when no filesystem is available', async () => {
        await expect(session.downloadFile(entry)).rejects.toThrow('No filesystem available');
      });

      it('triggers a browser download once the blob stream ends', async () => {
        const stream = makeFakeInputStream();
        attachFilesystem({
          requestInputStream: vi.fn(
            (_name: string, cb: (stream: unknown, mimetype: string) => void) => {
              cb(stream, 'text/plain');
            },
          ),
        });
        const clickSpy = vi
          .spyOn(HTMLAnchorElement.prototype, 'click')
          .mockImplementation(() => undefined);
        const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake');
        const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);

        const promise = session.downloadFile(entry);
        // Regression: same initial ack requirement as the directory listing.
        expect(stream.sendAck).toHaveBeenCalledWith('Ready', 0);
        const reader = blobReaderInstances[blobReaderInstances.length - 1];
        reader.onend?.();
        await promise;

        expect(createObjectUrl).toHaveBeenCalled();
        expect(clickSpy).toHaveBeenCalled();
        expect(revokeObjectUrl).toHaveBeenCalledWith('blob:fake');
      });

      it('rejects with a timeout error when guacd never responds', async () => {
        vi.useFakeTimers();
        attachFilesystem({ requestInputStream: vi.fn() });

        const promise = session.downloadFile(entry);
        const assertion = expect(promise).rejects.toThrow('No data received for 20s');
        await vi.advanceTimersByTimeAsync(FILE_TRANSFER_TIMEOUT_MS);
        await assertion;
      });
    });

    describe('uploadFile', () => {
      it('rejects when no filesystem is available', async () => {
        await expect(session.uploadFile('/', new File(['x'], 'a.txt'))).rejects.toThrow(
          'No filesystem available',
        );
      });

      it('creates the output stream at directory + filename and sends the blob', async () => {
        const obj = attachFilesystem();
        const file = new File(['x'], 'note.txt', { type: 'text/plain' });

        const promise = session.uploadFile('/docs', file);
        expect(obj.createOutputStream).toHaveBeenCalledWith('text/plain', '/docs/note.txt');

        const writer = blobWriterInstances[blobWriterInstances.length - 1];
        expect(writer.sendBlob).toHaveBeenCalledWith(file);
        writer.oncomplete?.(file);
        await promise;
        expect(writer.sendEnd).toHaveBeenCalled();
      });

      it('does not double a trailing slash when the directory is root', () => {
        const obj = attachFilesystem();
        const file = new File(['x'], 'note.txt');
        // Fire-and-forget: the assertion is synchronous, but the upload promise
        // never settles here, so its 20s stall guard would later reject with no
        // handler attached and fail the run as an unhandled rejection.
        session.uploadFile(ROOT_STREAM, file).catch(() => undefined);
        expect(obj.createOutputStream).toHaveBeenCalledWith(
          'application/octet-stream',
          '/note.txt',
        );
      });

      it('reports upload progress as a 0-1 fraction', () => {
        attachFilesystem();
        const file = new File(['x'], 'note.txt');
        const onProgress = vi.fn();
        // Same as above — settle-less upload, so swallow the eventual stall rejection.
        session.uploadFile('/docs', file, onProgress).catch(() => undefined);
        const writer = blobWriterInstances[blobWriterInstances.length - 1];
        writer.onprogress?.(new Blob(['0123456789']), 5);
        expect(onProgress).toHaveBeenCalledWith(0.5);
      });

      it('rejects when the writer reports an error', async () => {
        attachFilesystem();
        const file = new File(['x'], 'note.txt');
        const promise = session.uploadFile('/docs', file);
        const writer = blobWriterInstances[blobWriterInstances.length - 1];
        writer.onerror?.(file, 0, new DOMException('disk full'));
        await expect(promise).rejects.toThrow('disk full');
      });

      it('rejects immediately with the remote message when guacd acks an error (e.g. permission denied)', async () => {
        // Regression: an upload into a folder the remote user cannot write to
        // is refused by guacd via an error-status ack ("SFTP: Open failed",
        // code 0x0303). Ignoring it (the old behavior) surfaced only as a
        // meaningless timeout after 20 silent seconds.
        attachFilesystem();
        const file = new File(['x'], 'note.txt');
        const promise = session.uploadFile('/denied', file);
        const writer = blobWriterInstances[blobWriterInstances.length - 1];
        writer.onack?.({
          code: 0x0303,
          message: 'SFTP: Open failed',
          isError: () => true,
        });
        await expect(promise).rejects.toMatchObject({
          name: 'FileTransferError',
          code: 'remote',
          message: 'SFTP: Open failed',
          statusCode: 0x0303,
        });
        // The failed stream must be terminated (like the reference client does),
        // or guacd's deferred close gets acked to whichever upload reuses the index.
        expect(writer.sendEnd).toHaveBeenCalled();
        // Trailing stale acks after settlement must not throw or double-reject.
        writer.onack?.({ code: 0x0200, message: 'SFTP: Close failed', isError: () => true });
      });

      it('treats successful acks as activity, not errors', async () => {
        vi.useFakeTimers();
        attachFilesystem();
        const file = new File(['x'], 'note.txt');
        const promise = session.uploadFile('/docs', file);
        const writer = blobWriterInstances[blobWriterInstances.length - 1];
        // Successful chunk acks every 15s keep the 20s inactivity guard armed…
        for (let i = 0; i < 4; i++) {
          await vi.advanceTimersByTimeAsync(15000);
          writer.onack?.({ code: 0, message: 'OK', isError: () => false });
        }
        // …and the upload still completes normally.
        writer.oncomplete?.(file);
        await expect(promise).resolves.toBeUndefined();
      });

      it('rejects with a timeout error when guacd never acks', async () => {
        vi.useFakeTimers();
        attachFilesystem();
        const file = new File(['x'], 'note.txt');

        const promise = session.uploadFile('/docs', file);
        const assertion = expect(promise).rejects.toThrow('No data received for 20s');
        await vi.advanceTimersByTimeAsync(FILE_TRANSFER_TIMEOUT_MS);
        await assertion;
      });
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
    resetGuacamoleMockInstances();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
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
