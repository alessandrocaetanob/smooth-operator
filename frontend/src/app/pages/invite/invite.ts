import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InvitePreview, InvitesService } from '../../services/invites.service';

@Component({
  selector: 'app-invite',
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './invite.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './invite.css',
})
export class Invite implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly invites = inject(InvitesService);
  private readonly translate = inject(TranslateService);

  readonly token = signal<string>('');
  readonly preview = signal<InvitePreview | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly name = signal('');
  readonly password = signal('');
  readonly confirm = signal('');
  readonly busy = signal(false);
  readonly success = signal(false);

  ngOnInit(): void {
    const t = this.route.snapshot.paramMap.get('token') ?? '';
    this.token.set(t);
    if (!t) {
      this.error.set(this.translate.instant('pages.invite.missingToken'));
      this.loading.set(false);
      return;
    }
    this.invites.preview(t).subscribe({
      next: (p) => {
        this.preview.set(p);
        this.name.set(p.name);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(
          this.toMessage(err) || this.translate.instant('pages.invite.invalidOrExpired'),
        );
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    if (this.busy()) return;
    const pw = this.password();
    if (pw.length < 8) {
      this.error.set(this.translate.instant('pages.invite.passwordTooShort'));
      return;
    }
    if (pw !== this.confirm()) {
      this.error.set(this.translate.instant('pages.invite.passwordMismatch'));
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.invites
      .redeem(this.token(), { password: pw, name: this.name().trim() || undefined })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.success.set(true);
          setTimeout(() => this.router.navigate(['/login']), 1500);
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(this.toMessage(err) || this.translate.instant('pages.invite.setupFailed'));
        },
      });
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
