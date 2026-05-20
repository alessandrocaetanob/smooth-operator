import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProfileService } from '../../services/profile.service';
import { AuthService } from '../../services/auth.service';
import { MfaService, MfaStatus } from '../../services/mfa.service';
import { toDataURL as qrToDataURL } from 'qrcode';

const MAX_AVATAR_BYTES = 1_048_576; // 1 MB
const TARGET_DIMENSION = 512;

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './profile.html',
  styleUrl: './profile.css',
})
export class Profile {
  private readonly profile = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly mfaSvc = inject(MfaService);

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

  readonly initials = computed(() => {
    const u = this.user();
    if (!u?.name) return '?';
    const parts = u.name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  });

  readonly currentAvatar = computed(() => this.previewUrl() ?? this.user()?.avatarUrl ?? null);

  readonly canRemove = computed(
    () => !!this.user()?.avatarUrl && !this.pendingBase64() && !this.removing(),
  );

  constructor() {
    const u = this.user();
    if (u) this.name.set(u.name);
    this.loadMfaStatus();
  }

  async onFileSelected(event: Event): Promise<void> {
    this.error.set(null);
    this.success.set(null);
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!/^image\/(png|jpeg|jpg|webp)$/.test(file.type)) {
      this.error.set('Avatar must be PNG, JPEG, or WebP.');
      input.value = '';
      return;
    }

    try {
      const { base64, mime, dataUrl } = await this.downscale(file);
      const decodedSize = Math.ceil((base64.length * 3) / 4);
      if (decodedSize > MAX_AVATAR_BYTES) {
        this.error.set('Avatar exceeds 1 MB after downscaling. Pick a simpler image.');
        input.value = '';
        return;
      }
      this.previewUrl.set(dataUrl);
      this.pendingBase64.set(base64);
      this.pendingMime.set(mime);
    } catch {
      this.error.set('Could not read that image.');
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
      this.error.set('Name is required.');
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
          this.success.set('Profile updated.');
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.error ?? 'Could not save profile.');
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
        this.success.set('Avatar removed.');
      },
      error: () => {
        this.removing.set(false);
        this.error.set('Could not remove avatar.');
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
        this.mfaError.set(err?.error?.message ?? 'Could not start MFA enrollment.');
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
        this.mfaError.set(err?.error?.message ?? 'Invalid code. Try again.');
      },
    });
  }

  downloadRecoveryCodes(): void {
    const codes = this.mfaRecoveryCodes();
    const date = new Date().toISOString().slice(0, 10);
    const content = [
      'Smooth Operator — MFA Recovery Codes',
      `Generated: ${date}`,
      '',
      'Each code can be used once to sign in if you lose your authenticator.',
      'Store this file somewhere safe and delete it after printing.',
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
        this.mfaError.set(err?.error?.message ?? 'Could not disable MFA.');
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
