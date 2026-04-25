import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AppUser, UsersService } from '../../services/users.service';
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
  readonly invitePassword = signal('');
  readonly inviteBusy = signal(false);

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
      this.invitePassword.set('');
    }
  }

  submitInvite(): void {
    if (this.inviteBusy()) return;
    const name = this.inviteName().trim();
    const email = this.inviteEmail().trim();
    const password = this.invitePassword();
    if (!name || !email) {
      this.errorMessage.set('Name and email are required.');
      return;
    }
    this.inviteBusy.set(true);
    this.errorMessage.set(null);
    this.usersSvc
      .invite({ name, email, password: password ? password : undefined })
      .subscribe({
        next: () => {
          this.inviteBusy.set(false);
          this.toggleInvite();
          this.refresh();
        },
        error: (err) => {
          this.inviteBusy.set(false);
          this.errorMessage.set(this.toMessage(err) || 'Invite failed.');
        },
      });
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
