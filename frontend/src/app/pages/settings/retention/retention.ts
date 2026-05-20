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
  readonly idleTimeoutMinutes = signal(0);
  readonly maxSessionMinutes = signal(0);

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
        this.idleTimeoutMinutes.set(s.idleTimeoutMinutes);
        this.maxSessionMinutes.set(s.maxSessionMinutes);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.toMessage(err) || 'Failed to load system settings.');
      },
    });
  }

  save(): void {
    if (this.busy()) return;
    const retention = this.retentionDays();
    const idle = this.idleTimeoutMinutes();
    const max = this.maxSessionMinutes();
    if (retention < 0 || retention > 3650 || !Number.isInteger(retention)) {
      this.error.set('Retention must be an integer between 0 (forever) and 3650 days.');
      return;
    }
    if (idle < 0 || idle > 10080 || !Number.isInteger(idle)) {
      this.error.set('Idle timeout must be an integer between 0 (disabled) and 10080 minutes.');
      return;
    }
    if (max < 0 || max > 10080 || !Number.isInteger(max)) {
      this.error.set('Max session must be an integer between 0 (unlimited) and 10080 minutes.');
      return;
    }
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.system
      .update({
        auditLogRetentionDays: retention,
        idleTimeoutMinutes: idle,
        maxSessionMinutes: max,
      })
      .subscribe({
        next: (s) => {
          this.busy.set(false);
          this.retentionDays.set(s.auditLogRetentionDays);
          this.idleTimeoutMinutes.set(s.idleTimeoutMinutes);
          this.maxSessionMinutes.set(s.maxSessionMinutes);
          this.message.set('Settings saved.');
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
