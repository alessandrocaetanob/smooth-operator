import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-sso-finalize',
  imports: [Spinner, RouterLink],
  templateUrl: './sso-finalize.html',
})
export class SsoFinalize implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const error = params.get('error');
    const token = params.get('token');
    const returnUrl = params.get('returnUrl') || '/vault';

    if (error) {
      this.errorMessage.set(error);
      return;
    }

    if (!token) {
      this.errorMessage.set('Missing authentication token from SSO callback.');
      return;
    }

    this.auth.acceptSsoToken(token);
    this.auth.me().subscribe({
      next: () => this.router.navigateByUrl(this.safeReturnUrl(returnUrl)),
      error: () => {
        // Token was minted but /me failed — keep token and try to navigate
        // anyway; route guards will gracefully bounce to /login if invalid.
        this.router.navigateByUrl(this.safeReturnUrl(returnUrl));
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
