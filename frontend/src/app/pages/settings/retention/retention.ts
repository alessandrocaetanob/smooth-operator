import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SystemSettingsService } from '../../../services/system-settings.service';

@Component({
  selector: 'app-retention-settings',
  imports: [FormsModule],
  templateUrl: './retention.html',
  styleUrl: './retention.css',
})
export class Retention implements OnInit {
  private readonly system = inject(SystemSettingsService);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly retentionDays = signal(0);

  readonly presets = [
    { days: 0, label: 'Forever' },
    { days: 30, label: '30 days' },
    { days: 90, label: '90 days' },
    { days: 180, label: '6 months' },
    { days: 365, label: '1 year' },
    { days: 730, label: '2 years' },
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
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.toMessage(err) || 'Failed to load system settings.');
      },
    });
  }

  save(): void {
    if (this.busy()) return;
    const value = this.retentionDays();
    if (value < 0 || value > 3650 || !Number.isInteger(value)) {
      this.error.set('Retention must be an integer between 0 (forever) and 3650 days.');
      return;
    }
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.system.update({ auditLogRetentionDays: value }).subscribe({
      next: (s) => {
        this.busy.set(false);
        this.retentionDays.set(s.auditLogRetentionDays);
        this.message.set(
          s.auditLogRetentionDays === 0
            ? 'Retention disabled — audit logs will be kept forever.'
            : `Audit logs older than ${s.auditLogRetentionDays} day(s) will be purged daily.`,
        );
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(this.toMessage(err) || 'Failed to save settings.');
      },
    });
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
