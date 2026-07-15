import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { Vault } from './vault';
import { Connection, ConnectionsService } from '../../services/connections.service';
import { VaultsService, Vault as VaultEntity } from '../../services/vaults.service';
import { AuthService } from '../../services/auth.service';
import { OnboardingTourService } from '../../services/onboarding-tour.service';

describe('Vault', () => {
  let component: Vault;
  let fixture: ComponentFixture<Vault>;
  let connections: {
    list: ReturnType<typeof signal<Connection[]>>;
    loading: ReturnType<typeof signal<boolean>>;
    reload: ReturnType<typeof vi.fn>;
    getLastConnected: ReturnType<typeof vi.fn>;
    probeConnectionsBulk: ReturnType<typeof vi.fn>;
    downloadConnectionFile: ReturnType<typeof vi.fn>;
  };
  let vaults: {
    list: ReturnType<typeof signal<VaultEntity[]>>;
    reload: ReturnType<typeof vi.fn>;
  };
  let auth: {
    canManageConnections: ReturnType<typeof signal<boolean>>;
    currentUser: ReturnType<typeof signal<{ id: string } | null>>;
    isOwner: ReturnType<typeof signal<boolean>>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };
  let tour: OnboardingTourService;

  beforeEach(async () => {
    connections = {
      list: signal<Connection[]>([]),
      loading: signal(false),
      reload: vi.fn(() => of([] as Connection[])),
      getLastConnected: vi.fn(() => of([])),
      probeConnectionsBulk: vi.fn(() => of({})),
      downloadConnectionFile: vi.fn(() => of(undefined)),
    };
    vaults = {
      list: signal<VaultEntity[]>([]),
      reload: vi.fn(() => of([] as VaultEntity[])),
    };
    auth = {
      canManageConnections: signal(true),
      currentUser: signal(null),
      isOwner: signal(false),
    };
    router = { navigate: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [Vault],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        provideTranslateService(),
        { provide: ConnectionsService, useValue: connections },
        { provide: VaultsService, useValue: vaults },
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        vault: {
          lastConnected: {
            justNow: 'just now',
            minutesAgo: '{{minutes}}m ago',
            hoursAgo: '{{hours}}h ago',
            daysAgo: '{{days}}d ago',
            monthsAgo: '{{months}}mo ago',
          },
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(Vault);
    component = fixture.componentInstance;
    tour = TestBed.inject(OnboardingTourService);
  });

  function seed() {
    connections.list.set([
      {
        id: 'c1',
        name: 'web',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: null,
        settings: '{}',
        tags: [],
        host: null,
      },
      {
        id: 'c2',
        name: 'db',
        protocol: 'ssh',
        hostId: 'h2',
        connectionGroupId: 'v1',
        credentialId: null,
        settings: '{}',
        tags: [],
        host: null,
      },
      {
        id: 'c3',
        name: 'orphan',
        protocol: 'vnc',
        hostId: 'h3',
        connectionGroupId: null,
        credentialId: null,
        settings: '{}',
        tags: [],
        host: null,
      },
    ]);
    vaults.list.set([
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      { id: 'v1', name: 'prod-vault' } as any,
    ]);
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('computed signals', () => {
    beforeEach(() => seed());

    it('hasConnections is true when list non-empty', () => {
      expect(component.hasConnections()).toBe(true);
      connections.list.set([]);
      expect(component.hasConnections()).toBe(false);
    });

    it('vaultsMap maps id to name', () => {
      expect(component.vaultsMap().get('v1')).toBe('prod-vault');
    });

    it('connectionsByVault groups by connectionGroupId, ignoring null group', () => {
      const map = component.connectionsByVault();
      expect(map.get('v1')?.length).toBe(2);
      expect(map.has(null as unknown as string)).toBe(false);
    });

    it('vaultConnectionCounts counts per vault', () => {
      expect(component.vaultConnectionCounts().get('v1')).toBe(2);
    });

    it('filteredConnections returns all when no vault selected', () => {
      component.selectVault(null);
      expect(component.filteredConnections().length).toBe(3);
    });

    it('filteredConnections filters by selected vault', () => {
      component.selectVault('v1');
      expect(component.filteredConnections().map((c) => c.id)).toEqual(['c1', 'c2']);
    });

    it('filteredConnections returns empty when vault has no connections', () => {
      component.selectVault('does-not-exist');
      expect(component.filteredConnections()).toEqual([]);
    });
  });

  describe('refresh()', () => {
    it('triggers reload and probes reachability', () => {
      seed();
      component.refresh();
      expect(connections.reload).toHaveBeenCalled();
      expect(vaults.reload).toHaveBeenCalled();
      expect(connections.probeConnectionsBulk).toHaveBeenCalled();
      expect(connections.getLastConnected).toHaveBeenCalled();
    });

    it('skips probe and last-connected when reload returns null', () => {
      seed();
      connections.reload.mockReturnValueOnce(throwError(() => new Error('x')));
      component.refresh();
      // Both connections probes and last-connected should not run on failure
      expect(connections.probeConnectionsBulk).not.toHaveBeenCalled();
      expect(connections.getLastConnected).not.toHaveBeenCalled();
    });

    it('handles getLastConnected error silently', () => {
      seed();
      connections.getLastConnected.mockReturnValueOnce(throwError(() => new Error('x')));
      expect(() => component.refresh()).not.toThrow();
    });

    it('ngOnInit calls refresh', () => {
      const spy = vi.spyOn(component, 'refresh');
      component.ngOnInit();
      expect(spy).toHaveBeenCalled();
    });

    it('skips reachability probe when no connections', () => {
      connections.list.set([]);
      component.refresh();
      expect(connections.probeConnectionsBulk).not.toHaveBeenCalled();
    });

    it('populates reachability map from probe result', () => {
      seed();
      connections.probeConnectionsBulk.mockReturnValueOnce(
        of({ c1: 'up', c2: 'down', c3: 'forbidden' }),
      );
      component.refresh();
      expect(component.reachability('c1')).toBe('up');
      expect(component.reachability('c2')).toBe('down');
      expect(component.reachability('c3')).toBe('unknown');
    });

    it('keeps reachability as unknown on probe error', () => {
      seed();
      connections.probeConnectionsBulk.mockReturnValueOnce(throwError(() => new Error('x')));
      component.refresh();
      expect(component.reachability('c1')).toBe('unknown');
    });

    it('populates last-connected map from rows', () => {
      seed();
      const now = new Date().toISOString();
      connections.getLastConnected.mockReturnValueOnce(
        of([
          { connectionId: 'c1', lastConnectedAt: now },
          { connectionId: '', lastConnectedAt: now },
          { connectionId: 'c2', lastConnectedAt: '' },
        ]),
      );
      component.refresh();
      expect(component.lastConnectedLabel('c1')).toBe('just now');
      expect(component.lastConnectedLabel('c2')).toBeNull();
    });
  });

  describe('selectVault()', () => {
    it('sets selectedVaultId', () => {
      component.selectVault('v1');
      expect(component.selectedVaultId()).toBe('v1');
      component.selectVault(null);
      expect(component.selectedVaultId()).toBeNull();
    });
  });

  describe('connect()', () => {
    it('navigates to /connecting/:id', () => {
      component.connect('c1');
      expect(router.navigate).toHaveBeenCalledWith(['/connecting', 'c1']);
    });

    it('marks the "session" onboarding step complete', () => {
      const completeStep = vi.spyOn(tour, 'completeStep');
      component.connect('c1');
      expect(completeStep).toHaveBeenCalledWith('session');
    });
  });

  describe('downloadFile()', () => {
    const baseConn: Connection = {
      id: 'c1',
      name: 'web',
      protocol: 'rdp',
      hostId: 'h1',
      connectionGroupId: 'v1',
      credentialId: null,
      settings: '{}',
      tags: [],
      host: null,
    };

    it('calls downloadConnectionFile with matching format', () => {
      const ev = { stopPropagation: vi.fn() } as unknown as MouseEvent;
      component.downloadFile({ ...baseConn, protocol: 'ssh' }, ev);
      expect(connections.downloadConnectionFile).toHaveBeenCalledWith('c1', 'ssh');
      expect(ev.stopPropagation).toHaveBeenCalled();
    });

    it('falls back to rdp when protocol is unknown', () => {
      const ev = { stopPropagation: vi.fn() } as unknown as MouseEvent;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      component.downloadFile({ ...baseConn, protocol: 'weird' as any }, ev);
      expect(connections.downloadConnectionFile).toHaveBeenCalledWith('c1', 'rdp');
    });

    it('falls back to rdp when protocol is empty', () => {
      const ev = { stopPropagation: vi.fn() } as unknown as MouseEvent;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      component.downloadFile({ ...baseConn, protocol: '' as any }, ev);
      expect(connections.downloadConnectionFile).toHaveBeenCalledWith('c1', 'rdp');
    });

    it('swallows download errors', () => {
      connections.downloadConnectionFile.mockReturnValueOnce(throwError(() => new Error('x')));
      const ev = { stopPropagation: vi.fn() } as unknown as MouseEvent;
      expect(() => component.downloadFile(baseConn, ev)).not.toThrow();
    });
  });

  describe('lastConnectedLabel()', () => {
    beforeEach(() => seed());

    function setLastConnected(id: string, isoMinutesAgo: number) {
      const iso = new Date(Date.now() - isoMinutesAgo * 60_000).toISOString();
      connections.getLastConnected.mockReturnValueOnce(
        of([{ connectionId: id, lastConnectedAt: iso }]),
      );
      component.refresh();
    }

    it('returns null when there is no entry', () => {
      expect(component.lastConnectedLabel('unknown')).toBeNull();
    });

    it('returns "just now" for sub-minute', () => {
      setLastConnected('c1', 0);
      expect(component.lastConnectedLabel('c1')).toBe('just now');
    });

    it('returns minutes label', () => {
      setLastConnected('c1', 5);
      expect(component.lastConnectedLabel('c1')).toBe('5m ago');
    });

    it('returns hours label', () => {
      setLastConnected('c1', 120);
      expect(component.lastConnectedLabel('c1')).toBe('2h ago');
    });

    it('returns days label', () => {
      setLastConnected('c1', 60 * 24 * 3);
      expect(component.lastConnectedLabel('c1')).toBe('3d ago');
    });

    it('returns months label', () => {
      setLastConnected('c1', 60 * 24 * 90);
      expect(component.lastConnectedLabel('c1')).toBe('3mo ago');
    });

    it('returns null for invalid date', () => {
      connections.getLastConnected.mockReturnValueOnce(
        of([{ connectionId: 'c1', lastConnectedAt: 'not-a-date' }]),
      );
      component.refresh();
      expect(component.lastConnectedLabel('c1')).toBeNull();
    });
  });

  describe('protocol class helpers', () => {
    it.each([
      ['rdp', 'desktop_windows'],
      ['ssh', 'terminal'],
      ['vnc', 'screen_share'],
      ['other', 'hub'],
      ['', 'hub'],
    ] as const)('protocolIcon(%s) returns %s', (p, expected) => {
      expect(component.protocolIcon(p)).toBe(expected);
    });

    it.each(['rdp', 'ssh', 'vnc', 'unknown'] as const)(
      'protocolIconBg/Color/FooterBg/BadgeColor returns non-empty string for %s',
      (p) => {
        expect(component.protocolIconBg(p)).toBeTruthy();
        expect(component.protocolIconColor(p)).toBeTruthy();
        expect(component.protocolFooterBg(p)).toBeTruthy();
        expect(component.protocolBadgeColor(p)).toBeTruthy();
      },
    );

    it('handles empty protocol string', () => {
      expect(component.protocolIconBg('')).toBeTruthy();
      expect(component.protocolIconColor('')).toBeTruthy();
    });
  });
});
