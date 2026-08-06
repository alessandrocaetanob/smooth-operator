import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { SmtpSettingsService, UpdateSmtpSettingsRequest } from './smtp-settings.service';

describe('SmtpSettingsService', () => {
  let service: SmtpSettingsService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SmtpSettingsService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });

    service = TestBed.inject(SmtpSettingsService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should be created with a null current state', () => {
    expect(service).toBeTruthy();
    expect(service.current()).toBeNull();
  });

  describe('load()', () => {
    it('should make a GET request to /api/settings/smtp and update current signal', () => {
      const mockResponse = {
        host: 'smtp.example.com',
        port: 465,
        useSsl: true,
        username: 'user@example.com',
        fromAddress: 'noreply@example.com',
        fromName: null,
        enabled: true,
        configured: true,
        hasPassword: true,
        updatedAt: null,
      };

      service.load().subscribe((settings) => {
        expect(settings.host).toBe('smtp.example.com');
        expect(settings.port).toBe(465);
        expect(settings.useSsl).toBe(true);
        expect(settings.username).toBe('user@example.com');
        expect(settings.fromAddress).toBe('noreply@example.com');
        expect(settings.enabled).toBe(true);
        expect(settings.configured).toBe(true);
        expect(settings.hasPassword).toBe(true);
      });

      const req = httpTestingController.expectOne('/api/settings/smtp');
      expect(req.request.method).toBe('GET');
      req.flush(mockResponse);

      // Verify signal was updated
      expect(service.current()).toEqual(mockResponse);
    });

    it('should correctly normalize PascalCase properties from the backend', () => {
      const mockPascalResponse = {
        Host: 'smtp.pascal.com',
        Port: 25,
        UseSsl: false,
        Username: 'pascal_user',
        FromAddress: 'pascal@example.com',
        FromName: 'Pascal Sender',
        Enabled: false,
        Configured: true,
        HasPassword: false,
      };

      service.load().subscribe((settings) => {
        expect(settings.host).toBe('smtp.pascal.com');
        expect(settings.port).toBe(25);
        expect(settings.useSsl).toBe(false);
        expect(settings.username).toBe('pascal_user');
        expect(settings.fromAddress).toBe('pascal@example.com');
        expect(settings.fromName).toBe('Pascal Sender');
        expect(settings.enabled).toBe(false);
        expect(settings.configured).toBe(true);
        expect(settings.hasPassword).toBe(false);
      });

      const req = httpTestingController.expectOne('/api/settings/smtp');
      expect(req.request.method).toBe('GET');
      req.flush(mockPascalResponse);
    });

    it('should provide default values for missing properties', () => {
      service.load().subscribe((settings) => {
        expect(settings.configured).toBe(false);
        expect(settings.host).toBe('');
        expect(settings.port).toBe(587);
        expect(settings.useSsl).toBe(true);
        expect(settings.username).toBeNull();
        expect(settings.fromAddress).toBe('');
        expect(settings.fromName).toBeNull();
        expect(settings.enabled).toBe(true);
        expect(settings.hasPassword).toBe(false);
        expect(settings.updatedAt).toBeNull();
      });

      const req = httpTestingController.expectOne('/api/settings/smtp');
      req.flush({}); // Empty object to test defaults
    });
  });

  describe('update()', () => {
    it('should make a PUT request and update the signal on success', () => {
      const updatePayload: UpdateSmtpSettingsRequest = {
        host: 'new.smtp.com',
        port: 587,
        useSsl: true,
        fromAddress: 'new@example.com',
        enabled: true,
      };

      const mockResponse = {
        ...updatePayload,
        configured: true,
        hasPassword: false,
        fromName: null,
        username: null,
        updatedAt: null,
      };

      service.update(updatePayload).subscribe((settings) => {
        expect(settings.host).toBe('new.smtp.com');
        expect(settings.configured).toBe(true);
      });

      const req = httpTestingController.expectOne('/api/settings/smtp');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(updatePayload);
      req.flush(mockResponse);

      // Verify signal was updated
      expect(service.current()?.host).toBe('new.smtp.com');
    });
  });

  describe('test()', () => {
    it('should make a POST request with the target email', () => {
      const testEmail = 'test@example.com';

      service.test(testEmail).subscribe((res) => {
        expect(res.success).toBe(true);
      });

      const req = httpTestingController.expectOne('/api/settings/smtp/test');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ toEmail: testEmail });
      req.flush({ success: true });
    });
  });
});
