import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { Hosts } from './hosts';
import { HostsService, AppHost } from '../../services/hosts.service';
import { AuthService } from '../../services/auth.service';
import { OnboardingTourService } from '../../services/onboarding-tour.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

describe('Hosts', () => {
  let component: Hosts;
  let fixture: ComponentFixture<Hosts>;
  let svc: {
    list: ReturnType<typeof signal<AppHost[]>>;
    reload: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
  };
  let auth: {
    canManageConnections: ReturnType<typeof signal<boolean>>;
    currentUser: ReturnType<typeof signal<{ id: string } | null>>;
  };
  let tour: OnboardingTourService;
  let confirm: { ask: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    svc = {
      list: signal<AppHost[]>([]),
      reload: vi.fn(() => of([] as AppHost[])),
      create: vi.fn(() => of({ id: 'h-new', name: 'n', address: 'a' })),
      update: vi.fn(() => of(undefined)),
      remove: vi.fn(() => of(undefined)),
    };
    auth = { canManageConnections: signal(true), currentUser: signal(null) };
    confirm = { ask: vi.fn() };
    toast = { success: vi.fn(), error: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [Hosts],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: HostsService, useValue: svc },
        { provide: AuthService, useValue: auth },
        { provide: ConfirmDialogService, useValue: confirm },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      common: {
        actions: {
          delete: 'Delete',
        },
      },
      pages: {
        hosts: {
          loadFailed: 'Failed to load.',
          nameAddressRequired: 'Name and address are required.',
          hostSaved: 'Host saved.',
          hostUpdated: 'Host updated.',
          saveFailed: 'Save failed.',
          deleteFailed: 'Delete failed.',
          hostDeleted: 'Host "{{name}}" deleted.',
          deleteHostTitle: 'Delete host',
          deleteHostMessage:
            'Delete host "{{name}}"? Connections using this host will break. This cannot be undone.',
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(Hosts);
    component = fixture.componentInstance;
    tour = TestBed.inject(OnboardingTourService);
  });

  const h1: AppHost = { id: 'h1', name: 'web', address: '10.0.0.1' };
  const h2: AppHost = { id: 'h2', name: 'db', address: '10.0.0.2' };

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('refresh()', () => {
    it('ngOnInit calls refresh', () => {
      const spy = vi.spyOn(component, 'refresh');
      component.ngOnInit();
      expect(spy).toHaveBeenCalled();
    });

    it('clears loading and error on success', () => {
      component.refresh();
      expect(component.loading()).toBe(false);
      expect(component.errorMessage()).toBeNull();
    });

    it('sets errorMessage on failure', () => {
      svc.reload.mockReturnValueOnce(throwError(() => ({ error: { Message: 'oops' } })));
      component.refresh();
      expect(component.errorMessage()).toBe('oops');
      expect(component.loading()).toBe(false);
    });

    it('falls back to default error message', () => {
      svc.reload.mockReturnValueOnce(throwError(() => ({})));
      component.refresh();
      expect(component.errorMessage()).toBe('Failed to load.');
    });
  });

  describe('drawer state', () => {
    it('newHost() opens empty form', () => {
      component.newHost();
      expect(component.showDrawer()).toBe(true);
      expect(component.form().id).toBeNull();
    });

    it('newHost() no-ops when user lacks permission', () => {
      auth.canManageConnections.set(false);
      component.newHost();
      expect(component.showDrawer()).toBe(false);
    });

    it('edit() loads host into form', () => {
      component.edit(h1);
      expect(component.form()).toEqual({ id: 'h1', name: 'web', address: '10.0.0.1' });
      expect(component.showDrawer()).toBe(true);
    });

    it('edit() no-ops without permission', () => {
      auth.canManageConnections.set(false);
      component.edit(h1);
      expect(component.showDrawer()).toBe(false);
    });

    it('closeDrawer() resets', () => {
      component.edit(h1);
      component.closeDrawer();
      expect(component.showDrawer()).toBe(false);
      expect(component.form().id).toBeNull();
    });

    it('patch() updates a field', () => {
      component.patch('name', 'foo');
      expect(component.form().name).toBe('foo');
    });
  });

  describe('save()', () => {
    it('no-ops without permission', () => {
      auth.canManageConnections.set(false);
      component.save();
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('no-ops when busy', () => {
      component.busy.set(true);
      component.save();
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('validates name and address', () => {
      component.form.set({ id: null, name: '   ', address: '' });
      component.save();
      expect(component.errorMessage()).toContain('Name and address');
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('creates a new host', () => {
      const completeStep = vi.spyOn(tour, 'completeStep');
      component.form.set({ id: null, name: 'web', address: '10.0.0.1' });
      component.save();
      expect(svc.create).toHaveBeenCalledWith({ name: 'web', address: '10.0.0.1' });
      expect(toast.success).toHaveBeenCalledWith('Host saved.');
      expect(completeStep).toHaveBeenCalledWith('host');
    });

    it('updates an existing host', () => {
      const completeStep = vi.spyOn(tour, 'completeStep');
      component.form.set({ id: 'h1', name: 'web', address: '10.0.0.1' });
      component.save();
      expect(svc.update).toHaveBeenCalledWith('h1', { name: 'web', address: '10.0.0.1' });
      expect(toast.success).toHaveBeenCalledWith('Host updated.');
      expect(completeStep).not.toHaveBeenCalled();
    });

    it('surfaces error on create failure', () => {
      svc.create.mockReturnValueOnce(throwError(() => ({ message: 'dup' })));
      component.form.set({ id: null, name: 'web', address: '10.0.0.1' });
      component.save();
      expect(component.errorMessage()).toBe('dup');
      expect(toast.error).toHaveBeenCalledWith('dup');
    });

    it('falls back to default error message', () => {
      svc.create.mockReturnValueOnce(throwError(() => ({})));
      component.form.set({ id: null, name: 'web', address: '10.0.0.1' });
      component.save();
      expect(component.errorMessage()).toBe('Save failed.');
    });

    it('surfaces error on update failure', () => {
      svc.update.mockReturnValueOnce(throwError(() => ({ error: { message: 'in-use' } })));
      component.form.set({ id: 'h1', name: 'web', address: '10.0.0.1' });
      component.save();
      expect(component.errorMessage()).toBe('in-use');
    });
  });

  describe('remove()', () => {
    it('no-ops without permission', async () => {
      auth.canManageConnections.set(false);
      await component.remove(h1);
      expect(confirm.ask).not.toHaveBeenCalled();
    });

    it('cancels when confirm returns false', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.remove(h1);
      expect(svc.remove).not.toHaveBeenCalled();
    });

    it('deletes when confirmed', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.remove(h1);
      expect(svc.remove).toHaveBeenCalledWith('h1');
      expect(toast.success).toHaveBeenCalledWith('Host "web" deleted.');
    });

    it('surfaces error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      svc.remove.mockReturnValueOnce(throwError(() => ({ message: 'in-use' })));
      await component.remove(h1);
      expect(component.errorMessage()).toBe('in-use');
      expect(toast.error).toHaveBeenCalled();
    });

    it('falls back to default error message', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      svc.remove.mockReturnValueOnce(throwError(() => ({})));
      await component.remove(h1);
      expect(component.errorMessage()).toBe('Delete failed.');
    });
  });

  describe('filters and computed', () => {
    beforeEach(() => svc.list.set([h1, h2]));

    it('filteredHosts returns all when no query', () => {
      expect(component.filteredHosts().length).toBe(2);
    });

    it('filteredHosts filters by name', () => {
      component.searchQuery.set('web');
      expect(component.filteredHosts().map((h) => h.id)).toEqual(['h1']);
    });

    it('filteredHosts filters by address', () => {
      component.searchQuery.set('10.0.0.2');
      expect(component.filteredHosts().map((h) => h.id)).toEqual(['h2']);
    });

    it('isFiltered reflects search', () => {
      expect(component.isFiltered()).toBe(false);
      component.searchQuery.set('x');
      expect(component.isFiltered()).toBe(true);
    });

    it('trackById returns id', () => {
      expect(component.trackById(0, h1)).toBe('h1');
    });
  });
});
