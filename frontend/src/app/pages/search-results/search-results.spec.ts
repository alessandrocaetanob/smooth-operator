import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { BehaviorSubject } from 'rxjs';
import { of, throwError } from 'rxjs';

import { SearchResults } from './search-results';
import { VaultsService, Vault } from '../../services/vaults.service';
import { ConnectionsService, Connection } from '../../services/connections.service';

const EN_TRANSLATIONS = {
  pages: {
    searchResults: {
      title: 'Search Results',
      searchingFor: 'Searching for',
      resultsForOne: '{{count}} result for',
      resultsForOther: '{{count}} results for',
      typeQueryPrompt: 'Type a query in the search bar to find vaults and connections.',
      loading: 'Loading…',
      noMatchesFound: 'No matches found',
      noMatchesFor: 'Nothing matched',
      tryDifferentKeyword: 'Try a different keyword.',
      vaultsHeading: 'Vaults',
      connectionsHeading: 'Connections',
      vaultFallback: 'Vault',
      connectionFallback: 'Connection',
      userCountOne: '{{count}} user',
      userCountOther: '{{count}} users',
      groupCountOne: '{{count}} group',
      groupCountOther: '{{count}} groups',
    },
  },
};

describe('SearchResults', () => {
  let component: SearchResults;
  let fixture: ComponentFixture<SearchResults>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let queryParamMap: BehaviorSubject<any>;
  let vaultsSvc: { reload: ReturnType<typeof vi.fn> };
  let connectionsSvc: { reload: ReturnType<typeof vi.fn> };
  let router: { navigate: ReturnType<typeof vi.fn> };

  const sampleVaults: Vault[] = [
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    { id: 'v1', name: 'prod-vault', userCount: 1, groupCount: 2 } as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    { id: 'v2', name: 'dev', userCount: 0 } as any,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    { id: 'v3', name: 'staging' } as any,
  ];
  const sampleConns: Connection[] = [
    {
      id: 'c1',
      name: 'web-rdp',
      protocol: 'rdp',
      hostId: 'h1',
      connectionGroupId: 'v1',
      credentialId: null,
      settings: '{}',
      tags: [],
      host: { id: 'h1', name: 'web', address: '10.0.0.1' },
    },
    {
      id: 'c2',
      name: 'orphan',
      protocol: 'ssh',
      hostId: '',
      connectionGroupId: null,
      credentialId: null,
      settings: '{}',
      tags: [],
      host: null,
    },
  ];

  beforeEach(async () => {
    queryParamMap = new BehaviorSubject({ get: (_: string) => 'web' });
    vaultsSvc = { reload: vi.fn(() => of(sampleVaults)) };
    connectionsSvc = { reload: vi.fn(() => of(sampleConns)) };
    router = { navigate: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SearchResults],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: ActivatedRoute, useValue: { queryParamMap } },
        { provide: VaultsService, useValue: vaultsSvc },
        { provide: ConnectionsService, useValue: connectionsSvc },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', EN_TRANSLATIONS);
    translate.use('en');

    fixture = TestBed.createComponent(SearchResults);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('triggers vaults.reload and connections.reload on construction', () => {
    expect(vaultsSvc.reload).toHaveBeenCalled();
    expect(connectionsSvc.reload).toHaveBeenCalled();
  });

  it('vaultMatches filters by query', () => {
    queryParamMap.next({ get: () => 'prod' });
    expect(component.vaultMatches().map((m) => m.id)).toEqual(['v1']);
    expect(component.vaultMatches()[0].subtitle).toContain('1 user');
  });

  it('vaultSubtitle handles plural counts', () => {
    queryParamMap.next({ get: () => 'prod-vault' });
    const m = component.vaultMatches().find((x) => x.id === 'v1');
    expect(m?.subtitle).toContain('2 groups');
  });

  it('vaultSubtitle defaults to "Vault" when no counts', () => {
    queryParamMap.next({ get: () => 'staging' });
    const m = component.vaultMatches().find((x) => x.id === 'v3');
    expect(m?.subtitle).toBe('Vault');
  });

  it('returns empty matches when query is empty', () => {
    queryParamMap.next({ get: () => '' });
    expect(component.vaultMatches()).toEqual([]);
    expect(component.connectionMatches()).toEqual([]);
    expect(component.hasQuery()).toBe(false);
  });

  it('connectionMatches filter by name and host', () => {
    queryParamMap.next({ get: () => 'web' });
    const ids = component.connectionMatches().map((m) => m.id);
    expect(ids).toEqual(['c1']);
    expect(component.connectionMatches()[0].subtitle).toContain('10.0.0.1');
    expect(component.connectionMatches()[0].protocol).toBe('RDP');
  });

  it('connectionSubtitle returns "Connection" when no host', () => {
    queryParamMap.next({ get: () => 'orphan' });
    const m = component.connectionMatches().find((x) => x.id === 'c2');
    expect(m?.subtitle).toBe('Connection');
  });

  it('connectionSubtitle returns host name when only name available', () => {
    queryParamMap.next({ get: () => 'edge' });
    connectionsSvc.reload.mockReturnValueOnce(
      of([
        {
          id: 'c3',
          name: 'edge',
          protocol: 'rdp',
          hostId: 'h3',
          connectionGroupId: null,
          credentialId: null,
          settings: '{}',
          tags: [],
          host: { id: 'h3', name: 'edge-host', address: '' },
        },
      ]),
    );
    // Re-create the component so the new mock is consumed
    fixture = TestBed.createComponent(SearchResults);
    component = fixture.componentInstance;
    queryParamMap.next({ get: () => 'edge' });
    const m = component.connectionMatches().find((x) => x.id === 'c3');
    expect(m?.subtitle).toBe('edge-host');
  });

  it('totalCount sums vault and connection matches', () => {
    queryParamMap.next({ get: () => 'web' });
    expect(component.totalCount()).toBeGreaterThan(0);
  });

  it('openVault navigates with query params', () => {
    component.openVault('v1');
    expect(router.navigate).toHaveBeenCalledWith(['/vault'], { queryParams: { vaultId: 'v1' } });
  });

  it('openConnection navigates with query params', () => {
    component.openConnection('c1');
    expect(router.navigate).toHaveBeenCalledWith(['/connections'], {
      queryParams: { connectionId: 'c1' },
    });
  });

  it('handles vault reload error silently and still finishes loading', () => {
    vaultsSvc.reload.mockReturnValueOnce(throwError(() => new Error('x')));
    connectionsSvc.reload.mockReturnValueOnce(of(sampleConns));
    fixture = TestBed.createComponent(SearchResults);
    component = fixture.componentInstance;
    expect(component.loading()).toBe(false);
  });

  it('handles connections reload error silently and still finishes loading', () => {
    vaultsSvc.reload.mockReturnValueOnce(of(sampleVaults));
    connectionsSvc.reload.mockReturnValueOnce(throwError(() => new Error('x')));
    fixture = TestBed.createComponent(SearchResults);
    component = fixture.componentInstance;
    expect(component.loading()).toBe(false);
  });

  it('handles null payloads from reload', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vaultsSvc.reload.mockReturnValueOnce(of(null as any));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    connectionsSvc.reload.mockReturnValueOnce(of(null as any));
    fixture = TestBed.createComponent(SearchResults);
    component = fixture.componentInstance;
    expect(component.loading()).toBe(false);
  });
});
