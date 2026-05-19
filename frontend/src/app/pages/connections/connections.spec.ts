import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Observable, of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { Connections } from './connections';
import {
  Connection,
  ConnectionsService,
  CreateConnectionPayload,
} from '../../services/connections.service';
import { HostsService, AppHost } from '../../services/hosts.service';
import { CredentialsService, Credential } from '../../services/credentials.service';
import { VaultsService, Vault } from '../../services/vaults.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

interface ConnectionsStub {
  list: ReturnType<typeof signal<Connection[]>>;
  reload: ReturnType<typeof vi.fn>;
  create: ReturnType<typeof vi.fn>;
  update: ReturnType<typeof vi.fn>;
  remove: ReturnType<typeof vi.fn>;
}
interface HostsStub {
  list: ReturnType<typeof signal<AppHost[]>>;
  reload: ReturnType<typeof vi.fn>;
  create: ReturnType<typeof vi.fn>;
}
interface CredsStub {
  list: ReturnType<typeof signal<Credential[]>>;
  reload: ReturnType<typeof vi.fn>;
}
interface VaultsStub {
  list: ReturnType<typeof signal<Vault[]>>;
  reload: ReturnType<typeof vi.fn>;
}
interface ConfirmStub {
  ask: ReturnType<typeof vi.fn>;
}
interface ToastStub {
  success: ReturnType<typeof vi.fn>;
  error: ReturnType<typeof vi.fn>;
}

describe('Connections', () => {
  let component: Connections;
  let fixture: ComponentFixture<Connections>;
  let connections: ConnectionsStub;
  let hosts: HostsStub;
  let creds: CredsStub;
  let vaults: VaultsStub;
  let confirm: ConfirmStub;
  let toast: ToastStub;

  beforeEach(async () => {
    connections = {
      list: signal<Connection[]>([]),
      reload: vi.fn(() => of([] as Connection[])),
      create: vi.fn(
        (_p: CreateConnectionPayload): Observable<Connection> => of({} as unknown as Connection),
      ),
      update: vi.fn((_id: string, _p: CreateConnectionPayload) => of(undefined)),
      remove: vi.fn((_id: string) => of(undefined)),
    };
    hosts = {
      list: signal<AppHost[]>([]),
      reload: vi.fn(() => of([] as AppHost[])),
      create: vi.fn(() => of({ id: 'h-new', name: 'new', address: '10.0.0.99' })),
    };
    creds = {
      list: signal<Credential[]>([]),
      reload: vi.fn(() => of([] as Credential[])),
    };
    vaults = {
      list: signal<Vault[]>([]),
      reload: vi.fn(() => of([] as Vault[])),
    };
    confirm = { ask: vi.fn() };
    toast = { success: vi.fn(), error: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [Connections],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ConnectionsService, useValue: connections },
        { provide: HostsService, useValue: hosts },
        { provide: CredentialsService, useValue: creds },
        { provide: VaultsService, useValue: vaults },
        { provide: ConfirmDialogService, useValue: confirm },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Connections);
    component = fixture.componentInstance;
  });

  function seedData() {
    connections.list.set([
      {
        id: 'c1',
        name: 'web-rdp',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: 'cred1',
        settings: '{}',
        tags: ['prod', 'web'],
        host: { id: 'h1', name: 'web', address: '10.0.0.1' },
      },
      {
        id: 'c2',
        name: 'db-ssh',
        protocol: 'ssh',
        hostId: 'h2',
        connectionGroupId: 'v1',
        credentialId: 'cred2',
        settings: '{"color-scheme":"solarized-dark","font-name":"Consolas","font-size":"14"}',
        tags: ['prod', 'db'],
        host: { id: 'h2', name: 'db', address: '10.0.0.2' },
      },
    ]);
    hosts.list.set([
      { id: 'h1', name: 'web', address: '10.0.0.1' },
      { id: 'h2', name: 'db', address: '10.0.0.2' },
    ]);
    creds.list.set([
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      { id: 'cred1', name: 'admin', username: 'admin', storageMode: 'Local' } as any,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      { id: 'cred2', name: 'root', username: 'root', storageMode: 'Local' } as any,
    ]);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vaults.list.set([{ id: 'v1', name: 'prod-vault' } as any]);
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('refresh()', () => {
    it('flips loading false on success', () => {
      component.refresh();
      expect(component.loading()).toBe(false);
      expect(component.errorMessage()).toBeNull();
    });

    it('sets errorMessage on failure', () => {
      connections.reload.mockReturnValueOnce(throwError(() => ({ error: { message: 'boom' } })));
      component.refresh();
      expect(component.errorMessage()).toBe('boom');
      expect(component.loading()).toBe(false);
    });

    it('falls back to generic message when error has none', () => {
      connections.reload.mockReturnValueOnce(throwError(() => ({})));
      component.refresh();
      expect(component.errorMessage()).toBe('Failed to load.');
    });

    it('ngOnInit triggers refresh', () => {
      const spy = vi.spyOn(component, 'refresh');
      component.ngOnInit();
      expect(spy).toHaveBeenCalled();
    });
  });

  describe('drawer state', () => {
    it('newConnection() opens an empty form', () => {
      component.newConnection();
      expect(component.showDrawer()).toBe(true);
      expect(component.form().id).toBeNull();
      expect(component.form().name).toBe('');
    });

    it('closeDrawer() resets state', () => {
      component.newConnection();
      component.patch('name', 'X');
      component.termColorScheme.set('solarized-light');
      component.termFontName.set('Consolas');
      component.termFontSize.set(20);
      component.tagInputValue.set('typing');
      component.mascotState.set('thinking');

      component.closeDrawer();

      expect(component.showDrawer()).toBe(false);
      expect(component.form().name).toBe('');
      expect(component.termColorScheme()).toBe('gray-black');
      expect(component.termFontName()).toBe('monospace');
      expect(component.termFontSize()).toBe(12);
      expect(component.tagInputValue()).toBe('');
      expect(component.mascotState()).toBe('idle');
    });

    it('patch() updates a single field', () => {
      component.newConnection();
      component.patch('name', 'web-1');
      component.patch('protocol', 'ssh');
      expect(component.form().name).toBe('web-1');
      expect(component.form().protocol).toBe('ssh');
    });
  });

  describe('edit()', () => {
    it('hydrates form from non-ssh connection', () => {
      seedData();
      component.edit({
        id: 'c1',
        name: 'web-rdp',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: 'cred1',
        settings: '{"port":3389}',
        tags: ['prod', 'web'],
        host: null,
      });
      expect(component.form().id).toBe('c1');
      expect(component.form().name).toBe('web-rdp');
      expect(component.form().protocol).toBe('rdp');
      expect(component.form().tags).toEqual(['prod', 'web']);
      expect(component.showDrawer()).toBe(true);
    });

    it('hydrates terminal settings from ssh and strips them from form.settings', () => {
      component.edit({
        id: 'c2',
        name: 'db-ssh',
        protocol: 'ssh',
        hostId: 'h2',
        connectionGroupId: 'v1',
        credentialId: 'cred2',
        settings:
          '{"color-scheme":"solarized-dark","font-name":"Consolas","font-size":"14","port":22}',
        tags: ['db'],
        host: null,
      });
      expect(component.termColorScheme()).toBe('solarized-dark');
      expect(component.termFontName()).toBe('Consolas');
      expect(component.termFontSize()).toBe(14);
      const parsed = JSON.parse(component.form().settings);
      expect(parsed['color-scheme']).toBeUndefined();
      expect(parsed.port).toBe(22);
    });

    it('defaults terminal font size when malformed', () => {
      component.edit({
        id: 'c3',
        name: 'x',
        protocol: 'ssh',
        hostId: 'h2',
        connectionGroupId: 'v1',
        credentialId: null,
        settings: '{"font-size":"not-a-number"}',
        tags: [],
        host: null,
      });
      expect(component.termFontSize()).toBe(12);
    });

    it('handles invalid JSON settings gracefully', () => {
      component.edit({
        id: 'c4',
        name: 'broken',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: null,
        settings: '{not json',
        tags: [],
        host: null,
      });
      expect(component.form().settings).toBe('{}');
    });
  });

  describe('tag input', () => {
    beforeEach(() => component.newConnection());

    it('addTagFromInput() splits comma-separated values', () => {
      component.tagInputValue.set('prod, web ,prod');
      component.addTagFromInput();
      expect(component.form().tags).toEqual(['prod', 'web']);
      expect(component.tagInputValue()).toBe('');
    });

    it('addTagFromInput() ignores empty/whitespace input', () => {
      component.tagInputValue.set('   ');
      component.addTagFromInput();
      expect(component.form().tags).toEqual([]);
    });

    it('onTagInputKeydown commits on Enter', () => {
      component.tagInputValue.set('staging');
      const ev = new KeyboardEvent('keydown', { key: 'Enter' });
      component.onTagInputKeydown(ev);
      expect(component.form().tags).toContain('staging');
    });

    it('onTagInputKeydown commits on comma', () => {
      component.tagInputValue.set('dev');
      const ev = new KeyboardEvent('keydown', { key: ',' });
      component.onTagInputKeydown(ev);
      expect(component.form().tags).toContain('dev');
    });

    it('onTagInputKeydown Backspace removes last tag when input empty', () => {
      component.form.update((f) => ({ ...f, tags: ['a', 'b', 'c'] }));
      component.tagInputValue.set('');
      component.onTagInputKeydown(new KeyboardEvent('keydown', { key: 'Backspace' }));
      expect(component.form().tags).toEqual(['a', 'b']);
    });

    it('onTagInputKeydown Backspace does NOT remove last tag when input has text', () => {
      component.form.update((f) => ({ ...f, tags: ['a', 'b'] }));
      component.tagInputValue.set('half');
      component.onTagInputKeydown(new KeyboardEvent('keydown', { key: 'Backspace' }));
      expect(component.form().tags).toEqual(['a', 'b']);
    });

    it('onTagInputKeydown ignores other keys', () => {
      component.form.update((f) => ({ ...f, tags: ['a'] }));
      component.tagInputValue.set('abc');
      const ev = new KeyboardEvent('keydown', { key: 'x' });
      const spy = vi.spyOn(ev, 'preventDefault');
      component.onTagInputKeydown(ev);
      expect(spy).not.toHaveBeenCalled();
    });

    it('removeTag() drops the matching tag', () => {
      component.form.update((f) => ({ ...f, tags: ['a', 'b', 'c'] }));
      component.removeTag('b');
      expect(component.form().tags).toEqual(['a', 'c']);
    });

    it('tagColor() is deterministic for same input', () => {
      expect(component.tagColor('prod')).toBe(component.tagColor('prod'));
      expect(typeof component.tagColor('any')).toBe('string');
    });
  });

  describe('filters and computed signals', () => {
    beforeEach(() => seedData());

    it('hostsMap, credentialsMap, vaultsMap', () => {
      expect(component.hostsMap().get('h1')?.address).toBe('10.0.0.1');
      expect(component.credentialsMap().get('cred1')?.name).toBe('admin');
      expect(component.vaultsMap().get('v1')?.name).toBe('prod-vault');
    });

    it('allTags() returns unique sorted tags', () => {
      expect(component.allTags()).toEqual(['db', 'prod', 'web']);
    });

    it('filteredConnections() applies search query', () => {
      component.searchQuery.set('db');
      expect(component.filteredConnections().map((c) => c.id)).toEqual(['c2']);
    });

    it('filteredConnections() applies tag filter', () => {
      component.activeTagFilter.set('web');
      expect(component.filteredConnections().map((c) => c.id)).toEqual(['c1']);
    });

    it('filteredConnections() combines search and tag filter', () => {
      component.activeTagFilter.set('prod');
      component.searchQuery.set('ssh');
      expect(component.filteredConnections().map((c) => c.id)).toEqual(['c2']);
    });

    it('resultCount() equals total when no filter, filtered count otherwise', () => {
      expect(component.resultCount()).toBe(2);
      component.searchQuery.set('web');
      expect(component.resultCount()).toBe(1);
    });

    it('isFiltered() reflects search or tag filter', () => {
      expect(component.isFiltered()).toBe(false);
      component.searchQuery.set('x');
      expect(component.isFiltered()).toBe(true);
      component.searchQuery.set('');
      component.activeTagFilter.set('prod');
      expect(component.isFiltered()).toBe(true);
    });

    it('setTagFilter() toggles', () => {
      component.setTagFilter('prod');
      expect(component.activeTagFilter()).toBe('prod');
      component.setTagFilter('prod');
      expect(component.activeTagFilter()).toBe('');
    });

    it('clearFilters() resets both', () => {
      component.searchQuery.set('x');
      component.activeTagFilter.set('y');
      component.clearFilters();
      expect(component.searchQuery()).toBe('');
      expect(component.activeTagFilter()).toBe('');
    });

    it('settingsPlaceholder() picks protocol-specific example', () => {
      component.newConnection();
      component.patch('protocol', 'ssh');
      expect(component.settingsPlaceholder()).toContain('terminal-type');
      component.patch('protocol', 'vnc');
      expect(component.settingsPlaceholder()).toContain('encoding');
      component.patch('protocol', 'unknown');
      expect(component.settingsPlaceholder()).toContain('security');
    });
  });

  describe('save()', () => {
    beforeEach(() => {
      seedData();
      component.newConnection();
    });

    it('validates required fields and aborts', () => {
      component.save();
      expect(component.errorMessage()).toContain('required');
      expect(component.busy()).toBe(false);
      expect(component.mascotState()).toBe('error');
    });

    it('no-ops when already busy', () => {
      component.busy.set(true);
      component.save();
      expect(connections.create).not.toHaveBeenCalled();
    });

    it('creates a new connection with existing host', () => {
      component.form.set({
        id: null,
        name: 'new-conn',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: 'cred1',
        settings: '',
        newHostAddress: '',
        tags: [],
      });
      component.save();
      expect(connections.create).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'new-conn', hostId: 'h1' }),
      );
      expect(toast.success).toHaveBeenCalledWith('Connection created.');
      expect(component.mascotState()).toBe('success');
    });

    it('updates an existing connection', () => {
      component.form.set({
        id: 'c1',
        name: 'web-rdp',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: 'cred1',
        settings: '',
        newHostAddress: '',
        tags: ['prod'],
      });
      component.save();
      expect(connections.update).toHaveBeenCalledWith(
        'c1',
        expect.objectContaining({ name: 'web-rdp', tags: ['prod'] }),
      );
      expect(toast.success).toHaveBeenCalledWith('Connection updated.');
    });

    it('saves ssh with terminal appearance merged into settings', () => {
      let captured: CreateConnectionPayload | undefined;
      connections.create.mockImplementationOnce((p: CreateConnectionPayload) => {
        captured = p;
        return of({} as unknown as Connection);
      });

      component.form.set({
        id: null,
        name: 'ssh-1',
        protocol: 'ssh',
        hostId: 'h2',
        connectionGroupId: 'v1',
        credentialId: 'cred2',
        settings: '{"port":22}',
        newHostAddress: '',
        tags: [],
      });
      component.termColorScheme.set('solarized-dark');
      component.termFontName.set('Consolas');
      component.termFontSize.set(14);
      component.save();

      const settings = JSON.parse(captured?.settings ?? '{}');
      expect(settings['color-scheme']).toBe('solarized-dark');
      expect(settings['font-name']).toBe('Consolas');
      expect(settings['font-size']).toBe('14');
      expect(settings.port).toBe(22);
    });

    it('creates a new host when newHostAddress provided', () => {
      hosts.reload.mockReturnValueOnce(
        of([{ id: 'h-new', name: 'new Host', address: '10.0.0.99' }]),
      );

      component.form.set({
        id: null,
        name: 'new',
        protocol: 'rdp',
        hostId: '',
        connectionGroupId: 'v1',
        credentialId: '',
        settings: '',
        newHostAddress: '10.0.0.99',
        tags: [],
      });
      component.save();

      expect(hosts.create).toHaveBeenCalledWith({ name: 'new Host', address: '10.0.0.99' });
      expect(connections.create).toHaveBeenCalledWith(expect.objectContaining({ hostId: 'h-new' }));
    });

    it('errors when newHostAddress is provided but host cannot be located post-create', () => {
      hosts.reload.mockReturnValueOnce(of([]));

      component.form.set({
        id: null,
        name: 'new',
        protocol: 'rdp',
        hostId: '',
        connectionGroupId: 'v1',
        credentialId: '',
        settings: '',
        newHostAddress: '10.0.0.99',
        tags: [],
      });
      component.save();

      expect(component.mascotState()).toBe('error');
      expect(toast.error).toHaveBeenCalled();
      expect(component.errorMessage()).toContain('Failed to locate created host');
    });

    it('surfaces backend error message', () => {
      connections.create.mockReturnValueOnce(
        throwError(() => ({ error: { Message: 'duplicate name' } })),
      );
      component.form.set({
        id: null,
        name: 'dup',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: '',
        settings: '',
        newHostAddress: '',
        tags: [],
      });
      component.save();
      expect(component.errorMessage()).toBe('duplicate name');
      expect(component.mascotState()).toBe('error');
      expect(toast.error).toHaveBeenCalledWith('duplicate name');
    });

    it('falls back to generic message when error has none', () => {
      connections.create.mockReturnValueOnce(throwError(() => ({})));
      component.form.set({
        id: null,
        name: 'x',
        protocol: 'rdp',
        hostId: 'h1',
        connectionGroupId: 'v1',
        credentialId: '',
        settings: '',
        newHostAddress: '',
        tags: [],
      });
      component.save();
      expect(component.errorMessage()).toBe('Save failed.');
    });
  });

  describe('remove()', () => {
    beforeEach(() => seedData());

    const sampleConn: Connection = {
      id: 'c1',
      name: 'web-rdp',
      protocol: 'rdp',
      hostId: 'h1',
      connectionGroupId: 'v1',
      credentialId: null,
      settings: '{}',
      tags: [],
      host: null,
    };

    it('cancels when confirm returns false', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.remove(sampleConn);
      expect(connections.remove).not.toHaveBeenCalled();
    });

    it('deletes when confirmed and toasts', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.remove(sampleConn);
      expect(toast.success).toHaveBeenCalledWith('Connection "web-rdp" deleted.');
    });

    it('shows error toast and message on remove failure', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      connections.remove.mockReturnValueOnce(throwError(() => ({ message: 'in-use' })));
      await component.remove(sampleConn);
      expect(component.errorMessage()).toBe('in-use');
      expect(toast.error).toHaveBeenCalledWith('in-use');
    });
  });

  describe('helper methods', () => {
    beforeEach(() => seedData());

    it('trackBy methods return the id', () => {
      expect(
        component.trackById(0, {
          id: 'c1',
          name: '',
          protocol: 'rdp',
          hostId: '',
          connectionGroupId: '',
          credentialId: null,
          settings: '',
          tags: [],
          host: null,
        }),
      ).toBe('c1');
      expect(component.trackHost(0, { id: 'h1', name: '', address: '' })).toBe('h1');
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect(component.trackCred(0, { id: 'cred1' } as any)).toBe('cred1');
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      expect(component.trackVault(0, { id: 'v1' } as any)).toBe('v1');
    });

    it('name lookups return em-dash for unknown ids', () => {
      expect(component.hostName('h1')).toBe('web');
      expect(component.hostName('missing')).toBe('—');
      expect(component.credentialName(null)).toBe('—');
      expect(component.credentialName('missing')).toBe('—');
      expect(component.credentialName('cred1')).toBe('admin');
      expect(component.vaultName(null)).toBe('—');
      expect(component.vaultName('missing')).toBe('—');
      expect(component.vaultName('v1')).toBe('prod-vault');
    });
  });
});
