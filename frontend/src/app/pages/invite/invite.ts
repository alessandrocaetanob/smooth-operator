import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { InvitePreview, InvitesService } from '../../services/invites.service';

@Component({
  selector: 'app-invite',
  imports: [FormsModule, RouterLink],
  templateUrl: './invite.html',
  styleUrl: './invite.css',
})
export class Invite implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly invites = inject(InvitesService);

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
      this.error.set('Missing invitation token.');
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
        this.error.set(this.toMessage(err) || 'This invitation is invalid or has expired.');
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    if (this.busy()) return;
    const pw = this.password();
    if (pw.length < 8) {
      this.error.set('Password must be at least 8 characters.');
      return;
    }
    if (pw !== this.confirm()) {
      this.error.set('Passwords do not match.');
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
          this.error.set(this.toMessage(err) || 'Failed to complete account setup.');
        },
      });
  }

  private toMessage(err: any): string | null {
    return err?.error?.message ?? err?.error?.Message ?? err?.message ?? null;
  }
}
