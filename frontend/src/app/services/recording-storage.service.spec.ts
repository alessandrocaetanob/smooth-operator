import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import {
  RecordingStorageService,
  UpdateRecordingStorageSettingsRequest,
} from './recording-storage.service';

describe('RecordingStorageService', () => {
  let service: RecordingStorageService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        RecordingStorageService,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(RecordingStorageService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('load() GETs and writes current signal', () => {
    let result: unknown;
    service.load().subscribe((r) => (result = r));

    http.expectOne('/api/settings/recording-storage').flush({
      Configured: true,
      StorageType: 'S3',
      LocalPath: '/var/recordings',
      S3Bucket: 'my-bucket',
      HasS3SecretAccessKey: true,
      RetentionDays: 30,
    });

    const r = result as { configured: boolean; storageType: string; retentionDays: number };
    expect(r.configured).toBe(true);
    expect(r.storageType).toBe('S3');
    expect(r.retentionDays).toBe(30);
    expect(service.current()).not.toBeNull();
    expect(service.current()!.storageType).toBe('S3');
  });

  it('load() defaults storageType to Local when missing', () => {
    let result: unknown;
    service.load().subscribe((r) => (result = r));
    http.expectOne('/api/settings/recording-storage').flush({});
    const r = result as { storageType: string; retentionDays: number };
    expect(r.storageType).toBe('Local');
    expect(r.retentionDays).toBe(90);
  });

  it('update() PUTs with payload and writes current signal', () => {
    const payload: UpdateRecordingStorageSettingsRequest = {
      storageType: 'Local',
      localPath: '/var/recordings',
      retentionDays: 14,
      s3ForcePathStyle: false,
    };

    service.update(payload).subscribe();
    const req = http.expectOne('/api/settings/recording-storage');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush({ Configured: true, StorageType: 'Local', RetentionDays: 14 });

    expect(service.current()!.retentionDays).toBe(14);
  });

  it('test() POSTs to /test', () => {
    let result: unknown;
    service.test().subscribe((r) => (result = r));
    const req = http.expectOne('/api/settings/recording-storage/test');
    expect(req.request.method).toBe('POST');
    req.flush({ success: true });
    const r = result as { success: boolean };
    expect(r.success).toBe(true);
  });
});
