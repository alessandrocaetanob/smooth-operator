import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ThemeToggle } from '../../shared/theme-toggle/theme-toggle';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, ThemeToggle, Spinner],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.css',
})
export class ForgotPassword implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);

  readonly smtpAvailable = signal<boolean | null>(null);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  ngOnInit(): void {
    this.http.get<{ available: boolean }>('/api/auth/smtp-available').subscribe({
      next: (res) => this.smtpAvailable.set(res.available),
      error: () => this.smtpAvailable.set(false),
    });
  }

  submit(): void {
    if (this.submitting()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    this.http
      .post('/api/auth/forgot-password', { email: this.form.getRawValue().email })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.submitted.set(true);
        },
        error: () => {
          this.submitting.set(false);
          // Transport failure — set error message so the banner renders.
          // Keep submitted false so the user can retry.
          this.errorMessage.set('An unexpected error occurred. Please try again.');
        },
      });
  }
}
