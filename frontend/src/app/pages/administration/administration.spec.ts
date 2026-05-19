import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { Administration } from './administration';
import { AppRole, AppUser, UsersService } from '../../services/users.service';
import { VaultsService } from '../../services/vaults.service';
import { AuthService } from '../../services/auth.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

describe('Administration', () => {
  let component: Administration;
  let fixture: ComponentFixture<Administration>;
  let users: {
    list: ReturnType<typeof signal<AppUser[]>>;
    reload: ReturnType<typeof vi.fn>;
    roleCatalog: ReturnType<typeof vi.fn>;
    invite: ReturnType<typeof vi.fn>;
    setRole: ReturnType<typeof vi.fn>;
    setActive: ReturnType<typeof vi.fn>;
    setVaultAssignments: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
  };
  let vaults: {
    list: ReturnType<typeof signal<unknown[]>>;
    reload: ReturnType<typeof vi.fn>;
  };
  let auth: { currentUser: ReturnType<typeof signal<{ id: string } | null>> };
  let confirm: { ask: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  const sampleUser: AppUser = {
    id: 'u1',
    name: 'Alice',
    email: 'alice@example.com',
    roles: ['User'],
    isActive: true,
    vaultIds: ['v1'],
  } as AppUser;

  beforeEach(async () => {
    users = {
      list: signal<AppUser[]>([]),
      reload: vi.fn(() => of([] as AppUser[])),
      roleCatalog: vi.fn(() => of([] as AppRole[])),
      invite: vi.fn(() => of({ message: 'invited', inviteUrl: 'https://x/i', emailSent: true })),
      setRole: vi.fn(() => of(undefined)),
      setActive: vi.fn(() => of(undefined)),
      setVaultAssignments: vi.fn(() => of(undefined)),
      remove: vi.fn(() => of(undefined)),
    };
    vaults = {
      list: signal<unknown[]>([]),
      reload: vi.fn(() => of([] as unknown[])),
    };
    auth = { currentUser: signal<{ id: string } | null>({ id: 'me' }) };
    confirm = { ask: vi.fn() };
    toast = { success: vi.fn(), error: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [Administration],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: UsersService, useValue: users },
        { provide: VaultsService, useValue: vaults },
        { provide: AuthService, useValue: auth },
        { provide: ConfirmDialogService, useValue: confirm },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Administration);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('refresh()', () => {
    it('loads users, roles, vaults', () => {
      users.reload.mockReturnValueOnce(of([sampleUser]));
      users.roleCatalog.mockReturnValueOnce(of([{ name: 'User' }] as AppRole[]));
      component.refresh();
      expect(users.reload).toHaveBeenCalled();
      expect(users.roleCatalog).toHaveBeenCalled();
      expect(vaults.reload).toHaveBeenCalled();
      expect(component.loading()).toBe(false);
      expect(component.roles().length).toBe(1);
    });

    it('sets errorMessage on failure', () => {
      users.reload.mockReturnValueOnce(throwError(() => ({ error: { Message: 'oh no' } })));
      component.refresh();
      expect(component.errorMessage()).toBe('oh no');
      expect(component.loading()).toBe(false);
    });

    it('falls back to default failure message', () => {
      users.reload.mockReturnValueOnce(throwError(() => ({})));
      component.refresh();
      expect(component.errorMessage()).toBe('Failed to load users.');
    });

    it('ngOnInit triggers refresh', () => {
      const spy = vi.spyOn(component, 'refresh');
      component.ngOnInit();
      expect(spy).toHaveBeenCalled();
    });
  });

  describe('invite flow', () => {
    it('toggleInvite() shows then hides, resetting fields', () => {
      component.inviteName.set('x');
      component.inviteEmail.set('x@y');
      component.inviteRole.set('Admin');
      component.toggleInvite();
      expect(component.showInvite()).toBe(true);
      component.toggleInvite();
      expect(component.showInvite()).toBe(false);
      expect(component.inviteName()).toBe('');
      expect(component.inviteEmail()).toBe('');
      expect(component.inviteRole()).toBe('User');
    });

    it('submitInvite() rejects when email missing', () => {
      component.inviteEmail.set('');
      component.submitInvite();
      expect(component.errorMessage()).toBe('Email is required.');
      expect(users.invite).not.toHaveBeenCalled();
    });

    it('submitInvite() succeeds and sets result', () => {
      component.inviteEmail.set('bob@x.com');
      component.inviteName.set('bob');
      component.submitInvite();
      expect(users.invite).toHaveBeenCalled();
      expect(component.inviteResult()?.inviteUrl).toBe('https://x/i');
      expect(component.showInvite()).toBe(false);
    });

    it('submitInvite() omits empty name', () => {
      component.inviteEmail.set('bob@x.com');
      component.inviteName.set('   ');
      component.submitInvite();
      expect(users.invite).toHaveBeenCalledWith({
        name: undefined,
        email: 'bob@x.com',
        role: 'User',
      });
    });

    it('submitInvite() no-ops when busy', () => {
      component.inviteBusy.set(true);
      component.inviteEmail.set('x@x');
      component.submitInvite();
      expect(users.invite).not.toHaveBeenCalled();
    });

    it('submitInvite() sets errorMessage on failure', () => {
      users.invite.mockReturnValueOnce(throwError(() => ({ message: 'taken' })));
      component.inviteEmail.set('bob@x.com');
      component.submitInvite();
      expect(component.errorMessage()).toBe('taken');
    });

    it('submitInvite() falls back to default error message', () => {
      users.invite.mockReturnValueOnce(throwError(() => ({})));
      component.inviteEmail.set('bob@x.com');
      component.submitInvite();
      expect(component.errorMessage()).toBe('Invite failed.');
    });

    it('invitableRoles() filters out Owner', () => {
      component.roles.set([
        { name: 'Owner' } as AppRole,
        { name: 'TeamAdmin' } as AppRole,
        { name: 'User' } as AppRole,
      ]);
      expect(component.invitableRoles().map((r) => r.name)).toEqual(['TeamAdmin', 'User']);
    });

    it('dismissInviteResult clears state', () => {
      component.inviteResult.set({
        message: 'ok',
        inviteUrl: 'https://x',
        emailSent: false,
      });
      component.copyState.set('copied');
      component.dismissInviteResult();
      expect(component.inviteResult()).toBeNull();
      expect(component.copyState()).toBe('idle');
    });
  });

  describe('changeRole()', () => {
    it('no-ops on empty role', async () => {
      await component.changeRole(sampleUser, '');
      expect(confirm.ask).not.toHaveBeenCalled();
    });

    it('no-ops on same role', async () => {
      await component.changeRole(sampleUser, 'User');
      expect(confirm.ask).not.toHaveBeenCalled();
    });

    it('no-ops when same user already busy', async () => {
      component.roleBusyUserId.set(sampleUser.id);
      await component.changeRole(sampleUser, 'TeamAdmin');
      expect(confirm.ask).not.toHaveBeenCalled();
    });

    it('confirms and updates role', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.changeRole(sampleUser, 'TeamAdmin');
      expect(users.setRole).toHaveBeenCalledWith('u1', 'TeamAdmin');
      expect(toast.success).toHaveBeenCalled();
      expect(component.roleBusyUserId()).toBeNull();
    });

    it('uses danger tone for Owner moves', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.changeRole(sampleUser, 'Owner');
      expect(confirm.ask).toHaveBeenCalledWith(expect.objectContaining({ tone: 'danger' }));
    });

    it('refreshes without saving when user cancels', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.changeRole(sampleUser, 'TeamAdmin');
      expect(users.setRole).not.toHaveBeenCalled();
    });

    it('surfaces error from setRole', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      users.setRole.mockReturnValueOnce(throwError(() => ({ message: 'denied' })));
      // Stub refresh so its errorMessage.set(null) doesn't clear our assertion.
      vi.spyOn(component, 'refresh').mockImplementation(() => undefined);
      await component.changeRole(sampleUser, 'TeamAdmin');
      expect(component.errorMessage()).toBe('denied');
      expect(toast.error).toHaveBeenCalled();
    });

    it('falls back to default setRole error message', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      users.setRole.mockReturnValueOnce(throwError(() => ({})));
      vi.spyOn(component, 'refresh').mockImplementation(() => undefined);
      await component.changeRole(sampleUser, 'TeamAdmin');
      expect(component.errorMessage()).toBe('Failed to update role.');
    });
  });

  describe('vault assignment modal', () => {
    it('openVaultAssignments sets modal user and ids', () => {
      component.openVaultAssignments(sampleUser);
      expect(component.vaultModalUser()).toBe(sampleUser);
      expect(component.selectedVaultIds()).toEqual(['v1']);
    });

    it('openVaultAssignments handles user with no vaultIds', () => {
      const u: AppUser = { ...sampleUser, vaultIds: undefined as unknown as string[] };
      component.openVaultAssignments(u);
      expect(component.selectedVaultIds()).toEqual([]);
    });

    it('closeVaultAssignments clears state', () => {
      component.openVaultAssignments(sampleUser);
      component.closeVaultAssignments();
      expect(component.vaultModalUser()).toBeNull();
      expect(component.selectedVaultIds()).toEqual([]);
    });

    it('toggleVaultSelection adds and removes', () => {
      component.selectedVaultIds.set(['v1']);
      component.toggleVaultSelection('v2', true);
      expect(component.selectedVaultIds()).toContain('v2');
      component.toggleVaultSelection('v1', false);
      expect(component.selectedVaultIds()).not.toContain('v1');
    });

    it('saveVaultAssignments saves and closes modal', () => {
      component.openVaultAssignments(sampleUser);
      component.selectedVaultIds.set(['v1', 'v2']);
      component.saveVaultAssignments();
      expect(users.setVaultAssignments).toHaveBeenCalledWith('u1', ['v1', 'v2']);
      expect(component.vaultModalUser()).toBeNull();
      expect(toast.success).toHaveBeenCalled();
    });

    it('saveVaultAssignments no-ops with no modal user', () => {
      component.saveVaultAssignments();
      expect(users.setVaultAssignments).not.toHaveBeenCalled();
    });

    it('saveVaultAssignments no-ops when busy', () => {
      component.openVaultAssignments(sampleUser);
      component.vaultAssignmentBusy.set(true);
      component.saveVaultAssignments();
      expect(users.setVaultAssignments).not.toHaveBeenCalled();
    });

    it('saveVaultAssignments surfaces error', () => {
      component.openVaultAssignments(sampleUser);
      users.setVaultAssignments.mockReturnValueOnce(throwError(() => ({ message: 'fail' })));
      component.saveVaultAssignments();
      expect(component.errorMessage()).toBe('fail');
      expect(toast.error).toHaveBeenCalled();
    });

    it('saveVaultAssignments falls back to default error', () => {
      component.openVaultAssignments(sampleUser);
      users.setVaultAssignments.mockReturnValueOnce(throwError(() => ({})));
      component.saveVaultAssignments();
      expect(component.errorMessage()).toBe('Failed to update vault assignments.');
    });

    it('canAssignVaults returns true for TeamAdmin/User and false for Owner', () => {
      expect(component.canAssignVaults({ ...sampleUser, roles: ['Owner'] })).toBe(false);
      expect(component.canAssignVaults({ ...sampleUser, roles: ['TeamAdmin'] })).toBe(true);
      expect(component.canAssignVaults({ ...sampleUser, roles: ['User'] })).toBe(true);
    });

    it('isVaultSelected reflects selectedVaultIds', () => {
      component.selectedVaultIds.set(['v1']);
      expect(component.isVaultSelected('v1')).toBe(true);
      expect(component.isVaultSelected('v2')).toBe(false);
    });
  });

  describe('search and computed', () => {
    it('primaryRole returns first role or User fallback', () => {
      expect(component.primaryRole(sampleUser)).toBe('User');
      expect(component.primaryRole({ ...sampleUser, roles: [] })).toBe('User');
    });

    it('filteredUsers returns all when no query', () => {
      users.list.set([sampleUser]);
      expect(component.filteredUsers().length).toBe(1);
    });

    it('filteredUsers filters by name, email, role', () => {
      users.list.set([
        sampleUser,
        { ...sampleUser, id: 'u2', name: 'Bob', email: 'bob@x', roles: ['TeamAdmin'] },
      ]);
      component.searchQuery.set('bob');
      expect(component.filteredUsers().length).toBe(1);
      component.searchQuery.set('teamadmin');
      expect(component.filteredUsers().length).toBe(1);
    });
  });

  describe('copyInviteUrl()', () => {
    it('no-ops without inviteResult', () => {
      const spy = vi.fn(() => Promise.resolve());
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText: spy },
      });
      component.copyInviteUrl();
      expect(spy).not.toHaveBeenCalled();
    });

    it('sets state to "copied" on success', async () => {
      vi.useFakeTimers();
      const spy = vi.fn(() => Promise.resolve());
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText: spy },
      });
      component.inviteResult.set({
        message: 'ok',
        inviteUrl: 'https://example.com',
        emailSent: false,
      });
      component.copyInviteUrl();
      // Wait for the microtask in .then
      await Promise.resolve();
      await Promise.resolve();
      expect(spy).toHaveBeenCalledWith('https://example.com');
      expect(component.copyState()).toBe('copied');
      vi.advanceTimersByTime(2100);
      expect(component.copyState()).toBe('idle');
    });

    it('sets state to "failed" on rejection', async () => {
      const spy = vi.fn(() => Promise.reject(new Error('x')));
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText: spy },
      });
      component.inviteResult.set({
        message: 'ok',
        inviteUrl: 'https://example.com',
        emailSent: false,
      });
      component.copyInviteUrl();
      await Promise.resolve();
      await Promise.resolve();
      expect(component.copyState()).toBe('failed');
    });
  });

  describe('setActive() and delete()', () => {
    it('setActive succeeds and toasts', () => {
      component.setActive(sampleUser, false);
      expect(users.setActive).toHaveBeenCalledWith('u1', false);
      expect(toast.success).toHaveBeenCalled();
    });

    it('setActive surfaces error', () => {
      users.setActive.mockReturnValueOnce(throwError(() => ({ message: 'no' })));
      component.setActive(sampleUser, true);
      expect(component.errorMessage()).toBe('no');
      expect(toast.error).toHaveBeenCalled();
    });

    it('setActive falls back to default error', () => {
      users.setActive.mockReturnValueOnce(throwError(() => ({})));
      component.setActive(sampleUser, true);
      expect(component.errorMessage()).toBe('Update failed.');
    });

    it('delete prevents self-delete', async () => {
      auth.currentUser.set({ id: 'u1' });
      await component.delete(sampleUser);
      expect(component.errorMessage()).toContain('your own account');
      expect(confirm.ask).not.toHaveBeenCalled();
    });

    it('delete cancels when confirm returns false', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.delete(sampleUser);
      expect(users.remove).not.toHaveBeenCalled();
    });

    it('delete removes when confirmed', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.delete(sampleUser);
      expect(users.remove).toHaveBeenCalledWith('u1');
      expect(toast.success).toHaveBeenCalled();
    });

    it('delete surfaces error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      users.remove.mockReturnValueOnce(throwError(() => ({ message: 'in-use' })));
      await component.delete(sampleUser);
      expect(component.errorMessage()).toBe('in-use');
      expect(toast.error).toHaveBeenCalled();
    });

    it('delete falls back to default error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      users.remove.mockReturnValueOnce(throwError(() => ({})));
      await component.delete(sampleUser);
      expect(component.errorMessage()).toBe('Delete failed.');
    });
  });

  describe('trackBy helpers', () => {
    it('returns id/name/id', () => {
      expect(component.trackById(0, sampleUser)).toBe('u1');
      expect(component.trackRole(0, { name: 'r' } as AppRole)).toBe('r');
      expect(component.trackVault(0, { id: 'v1' })).toBe('v1');
    });
  });
});
