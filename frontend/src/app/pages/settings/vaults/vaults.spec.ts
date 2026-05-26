import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { SettingsVaults } from './vaults';
import { Vault, VaultsService } from '../../../services/vaults.service';
import { AppUser, UsersService } from '../../../services/users.service';
import { UserGroup, GroupsService } from '../../../services/groups.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../../shared/toast/toast.service';

describe('SettingsVaults', () => {
  let component: SettingsVaults;
  let fixture: ComponentFixture<SettingsVaults>;
  let vaultsSvc: {
    list: ReturnType<typeof signal<Vault[]>>;
    reload: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
    getAssignments: ReturnType<typeof vi.fn>;
    setAssignments: ReturnType<typeof vi.fn>;
  };
  let usersSvc: { list: ReturnType<typeof signal<AppUser[]>>; reload: ReturnType<typeof vi.fn> };
  let groupsSvc: { list: ReturnType<typeof signal<UserGroup[]>>; reload: ReturnType<typeof vi.fn> };
  let confirm: { ask: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  const vault: Vault = { id: 'v1', name: 'prod' } as Vault;

  beforeEach(async () => {
    vaultsSvc = {
      list: signal<Vault[]>([]),
      reload: vi.fn(() => of([])),
      create: vi.fn(() => of(undefined)),
      update: vi.fn(() => of(undefined)),
      remove: vi.fn(() => of(undefined)),
      getAssignments: vi.fn(() => of({ userIds: ['u1'], groupIds: ['g1'] })),
      setAssignments: vi.fn(() => of(undefined)),
    };
    usersSvc = {
      list: signal<AppUser[]>([
        { id: 'u1', name: 'Alice', email: 'alice@x' } as AppUser,
        { id: 'u2', name: 'Bob', email: 'bob@y' } as AppUser,
      ]),
      reload: vi.fn(() => of([])),
    };
    groupsSvc = {
      list: signal<UserGroup[]>([{ id: 'g1', name: 'team-a' } as UserGroup]),
      reload: vi.fn(() => of([])),
    };
    confirm = { ask: vi.fn() };
    toast = { success: vi.fn(), error: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SettingsVaults],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: VaultsService, useValue: vaultsSvc },
        { provide: UsersService, useValue: usersSvc },
        { provide: GroupsService, useValue: groupsSvc },
        { provide: ConfirmDialogService, useValue: confirm },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(SettingsVaults);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit calls refresh', () => {
    const spy = vi.spyOn(component, 'refresh');
    component.ngOnInit();
    expect(spy).toHaveBeenCalled();
  });

  it('refresh failure sets errorMessage', () => {
    vaultsSvc.reload.mockReturnValueOnce(throwError(() => ({ message: 'fail' })));
    component.refresh();
    expect(component.errorMessage()).toBe('fail');
  });

  it('refresh falls back to default error', () => {
    vaultsSvc.reload.mockReturnValueOnce(throwError(() => ({})));
    component.refresh();
    expect(component.errorMessage()).toBe('Failed to load vaults.');
  });

  describe('createVault', () => {
    it('no-ops when busy', () => {
      component.vaultBusy.set(true);
      component.newVaultName.set('x');
      component.createVault();
      expect(vaultsSvc.create).not.toHaveBeenCalled();
    });

    it('validates name', () => {
      component.newVaultName.set('  ');
      component.createVault();
      expect(component.errorMessage()).toContain('required');
    });

    it('creates and toasts', () => {
      component.newVaultName.set('prod');
      component.createVault();
      expect(vaultsSvc.create).toHaveBeenCalledWith({ name: 'prod' });
      expect(toast.success).toHaveBeenCalled();
    });

    it('surfaces error', () => {
      vaultsSvc.create.mockReturnValueOnce(throwError(() => ({ message: 'dup' })));
      component.newVaultName.set('x');
      component.createVault();
      expect(component.errorMessage()).toBe('dup');
    });

    it('falls back to default error', () => {
      vaultsSvc.create.mockReturnValueOnce(throwError(() => ({})));
      component.newVaultName.set('x');
      component.createVault();
      expect(component.errorMessage()).toBe('Failed to create vault.');
    });
  });

  describe('edit lifecycle', () => {
    it('startEdit/cancelEdit toggle state', () => {
      component.startEdit(vault);
      expect(component.editingId()).toBe('v1');
      component.cancelEdit();
      expect(component.editingId()).toBeNull();
    });

    it('saveEdit no-ops on empty name', () => {
      component.editingName.set('  ');
      component.saveEdit(vault);
      expect(vaultsSvc.update).not.toHaveBeenCalled();
    });

    it('saveEdit renames', () => {
      component.editingName.set('renamed');
      component.saveEdit(vault);
      expect(vaultsSvc.update).toHaveBeenCalledWith(
        'v1',
        expect.objectContaining({ name: 'renamed' }),
      );
      expect(toast.success).toHaveBeenCalled();
    });

    it('saveEdit surfaces error', () => {
      vaultsSvc.update.mockReturnValueOnce(throwError(() => ({ message: 'denied' })));
      component.editingName.set('x');
      component.saveEdit(vault);
      expect(component.errorMessage()).toBe('denied');
    });

    it('saveEdit falls back to default error', () => {
      vaultsSvc.update.mockReturnValueOnce(throwError(() => ({})));
      component.editingName.set('x');
      component.saveEdit(vault);
      expect(component.errorMessage()).toBe('Failed to rename vault.');
    });
  });

  describe('deleteVault', () => {
    it('cancels when confirm denies', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.deleteVault(vault);
      expect(vaultsSvc.remove).not.toHaveBeenCalled();
    });

    it('removes when confirmed', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.deleteVault(vault);
      expect(vaultsSvc.remove).toHaveBeenCalledWith('v1');
    });

    it('surfaces error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      vaultsSvc.remove.mockReturnValueOnce(throwError(() => ({ message: 'in-use' })));
      await component.deleteVault(vault);
      expect(component.errorMessage()).toBe('in-use');
    });

    it('falls back to default error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      vaultsSvc.remove.mockReturnValueOnce(throwError(() => ({})));
      await component.deleteVault(vault);
      expect(component.errorMessage()).toBe('Failed to delete vault.');
    });
  });

  describe('assignment modal', () => {
    it('openAssignments seeds ids from API', () => {
      component.openAssignments(vault);
      expect(component.assignModalVault()).toBe(vault);
      expect(component.selectedUserIds()).toEqual(['u1']);
      expect(component.selectedGroupIds()).toEqual(['g1']);
    });

    it('openAssignments surfaces error on load failure', () => {
      vaultsSvc.getAssignments.mockReturnValueOnce(throwError(() => ({ message: 'down' })));
      component.openAssignments(vault);
      expect(component.errorMessage()).toBe('down');
    });

    it('closeAssignments resets state', () => {
      component.openAssignments(vault);
      component.closeAssignments();
      expect(component.assignModalVault()).toBeNull();
      expect(component.selectedUserIds()).toEqual([]);
    });

    it('toggleUser / toggleGroup add and remove', () => {
      component.toggleUser('u2', true);
      expect(component.isUserSelected('u2')).toBe(true);
      component.toggleUser('u2', false);
      expect(component.isUserSelected('u2')).toBe(false);
      component.toggleGroup('g1', true);
      expect(component.isGroupSelected('g1')).toBe(true);
      component.toggleGroup('g1', false);
      expect(component.isGroupSelected('g1')).toBe(false);
    });

    it('saveAssignments no-ops without modal', () => {
      component.saveAssignments();
      expect(vaultsSvc.setAssignments).not.toHaveBeenCalled();
    });

    it('saveAssignments no-ops when busy', () => {
      component.openAssignments(vault);
      component.assignBusy.set(true);
      component.saveAssignments();
      expect(vaultsSvc.setAssignments).not.toHaveBeenCalled();
    });

    it('saveAssignments persists and closes', () => {
      component.openAssignments(vault);
      component.selectedUserIds.set(['u1', 'u2']);
      component.selectedGroupIds.set(['g1']);
      component.saveAssignments();
      expect(vaultsSvc.setAssignments).toHaveBeenCalledWith('v1', {
        userIds: ['u1', 'u2'],
        groupIds: ['g1'],
      });
      expect(component.assignModalVault()).toBeNull();
    });

    it('saveAssignments surfaces error', () => {
      component.openAssignments(vault);
      vaultsSvc.setAssignments.mockReturnValueOnce(throwError(() => ({ message: 'no' })));
      component.saveAssignments();
      expect(component.errorMessage()).toBe('no');
    });

    it('saveAssignments falls back to default error', () => {
      component.openAssignments(vault);
      vaultsSvc.setAssignments.mockReturnValueOnce(throwError(() => ({})));
      component.saveAssignments();
      expect(component.errorMessage()).toBe('Failed to save assignments.');
    });
  });

  describe('filters', () => {
    it('filteredAssignUsers searches name/email', () => {
      component.userAssignSearch.set('alice');
      expect(component.filteredAssignUsers().map((u) => u.id)).toEqual(['u1']);
      component.userAssignSearch.set('bob@');
      expect(component.filteredAssignUsers().map((u) => u.id)).toEqual(['u2']);
    });

    it('filteredAssignGroups searches by name', () => {
      component.groupAssignSearch.set('team');
      expect(component.filteredAssignGroups().map((g) => g.id)).toEqual(['g1']);
    });

    it('filteredSelectedAssignUsers returns selected users matching search', () => {
      component.selectedUserIds.set(['u1']);
      expect(component.filteredSelectedAssignUsers().map((u) => u.id)).toEqual(['u1']);
      component.userAssignSearch.set('bob');
      expect(component.filteredSelectedAssignUsers().map((u) => u.id)).toEqual([]);
    });

    it('filteredUnselectedAssignUsers returns non-selected users matching search', () => {
      component.selectedUserIds.set(['u1']);
      expect(component.filteredUnselectedAssignUsers().map((u) => u.id)).toEqual(['u2']);
      component.userAssignSearch.set('alice');
      expect(component.filteredUnselectedAssignUsers().map((u) => u.id)).toEqual([]);
    });

    it('filteredSelectedAssignGroups returns selected groups matching search', () => {
      component.selectedGroupIds.set(['g1']);
      expect(component.filteredSelectedAssignGroups().map((g) => g.id)).toEqual(['g1']);
      component.groupAssignSearch.set('xyz');
      expect(component.filteredSelectedAssignGroups().map((g) => g.id)).toEqual([]);
    });

    it('filteredUnselectedAssignGroups returns non-selected groups matching search', () => {
      component.selectedGroupIds.set([]);
      expect(component.filteredUnselectedAssignGroups().map((g) => g.id)).toEqual(['g1']);
      component.selectedGroupIds.set(['g1']);
      expect(component.filteredUnselectedAssignGroups().map((g) => g.id)).toEqual([]);
    });

    it('trackBy helpers return ids', () => {
      expect(component.trackVault(0, vault)).toBe('v1');
      expect(component.trackUser(0, { id: 'u1' } as AppUser)).toBe('u1');
      expect(component.trackGroup(0, { id: 'g1' } as UserGroup)).toBe('g1');
    });
  });
});
