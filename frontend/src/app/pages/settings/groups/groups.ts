import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { UserGroup, GroupsService } from '../../../services/groups.service';
import { AppUser, UsersService } from '../../../services/users.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../../shared/toast/toast.service';

@Component({
  selector: 'app-settings-groups',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './groups.html',
})
export class SettingsGroups implements OnInit {
  private readonly groupsSvc = inject(GroupsService);
  private readonly usersSvc = inject(UsersService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);

  readonly groups = this.groupsSvc.list;
  readonly users = this.usersSvc.list;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly newGroupName = signal('');
  readonly groupBusy = signal(false);

  readonly editingId = signal<string | null>(null);
  readonly editingName = signal('');

  readonly membersModalGroup = signal<UserGroup | null>(null);
  readonly selectedMemberIds = signal<string[]>([]);
  // Performance optimization: O(1) lookup set for template bindings inside loops to prevent O(N*M) change detection cycles
  readonly selectedMemberIdSet = computed(() => new Set(this.selectedMemberIds()));
  readonly membersBusy = signal(false);
  readonly memberSearch = signal('');

  readonly filteredUsers = computed(() => {
    const term = this.memberSearch().toLowerCase();
    if (!term) return this.users();
    return this.users().filter(
      (u) => (u.name?.toLowerCase() ?? '').includes(term) || u.email.toLowerCase().includes(term),
    );
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      groups: this.groupsSvc.reload(),
      users: this.usersSvc.reload(),
    }).subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to load groups.');
      },
    });
  }

  createGroup(): void {
    if (this.groupBusy()) return;
    const name = this.newGroupName().trim();
    if (!name) {
      this.errorMessage.set('Group name is required.');
      return;
    }
    this.groupBusy.set(true);
    this.errorMessage.set(null);
    this.groupsSvc.create({ name }).subscribe({
      next: () => {
        this.groupBusy.set(false);
        this.newGroupName.set('');
        this.toastSvc.success(`Group "${name}" created.`);
        this.refresh();
      },
      error: (err) => {
        this.groupBusy.set(false);
        const msg = this.toMessage(err) || 'Failed to create group.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  startEdit(group: UserGroup): void {
    this.editingId.set(group.id);
    this.editingName.set(group.name);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editingName.set('');
  }

  saveEdit(group: UserGroup): void {
    const name = this.editingName().trim();
    if (!name) return;
    this.groupBusy.set(true);
    this.errorMessage.set(null);
    this.groupsSvc.rename(group.id, name).subscribe({
      next: () => {
        this.groupBusy.set(false);
        this.cancelEdit();
        this.toastSvc.success(`Group renamed to "${name}".`);
        this.refresh();
      },
      error: (err) => {
        this.groupBusy.set(false);
        const msg = this.toMessage(err) || 'Failed to rename group.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  async deleteGroup(group: UserGroup): Promise<void> {
    const ok = await this.confirmSvc.ask({
      title: 'Delete group',
      message: `Delete group "${group.name}"? Users in this group will lose vault access granted via the group. This cannot be undone.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.groupsSvc.remove(group.id).subscribe({
      next: () => {
        this.toastSvc.success(`Group "${group.name}" deleted.`);
        this.refresh();
      },
      error: (err) => {
        const msg = this.toMessage(err) || 'Failed to delete group.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  openMembers(group: UserGroup): void {
    this.membersModalGroup.set(group);
    this.selectedMemberIds.set(group.members.map((m) => m.id));
  }

  closeMembers(): void {
    this.membersModalGroup.set(null);
    this.selectedMemberIds.set([]);
    this.membersBusy.set(false);
    this.memberSearch.set('');
  }

  toggleMember(userId: string, checked: boolean): void {
    const set = new Set(this.selectedMemberIds());
    if (checked) set.add(userId);
    else set.delete(userId);
    this.selectedMemberIds.set(Array.from(set));
  }

  isMemberSelected(userId: string): boolean {
    return this.selectedMemberIdSet().has(userId);
  }

  saveMembers(): void {
    const group = this.membersModalGroup();
    if (!group || this.membersBusy()) return;
    this.membersBusy.set(true);
    this.errorMessage.set(null);
    this.groupsSvc.setMembers(group.id, this.selectedMemberIds()).subscribe({
      next: () => {
        this.membersBusy.set(false);
        this.toastSvc.success(`Members updated for "${group.name}".`);
        this.closeMembers();
        this.refresh();
      },
      error: (err) => {
        this.membersBusy.set(false);
        const msg = this.toMessage(err) || 'Failed to update members.';
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  trackGroup(_: number, g: UserGroup): string {
    return g.id;
  }

  trackUser(_: number, u: AppUser): string {
    return u.id;
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
