import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Vault, VaultsService } from '../../../services/vaults.service';
import { AppUser, UsersService } from '../../../services/users.service';
import { UserGroup, GroupsService } from '../../../services/groups.service';

@Component({
  selector: 'app-settings-vaults',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './vaults.html',
})
export class SettingsVaults implements OnInit {
  private readonly vaultsSvc = inject(VaultsService);
  private readonly usersSvc = inject(UsersService);
  private readonly groupsSvc = inject(GroupsService);

  readonly vaults = this.vaultsSvc.list;
  readonly users = this.usersSvc.list;
  readonly groups = this.groupsSvc.list;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly newVaultName = signal('');
  readonly editingId = signal<string | null>(null);
  readonly editingName = signal('');
  readonly vaultBusy = signal(false);

  readonly assignModalVault = signal<Vault | null>(null);
  readonly selectedUserIds = signal<string[]>([]);
  readonly selectedGroupIds = signal<string[]>([]);
  readonly userAssignSearch = signal('');
  readonly groupAssignSearch = signal('');
  readonly assignBusy = signal(false);

  readonly filteredAssignUsers = computed(() => {
    const term = this.userAssignSearch().toLowerCase();
    if (!term) return this.users();
    return this.users().filter(
      (u) =>
        (u.name?.toLowerCase() ?? '').includes(term) ||
        u.email.toLowerCase().includes(term),
    );
  });

  readonly filteredAssignGroups = computed(() => {
    const term = this.groupAssignSearch().toLowerCase();
    if (!term) return this.groups();
    return this.groups().filter((g) => g.name.toLowerCase().includes(term));
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      vaults: this.vaultsSvc.reload(),
      users: this.usersSvc.reload(),
      groups: this.groupsSvc.reload(),
    }).subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to load vaults.');
      },
    });
  }

  createVault(): void {
    if (this.vaultBusy()) return;
    const name = this.newVaultName().trim();
    if (!name) {
      this.errorMessage.set('Vault name is required.');
      return;
    }
    this.vaultBusy.set(true);
    this.errorMessage.set(null);
    this.vaultsSvc.create({ name }).subscribe({
      next: () => {
        this.vaultBusy.set(false);
        this.newVaultName.set('');
        this.refresh();
      },
      error: (err) => {
        this.vaultBusy.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to create vault.');
      },
    });
  }

  startEdit(vault: Vault): void {
    this.editingId.set(vault.id);
    this.editingName.set(vault.name);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editingName.set('');
  }

  saveEdit(vault: Vault): void {
    const name = this.editingName().trim();
    if (!name) return;
    this.vaultBusy.set(true);
    this.errorMessage.set(null);
    this.vaultsSvc.update(vault.id, { name }).subscribe({
      next: () => {
        this.vaultBusy.set(false);
        this.cancelEdit();
        this.refresh();
      },
      error: (err) => {
        this.vaultBusy.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to rename vault.');
      },
    });
  }

  deleteVault(vault: Vault): void {
    if (!confirm(`Delete vault "${vault.name}"? All connections inside will be unlinked.`)) return;
    this.errorMessage.set(null);
    this.vaultsSvc.remove(vault.id).subscribe({
      next: () => this.refresh(),
      error: (err) => this.errorMessage.set(this.toMessage(err) || 'Failed to delete vault.'),
    });
  }

  openAssignments(vault: Vault): void {
    this.assignModalVault.set(vault);
    this.userAssignSearch.set('');
    this.groupAssignSearch.set('');
    this.selectedUserIds.set([]);
    this.selectedGroupIds.set([]);
    this.vaultsSvc.getAssignments(vault.id).subscribe({
      next: (a) => {
        this.selectedUserIds.set(a.userIds);
        this.selectedGroupIds.set(a.groupIds);
      },
      error: (err) =>
        this.errorMessage.set(this.toMessage(err) || 'Failed to load assignments.'),
    });
  }

  closeAssignments(): void {
    this.assignModalVault.set(null);
    this.selectedUserIds.set([]);
    this.selectedGroupIds.set([]);
    this.assignBusy.set(false);
  }

  toggleUser(id: string, checked: boolean): void {
    const set = new Set(this.selectedUserIds());
    if (checked) set.add(id);
    else set.delete(id);
    this.selectedUserIds.set(Array.from(set));
  }

  toggleGroup(id: string, checked: boolean): void {
    const set = new Set(this.selectedGroupIds());
    if (checked) set.add(id);
    else set.delete(id);
    this.selectedGroupIds.set(Array.from(set));
  }

  isUserSelected(id: string): boolean {
    return this.selectedUserIds().includes(id);
  }

  isGroupSelected(id: string): boolean {
    return this.selectedGroupIds().includes(id);
  }

  saveAssignments(): void {
    const vault = this.assignModalVault();
    if (!vault || this.assignBusy()) return;
    this.assignBusy.set(true);
    this.errorMessage.set(null);
    this.vaultsSvc
      .setAssignments(vault.id, {
        userIds: this.selectedUserIds(),
        groupIds: this.selectedGroupIds(),
      })
      .subscribe({
        next: () => {
          this.assignBusy.set(false);
          this.closeAssignments();
          this.refresh();
        },
        error: (err) => {
          this.assignBusy.set(false);
          this.errorMessage.set(this.toMessage(err) || 'Failed to save assignments.');
        },
      });
  }

  trackVault(_: number, vault: Vault): string {
    return vault.id;
  }

  trackUser(_: number, u: AppUser): string {
    return u.id;
  }

  trackGroup(_: number, g: UserGroup): string {
    return g.id;
  }

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
