import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AppRole, AppUser, InviteResult, UsersService } from '../../services/users.service';
import { AuthService } from '../../services/auth.service';
import { VaultsService } from '../../services/vaults.service';

@Component({
  selector: 'app-administration',
  imports: [FormsModule],
  templateUrl: './administration.html',
  styleUrl: './administration.css',
})
export class Administration implements OnInit {
  private readonly usersSvc = inject(UsersService);
  private readonly auth = inject(AuthService);
  private readonly vaultsSvc = inject(VaultsService);

  readonly users = this.usersSvc.list;
  readonly vaults = this.vaultsSvc.list;
  readonly currentUser = this.auth.currentUser;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

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
        this.errorMessage.set(this.toMessage(err) || 'Failed to load users.');
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
      this.errorMessage.set('Email is required.');
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
        this.errorMessage.set(this.toMessage(err) || 'Invite failed.');
      },
    });
  }

  invitableRoles(): AppRole[] {
    return this.roles().filter((r) => r.name !== 'Owner');
  }

  changeRole(user: AppUser, role: string): void {
    if (!role || this.roleBusyUserId() === user.id) return;

    this.roleBusyUserId.set(user.id);
    this.errorMessage.set(null);
    this.usersSvc.setRole(user.id, role).subscribe({
      next: () => {
        this.roleBusyUserId.set(null);
        this.refresh();
      },
      error: (err) => {
        this.roleBusyUserId.set(null);
        this.errorMessage.set(this.toMessage(err) || 'Failed to update role.');
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
        this.closeVaultAssignments();
        this.refresh();
      },
      error: (err) => {
        this.vaultAssignmentBusy.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Failed to update vault assignments.');
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
    return this.selectedVaultIds().includes(vaultId);
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
      next: () => this.refresh(),
      error: (err) => this.errorMessage.set(this.toMessage(err) || 'Update failed.'),
    });
  }

  delete(user: AppUser): void {
    if (this.currentUser()?.id === user.id) {
      this.errorMessage.set('You cannot delete your own account.');
      return;
    }
    if (!confirm(`Delete user ${user.email}? This cannot be undone.`)) return;
    this.errorMessage.set(null);
    this.usersSvc.remove(user.id).subscribe({
      next: () => this.refresh(),
      error: (err) => this.errorMessage.set(this.toMessage(err) || 'Delete failed.'),
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

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
