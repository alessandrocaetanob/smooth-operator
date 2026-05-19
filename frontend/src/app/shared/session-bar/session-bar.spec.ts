import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';

import { SessionBar } from './session-bar';
import { GuacamoleSession, GuacamoleSessionManagerService } from '../../services/guacamole.service';
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

describe('SessionBar', () => {
  let component: SessionBar;
  let manager: {
    minimizedSessions: ReturnType<typeof signal<GuacamoleSession[]>>;
    destroy: ReturnType<typeof vi.fn>;
  };
  let connections: {
    listAsMap: ReturnType<typeof signal<Map<string, Connection>>>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    manager = {
      minimizedSessions: signal<GuacamoleSession[]>([]),
      destroy: vi.fn(),
    };
    connections = { listAsMap: signal(new Map<string, Connection>()) };
    router = { navigate: vi.fn() };
    TestBed.configureTestingModule({
      imports: [SessionBar],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: GuacamoleSessionManagerService, useValue: manager },
        { provide: ConnectionsService, useValue: connections },
        { provide: Router, useValue: router },
      ],
    });
    const fixture = TestBed.createComponent(SessionBar);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('connectionName prefers the connection map, then displayName, then id', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const session: any = {
      connectionId: 'c1',
      displayName: () => 'remote-host',
      minimized: signal(true),
    };
    expect(component.connectionName(session)).toBe('remote-host');
    connections.listAsMap.set(
      new Map<string, Connection>([
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        ['c1', { id: 'c1', name: 'web-rdp' } as any],
      ]),
    );
    expect(component.connectionName(session)).toBe('web-rdp');
    session.displayName = () => null;
    connections.listAsMap.set(new Map());
    expect(component.connectionName(session)).toBe('c1');
  });

  it('restore unminimizes session and navigates', () => {
    const min = signal(true);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const session: any = { connectionId: 'c1', minimized: min };
    component.restore(session);
    expect(min()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/session', 'c1']);
  });

  it('close stops propagation and destroys session', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const session: any = { connectionId: 'c1' };
    const event = { stopPropagation: vi.fn() } as unknown as Event;
    component.close(session, event);
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(manager.destroy).toHaveBeenCalledWith('c1');
  });
});
