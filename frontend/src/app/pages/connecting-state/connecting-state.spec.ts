import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { BehaviorSubject, of } from 'rxjs';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { signal } from '@angular/core';

import { ConnectingState } from './connecting-state';
import { GuacamoleSessionManagerService } from '../../services/guacamole.service';
import { Connection, ConnectionsService } from '../../services/connections.service';

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
  logs: ReturnType<typeof signal>;
  error: ReturnType<typeof signal>;
  connect: ReturnType<typeof vi.fn>;
  disconnect: ReturnType<typeof vi.fn>;
  reset: ReturnType<typeof vi.fn>;
}

function makeFake(): FakeSession {
  return {
    state: signal<string>('idle'),
    progress: signal(0),
    logs: signal<unknown[]>([]),
    error: signal<string | null>(null),
    connect: vi.fn(() => Promise.resolve()),
    disconnect: vi.fn(),
    reset: vi.fn(),
  };
}

describe('ConnectingState', () => {
  let component: ConnectingState;
  let fixture: ComponentFixture<ConnectingState>;
  let sessionsMap: ReturnType<typeof signal<Map<string, FakeSession>>>;
  let manager: {
    sessions: ReturnType<typeof signal>;
    getOrCreate: ReturnType<typeof vi.fn>;
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
      getOrCreate: vi.fn((id: string) => {
        let s = sessionsMap().get(id);
        if (!s) {
          s = makeFake();
          sessionsMap.update((m) => new Map(m).set(id, s as FakeSession));
        }
        return s;
      }),
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
      imports: [ConnectingState],
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

    fixture = TestBed.createComponent(ConnectingState);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit navigates to /vault when no id', () => {
    routeParamMap.next(convertToParamMap({}));
    component.ngOnInit();
    expect(router.navigate).toHaveBeenCalledWith(['/vault']);
  });

  it('ngOnInit triggers connections.reload when list empty', () => {
    component.ngOnInit();
    expect(connectionsSvc.reload).toHaveBeenCalled();
  });

  it('ngOnInit creates and connects an idle session', () => {
    component.ngOnInit();
    const session = sessionsMap().get('c1');
    expect(session?.connect).toHaveBeenCalled();
  });

  it('ngOnInit does NOT reconnect an already-connecting session', () => {
    const s = makeFake();
    s.state.set('connecting');
    sessionsMap.set(new Map([['c1', s]]));
    component.ngOnInit();
    expect(s.connect).not.toHaveBeenCalled();
  });

  it('cancel destroys session and navigates to /vault', () => {
    component.cancel();
    expect(manager.destroy).toHaveBeenCalledWith('c1');
    expect(router.navigate).toHaveBeenCalledWith(['/vault']);
  });

  it('cancel no-ops when no id', () => {
    routeParamMap.next(convertToParamMap({}));
    component.cancel();
    expect(manager.destroy).not.toHaveBeenCalled();
  });

  it('retry disconnects, resets, and reconnects', () => {
    component.ngOnInit();
    const s = sessionsMap().get('c1')!;
    s.connect.mockClear();
    component.retry();
    expect(s.disconnect).toHaveBeenCalled();
    expect(s.reset).toHaveBeenCalled();
    expect(s.connect).toHaveBeenCalled();
  });

  it('retry no-ops when no id', () => {
    routeParamMap.next(convertToParamMap({}));
    component.retry();
    expect(manager.getOrCreate).not.toHaveBeenCalled();
  });

  describe('signals reflect session state', () => {
    it('default values when no session', () => {
      expect(component.state()).toBe('idle');
      expect(component.progress()).toBe(0);
      expect(component.logs()).toEqual([]);
      expect(component.errorMsg()).toBeNull();
    });

    it('proxies session values', () => {
      const s = makeFake();
      s.state.set('waiting');
      s.progress.set(70);
      s.logs.set([{ level: 'ok', message: 'x', timestamp: 1 }]);
      s.error.set('boom');
      sessionsMap.set(new Map([['c1', s]]));
      expect(component.state()).toBe('waiting');
      expect(component.progress()).toBe(70);
      expect(component.logs().length).toBe(1);
      expect(component.errorMsg()).toBe('boom');
    });
  });

  describe('mascotState', () => {
    it.each([
      ['connected', 'connected'],
      ['error', 'crashed'],
      ['requesting-ticket', 'thinking'],
      ['connecting', 'thinking'],
      ['waiting', 'thinking'],
      ['idle', 'thinking'],
    ] as const)('%s state -> %s mascot', (state, mascot) => {
      const s = makeFake();
      s.state.set(state);
      sessionsMap.set(new Map([['c1', s]]));
      expect(component.mascotState()).toBe(mascot);
    });

    it('disconnected without error → thinking', () => {
      const s = makeFake();
      s.state.set('disconnected');
      sessionsMap.set(new Map([['c1', s]]));
      expect(component.mascotState()).toBe('thinking');
    });

    it('disconnected with error → crashed', () => {
      const s = makeFake();
      s.state.set('disconnected');
      s.error.set('boom');
      sessionsMap.set(new Map([['c1', s]]));
      expect(component.mascotState()).toBe('crashed');
    });
  });

  describe('statusLabel', () => {
    it.each([
      ['requesting-ticket', 'pages.connectingState.status.initiating'],
      ['connecting', 'pages.connectingState.status.connecting'],
      ['waiting', 'pages.connectingState.status.authenticating'],
      ['connected', 'pages.connectingState.status.ready'],
      ['error', 'pages.connectingState.status.error'],
      ['disconnected', 'pages.connectingState.status.disconnected'],
      ['idle', 'pages.connectingState.status.idle'],
    ] as const)('%s -> %s', (state, label) => {
      const s = makeFake();
      s.state.set(state);
      sessionsMap.set(new Map([['c1', s]]));
      expect(component.statusLabel()).toBe(label);
    });
  });

  describe('step helpers', () => {
    it.each([
      ['waiting', 'requesting-ticket', true],
      ['connecting', 'connecting', false],
      ['requesting-ticket', 'connecting', false],
    ] as const)('isStepDone(%s, %s) === %s', (state, stepKey, expected) => {
      const s = makeFake();
      s.state.set(state);
      sessionsMap.set(new Map([['c1', s]]));
      const step = component.steps.find((x) => x.key === stepKey)!;
      expect(component.isStepDone(step)).toBe(expected);
    });

    it('isStepActive returns true only for matching key', () => {
      const s = makeFake();
      s.state.set('connecting');
      sessionsMap.set(new Map([['c1', s]]));
      const step = component.steps.find((x) => x.key === 'connecting')!;
      expect(component.isStepActive(step)).toBe(true);
    });
  });

  describe('log helpers', () => {
    it.each([
      ['ok', 'text-green-500'],
      ['warn', 'text-amber-400'],
      ['error', 'text-error'],
      ['info', 'text-primary'],
      ['', 'text-primary'],
    ] as const)('logColorClass(%s) -> %s', (level, klass) => {
      expect(component.logColorClass(level)).toBe(klass);
    });

    it.each([
      ['ok', 'pages.connectingState.logTag.ok'],
      ['warn', 'pages.connectingState.logTag.warn'],
      ['error', 'pages.connectingState.logTag.error'],
      ['info', 'pages.connectingState.logTag.info'],
      ['', 'pages.connectingState.logTag.info'],
    ] as const)('logTag(%s) -> %s', (level, tag) => {
      expect(component.logTag(level)).toBe(tag);
    });
  });

  it('navigates to /session/:id when state becomes connected', () => {
    vi.useFakeTimers();
    component.ngOnInit();
    const s = sessionsMap().get('c1')!;
    s.state.set('connected');
    fixture.detectChanges();
    vi.advanceTimersByTime(500);
    expect(router.navigate).toHaveBeenCalledWith(['/session', 'c1']);
    component.ngOnDestroy();
  });
});
