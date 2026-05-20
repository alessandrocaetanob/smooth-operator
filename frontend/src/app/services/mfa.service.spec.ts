import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { MfaService } from './mfa.service';

describe('MfaService', () => {
  let service: MfaService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MfaService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MfaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(service).toBeTruthy();
  });

  describe('getStatus()', () => {
    it('GETs /api/mfa/status and parses camelCase keys', () => {
      let result: { isEnabled: boolean; recoveryCodesRemaining: number } | undefined;
      service.getStatus().subscribe((s) => (result = s));

      const req = http.expectOne('/api/mfa/status');
      expect(req.request.method).toBe('GET');
      req.flush({ isEnabled: true, recoveryCodesRemaining: 7 });
      expect(result).toEqual({ isEnabled: true, recoveryCodesRemaining: 7 });
    });

    it('parses PascalCase fallback keys', () => {
      let result: { isEnabled: boolean; recoveryCodesRemaining: number } | undefined;
      service.getStatus().subscribe((s) => (result = s));
      http.expectOne('/api/mfa/status').flush({ IsEnabled: true, RecoveryCodesRemaining: 3 });
      expect(result).toEqual({ isEnabled: true, recoveryCodesRemaining: 3 });
    });

    it('returns defaults for empty response body', () => {
      let result: { isEnabled: boolean; recoveryCodesRemaining: number } | undefined;
      service.getStatus().subscribe((s) => (result = s));
      http.expectOne('/api/mfa/status').flush({});
      expect(result).toEqual({ isEnabled: false, recoveryCodesRemaining: 0 });
    });
  });

  describe('enroll()', () => {
    it('POSTs empty body and parses camelCase response', () => {
      let result: { secretBase32: string; otpAuthUri: string } | undefined;
      service.enroll().subscribe((r) => (result = r));

      const req = http.expectOne('/api/mfa/enroll');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      req.flush({ secretBase32: 'JBSWY3DPEHPK3PXP', otpAuthUri: 'otpauth://totp/x' });
      expect(result).toEqual({ secretBase32: 'JBSWY3DPEHPK3PXP', otpAuthUri: 'otpauth://totp/x' });
    });

    it('parses PascalCase fallback keys', () => {
      let result: { secretBase32: string; otpAuthUri: string } | undefined;
      service.enroll().subscribe((r) => (result = r));
      http
        .expectOne('/api/mfa/enroll')
        .flush({ SecretBase32: 'AAAA', OtpAuthUri: 'otpauth://totp/y' });
      expect(result).toEqual({ secretBase32: 'AAAA', otpAuthUri: 'otpauth://totp/y' });
    });

    it('returns empty strings when keys missing', () => {
      let result: { secretBase32: string; otpAuthUri: string } | undefined;
      service.enroll().subscribe((r) => (result = r));
      http.expectOne('/api/mfa/enroll').flush({});
      expect(result).toEqual({ secretBase32: '', otpAuthUri: '' });
    });
  });

  describe('confirm()', () => {
    it('POSTs { code } to /api/mfa/confirm and returns recoveryCodes', () => {
      let result: { recoveryCodes: string[] } | undefined;
      service.confirm('123456').subscribe((r) => (result = r));

      const req = http.expectOne('/api/mfa/confirm');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ code: '123456' });
      req.flush({ recoveryCodes: ['AAAA-BBBB', 'CCCC-DDDD'] });
      expect(result).toEqual({ recoveryCodes: ['AAAA-BBBB', 'CCCC-DDDD'] });
    });

    it('parses PascalCase fallback key', () => {
      let result: { recoveryCodes: string[] } | undefined;
      service.confirm('000000').subscribe((r) => (result = r));
      http.expectOne('/api/mfa/confirm').flush({ RecoveryCodes: ['ZZZZ-YYYY'] });
      expect(result).toEqual({ recoveryCodes: ['ZZZZ-YYYY'] });
    });

    it('returns empty array when recoveryCodes key missing', () => {
      let result: { recoveryCodes: string[] } | undefined;
      service.confirm('000000').subscribe((r) => (result = r));
      http.expectOne('/api/mfa/confirm').flush({});
      expect(result).toEqual({ recoveryCodes: [] });
    });
  });

  describe('disable()', () => {
    it('POSTs to /api/mfa/disable with { password }', () => {
      service.disable('hunter2').subscribe();
      const req = http.expectOne('/api/mfa/disable');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ password: 'hunter2' });
      req.flush(null);
    });
  });
});
