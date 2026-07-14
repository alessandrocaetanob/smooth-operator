import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Vault, VaultsService } from '../../../services/vaults.service';
import { AppUser, UsersService } from '../../../services/users.service';
import { UserGroup, GroupsService } from '../../../services/groups.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../../shared/toast/toast.service';

@Component({
  selector: 'app-settings-vaults',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './vaults.html',
})
export class SettingsVaults implements OnInit {
  private readonly vaultsSvc = inject(VaultsService);
  private readonly usersSvc = inject(UsersService);
  private readonly groupsSvc = inject(GroupsService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);
  private readonly translate = inject(TranslateService);

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

  // Recording configuration modal — one open at a time.
  readonly recordingModalVault = signal<Vault | null>(null);
  readonly recordingEnabledForm = signal(false);
  readonly recordingIncludeKeysForm = signal(false);
  readonly recordingRetentionDaysForm = signal<number | null>(null);
  readonly recordingBusy = signal(false);
  // Performance optimization: O(1) lookup sets for template bindings inside loops to prevent O(N*M) change detection cycles
  readonly selectedUserIdSet = computed(() => new Set(this.selectedUserIds()));
  readonly selectedGroupIdSet = computed(() => new Set(this.selectedGroupIds()));
  readonly userAssignSearch = signal('');
  readonly groupAssignSearch = signal('');
  readonly assignBusy = signal(false);

  readonly filteredAssignUsers = computed(() => {
    const term = this.userAssignSearch().toLowerCase();
    if (!term) return this.users();
    return this.users().filter(
      (u) => (u.name?.toLowerCase() ?? '').includes(term) || u.email.toLowerCase().includes(term),
    );
  });

  readonly filteredAssignGroups = computed(() => {
    const term = this.groupAssignSearch().toLowerCase();
    if (!term) return this.groups();
    return this.groups().filter((g) => g.name.toLowerCase().includes(term));
  });

  readonly filteredSelectedAssignUsers = computed(() =>
    this.filteredAssignUsers().filter((u) => this.selectedUserIdSet().has(u.id)),
  );
  readonly filteredUnselectedAssignUsers = computed(() =>
    this.filteredAssignUsers().filter((u) => !this.selectedUserIdSet().has(u.id)),
  );
  readonly filteredSelectedAssignGroups = computed(() =>
    this.filteredAssignGroups().filter((g) => this.selectedGroupIdSet().has(g.id)),
  );
  readonly filteredUnselectedAssignGroups = computed(() =>
    this.filteredAssignGroups().filter((g) => !this.selectedGroupIdSet().has(g.id)),
  );

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
        this.errorMessage.set(
          this.toMessage(err) || this.translate.instant('pages.settingsVaults.errors.loadFailed'),
        );
      },
    });
  }

  createVault(): void {
    if (this.vaultBusy()) return;
    const name = this.newVaultName().trim();
    if (!name) {
      this.errorMessage.set(this.translate.instant('pages.settingsVaults.errors.nameRequired'));
      return;
    }
    this.vaultBusy.set(true);
    this.errorMessage.set(null);
    this.vaultsSvc.create({ name }).subscribe({
      next: () => {
        this.vaultBusy.set(false);
        this.newVaultName.set('');
        this.toastSvc.success(
          this.translate.instant('pages.settingsVaults.toasts.created', { name }),
        );
        this.refresh();
      },
      error: (err) => {
        this.vaultBusy.set(false);
        const msg =
          this.toMessage(err) || this.translate.instant('pages.settingsVaults.errors.createFailed');
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
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
    this.vaultsSvc
      .update(vault.id, {
        name,
        // Preserve any existing recording config — the rename row doesn't expose those fields.
        recordingEnabled: vault.recordingEnabled ?? false,
        recordingIncludeKeys: vault.recordingIncludeKeys ?? false,
        recordingRetentionDays: vault.recordingRetentionDays ?? null,
      })
      .subscribe({
        next: () => {
          this.vaultBusy.set(false);
          this.cancelEdit();
          this.toastSvc.success(
            this.translate.instant('pages.settingsVaults.toasts.renamed', { name }),
          );
          this.refresh();
        },
        error: (err) => {
          this.vaultBusy.set(false);
          const msg =
            this.toMessage(err) ||
            this.translate.instant('pages.settingsVaults.errors.renameFailed');
          this.errorMessage.set(msg);
          this.toastSvc.error(msg);
        },
      });
  }

  async deleteVault(vault: Vault): Promise<void> {
    const ok = await this.confirmSvc.ask({
      title: this.translate.instant('pages.settingsVaults.confirmDelete.title'),
      message: this.translate.instant('pages.settingsVaults.confirmDelete.message', {
        name: vault.name,
      }),
      confirmLabel: this.translate.instant('common.actions.delete'),
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.vaultsSvc.remove(vault.id).subscribe({
      next: () => {
        this.toastSvc.success(
          this.translate.instant('pages.settingsVaults.toasts.deleted', { name: vault.name }),
        );
        this.refresh();
      },
      error: (err) => {
        const msg =
          this.toMessage(err) || this.translate.instant('pages.settingsVaults.errors.deleteFailed');
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
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
        this.errorMessage.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsVaults.errors.loadAssignmentsFailed'),
        ),
    });
  }

  closeAssignments(): void {
    this.assignModalVault.set(null);
    this.selectedUserIds.set([]);
    this.selectedGroupIds.set([]);
    this.assignBusy.set(false);
  }

  openRecording(vault: Vault): void {
    this.recordingModalVault.set(vault);
    this.recordingEnabledForm.set(vault.recordingEnabled ?? false);
    this.recordingIncludeKeysForm.set(vault.recordingIncludeKeys ?? false);
    this.recordingRetentionDaysForm.set(vault.recordingRetentionDays ?? null);
  }

  closeRecording(): void {
    this.recordingModalVault.set(null);
    this.recordingBusy.set(false);
  }

  saveRecording(): void {
    const vault = this.recordingModalVault();
    if (!vault || this.recordingBusy()) return;
    this.recordingBusy.set(true);
    this.errorMessage.set(null);
    this.vaultsSvc
      .update(vault.id, {
        name: vault.name,
        parentGroupId: vault.parentGroupId ?? null,
        recordingEnabled: this.recordingEnabledForm(),
        recordingIncludeKeys: this.recordingIncludeKeysForm(),
        recordingRetentionDays: this.recordingRetentionDaysForm(),
      })
      .subscribe({
        next: () => {
          this.recordingBusy.set(false);
          this.toastSvc.success(
            this.translate.instant('pages.settingsVaults.toasts.recordingSaved', {
              name: vault.name,
            }),
          );
          this.closeRecording();
          this.refresh();
        },
        error: (err) => {
          this.recordingBusy.set(false);
          const msg =
            this.toMessage(err) ||
            this.translate.instant('pages.settingsVaults.errors.recordingSaveFailed');
          this.errorMessage.set(msg);
          this.toastSvc.error(msg);
        },
      });
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
    return this.selectedUserIdSet().has(id);
  }

  isGroupSelected(id: string): boolean {
    return this.selectedGroupIdSet().has(id);
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
          this.toastSvc.success(
            this.translate.instant('pages.settingsVaults.toasts.assignmentsUpdated', {
              name: vault.name,
            }),
          );
          this.closeAssignments();
          this.refresh();
        },
        error: (err) => {
          this.assignBusy.set(false);
          const msg =
            this.toMessage(err) ||
            this.translate.instant('pages.settingsVaults.errors.assignmentsSaveFailed');
          this.errorMessage.set(msg);
          this.toastSvc.error(msg);
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

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
