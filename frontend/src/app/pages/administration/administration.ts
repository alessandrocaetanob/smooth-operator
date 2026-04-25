import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AppUser, InviteResult, UsersService } from '../../services/users.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-administration',
  imports: [FormsModule],
  templateUrl: './administration.html',
  styleUrl: './administration.css',
})
export class Administration implements OnInit {
  private readonly usersSvc = inject(UsersService);
  private readonly auth = inject(AuthService);

  readonly users = this.usersSvc.list;
  readonly currentUser = this.auth.currentUser;
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly showInvite = signal(false);
  readonly inviteName = signal('');
  readonly inviteEmail = signal('');
  readonly inviteBusy = signal(false);

  readonly inviteResult = signal<InviteResult | null>(null);
  readonly copyState = signal<'idle' | 'copied' | 'failed'>('idle');

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.usersSvc.reload().subscribe({
      next: () => this.loading.set(false),
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
    }
  }

  submitInvite(): void {
    if (this.inviteBusy()) return;
    const name = this.inviteName().trim();
    const email = this.inviteEmail().trim();
    if (!email) {
      this.errorMessage.set('Email is required.');
      return;
    }
    this.inviteBusy.set(true);
    this.errorMessage.set(null);
    this.usersSvc.invite({ name: name || undefined, email }).subscribe({
      next: (res) => {
        this.inviteBusy.set(false);
        this.inviteResult.set(res);
        this.copyState.set('idle');
        this.showInvite.set(false);
        this.inviteName.set('');
        this.inviteEmail.set('');
        this.refresh();
      },
      error: (err) => {
        this.inviteBusy.set(false);
        this.errorMessage.set(this.toMessage(err) || 'Invite failed.');
      },
    });
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

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
