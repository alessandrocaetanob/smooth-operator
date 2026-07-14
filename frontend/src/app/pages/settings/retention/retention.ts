import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SystemSettingsService } from '../../../services/system-settings.service';

@Component({
  selector: 'app-retention-settings',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './retention.html',
  styleUrl: './retention.css',
})
export class Retention implements OnInit {
  private readonly system = inject(SystemSettingsService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly retentionDays = signal(0);

  /** Pass-through values so we don't clobber session-timeout settings when saving. */
  private idleTimeoutMinutes = 0;
  private maxSessionMinutes = 0;

  readonly presets = [
    { days: 0, key: 'forever' },
    { days: 30, key: 'days30' },
    { days: 90, key: 'days90' },
    { days: 180, key: 'months6' },
    { days: 365, key: 'year1' },
    { days: 730, key: 'years2' },
  ];

  selectPreset(days: number): void {
    this.retentionDays.set(days);
  }

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.system.load().subscribe({
      next: (s) => {
        this.loading.set(false);
        this.retentionDays.set(s.auditLogRetentionDays);
        this.idleTimeoutMinutes = s.idleTimeoutMinutes;
        this.maxSessionMinutes = s.maxSessionMinutes;
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsRetention.errors.loadFailed'),
        );
      },
    });
  }

  save(): void {
    if (this.busy()) return;
    const value = this.retentionDays();
    if (value < 0 || value > 3650 || !Number.isInteger(value)) {
      this.error.set(this.translate.instant('pages.settingsRetention.errors.invalidRange'));
      return;
    }
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.system
      .update({
        auditLogRetentionDays: value,
        idleTimeoutMinutes: this.idleTimeoutMinutes,
        maxSessionMinutes: this.maxSessionMinutes,
      })
      .subscribe({
        next: (s) => {
          this.busy.set(false);
          this.retentionDays.set(s.auditLogRetentionDays);
          this.message.set(
            s.auditLogRetentionDays === 0
              ? this.translate.instant('pages.settingsRetention.messages.disabledForever')
              : this.translate.instant('pages.settingsRetention.messages.purgeScheduled', {
                  days: s.auditLogRetentionDays,
                }),
          );
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(
            this.toMessage(err) ||
              this.translate.instant('pages.settingsRetention.errors.saveFailed'),
          );
        },
      });
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
