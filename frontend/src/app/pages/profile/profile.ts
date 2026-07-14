import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ProfileService } from '../../services/profile.service';
import { AuthService } from '../../services/auth.service';
import { MfaService, MfaStatus } from '../../services/mfa.service';
import {
  ApiTokensService,
  ApiTokenSummary,
  CreateApiTokenResult,
} from '../../services/api-tokens.service';
import { toDataURL as qrToDataURL } from 'qrcode';

const MAX_AVATAR_BYTES = 1_048_576; // 1 MB
const TARGET_DIMENSION = 512;

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [FormsModule, DatePipe, TranslatePipe],
  templateUrl: './profile.html',
  styleUrl: './profile.css',
})
export class Profile {
  private readonly profile = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly mfaSvc = inject(MfaService);
  private readonly apiTokensSvc = inject(ApiTokensService);
  private readonly translate = inject(TranslateService);

  readonly user = this.auth.currentUser;
  readonly name = signal('');
  readonly previewUrl = signal<string | null>(null);
  readonly pendingBase64 = signal<string | null>(null);
  readonly pendingMime = signal<string | null>(null);
  readonly saving = signal(false);
  readonly removing = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  // MFA state
  readonly mfaStatus = signal<MfaStatus | null>(null);
  readonly mfaEnrollStep = signal<'idle' | 'setup' | 'codes'>('idle');
  readonly mfaQrDataUrl = signal<string | null>(null);
  readonly mfaSecretBase32 = signal<string | null>(null);
  readonly mfaRecoveryCodes = signal<string[]>([]);
  readonly mfaConfirmCode = signal('');
  readonly mfaEnrolling = signal(false);
  readonly mfaConfirming = signal(false);
  readonly mfaDisableStep = signal<'idle' | 'confirm'>('idle');
  readonly mfaDisablePassword = signal('');
  readonly mfaDisabling = signal(false);
  readonly mfaError = signal<string | null>(null);

  // API tokens state
  readonly apiTokens = signal<ApiTokenSummary[]>([]);
  readonly newTokenName = signal('');
  readonly newTokenExpiresAt = signal('');
  readonly creatingToken = signal(false);
  readonly revokingTokenId = signal<string | null>(null);
  readonly newTokenResult = signal<CreateApiTokenResult | null>(null);
  readonly apiTokensError = signal<string | null>(null);

  readonly initials = computed(() => {
    const u = this.user();
    if (!u?.name) return '?';
    const parts = u.name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  });

  readonly currentAvatar = computed(() => this.previewUrl() ?? this.user()?.avatarUrl ?? null);

  /** Current UTC ISO timestamp; used for "expired?" comparisons in the template. */
  readonly currentIso = (): string => new Date().toISOString();

  readonly canRemove = computed(
    () => !!this.user()?.avatarUrl && !this.pendingBase64() && !this.removing(),
  );

  constructor() {
    const u = this.user();
    if (u) this.name.set(u.name);
    this.loadMfaStatus();
    this.loadApiTokens();
  }

  async onFileSelected(event: Event): Promise<void> {
    this.error.set(null);
    this.success.set(null);
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!/^image\/(png|jpeg|jpg|webp)$/.test(file.type)) {
      this.error.set(this.translate.instant('pages.profile.avatar.invalidType'));
      input.value = '';
      return;
    }

    try {
      const { base64, mime, dataUrl } = await this.downscale(file);
      const decodedSize = Math.ceil((base64.length * 3) / 4);
      if (decodedSize > MAX_AVATAR_BYTES) {
        this.error.set(this.translate.instant('pages.profile.avatar.tooLarge'));
        input.value = '';
        return;
      }
      this.previewUrl.set(dataUrl);
      this.pendingBase64.set(base64);
      this.pendingMime.set(mime);
    } catch {
      this.error.set(this.translate.instant('pages.profile.avatar.readError'));
    } finally {
      input.value = '';
    }
  }

  cancelPending(): void {
    this.previewUrl.set(null);
    this.pendingBase64.set(null);
    this.pendingMime.set(null);
  }

  save(): void {
    const trimmed = this.name().trim();
    if (!trimmed) {
      this.error.set(this.translate.instant('pages.profile.name.required'));
      return;
    }
    this.error.set(null);
    this.success.set(null);
    this.saving.set(true);
    this.profile
      .updateProfile({
        name: trimmed,
        avatarBase64: this.pendingBase64(),
        avatarMimeType: this.pendingMime(),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.previewUrl.set(null);
          this.pendingBase64.set(null);
          this.pendingMime.set(null);
          this.success.set(this.translate.instant('pages.profile.saved'));
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.error ?? this.translate.instant('pages.profile.saveError'));
        },
      });
  }

  removeAvatar(): void {
    this.error.set(null);
    this.success.set(null);
    this.removing.set(true);
    this.profile.removeAvatar().subscribe({
      next: () => {
        this.removing.set(false);
        this.success.set(this.translate.instant('pages.profile.avatar.removed'));
      },
      error: () => {
        this.removing.set(false);
        this.error.set(this.translate.instant('pages.profile.avatar.removeError'));
      },
    });
  }

  // ── MFA methods ──────────────────────────────────────────────────────────

  loadMfaStatus(): void {
    this.mfaSvc.getStatus().subscribe({
      next: (status) => this.mfaStatus.set(status),
      error: () => this.mfaStatus.set({ isEnabled: false, recoveryCodesRemaining: 0 }),
    });
  }

  startMfaEnrollment(): void {
    this.mfaError.set(null);
    this.mfaEnrolling.set(true);
    this.mfaSvc.enroll().subscribe({
      next: async (result) => {
        this.mfaSecretBase32.set(result.secretBase32);
        try {
          const dataUrl = await qrToDataURL(result.otpAuthUri, { width: 200, margin: 2 });
          this.mfaQrDataUrl.set(dataUrl);
        } catch {
          this.mfaQrDataUrl.set(null);
        }
        this.mfaEnrolling.set(false);
        this.mfaEnrollStep.set('setup');
      },
      error: (err) => {
        this.mfaEnrolling.set(false);
        this.mfaError.set(
          err?.error?.message ?? this.translate.instant('pages.profile.mfa.startError'),
        );
      },
    });
  }

  confirmMfaEnrollment(): void {
    const code = this.mfaConfirmCode().trim();
    if (!code) return;
    this.mfaError.set(null);
    this.mfaConfirming.set(true);
    this.mfaSvc.confirm(code).subscribe({
      next: (result) => {
        this.mfaConfirming.set(false);
        this.mfaRecoveryCodes.set(result.recoveryCodes);
        this.mfaEnrollStep.set('codes');
      },
      error: (err) => {
        this.mfaConfirming.set(false);
        this.mfaError.set(
          err?.error?.message ?? this.translate.instant('pages.profile.mfa.invalidCode'),
        );
      },
    });
  }

  downloadRecoveryCodes(): void {
    const codes = this.mfaRecoveryCodes();
    const date = new Date().toISOString().slice(0, 10);
    const content = [
      this.translate.instant('pages.profile.mfa.recoveryFile.title'),
      this.translate.instant('pages.profile.mfa.recoveryFile.generated', { date }),
      '',
      this.translate.instant('pages.profile.mfa.recoveryFile.instructions'),
      this.translate.instant('pages.profile.mfa.recoveryFile.storeSafely'),
      '',
      ...codes,
      '',
    ].join('\n');
    const blob = new Blob([content], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `smooth-operator-recovery-codes-${date}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  }

  finishMfaEnrollment(): void {
    this.mfaEnrollStep.set('idle');
    this.mfaQrDataUrl.set(null);
    this.mfaSecretBase32.set(null);
    this.mfaRecoveryCodes.set([]);
    this.mfaConfirmCode.set('');
    this.loadMfaStatus();
  }

  cancelMfaEnrollment(): void {
    this.mfaEnrollStep.set('idle');
    this.mfaQrDataUrl.set(null);
    this.mfaSecretBase32.set(null);
    this.mfaConfirmCode.set('');
    this.mfaError.set(null);
  }

  startMfaDisable(): void {
    this.mfaDisablePassword.set('');
    this.mfaError.set(null);
    this.mfaDisableStep.set('confirm');
  }

  cancelMfaDisable(): void {
    this.mfaDisableStep.set('idle');
    this.mfaDisablePassword.set('');
    this.mfaError.set(null);
  }

  confirmMfaDisable(): void {
    const password = this.mfaDisablePassword().trim();
    if (!password) return;
    this.mfaError.set(null);
    this.mfaDisabling.set(true);
    this.mfaSvc.disable(password).subscribe({
      next: () => {
        this.mfaDisabling.set(false);
        this.mfaDisableStep.set('idle');
        this.mfaDisablePassword.set('');
        this.loadMfaStatus();
      },
      error: (err) => {
        this.mfaDisabling.set(false);
        this.mfaError.set(
          err?.error?.message ?? this.translate.instant('pages.profile.mfa.disableError'),
        );
      },
    });
  }

  // ── API tokens methods ──────────────────────────────────────────────────

  loadApiTokens(): void {
    this.apiTokensSvc.list().subscribe({
      next: (tokens) => this.apiTokens.set(tokens),
      error: () => this.apiTokens.set([]),
    });
  }

  createApiToken(): void {
    const name = this.newTokenName().trim();
    if (!name) return;
    this.apiTokensError.set(null);
    this.creatingToken.set(true);
    const expiresAt = this.newTokenExpiresAt() || null;
    this.apiTokensSvc.create({ name, expiresAt }).subscribe({
      next: (result) => {
        this.creatingToken.set(false);
        this.newTokenResult.set(result);
        this.newTokenName.set('');
        this.newTokenExpiresAt.set('');
        this.loadApiTokens();
      },
      error: (err) => {
        this.creatingToken.set(false);
        this.apiTokensError.set(
          err?.error?.message ?? this.translate.instant('pages.profile.apiTokens.createError'),
        );
      },
    });
  }

  dismissNewToken(): void {
    this.newTokenResult.set(null);
  }

  async copyNewToken(): Promise<void> {
    const plaintext = this.newTokenResult()?.plaintextToken;
    if (!plaintext) return;
    try {
      await navigator.clipboard.writeText(plaintext);
    } catch {
      // Clipboard API may be unavailable (non-HTTPS, permission denied) — silently ignore.
    }
  }

  revokeApiToken(id: string): void {
    this.apiTokensError.set(null);
    this.revokingTokenId.set(id);
    this.apiTokensSvc.revoke(id).subscribe({
      next: () => {
        this.revokingTokenId.set(null);
        this.loadApiTokens();
      },
      error: (err) => {
        this.revokingTokenId.set(null);
        this.apiTokensError.set(
          err?.error?.message ?? this.translate.instant('pages.profile.apiTokens.revokeError'),
        );
      },
    });
  }

  private downscale(file: File): Promise<{ base64: string; mime: string; dataUrl: string }> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const img = new Image();
        img.onload = () => {
          const canvas = document.createElement('canvas');
          const max = Math.max(img.width, img.height);
          const scale = max > TARGET_DIMENSION ? TARGET_DIMENSION / max : 1;
          canvas.width = Math.round(img.width * scale);
          canvas.height = Math.round(img.height * scale);
          const ctx = canvas.getContext('2d');
          if (!ctx) {
            reject(new Error('canvas-2d-unavailable'));
            return;
          }
          ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

          const mime = file.type === 'image/png' ? 'image/png' : 'image/jpeg';
          const dataUrl = canvas.toDataURL(mime, 0.85);
          const base64 = dataUrl.split(',')[1] ?? '';
          resolve({ base64, mime, dataUrl });
        };
        img.onerror = () => reject(new Error('image-load-failed'));
        img.src = reader.result as string;
      };
      reader.onerror = () => reject(new Error('file-read-failed'));
      reader.readAsDataURL(file);
    });
  }
}
