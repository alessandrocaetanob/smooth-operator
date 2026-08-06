import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { SettingsGroups } from './groups';
import { UserGroup, GroupsService } from '../../../services/groups.service';
import { AppUser, UsersService } from '../../../services/users.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../../shared/toast/toast.service';

describe('SettingsGroups', () => {
  let component: SettingsGroups;
  let fixture: ComponentFixture<SettingsGroups>;
  let groupsSvc: {
    list: ReturnType<typeof signal<UserGroup[]>>;
    reload: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    rename: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
    setMembers: ReturnType<typeof vi.fn>;
  };
  let usersSvc: {
    list: ReturnType<typeof signal<AppUser[]>>;
    reload: ReturnType<typeof vi.fn>;
  };
  let confirm: { ask: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  const group: UserGroup = {
    id: 'g1',
    name: 'team-alpha',
    members: [{ id: 'u1', name: 'Alice', email: 'a@x' }],
  } as UserGroup;

  beforeEach(async () => {
    groupsSvc = {
      list: signal<UserGroup[]>([]),
      reload: vi.fn(() => of([])),
      create: vi.fn(() => of(undefined)),
      rename: vi.fn(() => of(undefined)),
      remove: vi.fn(() => of(undefined)),
      setMembers: vi.fn(() => of(undefined)),
    };
    usersSvc = {
      list: signal<AppUser[]>([]),
      reload: vi.fn(() => of([])),
    };
    confirm = { ask: vi.fn() };
    toast = { success: vi.fn(), error: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SettingsGroups],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: GroupsService, useValue: groupsSvc },
        { provide: UsersService, useValue: usersSvc },
        { provide: ConfirmDialogService, useValue: confirm },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        settingsGroups: {
          errors: {
            loadFailed: 'Failed to load groups.',
            nameRequired: 'Group name is required.',
            createFailed: 'Failed to create group.',
            renameFailed: 'Failed to rename group.',
            deleteFailed: 'Failed to delete group.',
            membersUpdateFailed: 'Failed to update members.',
          },
          toasts: {
            created: 'Group "{{name}}" created.',
            renamed: 'Group renamed to "{{name}}".',
            deleted: 'Group "{{name}}" deleted.',
            membersUpdated: 'Members updated for "{{name}}".',
          },
          confirmDelete: {
            title: 'Delete group',
            message:
              'Delete group "{{name}}"? Users in this group will lose vault access granted via the group. This cannot be undone.',
          },
        },
      },
      common: {
        actions: {
          delete: 'Delete',
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(SettingsGroups);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit triggers refresh', () => {
    const spy = vi.spyOn(component, 'refresh');
    component.ngOnInit();
    expect(spy).toHaveBeenCalled();
  });

  it('refresh failure sets errorMessage', () => {
    groupsSvc.reload.mockReturnValueOnce(throwError(() => ({ message: 'down' })));
    component.refresh();
    expect(component.errorMessage()).toBe('down');
  });

  it('refresh falls back to default error', () => {
    groupsSvc.reload.mockReturnValueOnce(throwError(() => ({})));
    component.refresh();
    expect(component.errorMessage()).toBe('Failed to load groups.');
  });

  describe('createGroup', () => {
    it('no-ops when busy', () => {
      component.groupBusy.set(true);
      component.newGroupName.set('x');
      component.createGroup();
      expect(groupsSvc.create).not.toHaveBeenCalled();
    });

    it('validates name', () => {
      component.newGroupName.set('   ');
      component.createGroup();
      expect(component.errorMessage()).toContain('required');
    });

    it('creates and toasts on success', () => {
      component.newGroupName.set('team');
      component.createGroup();
      expect(groupsSvc.create).toHaveBeenCalledWith({ name: 'team' });
      expect(toast.success).toHaveBeenCalled();
      expect(component.newGroupName()).toBe('');
    });

    it('surfaces error', () => {
      groupsSvc.create.mockReturnValueOnce(throwError(() => ({ message: 'dup' })));
      component.newGroupName.set('team');
      component.createGroup();
      expect(component.errorMessage()).toBe('dup');
      expect(toast.error).toHaveBeenCalled();
    });

    it('falls back to default error', () => {
      groupsSvc.create.mockReturnValueOnce(throwError(() => ({})));
      component.newGroupName.set('team');
      component.createGroup();
      expect(component.errorMessage()).toBe('Failed to create group.');
    });
  });

  describe('edit lifecycle', () => {
    it('startEdit / cancelEdit toggle state', () => {
      component.startEdit(group);
      expect(component.editingId()).toBe('g1');
      expect(component.editingName()).toBe('team-alpha');
      component.cancelEdit();
      expect(component.editingId()).toBeNull();
      expect(component.editingName()).toBe('');
    });

    it('saveEdit no-ops on empty name', () => {
      component.editingName.set('  ');
      component.saveEdit(group);
      expect(groupsSvc.rename).not.toHaveBeenCalled();
    });

    it('saveEdit renames and toasts', () => {
      component.startEdit(group);
      component.editingName.set('renamed');
      component.saveEdit(group);
      expect(groupsSvc.rename).toHaveBeenCalledWith('g1', 'renamed');
      expect(toast.success).toHaveBeenCalled();
      expect(component.editingId()).toBeNull();
    });

    it('saveEdit surfaces error', () => {
      groupsSvc.rename.mockReturnValueOnce(throwError(() => ({ message: 'dup' })));
      component.editingName.set('x');
      component.saveEdit(group);
      expect(component.errorMessage()).toBe('dup');
    });

    it('saveEdit falls back to default error', () => {
      groupsSvc.rename.mockReturnValueOnce(throwError(() => ({})));
      component.editingName.set('x');
      component.saveEdit(group);
      expect(component.errorMessage()).toBe('Failed to rename group.');
    });
  });

  describe('deleteGroup', () => {
    it('cancels when confirm denies', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.deleteGroup(group);
      expect(groupsSvc.remove).not.toHaveBeenCalled();
    });

    it('removes when confirmed', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      await component.deleteGroup(group);
      expect(groupsSvc.remove).toHaveBeenCalledWith('g1');
      expect(toast.success).toHaveBeenCalled();
    });

    it('surfaces error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      groupsSvc.remove.mockReturnValueOnce(throwError(() => ({ message: 'in-use' })));
      await component.deleteGroup(group);
      expect(component.errorMessage()).toBe('in-use');
    });

    it('falls back to default error', async () => {
      confirm.ask.mockResolvedValueOnce(true);
      groupsSvc.remove.mockReturnValueOnce(throwError(() => ({})));
      await component.deleteGroup(group);
      expect(component.errorMessage()).toBe('Failed to delete group.');
    });
  });

  describe('members modal', () => {
    it('openMembers seeds selectedMemberIds', () => {
      component.openMembers(group);
      expect(component.membersModalGroup()).toBe(group);
      expect(component.selectedMemberIds()).toEqual(['u1']);
    });

    it('closeMembers clears state', () => {
      component.openMembers(group);
      component.closeMembers();
      expect(component.membersModalGroup()).toBeNull();
      expect(component.selectedMemberIds()).toEqual([]);
    });

    it('toggleMember adds and removes', () => {
      component.toggleMember('u1', true);
      expect(component.isMemberSelected('u1')).toBe(true);
      component.toggleMember('u1', false);
      expect(component.isMemberSelected('u1')).toBe(false);
    });

    it('saveMembers no-ops without modal user', () => {
      component.saveMembers();
      expect(groupsSvc.setMembers).not.toHaveBeenCalled();
    });

    it('saveMembers no-ops when busy', () => {
      component.openMembers(group);
      component.membersBusy.set(true);
      component.saveMembers();
      expect(groupsSvc.setMembers).not.toHaveBeenCalled();
    });

    it('saveMembers persists selection and toasts', () => {
      component.openMembers(group);
      component.selectedMemberIds.set(['u1', 'u2']);
      component.saveMembers();
      expect(groupsSvc.setMembers).toHaveBeenCalledWith('g1', ['u1', 'u2']);
      expect(toast.success).toHaveBeenCalled();
      expect(component.membersModalGroup()).toBeNull();
    });

    it('saveMembers surfaces error', () => {
      component.openMembers(group);
      groupsSvc.setMembers.mockReturnValueOnce(throwError(() => ({ message: 'fail' })));
      component.saveMembers();
      expect(component.errorMessage()).toBe('fail');
    });

    it('saveMembers falls back to default error', () => {
      component.openMembers(group);
      groupsSvc.setMembers.mockReturnValueOnce(throwError(() => ({})));
      component.saveMembers();
      expect(component.errorMessage()).toBe('Failed to update members.');
    });
  });

  describe('filtering helpers', () => {
    beforeEach(() => {
      usersSvc.list.set([
        { id: 'u1', name: 'Alice', email: 'alice@x' } as AppUser,
        { id: 'u2', name: 'Bob', email: 'bob@y' } as AppUser,
      ]);
    });

    it('filteredUsers returns all when search empty', () => {
      expect(component.filteredUsers().length).toBe(2);
    });

    it('filteredUsers filters by name or email', () => {
      component.memberSearch.set('alice');
      expect(component.filteredUsers().map((u) => u.id)).toEqual(['u1']);
      component.memberSearch.set('bob@y');
      expect(component.filteredUsers().map((u) => u.id)).toEqual(['u2']);
    });

    it('filteredSelectedMembers returns selected users matching search', () => {
      component.selectedMemberIds.set(['u1']);
      expect(component.filteredSelectedMembers().map((u) => u.id)).toEqual(['u1']);
      component.memberSearch.set('bob');
      expect(component.filteredSelectedMembers().map((u) => u.id)).toEqual([]);
    });

    it('filteredUnselectedMembers returns non-selected users matching search', () => {
      component.selectedMemberIds.set(['u1']);
      expect(component.filteredUnselectedMembers().map((u) => u.id)).toEqual(['u2']);
      component.memberSearch.set('alice');
      expect(component.filteredUnselectedMembers().map((u) => u.id)).toEqual([]);
    });

    it('trackGroup / trackUser return id', () => {
      expect(component.trackGroup(0, group)).toBe('g1');
      expect(component.trackUser(0, { id: 'u1' } as AppUser)).toBe('u1');
    });
  });
});
