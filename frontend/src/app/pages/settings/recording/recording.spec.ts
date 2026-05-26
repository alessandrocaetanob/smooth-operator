import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { RecordingSettingsPage } from './recording';
import {
  RecordingStorageService,
  RecordingStorageSettings,
} from '../../../services/recording-storage.service';

describe('RecordingSettingsPage', () => {
  let component: RecordingSettingsPage;
  let fixture: ComponentFixture<RecordingSettingsPage>;
  let storage: {
    current: ReturnType<typeof signal<RecordingStorageSettings | null>>;
    load: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    test: ReturnType<typeof vi.fn>;
  };

  const defaults = (over: Partial<RecordingStorageSettings> = {}): RecordingStorageSettings => ({
    configured: true,
    storageType: 'Local',
    localPath: '/var/recordings',
    s3Bucket: null,
    s3Region: null,
    s3Endpoint: null,
    s3AccessKeyId: null,
    hasS3SecretAccessKey: false,
    s3PathPrefix: null,
    s3ForcePathStyle: false,
    azureAccountName: null,
    azureContainerName: null,
    hasAzureAccountKey: false,
    azureBlobEndpoint: null,
    azurePathPrefix: null,
    retentionDays: 90,
    updatedAt: null,
    ...over,
  });

  beforeEach(async () => {
    storage = {
      current: signal<RecordingStorageSettings | null>(null),
      load: vi.fn(() => of(defaults())),
      update: vi.fn(() => of(defaults())),
      test: vi.fn(() => of({ success: true, error: null })),
    };

    await TestBed.configureTestingModule({
      imports: [RecordingSettingsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RecordingStorageService, useValue: storage },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(RecordingSettingsPage);
    component = fixture.componentInstance;
  });

  describe('refresh', () => {
    it('hydrates form signals from the server response on ngOnInit', () => {
      storage.load.mockReturnValueOnce(
        of(
          defaults({
            storageType: 'S3',
            s3Bucket: 'b',
            s3Region: 'us-east-1',
            s3AccessKeyId: 'AKIA',
            hasS3SecretAccessKey: true,
            s3PathPrefix: 'prod',
            s3ForcePathStyle: true,
            retentionDays: 30,
          }),
        ),
      );
      component.ngOnInit();
      expect(component.storageType()).toBe('S3');
      expect(component.s3Bucket()).toBe('b');
      expect(component.s3Region()).toBe('us-east-1');
      expect(component.s3AccessKeyId()).toBe('AKIA');
      expect(component.s3PathPrefix()).toBe('prod');
      expect(component.s3ForcePathStyle()).toBe(true);
      expect(component.retentionDays()).toBe(30);
      // The secret stays blank on the form — server never returns it.
      expect(component.s3SecretAccessKey()).toBe('');
      expect(component.clearS3Secret()).toBe(false);
      expect(component.loading()).toBe(false);
    });

    it('hydrates the Azure block when storageType=AzureBlob', () => {
      storage.load.mockReturnValueOnce(
        of(
          defaults({
            storageType: 'AzureBlob',
            azureAccountName: 'acct',
            azureContainerName: 'container',
            azureBlobEndpoint: 'https://acct.blob.core.windows.net',
            azurePathPrefix: 'prefix',
            hasAzureAccountKey: true,
          }),
        ),
      );
      component.refresh();
      expect(component.storageType()).toBe('AzureBlob');
      expect(component.azureAccountName()).toBe('acct');
      expect(component.azureContainerName()).toBe('container');
      expect(component.azureBlobEndpoint()).toBe('https://acct.blob.core.windows.net');
      expect(component.azurePathPrefix()).toBe('prefix');
      expect(component.azureAccountKey()).toBe('');
      expect(component.clearAzureKey()).toBe(false);
    });

    it('sets error message when load fails', () => {
      storage.load.mockReturnValueOnce(throwError(() => ({ message: 'denied' })));
      component.refresh();
      expect(component.error()).toBe('denied');
      expect(component.loading()).toBe(false);
    });

    it('falls back to default error', () => {
      storage.load.mockReturnValueOnce(throwError(() => ({})));
      component.refresh();
      expect(component.error()).toBe('Failed to load recording storage settings.');
    });
  });

  describe('save', () => {
    it('no-ops when busy', () => {
      component.busy.set(true);
      component.save();
      expect(storage.update).not.toHaveBeenCalled();
    });

    it('trims string inputs and submits the payload', () => {
      component.storageType.set('S3');
      component.localPath.set('  /var/recordings  ');
      component.s3Bucket.set('  bucket  ');
      component.s3Region.set('us-east-1');
      component.s3AccessKeyId.set('AKIA');
      component.s3SecretAccessKey.set('plain-secret');
      component.s3PathPrefix.set('prefix');
      component.s3ForcePathStyle.set(true);
      component.retentionDays.set(45);
      component.save();
      expect(storage.update).toHaveBeenCalledWith(
        expect.objectContaining({
          storageType: 'S3',
          localPath: '/var/recordings',
          s3Bucket: 'bucket',
          s3Region: 'us-east-1',
          s3AccessKeyId: 'AKIA',
          s3SecretAccessKey: 'plain-secret',
          s3PathPrefix: 'prefix',
          s3ForcePathStyle: true,
          retentionDays: 45,
        }),
      );
    });

    it('passes "" for s3 secret when clearS3Secret is true', () => {
      component.storageType.set('S3');
      component.s3SecretAccessKey.set('typed-but-clearing');
      component.clearS3Secret.set(true);
      component.save();
      expect(storage.update).toHaveBeenCalledWith(
        expect.objectContaining({ s3SecretAccessKey: '' }),
      );
    });

    it('passes null for s3 secret when no input and clear flag is false (keep existing)', () => {
      component.storageType.set('S3');
      component.s3SecretAccessKey.set('');
      component.clearS3Secret.set(false);
      component.save();
      expect(storage.update).toHaveBeenCalledWith(
        expect.objectContaining({ s3SecretAccessKey: null }),
      );
    });

    it('mirrors the secret-input semantics for Azure key', () => {
      component.storageType.set('AzureBlob');
      component.azureAccountKey.set('new-key');
      component.clearAzureKey.set(false);
      component.save();
      expect(storage.update).toHaveBeenCalledWith(
        expect.objectContaining({ azureAccountKey: 'new-key' }),
      );
    });

    it('shows a success message and re-hydrates the form on success', () => {
      storage.update.mockReturnValueOnce(of(defaults({ retentionDays: 7 })));
      component.save();
      expect(component.busy()).toBe(false);
      expect(component.message()).toBe('Recording storage settings saved.');
      expect(component.retentionDays()).toBe(7);
    });

    it('surfaces server error', () => {
      storage.update.mockReturnValueOnce(throwError(() => ({ message: 'bad' })));
      component.save();
      expect(component.error()).toBe('bad');
      expect(component.busy()).toBe(false);
    });

    it('falls back to default error message', () => {
      storage.update.mockReturnValueOnce(throwError(() => ({})));
      component.save();
      expect(component.error()).toBe('Failed to save recording storage settings.');
    });
  });

  describe('testConnection', () => {
    it('no-ops when testBusy', () => {
      component.testBusy.set(true);
      component.testConnection();
      expect(storage.test).not.toHaveBeenCalled();
    });

    it('sets success message when service returns success', () => {
      storage.test.mockReturnValueOnce(of({ success: true, error: null }));
      component.testConnection();
      expect(component.message()).toBe('Storage backend responded successfully.');
      expect(component.error()).toBeNull();
      expect(component.testBusy()).toBe(false);
    });

    it('sets error from response when success=false', () => {
      storage.test.mockReturnValueOnce(of({ success: false, error: 'bucket missing' }));
      component.testConnection();
      expect(component.error()).toBe('bucket missing');
      expect(component.message()).toBeNull();
    });

    it('falls back to generic test-failed message when error is null', () => {
      storage.test.mockReturnValueOnce(of({ success: false, error: null }));
      component.testConnection();
      expect(component.error()).toBe('Storage test failed.');
    });

    it('handles thrown error with body.error', () => {
      storage.test.mockReturnValueOnce(
        throwError(() => ({ error: { error: 'permission denied' } })),
      );
      component.testConnection();
      expect(component.error()).toBe('permission denied');
    });

    it('handles thrown error with body.message', () => {
      storage.test.mockReturnValueOnce(
        throwError(() => ({ error: { message: 'remote rejected' } })),
      );
      component.testConnection();
      expect(component.error()).toBe('remote rejected');
    });

    it('falls back to generic message when error body is empty', () => {
      storage.test.mockReturnValueOnce(throwError(() => ({})));
      component.testConnection();
      expect(component.error()).toBe('Storage test failed.');
    });
  });
});
