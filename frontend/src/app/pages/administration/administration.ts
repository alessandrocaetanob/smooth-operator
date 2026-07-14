import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AppRole, AppUser, InviteResult, UsersService } from '../../services/users.service';
import { AuthService } from '../../services/auth.service';
import { VaultsService } from '../../services/vaults.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-administration',
  imports: [FormsModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './administration.html',
  styleUrl: './administration.css',
})
export class Administration implements OnInit {
  private readonly usersSvc = inject(UsersService);
  private readonly auth = inject(AuthService);
  private readonly vaultsSvc = inject(VaultsService);
  private readonly confirmSvc = inject(ConfirmDialogService);
  private readonly toastSvc = inject(ToastService);
  private readonly translate = inject(TranslateService);

  readonly users = this.usersSvc.list;
  readonly vaults = this.vaultsSvc.list;
  readonly currentUser = this.auth.currentUser;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly searchQuery = signal('');

  private readonly searchableUsers = computed(() => {
    return this.users().map((u) => ({
      item: u,
      _searchIndex: [u.name ?? '', u.email ?? '', this.primaryRole(u) ?? '']
        .join(' ')
        .toLowerCase(),
    }));
  });

  readonly filteredUsers = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    if (!q) return this.users();
    return this.searchableUsers()
      .filter((u) => u._searchIndex.includes(q))
      .map(({ item }) => item);
  });

  readonly roles = signal<AppRole[]>([]);
  readonly roleBusyUserId = signal<string | null>(null);

  readonly showInvite = signal(false);
  readonly inviteName = signal('');
  readonly inviteEmail = signal('');
  readonly inviteRole = signal<string>('User');
  readonly inviteBusy = signal(false);

  readonly inviteResult = signal<InviteResult | null>(null);
  readonly copyState = signal<'idle' | 'copied' | 'failed'>('idle');

  readonly vaultModalUser = signal<AppUser | null>(null);
  readonly selectedVaultIds = signal<string[]>([]);
  // Performance optimization: O(1) lookup set for template bindings inside loops to prevent O(N*M) change detection cycles
  readonly selectedVaultIdSet = computed(() => new Set(this.selectedVaultIds()));
  readonly vaultAssignmentBusy = signal(false);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      users: this.usersSvc.reload(),
      roles: this.usersSvc.roleCatalog(),
      vaults: this.vaultsSvc.reload(),
    }).subscribe({
      next: ({ roles }) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(
          this.toMessage(err) || this.translate.instant('pages.administration.loadUsersFailed'),
        );
      },
    });
  }

  toggleInvite(): void {
    this.showInvite.update((v) => !v);
    if (!this.showInvite()) {
      this.inviteName.set('');
      this.inviteEmail.set('');
      this.inviteRole.set('User');
    }
  }

  submitInvite(): void {
    if (this.inviteBusy()) return;
    const name = this.inviteName().trim();
    const email = this.inviteEmail().trim();
    const role = this.inviteRole();
    if (!email) {
      this.errorMessage.set(this.translate.instant('pages.administration.emailRequired'));
      return;
    }
    this.inviteBusy.set(true);
    this.errorMessage.set(null);
    this.usersSvc.invite({ name: name || undefined, email, role }).subscribe({
      next: (res) => {
        this.inviteBusy.set(false);
        this.inviteResult.set(res);
        this.copyState.set('idle');
        this.showInvite.set(false);
        this.inviteName.set('');
        this.inviteEmail.set('');
        this.inviteRole.set('User');
        this.refresh();
      },
      error: (err) => {
        this.inviteBusy.set(false);
        this.errorMessage.set(
          this.toMessage(err) || this.translate.instant('pages.administration.inviteFailed'),
        );
      },
    });
  }

  invitableRoles(): AppRole[] {
    return this.roles().filter((r) => r.name !== 'Owner');
  }

  async changeRole(user: AppUser, role: string): Promise<void> {
    if (!role || this.roleBusyUserId() === user.id) return;
    const previous = this.primaryRole(user);
    if (previous === role) return;

    const isOwnerMove = role === 'Owner' || previous === 'Owner';
    const ok = await this.confirmSvc.ask({
      title: this.translate.instant(
        isOwnerMove
          ? 'pages.administration.changeOwnershipTitle'
          : 'pages.administration.changeRoleTitle',
      ),
      message: isOwnerMove
        ? this.translate.instant('pages.administration.changeOwnershipMessage', {
            name: user.name,
            previous,
            role,
          })
        : this.translate.instant('pages.administration.changeRoleMessage', {
            name: user.name,
            previous,
            role,
          }),
      confirmLabel: this.translate.instant('pages.administration.changeRoleConfirmLabel'),
      tone: isOwnerMove ? 'danger' : 'default',
    });
    if (!ok) {
      this.refresh();
      return;
    }

    this.roleBusyUserId.set(user.id);
    this.errorMessage.set(null);
    this.usersSvc.setRole(user.id, role).subscribe({
      next: () => {
        this.roleBusyUserId.set(null);
        this.toastSvc.success(
          this.translate.instant('pages.administration.roleUpdated', { name: user.name }),
        );
        this.refresh();
      },
      error: (err) => {
        this.roleBusyUserId.set(null);
        const msg =
          this.toMessage(err) || this.translate.instant('pages.administration.updateRoleFailed');
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
        this.refresh();
      },
    });
  }

  openVaultAssignments(user: AppUser): void {
    this.vaultModalUser.set(user);
    this.selectedVaultIds.set([...(user.vaultIds ?? [])]);
  }

  closeVaultAssignments(): void {
    this.vaultModalUser.set(null);
    this.selectedVaultIds.set([]);
    this.vaultAssignmentBusy.set(false);
  }

  toggleVaultSelection(vaultId: string, checked: boolean): void {
    const current = new Set(this.selectedVaultIds());
    if (checked) current.add(vaultId);
    else current.delete(vaultId);
    this.selectedVaultIds.set(Array.from(current));
  }

  saveVaultAssignments(): void {
    const user = this.vaultModalUser();
    if (!user || this.vaultAssignmentBusy()) return;

    this.vaultAssignmentBusy.set(true);
    this.errorMessage.set(null);
    this.usersSvc.setVaultAssignments(user.id, this.selectedVaultIds()).subscribe({
      next: () => {
        this.vaultAssignmentBusy.set(false);
        this.toastSvc.success(
          this.translate.instant('pages.administration.vaultAssignmentsUpdated', {
            name: user.name,
          }),
        );
        this.closeVaultAssignments();
        this.refresh();
      },
      error: (err) => {
        this.vaultAssignmentBusy.set(false);
        const msg =
          this.toMessage(err) ||
          this.translate.instant('pages.administration.updateVaultAssignmentsFailed');
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  canAssignVaults(user: AppUser): boolean {
    const role = this.primaryRole(user);
    return role === 'TeamAdmin' || role === 'User';
  }

  primaryRole(user: AppUser): string {
    return user.roles[0] || 'User';
  }

  isVaultSelected(vaultId: string): boolean {
    return this.selectedVaultIdSet().has(vaultId);
  }

  copyInviteUrl(): void {
    const url = this.inviteResult()?.inviteUrl;
    if (!url) return;
    navigator.clipboard
      .writeText(url)
      .then(() => {
        this.copyState.set('copied');
        setTimeout(() => this.copyState.set('idle'), 2000);
      })
      .catch(() => this.copyState.set('failed'));
  }

  dismissInviteResult(): void {
    this.inviteResult.set(null);
    this.copyState.set('idle');
  }

  setActive(user: AppUser, active: boolean): void {
    this.errorMessage.set(null);
    this.usersSvc.setActive(user.id, active).subscribe({
      next: () => {
        this.toastSvc.success(
          this.translate.instant(
            active ? 'pages.administration.userEnabled' : 'pages.administration.userDisabled',
            { name: user.name },
          ),
        );
        this.refresh();
      },
      error: (err) => {
        const msg =
          this.toMessage(err) || this.translate.instant('pages.administration.updateFailed');
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  async delete(user: AppUser): Promise<void> {
    if (this.currentUser()?.id === user.id) {
      this.errorMessage.set(this.translate.instant('pages.administration.cannotDeleteSelf'));
      return;
    }
    const ok = await this.confirmSvc.ask({
      title: this.translate.instant('pages.administration.deleteUserTitle'),
      message: this.translate.instant('pages.administration.deleteUserMessage', {
        email: user.email,
      }),
      confirmLabel: this.translate.instant('common.actions.delete'),
      tone: 'danger',
    });
    if (!ok) return;
    this.errorMessage.set(null);
    this.usersSvc.remove(user.id).subscribe({
      next: () => {
        this.toastSvc.success(
          this.translate.instant('pages.administration.userDeleted', { email: user.email }),
        );
        this.refresh();
      },
      error: (err) => {
        const msg =
          this.toMessage(err) || this.translate.instant('pages.administration.deleteFailed');
        this.errorMessage.set(msg);
        this.toastSvc.error(msg);
      },
    });
  }

  trackById(_: number, u: AppUser): string {
    return u.id;
  }

  trackRole(_: number, role: AppRole): string {
    return role.name;
  }

  trackVault(_: number, v: { id: string }): string {
    return v.id;
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
