import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Mascot, MascotState } from '../../shared/mascot/mascot';
import { ThemeToggle } from '../../shared/theme-toggle/theme-toggle';
import { AuthService } from '../../services/auth.service';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-authentication',
  imports: [ReactiveFormsModule, Mascot, ThemeToggle, Spinner],
  templateUrl: './authentication.html',
  styleUrl: './authentication.css',
})
export class Authentication {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly providers = this.auth.providers;
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly isPasswordFocused = signal(false);
  readonly typingLength = signal(0);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  readonly mascotState = computed<MascotState>(() => {
    if (this.isPasswordFocused()) return 'password';
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
      next: () => {
        this.submitting.set(false);
        this.router.navigateByUrl(this.auth.canAccessSettings() ? '/administration' : '/vault');
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Sign-in failed.');
      },
    });
  }

  loginWithSso(): void {
    // Placeholder until the OIDC redirect flow is implemented in the SPA.
    this.errorMessage.set('SSO flow is not yet wired up in the frontend.');
  }
}
