import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SystemSettingsService } from '../../../services/system-settings.service';

@Component({
  selector: 'app-sessions-settings',
  imports: [FormsModule],
  templateUrl: './sessions.html',
  styleUrl: './sessions.css',
})
export class Sessions implements OnInit {
  private readonly system = inject(SystemSettingsService);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly idleTimeoutMinutes = signal(0);
  readonly maxSessionMinutes = signal(0);

  /** Pass-through so we don't clobber the retention setting when saving. */
  private auditLogRetentionDays = 0;

  /** Common idle-timeout presets in minutes. 0 = disabled. */
  readonly idlePresets = [
    { minutes: 0, label: 'Disabled' },
    { minutes: 5, label: '5 min' },
    { minutes: 15, label: '15 min' },
    { minutes: 30, label: '30 min' },
    { minutes: 60, label: '1 hour' },
    { minutes: 240, label: '4 hours' },
  ];

  /** Common max-session presets in minutes. 0 = unlimited. */
  readonly maxPresets = [
    { minutes: 0, label: 'Unlimited' },
    { minutes: 60, label: '1 hour' },
    { minutes: 240, label: '4 hours' },
    { minutes: 480, label: '8 hours' },
    { minutes: 720, label: '12 hours' },
    { minutes: 1440, label: '24 hours' },
  ];

  selectIdlePreset(minutes: number): void {
    this.idleTimeoutMinutes.set(minutes);
  }

  selectMaxPreset(minutes: number): void {
    this.maxSessionMinutes.set(minutes);
  }

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.system.load().subscribe({
      next: (s) => {
        this.loading.set(false);
        this.idleTimeoutMinutes.set(s.idleTimeoutMinutes);
        this.maxSessionMinutes.set(s.maxSessionMinutes);
        this.auditLogRetentionDays = s.auditLogRetentionDays;
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.toMessage(err) || 'Failed to load session settings.');
      },
    });
  }

  save(): void {
    if (this.busy()) return;
    const idle = this.idleTimeoutMinutes();
    const max = this.maxSessionMinutes();
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
        auditLogRetentionDays: this.auditLogRetentionDays,
        idleTimeoutMinutes: idle,
        maxSessionMinutes: max,
      })
      .subscribe({
        next: (s) => {
          this.busy.set(false);
          this.idleTimeoutMinutes.set(s.idleTimeoutMinutes);
          this.maxSessionMinutes.set(s.maxSessionMinutes);
          const idleMsg =
            s.idleTimeoutMinutes === 0
              ? 'Idle timeout disabled.'
              : `Sessions idle for ${s.idleTimeoutMinutes} min will be closed.`;
          const maxMsg =
            s.maxSessionMinutes === 0
              ? 'No hard session lifetime cap.'
              : `Sessions capped at ${s.maxSessionMinutes} min total.`;
          this.message.set(`${idleMsg} ${maxMsg}`);
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
