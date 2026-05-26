import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export type RecordingStorageType = 'Local' | 'S3' | 'AzureBlob';

export interface RecordingStorageSettings {
  configured: boolean;
  storageType: RecordingStorageType;
  localPath: string;
  s3Bucket?: string | null;
  s3Region?: string | null;
  s3Endpoint?: string | null;
  s3AccessKeyId?: string | null;
  hasS3SecretAccessKey: boolean;
  s3PathPrefix?: string | null;
  s3ForcePathStyle: boolean;
  azureAccountName?: string | null;
  azureContainerName?: string | null;
  hasAzureAccountKey: boolean;
  azureBlobEndpoint?: string | null;
  azurePathPrefix?: string | null;
  retentionDays: number;
  updatedAt?: string | null;
}

export interface UpdateRecordingStorageSettingsRequest {
  storageType: RecordingStorageType;
  localPath: string;
  s3Bucket?: string | null;
  s3Region?: string | null;
  s3Endpoint?: string | null;
  s3AccessKeyId?: string | null;
  /** null = keep existing; '' = clear; non-empty = replace */
  s3SecretAccessKey?: string | null;
  s3PathPrefix?: string | null;
  s3ForcePathStyle: boolean;
  azureAccountName?: string | null;
  azureContainerName?: string | null;
  /** null = keep existing; '' = clear; non-empty = replace */
  azureAccountKey?: string | null;
  azureBlobEndpoint?: string | null;
  azurePathPrefix?: string | null;
  retentionDays: number;
}

export interface RecordingStorageTestResult {
  success: boolean;
  error?: string | null;
}

@Injectable({ providedIn: 'root' })
export class RecordingStorageService {
  private readonly http = inject(HttpClient);
  private readonly _current = signal<RecordingStorageSettings | null>(null);
  readonly current = this._current.asReadonly();

  load(): Observable<RecordingStorageSettings> {
    return this.http.get<RawRecord>('/api/settings/recording-storage').pipe(
      map((r) => this.normalize(r)),
      tap((s) => this._current.set(s)),
    );
  }

  update(payload: UpdateRecordingStorageSettingsRequest): Observable<RecordingStorageSettings> {
    return this.http.put<RawRecord>('/api/settings/recording-storage', payload).pipe(
      map((r) => this.normalize(r)),
      tap((s) => this._current.set(s)),
    );
  }

  test(): Observable<RecordingStorageTestResult> {
    return this.http.post<RecordingStorageTestResult>('/api/settings/recording-storage/test', {});
  }

  private normalize(raw: RawRecord): RecordingStorageSettings {
    return {
      configured: pickOr(raw, false, 'configured', 'Configured'),
      storageType: pickOr<RecordingStorageType>(raw, 'Local', 'storageType', 'StorageType'),
      localPath: pickOr(raw, '/var/recordings', 'localPath', 'LocalPath'),
      s3Bucket: pickOr(raw, null as string | null, 's3Bucket', 'S3Bucket'),
      s3Region: pickOr(raw, null as string | null, 's3Region', 'S3Region'),
      s3Endpoint: pickOr(raw, null as string | null, 's3Endpoint', 'S3Endpoint'),
      s3AccessKeyId: pickOr(raw, null as string | null, 's3AccessKeyId', 'S3AccessKeyId'),
      hasS3SecretAccessKey: pickOr(raw, false, 'hasS3SecretAccessKey', 'HasS3SecretAccessKey'),
      s3PathPrefix: pickOr(raw, null as string | null, 's3PathPrefix', 'S3PathPrefix'),
      s3ForcePathStyle: pickOr(raw, false, 's3ForcePathStyle', 'S3ForcePathStyle'),
      azureAccountName: pickOr(raw, null as string | null, 'azureAccountName', 'AzureAccountName'),
      azureContainerName: pickOr(
        raw,
        null as string | null,
        'azureContainerName',
        'AzureContainerName',
      ),
      hasAzureAccountKey: pickOr(raw, false, 'hasAzureAccountKey', 'HasAzureAccountKey'),
      azureBlobEndpoint: pickOr(
        raw,
        null as string | null,
        'azureBlobEndpoint',
        'AzureBlobEndpoint',
      ),
      azurePathPrefix: pickOr(raw, null as string | null, 'azurePathPrefix', 'AzurePathPrefix'),
      retentionDays: pickOr(raw, 90, 'retentionDays', 'RetentionDays'),
      updatedAt: pickOr(raw, null as string | null, 'updatedAt', 'UpdatedAt'),
    };
  }
}
