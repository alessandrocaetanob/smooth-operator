import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SystemSettingsService } from '../../../services/system-settings.service';

@Component({
  selector: 'app-sessions-settings',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './sessions.html',
  styleUrl: './sessions.css',
})
export class Sessions implements OnInit {
  private readonly system = inject(SystemSettingsService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly idleTimeoutMinutes = signal(0);
  readonly maxSessionMinutes = signal(0);

  /** Pass-through so we don't clobber the retention setting when saving. */
  private auditLogRetentionDays = 0;

  /** Common idle-timeout presets in minutes. 0 = disabled. Labels are i18n keys. */
  readonly idlePresets = [
    { minutes: 0, label: 'pages.settingsSessions.presets.disabled' },
    { minutes: 5, label: 'pages.settingsSessions.presets.min5' },
    { minutes: 15, label: 'pages.settingsSessions.presets.min15' },
    { minutes: 30, label: 'pages.settingsSessions.presets.min30' },
    { minutes: 60, label: 'pages.settingsSessions.presets.hour1' },
    { minutes: 240, label: 'pages.settingsSessions.presets.hour4' },
  ];

  /** Common max-session presets in minutes. 0 = unlimited. Labels are i18n keys. */
  readonly maxPresets = [
    { minutes: 0, label: 'pages.settingsSessions.presets.unlimited' },
    { minutes: 60, label: 'pages.settingsSessions.presets.hour1' },
    { minutes: 240, label: 'pages.settingsSessions.presets.hour4' },
    { minutes: 480, label: 'pages.settingsSessions.presets.hour8' },
    { minutes: 720, label: 'pages.settingsSessions.presets.hour12' },
    { minutes: 1440, label: 'pages.settingsSessions.presets.hour24' },
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
        this.error.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsSessions.messages.loadFailed'),
        );
      },
    });
  }

  save(): void {
    if (this.busy()) return;
    const idle = this.idleTimeoutMinutes();
    const max = this.maxSessionMinutes();
    if (idle < 0 || idle > 10080 || !Number.isInteger(idle)) {
      this.error.set(this.translate.instant('pages.settingsSessions.messages.idleValidation'));
      return;
    }
    if (max < 0 || max > 10080 || !Number.isInteger(max)) {
      this.error.set(this.translate.instant('pages.settingsSessions.messages.maxValidation'));
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
              ? this.translate.instant('pages.settingsSessions.messages.idleDisabled')
              : this.translate.instant('pages.settingsSessions.messages.idleWillClose', {
                  minutes: s.idleTimeoutMinutes,
                });
          const maxMsg =
            s.maxSessionMinutes === 0
              ? this.translate.instant('pages.settingsSessions.messages.noHardCap')
              : this.translate.instant('pages.settingsSessions.messages.maxWillCap', {
                  minutes: s.maxSessionMinutes,
                });
          this.message.set(`${idleMsg} ${maxMsg}`);
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(
            this.toMessage(err) ||
              this.translate.instant('pages.settingsSessions.messages.saveFailed'),
          );
        },
      });
  }

  private toMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string; Message?: string }; message?: string };
    return e?.error?.message ?? e?.error?.Message ?? e?.message ?? null;
  }
}
