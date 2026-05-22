import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import {
  SystemSettingsService,
  SystemSettings,
  UpdateSystemSettingsRequest,
} from './system-settings.service';

describe('SystemSettingsService', () => {
  let service: SystemSettingsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SystemSettingsService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SystemSettingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with null current signal', () => {
    expect(service).toBeTruthy();
    expect(service.current()).toBeNull();
  });

  describe('load()', () => {
    it('should GET /api/settings/system and update current signal', () => {
      let result: SystemSettings | undefined;
      service.load().subscribe((s) => (result = s));

      const req = http.expectOne('/api/settings/system');
      expect(req.request.method).toBe('GET');
      req.flush({ auditLogRetentionDays: 90, updatedAt: '2024-01-01T00:00:00Z' });

      expect(result?.auditLogRetentionDays).toBe(90);
      expect(result?.updatedAt).toBe('2024-01-01T00:00:00Z');
      expect(service.current()?.auditLogRetentionDays).toBe(90);
    });

    it('should normalize PascalCase response', () => {
      let result: SystemSettings | undefined;
      service.load().subscribe((s) => (result = s));

      http
        .expectOne('/api/settings/system')
        .flush({ AuditLogRetentionDays: 30, UpdatedAt: '2024-06-01T00:00:00Z' });

      expect(result?.auditLogRetentionDays).toBe(30);
      expect(result?.updatedAt).toBe('2024-06-01T00:00:00Z');
      expect(service.current()?.auditLogRetentionDays).toBe(30);
    });

    it('should default to 0 for missing retention days', () => {
      let result: SystemSettings | undefined;
      service.load().subscribe((s) => (result = s));
      http.expectOne('/api/settings/system').flush({});
      expect(result?.auditLogRetentionDays).toBe(0);
    });

    it('should set null for missing updatedAt', () => {
      service.load().subscribe();
      http.expectOne('/api/settings/system').flush({ auditLogRetentionDays: 30 });
      expect(service.current()?.updatedAt).toBeNull();
    });
  });

  describe('update()', () => {
    it('should PUT /api/settings/system with payload and update current signal', () => {
      const payload: UpdateSystemSettingsRequest = {
        auditLogRetentionDays: 180,
        idleTimeoutMinutes: 0,
        maxSessionMinutes: 0,
      };
      let result: SystemSettings | undefined;
      service.update(payload).subscribe((s) => (result = s));

      const req = http.expectOne('/api/settings/system');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(payload);
      req.flush({
        auditLogRetentionDays: 180,
        idleTimeoutMinutes: 0,
        maxSessionMinutes: 0,
        updatedAt: '2024-06-15T00:00:00Z',
      });

      expect(result?.auditLogRetentionDays).toBe(180);
      expect(service.current()?.auditLogRetentionDays).toBe(180);
    });

    it('should normalize PascalCase update response', () => {
      let result: SystemSettings | undefined;
      service
        .update({ auditLogRetentionDays: 365, idleTimeoutMinutes: 0, maxSessionMinutes: 0 })
        .subscribe((s) => (result = s));

      http
        .expectOne('/api/settings/system')
        .flush({ AuditLogRetentionDays: 365, UpdatedAt: '2024-07-01T00:00:00Z' });

      expect(result?.auditLogRetentionDays).toBe(365);
    });

    it('should update current signal after PUT', () => {
      service
        .update({ auditLogRetentionDays: 45, idleTimeoutMinutes: 0, maxSessionMinutes: 0 })
        .subscribe();
      http.expectOne('/api/settings/system').flush({ auditLogRetentionDays: 45, updatedAt: null });
      expect(service.current()?.auditLogRetentionDays).toBe(45);
    });

    it('round-trips idleTimeoutMinutes and maxSessionMinutes', () => {
      let result: SystemSettings | undefined;
      service
        .update({ auditLogRetentionDays: 0, idleTimeoutMinutes: 15, maxSessionMinutes: 480 })
        .subscribe((s) => (result = s));
      http.expectOne('/api/settings/system').flush({
        auditLogRetentionDays: 0,
        idleTimeoutMinutes: 15,
        maxSessionMinutes: 480,
        updatedAt: '2026-01-01T00:00:00Z',
      });
      expect(result?.idleTimeoutMinutes).toBe(15);
      expect(result?.maxSessionMinutes).toBe(480);
    });
  });
});
