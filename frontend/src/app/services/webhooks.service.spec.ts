import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { Webhook, WebhookSecret, WebhooksService } from './webhooks.service';

describe('WebhooksService', () => {
  let service: WebhooksService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [WebhooksService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(WebhooksService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(service).toBeTruthy();
  });

  describe('load()', () => {
    it('GETs /api/webhooks and exposes the result through the list signal', () => {
      let result: Webhook[] | undefined;
      service.load().subscribe((r) => (result = r));

      const req = http.expectOne('/api/webhooks');
      expect(req.request.method).toBe('GET');
      req.flush([
        {
          id: 'w1',
          name: 'siem',
          url: 'https://example.com/hook',
          eventTypes: 'user.*',
          enabled: true,
          createdAt: '2026-05-22T20:00:00Z',
          consecutiveFailures: 0,
        },
      ]);

      expect(result).toHaveLength(1);
      expect(result?.[0].name).toBe('siem');
      expect(service.list()).toHaveLength(1);
    });

    it('normalizes a null response to an empty list', () => {
      let result: Webhook[] | undefined;
      service.load().subscribe((r) => (result = r));
      http.expectOne('/api/webhooks').flush(null);
      expect(result).toEqual([]);
      expect(service.list()).toEqual([]);
    });

    it('falls back to defaults when fields are missing', () => {
      let result: Webhook[] | undefined;
      service.load().subscribe((r) => (result = r));
      http.expectOne('/api/webhooks').flush([{ id: 'w1' }]);
      expect(result?.[0]).toMatchObject({
        id: 'w1',
        name: '',
        url: '',
        eventTypes: '*',
        enabled: true,
        consecutiveFailures: 0,
      });
    });
  });

  describe('create()', () => {
    it('POSTs the payload and returns the one-time secret', () => {
      let result: WebhookSecret | undefined;
      service
        .create({ name: 'siem', url: 'https://example.com/hook', eventTypes: '*' })
        .subscribe((r) => (result = r));

      const req = http.expectOne('/api/webhooks');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        name: 'siem',
        url: 'https://example.com/hook',
        eventTypes: '*',
      });
      req.flush({ id: 'w1', name: 'siem', secret: 'whsec_AbCdEf' });

      expect(result?.secret).toBe('whsec_AbCdEf');
    });
  });

  describe('update()', () => {
    it('PUTs /api/webhooks/{id}', () => {
      service
        .update('w1', {
          name: 'renamed',
          url: 'https://example.com/v2',
          eventTypes: 'connection.*',
          enabled: false,
        })
        .subscribe();

      const req = http.expectOne('/api/webhooks/w1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.enabled).toBe(false);
      req.flush(null);
    });
  });

  describe('remove()', () => {
    it('DELETEs /api/webhooks/{id}', () => {
      service.remove('w1').subscribe();
      const req = http.expectOne('/api/webhooks/w1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('rotateSecret()', () => {
    it('POSTs to /rotate-secret and returns the new secret', () => {
      let result: WebhookSecret | undefined;
      service.rotateSecret('w1').subscribe((r) => (result = r));
      const req = http.expectOne('/api/webhooks/w1/rotate-secret');
      expect(req.request.method).toBe('POST');
      req.flush({ id: 'w1', name: 'siem', secret: 'whsec_NewOne' });
      expect(result?.secret).toBe('whsec_NewOne');
    });
  });

  describe('sendTest()', () => {
    it('POSTs to /test', () => {
      service.sendTest('w1').subscribe();
      const req = http.expectOne('/api/webhooks/w1/test');
      expect(req.request.method).toBe('POST');
      req.flush(null);
    });
  });
});
