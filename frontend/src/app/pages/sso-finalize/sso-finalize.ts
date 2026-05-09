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
        this.errorMessage.set('Authentication failed. Please try logging in again.');
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
