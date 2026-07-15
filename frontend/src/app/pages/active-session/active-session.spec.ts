import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { BehaviorSubject, of } from 'rxjs';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { ElementRef, signal } from '@angular/core';

import { ActiveSession } from './active-session';
import { GuacamoleSessionManagerService, Keysyms } from '../../services/guacamole.service';
import { ConnectionsService, Connection } from '../../services/connections.service';

vi.mock('guacamole-common-js', () => ({
  default: {
    Client: vi.fn(),
    WebSocketTunnel: vi.fn(),
    Tunnel: { State: { OPEN: 1, UNSTABLE: 2, CLOSED: 3 } },
    Keyboard: vi.fn(),
    Mouse: vi.fn(),
    StringReader: vi.fn(),
    StringWriter: vi.fn(),
  },
}));

interface FakeSession {
  state: ReturnType<typeof signal>;
  progress: ReturnType<typeof signal>;
  error: ReturnType<typeof signal>;
  hostClipboard: ReturnType<typeof signal>;
  displayName: ReturnType<typeof signal>;
  formattedElapsed: ReturnType<typeof signal>;
  minimized: ReturnType<typeof signal>;
  isConnected: () => boolean;
  attachDisplay: ReturnType<typeof vi.fn>;
  detachDisplay: ReturnType<typeof vi.fn>;
  sendKeyCombo: ReturnType<typeof vi.fn>;
  sendKey: ReturnType<typeof vi.fn>;
  typeText: ReturnType<typeof vi.fn>;
  pasteToHost: ReturnType<typeof vi.fn>;
  captureScreenshot: ReturnType<typeof vi.fn>;
  setZoom: ReturnType<typeof vi.fn>;
  getZoom: ReturnType<typeof vi.fn>;
  fileTransferAvailable: () => boolean;
  fileSystemName: () => string | null;
  listDirectory: ReturnType<typeof vi.fn>;
  downloadFile: ReturnType<typeof vi.fn>;
  uploadFile: ReturnType<typeof vi.fn>;
}

/** Drains the microtask queue — enough ticks for a `.then().catch().finally()` chain. */
function flushPromises(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function makeFakeSession(overrides: Partial<FakeSession> = {}): FakeSession {
  const state = signal<string>('idle');
  return {
    state,
    progress: signal(0),
    error: signal<string | null>(null),
    hostClipboard: signal(''),
    displayName: signal<string | null>(null),
    formattedElapsed: signal('00:00'),
    minimized: signal(false),
    isConnected: () => state() === 'connected',
    attachDisplay: vi.fn(),
    detachDisplay: vi.fn(),
    sendKeyCombo: vi.fn(),
    sendKey: vi.fn(),
    typeText: vi.fn(),
    pasteToHost: vi.fn(),
    captureScreenshot: vi.fn(() => 'data:image/png;base64,abc'),
    setZoom: vi.fn(),
    getZoom: vi.fn(() => 1),
    fileTransferAvailable: () => false,
    fileSystemName: () => null,
    listDirectory: vi.fn(() => Promise.resolve([])),
    downloadFile: vi.fn(() => Promise.resolve()),
    uploadFile: vi.fn(() => Promise.resolve()),
    ...overrides,
  };
}

describe('ActiveSession', () => {
  let component: ActiveSession;
  let fixture: ComponentFixture<ActiveSession>;
  let sessionsMap: ReturnType<typeof signal<Map<string, FakeSession>>>;
  let manager: {
    sessions: ReturnType<typeof signal>;
    get: ReturnType<typeof vi.fn>;
    minimize: ReturnType<typeof vi.fn>;
    destroy: ReturnType<typeof vi.fn>;
  };
  let connectionsSvc: {
    list: ReturnType<typeof signal<Connection[]>>;
    listAsMap: ReturnType<typeof signal<Map<string, Connection>>>;
    reload: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };
  let routeParamMap: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(async () => {
    sessionsMap = signal(new Map<string, FakeSession>());
    manager = {
      sessions: sessionsMap,
      get: vi.fn((id: string) => sessionsMap().get(id)),
      minimize: vi.fn(),
      destroy: vi.fn(),
    };
    connectionsSvc = {
      list: signal<Connection[]>([]),
      listAsMap: signal(new Map<string, Connection>()),
      reload: vi.fn(() => of([] as Connection[])),
    };
    router = { navigate: vi.fn() };
    routeParamMap = new BehaviorSubject(convertToParamMap({ id: 'c1' }));

    await TestBed.configureTestingModule({
      imports: [ActiveSession],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: GuacamoleSessionManagerService, useValue: manager },
        { provide: ConnectionsService, useValue: connectionsSvc },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: routeParamMap,
            snapshot: { paramMap: convertToParamMap({ id: 'c1' }) },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ActiveSession);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('navigates to vault when there is no connection id', () => {
      routeParamMap.next(convertToParamMap({}));
      component.ngOnInit();
      expect(router.navigate).toHaveBeenCalledWith(['/vault']);
    });

    it('triggers reload when connection list is empty', () => {
      component.ngOnInit();
      expect(connectionsSvc.reload).toHaveBeenCalled();
    });

    it('navigates to /connecting when session is idle', () => {
      component.ngOnInit();
      expect(router.navigate).toHaveBeenCalledWith(['/connecting', 'c1']);
    });
  });

  describe('signals reflect session state', () => {
    it('returns defaults when no session', () => {
      expect(component.state()).toBe('idle');
      expect(component.progress()).toBe(0);
      expect(component.errorMsg()).toBeNull();
      expect(component.formattedElapsed()).toBe('00:00');
      expect(component.remoteName()).toBeNull();
      expect(component.hostClipboard()).toBe('');
    });

    it('proxies values from active session', () => {
      const s = makeFakeSession();
      s.state.set('connected');
      s.progress.set(100);
      s.error.set('whoops');
      s.formattedElapsed.set('05:00');
      s.displayName.set('host-1');
      s.hostClipboard.set('clip');
      sessionsMap.set(new Map([['c1', s]]));
      expect(component.state()).toBe('connected');
      expect(component.progress()).toBe(100);
      expect(component.errorMsg()).toBe('whoops');
      expect(component.formattedElapsed()).toBe('05:00');
      expect(component.remoteName()).toBe('host-1');
      expect(component.hostClipboard()).toBe('clip');
    });
  });

  describe('mascotState', () => {
    it('maps loading states to "loading"', () => {
      const s = makeFakeSession();
      sessionsMap.set(new Map([['c1', s]]));
      s.state.set('connecting');
      expect(component.mascotState()).toBe('loading');
      s.state.set('waiting');
      expect(component.mascotState()).toBe('loading');
      s.state.set('requesting-ticket');
      expect(component.mascotState()).toBe('loading');
    });

    it('maps error/disconnected to "error"', () => {
      const s = makeFakeSession();
      sessionsMap.set(new Map([['c1', s]]));
      s.state.set('error');
      expect(component.mascotState()).toBe('error');
    });

    it('returns "idle" when state is idle', () => {
      expect(component.mascotState()).toBe('idle');
    });
  });

  describe('combo preview and key selection', () => {
    it('builds preview with active modifiers', () => {
      component.comboCtrl.set(true);
      component.comboAlt.set(true);
      component.comboShift.set(false);
      component.comboSuper.set(false);
      expect(component.comboPreview()).toContain('Ctrl');
      expect(component.comboPreview()).toContain('Alt');
      expect(component.comboPreview()).not.toContain('Shift');
    });

    it('includes Super and Shift when active', () => {
      component.comboShift.set(true);
      component.comboSuper.set(true);
      expect(component.comboPreview()).toContain('Shift');
      expect(component.comboPreview()).toContain('Win');
    });

    it('selectComboKey updates current key', () => {
      const k = { label: 'F1', keysym: Keysyms.F1 };
      component.selectComboKey(k);
      expect(component.comboKey()).toBe(k);
    });

    it('toggleModifier flips each modifier', () => {
      component.toggleModifier('ctrl');
      expect(component.comboCtrl()).toBe(false);
      component.toggleModifier('alt');
      expect(component.comboAlt()).toBe(true);
      component.toggleModifier('shift');
      expect(component.comboShift()).toBe(true);
      component.toggleModifier('super');
      expect(component.comboSuper()).toBe(true);
    });
  });

  describe('toolbar actions', () => {
    let session: FakeSession;
    beforeEach(() => {
      session = makeFakeSession();
      session.state.set('connected');
      sessionsMap.set(new Map([['c1', session]]));
    });

    it('sendCtrlAltDel calls session.sendKeyCombo', () => {
      component.sendCtrlAltDel();
      expect(session.sendKeyCombo).toHaveBeenCalledWith([
        Keysyms.ControlLeft,
        Keysyms.AltLeft,
        Keysyms.Delete,
      ]);
    });

    it('openKeyComboModal / closeKeyComboModal toggle visible', () => {
      component.openKeyComboModal();
      expect(component.showKeyComboModal()).toBe(true);
      component.closeKeyComboModal();
      expect(component.showKeyComboModal()).toBe(false);
    });

    it('sendKeyCombo dispatches combo with selected mods', () => {
      vi.useFakeTimers();
      component.comboCtrl.set(true);
      component.comboShift.set(true);
      component.comboKey.set({ label: 'F4', keysym: Keysyms.F4 });
      component.sendKeyCombo();
      expect(session.sendKeyCombo).toHaveBeenCalledWith([
        Keysyms.ControlLeft,
        Keysyms.ShiftLeft,
        Keysyms.F4,
      ]);
      expect(component.comboSent()).toBe(true);
      vi.advanceTimersByTime(1000);
      expect(component.comboSent()).toBe(false);
    });

    it('openClipboardModal seeds draft from hostClipboard', () => {
      session.hostClipboard.set('hi');
      component.openClipboardModal();
      expect(component.clipboardDraft()).toBe('hi');
      expect(component.showClipboardModal()).toBe(true);
    });

    it('closeClipboardModal hides modal', () => {
      component.openClipboardModal();
      component.closeClipboardModal();
      expect(component.showClipboardModal()).toBe(false);
    });

    it('pushClipboardToHost calls session.pasteToHost', () => {
      vi.useFakeTimers();
      component.clipboardDraft.set('text');
      component.pushClipboardToHost();
      expect(session.pasteToHost).toHaveBeenCalledWith('text');
      expect(component.clipboardPushed()).toBe(true);
      vi.advanceTimersByTime(1500);
      expect(component.clipboardPushed()).toBe(false);
    });

    it('pushClipboardToHost is no-op when draft empty', () => {
      vi.useFakeTimers();
      component.clipboardDraft.set('');
      component.pushClipboardToHost();
      expect(session.pasteToHost).not.toHaveBeenCalled();
    });

    it('copyHostClipboardLocally writes to navigator.clipboard when available', () => {
      const wt = vi.fn(() => Promise.resolve());
      const original = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText: wt },
      });
      try {
        session.hostClipboard.set('to-copy');
        component.copyHostClipboardLocally();
        expect(wt).toHaveBeenCalledWith('to-copy');
      } finally {
        if (original) Object.defineProperty(navigator, 'clipboard', original);
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        else delete (navigator as any).clipboard;
      }
    });

    it('copyHostClipboardLocally is no-op when empty', () => {
      const wt = vi.fn(() => Promise.resolve());
      const original = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText: wt },
      });
      try {
        session.hostClipboard.set('');
        component.copyHostClipboardLocally();
        expect(wt).not.toHaveBeenCalled();
      } finally {
        if (original) Object.defineProperty(navigator, 'clipboard', original);
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        else delete (navigator as any).clipboard;
      }
    });

    it('takeScreenshot triggers anchor click on dataURL', () => {
      const clickSpy = vi
        .spyOn(HTMLAnchorElement.prototype, 'click')
        .mockImplementation(() => undefined);
      component.takeScreenshot();
      expect(clickSpy).toHaveBeenCalled();
    });

    it('takeScreenshot no-ops without screenshot', () => {
      session.captureScreenshot.mockReturnValue(null);
      const clickSpy = vi
        .spyOn(HTMLAnchorElement.prototype, 'click')
        .mockImplementation(() => undefined);
      clickSpy.mockClear();
      component.takeScreenshot();
      expect(clickSpy).not.toHaveBeenCalled();
    });

    it('minimize calls manager.minimize and navigates', () => {
      component.minimize();
      expect(manager.minimize).toHaveBeenCalledWith('c1');
      expect(router.navigate).toHaveBeenCalledWith(['/vault']);
    });

    it('terminate calls manager.destroy and navigates', () => {
      component.terminate();
      expect(manager.destroy).toHaveBeenCalledWith('c1');
      expect(router.navigate).toHaveBeenCalledWith(['/vault']);
    });
  });

  describe('terminal theme modal', () => {
    it('open and close toggles flag', () => {
      component.openTerminalThemeModal();
      expect(component.showTerminalThemeModal()).toBe(true);
      component.closeTerminalThemeModal();
      expect(component.showTerminalThemeModal()).toBe(false);
    });

    it('saveTerminalThemeForSession writes to sessionStorage', () => {
      sessionStorage.clear();
      component.termColorScheme.set('solarized-dark');
      component.termFontName.set('Consolas');
      component.termFontSize.set(16);
      component.saveTerminalThemeForSession();
      const stored = sessionStorage.getItem('smooth-operator.session-terminal.c1');
      expect(stored).not.toBeNull();
      expect(JSON.parse(stored!).colorScheme).toBe('solarized-dark');
      expect(component.showTerminalThemeModal()).toBe(false);
    });
  });

  describe('protocol computed signals', () => {
    it('reflects ssh detection from connection', () => {
      connectionsSvc.listAsMap.set(
        new Map<string, Connection>([
          [
            'c1',
            {
              id: 'c1',
              name: 'x',
              protocol: 'ssh',
              hostId: '',
              connectionGroupId: null,
              credentialId: null,
              settings: '{"color-scheme":"solarized-dark","font-name":"Consolas","font-size":"14"}',
              tags: [],
              host: null,
            },
          ],
        ]),
      );
      expect(component.protocol()).toBe('SSH');
      expect(component.isSshConnection()).toBe(true);
    });

    it('returns empty when no connection', () => {
      expect(component.protocol()).toBe('');
      expect(component.isSshConnection()).toBe(false);
    });
  });

  describe('file transfer', () => {
    function setConnectionPolicy(policy: Connection['effectiveFileTransferPolicy']): void {
      connectionsSvc.listAsMap.set(
        new Map<string, Connection>([
          [
            'c1',
            {
              id: 'c1',
              name: 'x',
              protocol: 'rdp',
              hostId: '',
              connectionGroupId: null,
              credentialId: null,
              settings: '{}',
              tags: [],
              host: null,
              effectiveFileTransferPolicy: policy,
            },
          ],
        ]),
      );
    }

    describe('policy computed signals', () => {
      it('defaults to Disabled with no connection loaded', () => {
        expect(component.fileTransferPolicy()).toBe('Disabled');
        expect(component.canDownloadFiles()).toBe(false);
        expect(component.canUploadFiles()).toBe(false);
      });

      it('DownloadOnly allows download but not upload', () => {
        setConnectionPolicy('DownloadOnly');
        expect(component.canDownloadFiles()).toBe(true);
        expect(component.canUploadFiles()).toBe(false);
      });

      it('UploadOnly allows upload but not download', () => {
        setConnectionPolicy('UploadOnly');
        expect(component.canDownloadFiles()).toBe(false);
        expect(component.canUploadFiles()).toBe(true);
      });

      it('Both allows upload and download', () => {
        setConnectionPolicy('Both');
        expect(component.canDownloadFiles()).toBe(true);
        expect(component.canUploadFiles()).toBe(true);
      });
    });

    it('fileTransferAvailable proxies the session signal', () => {
      const session = makeFakeSession({ fileTransferAvailable: () => true });
      sessionsMap.set(new Map([['c1', session]]));
      expect(component.fileTransferAvailable()).toBe(true);
    });

    it('fileTransferBreadcrumbs builds a crumb per path segment, root always first', () => {
      component.fileTransferPath.set('/docs/reports');
      expect(component.fileTransferBreadcrumbs()).toEqual([
        { label: '/', path: '/' },
        { label: 'docs', path: '/docs' },
        { label: 'reports', path: '/docs/reports' },
      ]);
    });

    describe('navigateFileTransfer', () => {
      let session: FakeSession;
      const entries = [
        { displayName: 'a.txt', streamName: '/a.txt', mimetype: 'text/plain', isDirectory: false },
      ];

      beforeEach(() => {
        session = makeFakeSession({ listDirectory: vi.fn(() => Promise.resolve(entries)) });
        sessionsMap.set(new Map([['c1', session]]));
      });

      it('loads entries and updates the current path on success', async () => {
        component.navigateFileTransfer('/docs');
        expect(component.fileTransferLoading()).toBe(true);
        await flushPromises();
        expect(session.listDirectory).toHaveBeenCalledWith('/docs');
        expect(component.fileTransferPath()).toBe('/docs');
        expect(component.fileTransferEntries()).toEqual(entries);
        expect(component.fileTransferLoading()).toBe(false);
        expect(component.fileTransferError()).toBeNull();
      });

      it('surfaces a rejection as fileTransferError without moving the path', async () => {
        session.listDirectory.mockReturnValueOnce(Promise.reject(new Error('not a directory')));
        const before = component.fileTransferPath();
        component.navigateFileTransfer('/broken');
        await flushPromises();
        expect(component.fileTransferError()).toBe('not a directory');
        expect(component.fileTransferPath()).toBe(before);
        expect(component.fileTransferLoading()).toBe(false);
      });

      it('no-ops when there is no active session', () => {
        sessionsMap.set(new Map());
        expect(() => component.navigateFileTransfer('/x')).not.toThrow();
        expect(component.fileTransferLoading()).toBe(false);
      });
    });

    describe('openSftpPanel', () => {
      it('opens the panel and lists the current path (root by default)', () => {
        const session = makeFakeSession();
        sessionsMap.set(new Map([['c1', session]]));
        component.openSftpPanel();
        expect(component.showSftpPanel()).toBe(true);
        expect(session.listDirectory).toHaveBeenCalledWith('/');
      });

      it('re-opens at the previously browsed path', () => {
        const session = makeFakeSession();
        sessionsMap.set(new Map([['c1', session]]));
        component.fileTransferPath.set('/docs');
        component.openSftpPanel();
        expect(session.listDirectory).toHaveBeenCalledWith('/docs');
      });

      it('closeSftpPanel hides it again and clears the drag state', () => {
        component.showSftpPanel.set(true);
        component.sftpDragActive.set(true);
        component.closeSftpPanel();
        expect(component.showSftpPanel()).toBe(false);
        expect(component.sftpDragActive()).toBe(false);
      });

      it('refreshFileTransfer re-lists the current path', () => {
        const session = makeFakeSession();
        sessionsMap.set(new Map([['c1', session]]));
        component.fileTransferPath.set('/etc');
        component.refreshFileTransfer();
        expect(session.listDirectory).toHaveBeenCalledWith('/etc');
      });
    });

    describe('openFileTransferEntry', () => {
      let session: FakeSession;
      beforeEach(() => {
        session = makeFakeSession();
        sessionsMap.set(new Map([['c1', session]]));
      });

      it('navigates into directories', () => {
        component.openFileTransferEntry({
          displayName: 'sub',
          streamName: '/sub',
          mimetype: 'application/vnd.glyptodon.guacamole.stream-index+json',
          isDirectory: true,
        });
        expect(session.listDirectory).toHaveBeenCalledWith('/sub');
      });

      it('downloads files when download is permitted', () => {
        setConnectionPolicy('Both');
        component.openFileTransferEntry({
          displayName: 'a.txt',
          streamName: '/a.txt',
          mimetype: 'text/plain',
          isDirectory: false,
        });
        expect(session.downloadFile).toHaveBeenCalled();
      });

      it('does not download files when policy forbids it', () => {
        setConnectionPolicy('UploadOnly');
        component.openFileTransferEntry({
          displayName: 'a.txt',
          streamName: '/a.txt',
          mimetype: 'text/plain',
          isDirectory: false,
        });
        expect(session.downloadFile).not.toHaveBeenCalled();
      });
    });

    describe('onFileTransferInputChange / upload queue', () => {
      let session: FakeSession;
      beforeEach(() => {
        session = makeFakeSession();
        sessionsMap.set(new Map([['c1', session]]));
      });

      function changeEventWith(files: File[]): Event {
        const input = document.createElement('input');
        input.type = 'file';
        Object.defineProperty(input, 'files', { value: files });
        return { target: input } as unknown as Event;
      }

      it('uploads the selected file to the current path, tracks it in the queue, and reloads the listing', async () => {
        component.showSftpPanel.set(true);
        component.fileTransferPath.set('/docs');
        const file = new File(['x'], 'note.txt', { type: 'text/plain' });
        component.onFileTransferInputChange(changeEventWith([file]));

        const queued = component.sftpQueue();
        expect(queued).toHaveLength(1);
        expect(queued[0]).toMatchObject({
          name: 'note.txt',
          direction: 'upload',
          status: 'active',
        });

        await flushPromises();
        expect(session.uploadFile).toHaveBeenCalledWith('/docs', file, expect.any(Function));
        expect(component.sftpQueue()[0].status).toBe('done');
        expect(session.listDirectory).toHaveBeenCalledWith('/docs');
      });

      it('uploads multiple files sequentially and refreshes the listing once', async () => {
        component.showSftpPanel.set(true);
        component.fileTransferPath.set('/docs');
        const a = new File(['a'], 'a.txt');
        const b = new File(['b'], 'b.txt');
        component.onFileTransferInputChange(changeEventWith([a, b]));

        expect(component.sftpQueue().map((t) => t.name)).toEqual(['a.txt', 'b.txt']);
        await flushPromises();
        expect(session.uploadFile).toHaveBeenNthCalledWith(1, '/docs', a, expect.any(Function));
        expect(session.uploadFile).toHaveBeenNthCalledWith(2, '/docs', b, expect.any(Function));
        expect(component.sftpQueue().every((t) => t.status === 'done')).toBe(true);
        expect(session.listDirectory).toHaveBeenCalledTimes(1);
      });

      it('marks a failed upload as error on its queue item without blocking the rest', async () => {
        component.showSftpPanel.set(true);
        session.uploadFile.mockReturnValueOnce(Promise.reject(new Error('disk full')));
        const bad = new File(['x'], 'bad.txt');
        const good = new File(['y'], 'good.txt');
        component.onFileTransferInputChange(changeEventWith([bad, good]));
        await flushPromises();

        const [first, second] = component.sftpQueue();
        expect(first).toMatchObject({ name: 'bad.txt', status: 'error', error: 'disk full' });
        expect(second).toMatchObject({ name: 'good.txt', status: 'done' });
      });

      it('reports per-file progress through the queue item', async () => {
        component.showSftpPanel.set(true);
        let capturedOnProgress: ((fraction: number) => void) | undefined;
        session.uploadFile.mockImplementationOnce(
          (_path: string, _file: File, onProgress: (fraction: number) => void) => {
            capturedOnProgress = onProgress;
            return new Promise(() => {
              /* never settles — keeps the item active */
            });
          },
        );
        component.onFileTransferInputChange(changeEventWith([new File(['x'], 'big.bin')]));
        await flushPromises();
        capturedOnProgress?.(0.5);
        expect(component.sftpQueue()[0].progress).toBe(0.5);
      });

      it('does nothing when no file was selected', () => {
        component.onFileTransferInputChange(changeEventWith([]));
        expect(session.uploadFile).not.toHaveBeenCalled();
        expect(component.sftpQueue()).toHaveLength(0);
      });

      it('clearCompletedTransfers keeps only active items', async () => {
        component.showSftpPanel.set(true);
        component.onFileTransferInputChange(changeEventWith([new File(['x'], 'done.txt')]));
        await flushPromises();
        session.uploadFile.mockReturnValueOnce(
          new Promise(() => {
            /* never settles */
          }),
        );
        component.onFileTransferInputChange(changeEventWith([new File(['y'], 'active.txt')]));
        await flushPromises();

        component.clearCompletedTransfers();
        expect(component.sftpQueue().map((t) => t.name)).toEqual(['active.txt']);
      });
    });

    describe('drag-and-drop upload', () => {
      let session: FakeSession;
      beforeEach(() => {
        session = makeFakeSession();
        sessionsMap.set(new Map([['c1', session]]));
      });

      function dragEventWith(files: File[]): DragEvent {
        return {
          preventDefault: vi.fn(),
          dataTransfer: { files },
        } as unknown as DragEvent;
      }

      it('activates the drop highlight only when upload is permitted', () => {
        setConnectionPolicy('DownloadOnly');
        component.onSftpDragOver(dragEventWith([]));
        expect(component.sftpDragActive()).toBe(false);

        setConnectionPolicy('Both');
        component.onSftpDragOver(dragEventWith([]));
        expect(component.sftpDragActive()).toBe(true);

        component.onSftpDragLeave();
        expect(component.sftpDragActive()).toBe(false);
      });

      it('uploads dropped files when permitted', async () => {
        setConnectionPolicy('Both');
        const file = new File(['x'], 'drop.txt');
        component.onSftpDrop(dragEventWith([file]));
        await flushPromises();
        expect(session.uploadFile).toHaveBeenCalledWith('/', file, expect.any(Function));
      });

      it('ignores dropped files when the policy forbids uploads', () => {
        setConnectionPolicy('DownloadOnly');
        component.onSftpDrop(dragEventWith([new File(['x'], 'drop.txt')]));
        expect(session.uploadFile).not.toHaveBeenCalled();
        expect(component.sftpDragActive()).toBe(false);
      });
    });

    describe('download queue', () => {
      it('tracks a download as indeterminate and marks it done on completion', async () => {
        const session = makeFakeSession();
        sessionsMap.set(new Map([['c1', session]]));
        setConnectionPolicy('Both');
        component.downloadFileTransferEntry({
          displayName: 'a.txt',
          streamName: '/a.txt',
          mimetype: 'text/plain',
          isDirectory: false,
        });

        expect(component.sftpQueue()[0]).toMatchObject({
          name: 'a.txt',
          direction: 'download',
          progress: null,
          status: 'active',
        });
        await flushPromises();
        expect(component.sftpQueue()[0].status).toBe('done');
      });

      it('marks a failed download as error on its queue item', async () => {
        const session = makeFakeSession({
          downloadFile: vi.fn(() => Promise.reject(new Error('timeout'))),
        });
        sessionsMap.set(new Map([['c1', session]]));
        component.downloadFileTransferEntry({
          displayName: 'a.txt',
          streamName: '/a.txt',
          mimetype: 'text/plain',
          isDirectory: false,
        });
        await flushPromises();
        expect(component.sftpQueue()[0]).toMatchObject({ status: 'error', error: 'timeout' });
      });
    });
  });

  describe('onCanvasMouseMove + toggleToolbarCollapse', () => {
    it('shows toolbar then hides after timeout', () => {
      vi.useFakeTimers();
      component.toolbarVisible.set(false);
      component.onCanvasMouseMove();
      expect(component.toolbarVisible()).toBe(true);
      vi.advanceTimersByTime(3100);
      expect(component.toolbarVisible()).toBe(false);
    });

    it('toggleToolbarCollapse flips the flag', () => {
      const before = component.toolbarCollapsed();
      component.toggleToolbarCollapse();
      expect(component.toolbarCollapsed()).toBe(!before);
    });
  });

  describe('display zoom', () => {
    let session: FakeSession;
    beforeEach(() => {
      session = makeFakeSession();
      session.state.set('connected');
      sessionsMap.set(new Map([['c1', session]]));
    });

    it('zoomIn raises zoom by a step and pushes it to the session', () => {
      component.zoomIn();
      expect(component.zoom()).toBe(1.25);
      expect(session.setZoom).toHaveBeenCalledWith(1.25);
    });

    it('zoomOut lowers zoom by a step', () => {
      component.zoomOut();
      expect(component.zoom()).toBe(0.75);
    });

    it('zoomReset returns zoom to fit (1)', () => {
      component.zoomIn();
      component.zoomReset();
      expect(component.zoom()).toBe(1);
      expect(session.setZoom).toHaveBeenLastCalledWith(1);
    });

    it('clamps zoom within bounds', () => {
      for (let i = 0; i < 20; i++) component.zoomIn();
      expect(component.zoom()).toBe(3);
      for (let i = 0; i < 30; i++) component.zoomOut();
      expect(component.zoom()).toBe(0.5);
    });

    it('zoomPercent reflects the current zoom', () => {
      component.zoomReset();
      expect(component.zoomPercent()).toBe('100%');
      component.zoomIn();
      expect(component.zoomPercent()).toBe('125%');
    });

    it('ngAfterViewInit adopts the zoom already applied to the session', () => {
      vi.useFakeTimers();
      session.getZoom.mockReturnValue(1.5);
      component.ngAfterViewInit();
      expect(component.zoom()).toBe(1.5);
    });
  });

  describe('mobile keyboard', () => {
    let session: FakeSession;
    let input: HTMLInputElement;
    beforeEach(() => {
      session = makeFakeSession();
      session.state.set('connected');
      sessionsMap.set(new Map([['c1', session]]));
      input = document.createElement('input');
      document.body.appendChild(input);
      component.hiddenKbdRef = new ElementRef(input);
    });
    afterEach(() => input.remove());

    it('toggleMobileKeyboard opens then closes the keyboard', () => {
      const focusSpy = vi.spyOn(input, 'focus');
      const blurSpy = vi.spyOn(input, 'blur');
      component.toggleMobileKeyboard();
      expect(component.keyboardActive()).toBe(true);
      expect(focusSpy).toHaveBeenCalled();
      component.toggleMobileKeyboard();
      expect(component.keyboardActive()).toBe(false);
      expect(blurSpy).toHaveBeenCalled();
    });

    it('onKbdBlur deactivates the keyboard and clears modifiers', () => {
      component.toggleMobileKeyboard();
      component.kbdCtrl.set(true);
      component.onKbdBlur();
      expect(component.keyboardActive()).toBe(false);
      expect(component.kbdCtrl()).toBe(false);
    });

    it('toggleKbdModifier flips ctrl and alt', () => {
      component.toggleKbdModifier('ctrl');
      expect(component.kbdCtrl()).toBe(true);
      component.toggleKbdModifier('alt');
      expect(component.kbdAlt()).toBe(true);
    });

    it('pressSpecialKey sends the key with active modifiers then clears them', () => {
      component.kbdCtrl.set(true);
      component.kbdAlt.set(true);
      component.pressSpecialKey(Keysyms.Escape);
      expect(session.sendKeyCombo).toHaveBeenCalledWith([
        Keysyms.ControlLeft,
        Keysyms.AltLeft,
        Keysyms.Escape,
      ]);
      expect(component.kbdCtrl()).toBe(false);
      expect(component.kbdAlt()).toBe(false);
    });

    it('pressSpecialKey re-focuses the input while the keyboard is open', () => {
      component.toggleMobileKeyboard();
      const focusSpy = vi.spyOn(input, 'focus');
      component.pressSpecialKey(Keysyms.Tab);
      expect(focusSpy).toHaveBeenCalled();
    });

    it('keyboard input is a no-op when there is no active session', () => {
      sessionsMap.set(new Map());
      const ev = new InputEvent('beforeinput', { inputType: 'insertText', data: 'x' });
      const prevent = vi.spyOn(ev, 'preventDefault');
      component.onKbdBeforeInput(ev);
      component.onKbdCompositionEnd(new CompositionEvent('compositionend', { data: 'x' }));
      expect(prevent).not.toHaveBeenCalled();
      expect(session.typeText).not.toHaveBeenCalled();
    });

    it('onKbdBeforeInput types plain text through the session', () => {
      const ev = new InputEvent('beforeinput', { inputType: 'insertText', data: 'hello' });
      const prevent = vi.spyOn(ev, 'preventDefault');
      component.onKbdBeforeInput(ev);
      expect(session.typeText).toHaveBeenCalledWith('hello');
      expect(prevent).toHaveBeenCalled();
    });

    it('onKbdBeforeInput applies a modifier to the first character only', () => {
      component.kbdCtrl.set(true);
      component.onKbdBeforeInput(
        new InputEvent('beforeinput', { inputType: 'insertText', data: 'cat' }),
      );
      expect(session.sendKeyCombo).toHaveBeenCalledWith([Keysyms.ControlLeft, 'c'.codePointAt(0)]);
      expect(session.typeText).toHaveBeenCalledWith('at');
      expect(component.kbdCtrl()).toBe(false);
    });

    it('onKbdBeforeInput maps line breaks and deletes to special keys', () => {
      component.onKbdBeforeInput(new InputEvent('beforeinput', { inputType: 'insertLineBreak' }));
      expect(session.sendKeyCombo).toHaveBeenCalledWith([Keysyms.Return]);
      component.onKbdBeforeInput(
        new InputEvent('beforeinput', { inputType: 'deleteContentBackward' }),
      );
      expect(session.sendKeyCombo).toHaveBeenCalledWith([Keysyms.Backspace]);
      component.onKbdBeforeInput(
        new InputEvent('beforeinput', { inputType: 'deleteContentForward' }),
      );
      expect(session.sendKeyCombo).toHaveBeenCalledWith([Keysyms.Delete]);
    });

    it('onKbdBeforeInput ignores composition input types', () => {
      const ev = new InputEvent('beforeinput', { inputType: 'insertCompositionText', data: 'x' });
      const prevent = vi.spyOn(ev, 'preventDefault');
      component.onKbdBeforeInput(ev);
      expect(prevent).not.toHaveBeenCalled();
      expect(session.typeText).not.toHaveBeenCalled();
    });

    it('composition commits text only on compositionend', () => {
      component.onKbdCompositionStart();
      component.onKbdBeforeInput(
        new InputEvent('beforeinput', { inputType: 'insertText', data: 'ni' }),
      );
      expect(session.typeText).not.toHaveBeenCalled();
      component.onKbdCompositionEnd(new CompositionEvent('compositionend', { data: 'にほん' }));
      expect(session.typeText).toHaveBeenCalledWith('にほん');
    });

    it('tracks the on-screen keyboard inset while active', () => {
      const listeners: Record<string, () => void> = {};
      const fakeVv = {
        height: 500,
        offsetTop: 0,
        addEventListener: vi.fn((t: string, cb: () => void) => {
          listeners[t] = cb;
        }),
        removeEventListener: vi.fn(),
      };
      const vvOriginal = Object.getOwnPropertyDescriptor(globalThis, 'visualViewport');
      const innerOriginal = globalThis.innerHeight;
      Object.defineProperty(globalThis, 'visualViewport', { configurable: true, value: fakeVv });
      Object.defineProperty(globalThis, 'innerHeight', { configurable: true, value: 800 });
      try {
        component.toggleMobileKeyboard();
        expect(component.kbdInset()).toBe(300); // 800 - 500 - 0
        expect(fakeVv.addEventListener).toHaveBeenCalled();
        component.onKbdBlur();
        expect(fakeVv.removeEventListener).toHaveBeenCalled();
        expect(component.kbdInset()).toBe(0);
      } finally {
        if (vvOriginal) Object.defineProperty(globalThis, 'visualViewport', vvOriginal);
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        else delete (globalThis as any).visualViewport;
        Object.defineProperty(globalThis, 'innerHeight', {
          configurable: true,
          value: innerOriginal,
        });
      }
    });
  });
});
