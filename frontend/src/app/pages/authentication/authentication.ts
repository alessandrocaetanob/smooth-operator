import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Mascot, MascotState } from '../../shared/mascot/mascot';
import { ThemeToggle } from '../../shared/theme-toggle/theme-toggle';
import { AuthService } from '../../services/auth.service';
import { Spinner } from '../../shared/spinner/spinner';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';

@Component({
  selector: 'app-authentication',
  imports: [ReactiveFormsModule, RouterLink, Mascot, ThemeToggle, Spinner],
  templateUrl: './authentication.html',
  styleUrl: './authentication.css',
})
export class Authentication {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  readonly providers = this.auth.providers;
  readonly sso = computed(() => this.providers().sso);
  readonly submitting = signal(false);
  readonly ssoLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly isPasswordFocused = signal(false);
  readonly typingLength = signal(0);
  readonly passwordVisible = signal(false);

  readonly step = signal<'credentials' | 'mfa'>('credentials');
  private readonly challengeToken = signal('');

  get helpUrl(): string {
    return this.runtimeConfig.config.helpUrl;
  }

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  readonly mfaForm = this.fb.nonNullable.group({
    code: [
      '',
      [Validators.required, Validators.pattern(/^(\d{6}|[A-Za-z0-9]{4}-[A-Za-z0-9]{4})$/)],
    ],
  });

  readonly mascotState = computed<MascotState>(() => {
    if (this.isPasswordFocused()) return 'password';
    if (this.ssoLoading()) return 'sso';
    if (this.submitting()) return 'loading';
    if (this.errorMessage()) return 'error';
    if (this.typingLength() > 0) return 'typing';
    return 'idle';
  });

  onPasswordFocus(): void {
    this.isPasswordFocused.set(true);
  }

  onPasswordBlur(): void {
    this.isPasswordFocused.set(false);
  }

  togglePasswordVisibility(): void {
    this.passwordVisible.update((v) => !v);
  }

  onEmailInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.typingLength.set(input.value.length);
  }

  submit(): void {
    if (this.submitting()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (result) => {
        this.submitting.set(false);
        if (result && 'mfaRequired' in result && result.mfaRequired) {
          this.challengeToken.set(result.challengeToken);
          this.step.set('mfa');
        } else {
          this.router.navigateByUrl(this.auth.canAccessSettings() ? '/administration' : '/vault');
        }
      },
      error: (err) => {
        this.submitting.set(false);
        const msg =
          err instanceof HttpErrorResponse && err.status === 429
            ? 'Too many sign-in attempts. Please wait a moment and try again.'
            : (err?.error?.message ?? 'Sign-in failed.');
        this.errorMessage.set(msg);
      },
    });
  }

  submitMfaCode(): void {
    if (this.submitting()) return;
    if (this.mfaForm.invalid) {
      this.mfaForm.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);

    this.auth.verifyMfa(this.challengeToken(), this.mfaForm.getRawValue().code).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigateByUrl(this.auth.canAccessSettings() ? '/administration' : '/vault');
      },
      error: (err) => {
        this.submitting.set(false);
        const msg =
          err instanceof HttpErrorResponse && err.status === 429
            ? 'Too many sign-in attempts. Please wait a moment and try again.'
            : (err?.error?.message ?? 'Verification failed.');
        this.errorMessage.set(msg);
      },
    });
  }

  backToCredentials(): void {
    this.step.set('credentials');
    this.challengeToken.set('');
    this.errorMessage.set(null);
    this.mfaForm.reset();
  }

  loginWithSso(): void {
    if (this.ssoLoading()) return;
    this.ssoLoading.set(true);
    const returnUrl = '/vault';
    globalThis.location.href = `/api/auth/sso/initiate?returnUrl=${encodeURIComponent(returnUrl)}`;
  }
}
