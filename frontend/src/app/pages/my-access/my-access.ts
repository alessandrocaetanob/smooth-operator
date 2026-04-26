import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { UsersService, EffectiveVaults } from '../../services/users.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-my-access',
  standalone: true,
  imports: [],
  templateUrl: './my-access.html',
})
export class MyAccess implements OnInit {
  private readonly usersSvc = inject(UsersService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly effective = signal<EffectiveVaults | null>(null);

  readonly profile = computed(() => this.auth.currentUser());
  readonly mergedCount = computed(() => this.effective()?.merged.length ?? 0);
  readonly directCount = computed(() => this.effective()?.direct.length ?? 0);
  readonly groupCount = computed(() => this.effective()?.viaGroups.length ?? 0);

  ngOnInit(): void {
    const user = this.profile();
    if (!user?.id) {
      this.loading.set(false);
      this.errorMessage.set('You are not signed in.');
      return;
    }
    this.usersSvc.getEffectiveVaults(user.id).subscribe({
      next: (data) => {
        this.effective.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(
          err?.error?.message ?? err?.error?.Message ?? err?.message ?? 'Failed to load access.',
        );
      },
    });
  }

  trackById(_: number, item: { id: string }): string {
    return item.id;
  }

  trackGroup(_: number, item: { groupId: string }): string {
    return item.groupId;
  }
}
