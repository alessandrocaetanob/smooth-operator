import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SmtpSettings, SmtpSettingsService } from '../../../services/smtp-settings.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-email-settings',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './email.html',
  styleUrl: './email.css',
})
export class Email implements OnInit {
  private readonly smtp = inject(SmtpSettingsService);
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);

  readonly current = this.smtp.current;
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly testBusy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly host = signal('');
  readonly port = signal(587);
  readonly useSsl = signal(true);
  readonly username = signal('');
  readonly password = signal('');
  readonly clearPassword = signal(false);
  readonly fromAddress = signal('');
  readonly fromName = signal('');
  readonly enabled = signal(true);

  readonly testTo = signal('');

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.smtp.load().subscribe({
      next: (s) => {
        this.loading.set(false);
        this.applyToForm(s);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.settingsEmail.errors.loadFailed'),
        );
      },
    });
  }

  private applyToForm(s: SmtpSettings): void {
    this.host.set(s.host);
    this.port.set(s.port);
    this.useSsl.set(s.useSsl);
    this.username.set(s.username ?? '');
    this.fromAddress.set(s.fromAddress);
    this.fromName.set(s.fromName ?? '');
    this.enabled.set(s.enabled);
    this.password.set('');
    this.clearPassword.set(false);
    if (!this.testTo()) {
      this.testTo.set(this.auth.currentUser()?.email ?? '');
    }
  }

  save(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);

    const pwInput = this.password();
    let password: string | null | undefined;
    if (this.clearPassword()) {
      password = '';
    } else if (pwInput.length > 0) {
      password = pwInput;
    } else {
      password = null;
    }

    this.smtp
      .update({
        host: this.host().trim(),
        port: this.port(),
        useSsl: this.useSsl(),
        username: this.username().trim() || null,
        password,
        fromAddress: this.fromAddress().trim(),
        fromName: this.fromName().trim() || null,
        enabled: this.enabled(),
      })
      .subscribe({
        next: (s) => {
          this.busy.set(false);
          this.applyToForm(s);
          this.message.set(this.translate.instant('pages.settingsEmail.messages.saved'));
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(
            this.toMessage(err) || this.translate.instant('pages.settingsEmail.errors.saveFailed'),
          );
        },
      });
  }

  sendTest(): void {
    if (this.testBusy()) return;
    const to = this.testTo().trim();
    if (!to) {
      this.error.set(this.translate.instant('pages.settingsEmail.errors.recipientRequired'));
      return;
    }
    this.testBusy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.smtp.test(to).subscribe({
      next: () => {
        this.testBusy.set(false);
        this.message.set(this.translate.instant('pages.settingsEmail.messages.testSent', { to }));
      },
      error: (err) => {
        this.testBusy.set(false);
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.settingsEmail.errors.testFailed'),
        );
      },
    });
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
