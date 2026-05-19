import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { BehaviorSubject, of } from 'rxjs';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { signal } from '@angular/core';

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
  pasteToHost: ReturnType<typeof vi.fn>;
  captureScreenshot: ReturnType<typeof vi.fn>;
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
    pasteToHost: vi.fn(),
    captureScreenshot: vi.fn(() => 'data:image/png;base64,abc'),
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
});
