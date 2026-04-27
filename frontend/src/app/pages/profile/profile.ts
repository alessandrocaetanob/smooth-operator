import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProfileService } from '../../services/profile.service';
import { AuthService } from '../../services/auth.service';

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

  readonly user = this.auth.currentUser;
  readonly name = signal('');
  readonly previewUrl = signal<string | null>(null);
  readonly pendingBase64 = signal<string | null>(null);
  readonly pendingMime = signal<string | null>(null);
  readonly saving = signal(false);
  readonly removing = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

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
