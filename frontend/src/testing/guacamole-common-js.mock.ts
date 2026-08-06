/**
 * Shared `guacamole-common-js` test double.
 *
 * Every spec that pulls in `services/guacamole.service` must mock this package so
 * no real WebSocket / canvas wiring runs. Four specs do
 * (`guacamole.service`, `active-session`, `connecting-state`, `session-bar`) — and
 * when each declared its *own* `vi.mock` factory they competed: the three UI specs
 * used a bare `Client: vi.fn()`, whose instances have no `connect`, and that factory
 * could win for `guacamole.service.spec.ts`, killing its tests with
 * "client.connect is not a function".
 *
 * That failure was not a concurrency race — it reproduces with
 * `fileParallelism: false` and with fork isolation on — and it is deterministic given
 * which spec files are in the run. It reproduces on 2 CPUs (`taskset -c 0,1`), which
 * is what CircleCI's `medium` executor gives the job, so a 4-core dev machine hides it.
 *
 * One shared definition removes the whole class of bug: whichever registry wins, the
 * mock is the same. Specs consume it as:
 *
 *   vi.mock('guacamole-common-js', async () =>
 *     (await import('<path>/testing/guacamole-common-js.mock')).guacamoleModuleMock);
 *
 * The async form matters — `vi.mock` factories are hoisted above imports, so a
 * statically-imported binding would not yet be initialised inside the factory body.
 */
import { vi } from 'vitest';

export const fakeDisplay = {
  getElement: vi.fn(() => document.createElement('div')),
  getWidth: vi.fn(() => 1024),
  getHeight: vi.fn(() => 768),
  scale: vi.fn(),
  onresize: null as null | (() => void),
};

export interface FakeClient {
  onstatechange: ((s: number) => void) | null;
  onerror: ((status: { message?: string; code?: number }) => void) | null;
  onname: ((name: string) => void) | null;
  onclipboard: ((stream: unknown, mimetype: string) => void) | null;
  onfilesystem: ((object: FakeGuacObject, name: string) => void) | null;
  getDisplay: () => typeof fakeDisplay;
  sendKeyEvent: ReturnType<typeof vi.fn>;
  sendMouseState: ReturnType<typeof vi.fn>;
  sendSize: ReturnType<typeof vi.fn>;
  connect: ReturnType<typeof vi.fn>;
  disconnect: ReturnType<typeof vi.fn>;
  createClipboardStream: ReturnType<typeof vi.fn>;
}

export interface FakeTunnel {
  onstatechange: ((s: number) => void) | null;
  onerror: ((status: { message?: string }) => void) | null;
}

export interface FakeGuacObject {
  index: number;
  requestInputStream: ReturnType<typeof vi.fn>;
  createOutputStream: ReturnType<typeof vi.fn>;
}

export interface FakeStringReader {
  ontext: ((text: string) => void) | null;
  onend: (() => void) | null;
}

export interface FakeBlobReader {
  onprogress: ((length: number) => void) | null;
  onend: (() => void) | null;
  getBlob: ReturnType<typeof vi.fn>;
  getLength: ReturnType<typeof vi.fn>;
}

export interface FakeBlobWriter {
  sendBlob: ReturnType<typeof vi.fn>;
  sendEnd: ReturnType<typeof vi.fn>;
  onack: ((status: unknown) => void) | null;
  onerror: ((blob: Blob, offset: number, error: DOMException) => void) | null;
  onprogress: ((blob: Blob, offset: number) => void) | null;
  oncomplete: ((blob: Blob) => void) | null;
}

export const clientInstances: FakeClient[] = [];
export const tunnelInstances: FakeTunnel[] = [];
export const objectInstances: FakeGuacObject[] = [];
export const stringReaderInstances: FakeStringReader[] = [];
export const blobReaderInstances: FakeBlobReader[] = [];
export const blobWriterInstances: FakeBlobWriter[] = [];

export const ROOT_STREAM = '/';
export const STREAM_INDEX_MIMETYPE = 'application/vnd.glyptodon.guacamole.stream-index+json';

export class FakeClipboardStream {
  // marker class
}

/** Empty every instance array in place — call from `beforeEach`. */
export function resetGuacamoleMockInstances(): void {
  for (const list of [
    clientInstances,
    tunnelInstances,
    objectInstances,
    stringReaderInstances,
    blobReaderInstances,
    blobWriterInstances,
  ]) {
    list.length = 0;
  }
}

// Plain factory functions that *return* the populated instance rather than mutating
// `this`: under Node 22 + Vitest 4.x, `vi.fn(function (this) {…})` wrappers can swap out
// the constructed object so `this` mutations never reach the caller. JS `new` always
// honours an object returned from a constructor, so this pattern is environment-stable.
function ClientCtor(): FakeClient {
  const self: FakeClient = {
    onstatechange: null,
    onerror: null,
    onname: null,
    onclipboard: null,
    onfilesystem: null,
    getDisplay: () => fakeDisplay,
    sendKeyEvent: vi.fn(),
    sendMouseState: vi.fn(),
    sendSize: vi.fn(),
    connect: vi.fn(),
    disconnect: vi.fn(),
    createClipboardStream: vi.fn(() => new FakeClipboardStream()),
  };
  clientInstances.push(self);
  return self;
}

function TunnelCtor(): FakeTunnel {
  const self: FakeTunnel = { onstatechange: null, onerror: null };
  tunnelInstances.push(self);
  return self;
}

function KeyboardCtor() {
  return {
    onkeydown: null as null | ((sym: number) => void),
    onkeyup: null as null | ((sym: number) => void),
    reset: vi.fn(),
  };
}

function MouseCtor() {
  return {
    onmousedown: null as null | ((s: unknown) => void),
    onmouseup: null as null | ((s: unknown) => void),
    onmousemove: null as null | ((s: unknown) => void),
  };
}
// eslint-disable-next-line @typescript-eslint/no-explicit-any
(MouseCtor as any).Touchscreen = function TouchscreenCtor() {
  return {
    onmousedown: null as null | ((s: unknown) => void),
    onmouseup: null as null | ((s: unknown) => void),
    onmousemove: null as null | ((s: unknown) => void),
  };
};

function StringReaderCtor(): FakeStringReader {
  const self: FakeStringReader = { ontext: null, onend: null };
  stringReaderInstances.push(self);
  return self;
}

function StringWriterCtor() {
  return { sendText: vi.fn(), sendEnd: vi.fn() };
}

function ObjectCtor(_client: unknown, index: number): FakeGuacObject {
  const self: FakeGuacObject = {
    index,
    requestInputStream: vi.fn(),
    createOutputStream: vi.fn(),
  };
  objectInstances.push(self);
  return self;
}
(ObjectCtor as unknown as { ROOT_STREAM: string }).ROOT_STREAM = ROOT_STREAM;
(ObjectCtor as unknown as { STREAM_INDEX_MIMETYPE: string }).STREAM_INDEX_MIMETYPE =
  STREAM_INDEX_MIMETYPE;

function BlobReaderCtor(): FakeBlobReader {
  const self: FakeBlobReader = {
    onprogress: null,
    onend: null,
    getBlob: vi.fn(() => new Blob(['fake'])),
    getLength: vi.fn(() => 4),
  };
  blobReaderInstances.push(self);
  return self;
}

function BlobWriterCtor(): FakeBlobWriter {
  const self: FakeBlobWriter = {
    sendBlob: vi.fn(),
    sendEnd: vi.fn(),
    onack: null,
    onerror: null,
    onprogress: null,
    oncomplete: null,
  };
  blobWriterInstances.push(self);
  return self;
}

/** The module namespace `vi.mock('guacamole-common-js', …)` should resolve to. */
export const guacamoleModuleMock = {
  default: {
    Client: ClientCtor,
    WebSocketTunnel: TunnelCtor,
    Tunnel: { State: { OPEN: 1, UNSTABLE: 2, CLOSED: 3 } },
    Object: ObjectCtor,
    BlobReader: BlobReaderCtor,
    BlobWriter: BlobWriterCtor,
    Keyboard: KeyboardCtor,
    Mouse: MouseCtor,
    StringReader: StringReaderCtor,
    StringWriter: StringWriterCtor,
  },
};
