import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-sso-finalize',
  imports: [Spinner, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './sso-finalize.html',
})
export class SsoFinalize implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);

  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const fragment = this.route.snapshot.fragment ?? '';
    const params = new URLSearchParams(fragment);
    const error = params.get('error');
    const returnUrl = params.get('returnUrl') || '/vault';

    if (error) {
      this.errorMessage.set(error);
      return;
    }

    this.auth.me().subscribe({
      next: () => this.router.navigateByUrl(this.safeReturnUrl(returnUrl)),
      error: () => {
        this.errorMessage.set(this.translate.instant('pages.ssoFinalize.authFailedFallback'));
      },
    });
  }

  /**
   * Belt-and-braces same-origin guard. Backend already sanitises returnUrl
   * before persisting it, but we re-validate on the client to defeat any
   * tamper-with-finalize-URL attempt that bypasses the persisted state.
   */
  private safeReturnUrl(value: string): string {
    if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
      return '/vault';
    }
    return value;
  }
}
