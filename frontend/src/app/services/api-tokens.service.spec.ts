import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { ApiTokensService, ApiTokenSummary, CreateApiTokenResult } from './api-tokens.service';

describe('ApiTokensService', () => {
  let service: ApiTokensService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApiTokensService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiTokensService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(service).toBeTruthy();
  });

  describe('list()', () => {
    it('GETs /api/api-tokens', () => {
      let result: ApiTokenSummary[] | undefined;
      service.list().subscribe((r) => (result = r));

      const req = http.expectOne('/api/api-tokens');
      expect(req.request.method).toBe('GET');
      req.flush([
        {
          id: 't1',
          name: 'ci-bot',
          createdAt: '2026-05-20T00:00:00Z',
          expiresAt: null,
          lastUsedAt: null,
          revokedAt: null,
        },
      ] satisfies ApiTokenSummary[]);

      expect(result).toHaveLength(1);
      expect(result?.[0].name).toBe('ci-bot');
    });
  });

  describe('create()', () => {
    it('POSTs payload and returns plaintext token once', () => {
      let result: CreateApiTokenResult | undefined;
      service.create({ name: 'ci-bot' }).subscribe((r) => (result = r));

      const req = http.expectOne('/api/api-tokens');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ name: 'ci-bot' });
      req.flush({
        id: 't1',
        name: 'ci-bot',
        plaintextToken: 'sop_abcdef123456_secretsecretsecret',
        createdAt: '2026-05-20T00:00:00Z',
        expiresAt: null,
      } satisfies CreateApiTokenResult);

      expect(result?.plaintextToken).toContain('sop_');
    });

    it('forwards optional expiresAt', () => {
      const exp = '2027-01-01T00:00:00Z';
      service.create({ name: 'tagged', expiresAt: exp }).subscribe();
      const req = http.expectOne('/api/api-tokens');
      expect(req.request.body).toEqual({ name: 'tagged', expiresAt: exp });
      req.flush({
        id: 'x',
        name: 'tagged',
        plaintextToken: 'sop_x_y',
        createdAt: '2026-05-20T00:00:00Z',
        expiresAt: exp,
      });
    });
  });

  describe('revoke()', () => {
    it('DELETEs /api/api-tokens/{id}', () => {
      service.revoke('abc-123').subscribe();
      const req = http.expectOne('/api/api-tokens/abc-123');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });
});
