import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { UsersService, EffectiveVaults } from '../../services/users.service';
import { AuthService } from '../../services/auth.service';
import { Mascot } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-my-access',
  standalone: true,
  imports: [Mascot, TranslatePipe],
  templateUrl: './my-access.html',
})
export class MyAccess implements OnInit {
  private readonly usersSvc = inject(UsersService);
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);

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
      this.errorMessage.set(this.translate.instant('pages.myAccess.notSignedIn'));
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
          err?.error?.message ??
            err?.error?.Message ??
            err?.message ??
            this.translate.instant('pages.myAccess.loadFailed'),
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
