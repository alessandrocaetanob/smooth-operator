import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  RecordingStorageService,
  RecordingStorageSettings,
  RecordingStorageType,
  UpdateRecordingStorageSettingsRequest,
} from '../../../services/recording-storage.service';

@Component({
  selector: 'app-recording-settings',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './recording.html',
  styleUrl: './recording.css',
})
export class RecordingSettingsPage implements OnInit {
  private readonly storage = inject(RecordingStorageService);
  private readonly translate = inject(TranslateService);

  readonly current = this.storage.current;
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly testBusy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly storageType = signal<RecordingStorageType>('Local');
  readonly localPath = signal('/var/recordings');
  readonly retentionDays = signal(90);

  // S3
  readonly s3Bucket = signal('');
  readonly s3Region = signal('');
  readonly s3Endpoint = signal('');
  readonly s3AccessKeyId = signal('');
  readonly s3SecretAccessKey = signal('');
  readonly clearS3Secret = signal(false);
  readonly s3PathPrefix = signal('');
  readonly s3ForcePathStyle = signal(false);

  // Azure
  readonly azureAccountName = signal('');
  readonly azureContainerName = signal('');
  readonly azureAccountKey = signal('');
  readonly clearAzureKey = signal(false);
  readonly azureBlobEndpoint = signal('');
  readonly azurePathPrefix = signal('');

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.storage.load().subscribe({
      next: (s) => {
        this.loading.set(false);
        this.applyToForm(s);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsRecording.errors.loadFailed'),
        );
      },
    });
  }

  private applyToForm(s: RecordingStorageSettings): void {
    this.storageType.set(s.storageType);
    this.localPath.set(s.localPath);
    this.retentionDays.set(s.retentionDays);

    this.s3Bucket.set(s.s3Bucket ?? '');
    this.s3Region.set(s.s3Region ?? '');
    this.s3Endpoint.set(s.s3Endpoint ?? '');
    this.s3AccessKeyId.set(s.s3AccessKeyId ?? '');
    this.s3PathPrefix.set(s.s3PathPrefix ?? '');
    this.s3ForcePathStyle.set(s.s3ForcePathStyle);
    this.s3SecretAccessKey.set('');
    this.clearS3Secret.set(false);

    this.azureAccountName.set(s.azureAccountName ?? '');
    this.azureContainerName.set(s.azureContainerName ?? '');
    this.azureBlobEndpoint.set(s.azureBlobEndpoint ?? '');
    this.azurePathPrefix.set(s.azurePathPrefix ?? '');
    this.azureAccountKey.set('');
    this.clearAzureKey.set(false);
  }

  save(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);

    const payload: UpdateRecordingStorageSettingsRequest = {
      storageType: this.storageType(),
      localPath: this.localPath().trim(),
      retentionDays: this.retentionDays(),
      s3Bucket: this.s3Bucket().trim() || null,
      s3Region: this.s3Region().trim() || null,
      s3Endpoint: this.s3Endpoint().trim() || null,
      s3AccessKeyId: this.s3AccessKeyId().trim() || null,
      s3SecretAccessKey: this.resolveSecretInput(this.s3SecretAccessKey(), this.clearS3Secret()),
      s3PathPrefix: this.s3PathPrefix().trim() || null,
      s3ForcePathStyle: this.s3ForcePathStyle(),
      azureAccountName: this.azureAccountName().trim() || null,
      azureContainerName: this.azureContainerName().trim() || null,
      azureAccountKey: this.resolveSecretInput(this.azureAccountKey(), this.clearAzureKey()),
      azureBlobEndpoint: this.azureBlobEndpoint().trim() || null,
      azurePathPrefix: this.azurePathPrefix().trim() || null,
    };

    this.storage.update(payload).subscribe({
      next: (s) => {
        this.busy.set(false);
        this.applyToForm(s);
        this.message.set(this.translate.instant('pages.settingsRecording.messages.saved'));
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(
          this.toMessage(err) ||
            this.translate.instant('pages.settingsRecording.errors.saveFailed'),
        );
      },
    });
  }

  testConnection(): void {
    if (this.testBusy()) return;
    this.testBusy.set(true);
    this.message.set(null);
    this.error.set(null);
    this.storage.test().subscribe({
      next: (r) => {
        this.testBusy.set(false);
        if (r.success) {
          this.message.set(this.translate.instant('pages.settingsRecording.messages.testSuccess'));
        } else {
          this.error.set(
            r.error ?? this.translate.instant('pages.settingsRecording.errors.testFailed'),
          );
        }
      },
      error: (err) => {
        this.testBusy.set(false);
        const body = err?.error;
        const msg =
          (typeof body === 'object' && body && (body.error || body.message)) ||
          this.toMessage(err) ||
          this.translate.instant('pages.settingsRecording.errors.testFailed');
        this.error.set(String(msg));
      },
    });
  }

  /**
   * null = leave server-side secret unchanged; '' = clear stored secret;
   * non-empty = replace. Mirrors the SMTP password pattern.
   */
  private resolveSecretInput(typed: string, clear: boolean): string | null | undefined {
    if (clear) return '';
    if (typed.length > 0) return typed;
    return null;
  }

  private toMessage(err: unknown): string {
    if (!err) return '';
    const anyErr = err as { error?: { message?: string }; message?: string };
    return anyErr.error?.message ?? anyErr.message ?? '';
  }
}
