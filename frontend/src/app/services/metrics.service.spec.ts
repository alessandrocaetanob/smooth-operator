import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { MetricsService, MetricsSummary, TimeseriesBucket, TopEvent } from './metrics.service';

describe('MetricsService', () => {
  let service: MetricsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MetricsService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MetricsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(service).toBeTruthy();
  });

  describe('getSummary()', () => {
    it('should GET /api/metrics/summary', () => {
      const mockSummary: MetricsSummary = {
        activeSessions: 3,
        loginAttemptsTotal24h: 100,
        loginFailures24h: 2,
        loginSuccessRate24h: 0.98,
        connectionsStarted24h: 50,
        connectionsEnded24h: 48,
        auditEventsTotal24h: 200,
        auditFailures24h: 5,
        authEvents24h: 120,
      };

      let result: MetricsSummary | undefined;
      service.getSummary().subscribe((s) => (result = s));

      const req = http.expectOne('/api/metrics/summary');
      expect(req.request.method).toBe('GET');
      req.flush(mockSummary);
      expect(result?.activeSessions).toBe(3);
      expect(result?.loginSuccessRate24h).toBe(0.98);
    });
  });

  describe('getLoginTimeseries()', () => {
    it('should GET /api/metrics/logins/timeseries with hours and bucketMinutes params', () => {
      const buckets: TimeseriesBucket[] = [
        { timestamp: '2024-01-01T00:00:00Z', values: { success: 5, failure: 1 } },
      ];

      let result: TimeseriesBucket[] | undefined;
      service.getLoginTimeseries(24, 60).subscribe((r) => (result = r));

      const req = http.expectOne((r) => r.url === '/api/metrics/logins/timeseries');
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('hours')).toBe('24');
      expect(req.request.params.get('bucketMinutes')).toBe('60');
      req.flush(buckets);
      expect(result?.length).toBe(1);
      expect(result?.[0].values['success']).toBe(5);
    });
  });

  describe('getConnectionsTimeseries()', () => {
    it('should GET /api/metrics/connections/timeseries with params', () => {
      service.getConnectionsTimeseries(48, 30).subscribe();
      const req = http.expectOne((r) => r.url === '/api/metrics/connections/timeseries');
      expect(req.request.params.get('hours')).toBe('48');
      expect(req.request.params.get('bucketMinutes')).toBe('30');
      req.flush([]);
    });
  });

  describe('getTopAuditEvents()', () => {
    it('should GET /api/metrics/audit-events/top with hours and limit params', () => {
      const events: TopEvent[] = [
        { action: 'login', count: 42 },
        { action: 'logout', count: 38 },
      ];

      let result: TopEvent[] | undefined;
      service.getTopAuditEvents(24, 5).subscribe((r) => (result = r));

      const req = http.expectOne((r) => r.url === '/api/metrics/audit-events/top');
      expect(req.request.params.get('hours')).toBe('24');
      expect(req.request.params.get('limit')).toBe('5');
      req.flush(events);
      expect(result?.length).toBe(2);
      expect(result?.[0].action).toBe('login');
    });

    it('should use default limit of 10 when not provided', () => {
      service.getTopAuditEvents(24).subscribe();
      const req = http.expectOne((r) => r.url === '/api/metrics/audit-events/top');
      expect(req.request.params.get('limit')).toBe('10');
      req.flush([]);
    });
  });

  describe('getAuditEventBreakdown()', () => {
    it('should GET /api/metrics/audit-events/breakdown with hours', () => {
      let result: { success: number; failure: number } | undefined;
      service.getAuditEventBreakdown(24).subscribe((r) => (result = r));

      const req = http.expectOne((r) => r.url === '/api/metrics/audit-events/breakdown');
      expect(req.request.params.get('hours')).toBe('24');
      req.flush({ success: 95, failure: 5 });
      expect(result?.success).toBe(95);
      expect(result?.failure).toBe(5);
    });
  });

  describe('getAuditEventTimeseries()', () => {
    it('should GET /api/metrics/audit-events/timeseries with params', () => {
      service.getAuditEventTimeseries(12, 15).subscribe();
      const req = http.expectOne((r) => r.url === '/api/metrics/audit-events/timeseries');
      expect(req.request.params.get('hours')).toBe('12');
      expect(req.request.params.get('bucketMinutes')).toBe('15');
      req.flush([]);
    });
  });
});
