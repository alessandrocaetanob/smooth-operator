import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { AuditLogsService, AuditLogQuery, PagedAuditLogs } from './audit-logs.service';

const emptyPage: PagedAuditLogs = {
  items: [],
  page: 1,
  pageSize: 25,
  totalItems: 0,
  totalPages: 0,
};

describe('AuditLogsService', () => {
  let service: AuditLogsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuditLogsService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuditLogsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with default result signal', () => {
    expect(service).toBeTruthy();
    expect(service.result().items).toEqual([]);
    expect(service.result().page).toBe(1);
  });

  describe('reload()', () => {
    it('should GET /api/audit-logs with no params when query is empty', () => {
      service.reload({}).subscribe();
      const req = http.expectOne('/api/audit-logs');
      expect(req.request.method).toBe('GET');
      req.flush(emptyPage);
    });

    it('should normalize PascalCase items into result signal', () => {
      const raw = {
        Items: [
          {
            Id: 'al1',
            Timestamp: '2024-01-01T00:00:00Z',
            UserId: 'u1',
            UserEmail: 'alice@example.com',
            UserName: 'alice',
            Action: 'login',
            ResourceType: 'auth',
            ResourceId: '',
            Details: 'ok',
            IpAddress: '10.0.0.1',
            Outcome: 'success',
            UserAgent: 'Mozilla',
            CorrelationId: null,
          },
        ],
        Page: 1,
        PageSize: 25,
        TotalItems: 1,
        TotalPages: 1,
      };

      service.reload({}).subscribe();
      http.expectOne('/api/audit-logs').flush(raw);

      const result = service.result();
      expect(result.items.length).toBe(1);
      expect(result.page).toBe(1);
      expect(result.totalItems).toBe(1);

      const log = result.items[0];
      expect(log.id).toBe('al1');
      expect(log.action).toBe('login');
      expect(log.userId).toBe('u1');
      expect(log.userEmail).toBe('alice@example.com');
      expect(log.userName).toBe('alice');
      expect(log.ipAddress).toBe('10.0.0.1');
      expect(log.outcome).toBe('success');
      expect(log.timestamp).toBe('2024-01-01T00:00:00Z');
    });

    it('should normalize camelCase items', () => {
      const raw = {
        items: [
          {
            id: 'al2',
            timestamp: '2024-06-01T12:00:00Z',
            userId: 'u2',
            userEmail: 'bob@example.com',
            userName: 'bob',
            action: 'create_connection',
            resourceType: 'connection',
            resourceId: 'c1',
            details: '',
            ipAddress: '192.168.1.1',
            outcome: 'success',
            userAgent: 'curl',
            correlationId: 'cor1',
          },
        ],
        page: 2,
        pageSize: 10,
        totalItems: 20,
        totalPages: 2,
      };

      service.reload({}).subscribe();
      http.expectOne('/api/audit-logs').flush(raw);

      const result = service.result();
      expect(result.page).toBe(2);
      expect(result.totalPages).toBe(2);
      expect(result.items[0].resourceType).toBe('connection');
      expect(result.items[0].correlationId).toBe('cor1');
    });

    it('should append page and pageSize params', () => {
      service.reload({ page: 3, pageSize: 10 }).subscribe();
      const req = http.expectOne((r) => r.url === '/api/audit-logs');
      expect(req.request.params.get('page')).toBe('3');
      expect(req.request.params.get('pageSize')).toBe('10');
      req.flush(emptyPage);
    });

    it('should append user param', () => {
      service.reload({ user: 'alice' }).subscribe();
      const req = http.expectOne((r) => r.url === '/api/audit-logs');
      expect(req.request.params.get('user')).toBe('alice');
      req.flush(emptyPage);
    });

    it('should append action param', () => {
      service.reload({ action: 'login' }).subscribe();
      const req = http.expectOne((r) => r.url === '/api/audit-logs');
      expect(req.request.params.get('action')).toBe('login');
      req.flush(emptyPage);
    });

    it('should append from and to date params', () => {
      service.reload({ from: '2024-01-01', to: '2024-12-31' }).subscribe();
      const req = http.expectOne((r) => r.url === '/api/audit-logs');
      expect(req.request.params.get('from')).toBe('2024-01-01');
      expect(req.request.params.get('to')).toBe('2024-12-31');
      req.flush(emptyPage);
    });

    it('should append outcome and resourceType params', () => {
      service.reload({ outcome: 'failure', resourceType: 'connection' }).subscribe();
      const req = http.expectOne((r) => r.url === '/api/audit-logs');
      expect(req.request.params.get('outcome')).toBe('failure');
      expect(req.request.params.get('resourceType')).toBe('connection');
      req.flush(emptyPage);
    });

    it('should not append undefined params', () => {
      service.reload({ page: undefined, user: undefined }).subscribe();
      const req = http.expectOne('/api/audit-logs');
      expect(req.request.params.keys()).toHaveLength(0);
      req.flush(emptyPage);
    });

    it('should handle empty items list', () => {
      service.reload({}).subscribe();
      http.expectOne('/api/audit-logs').flush(emptyPage);
      expect(service.result().items).toEqual([]);
    });
  });

  describe('exportCsvUrl()', () => {
    it('should return plain URL when no params', () => {
      const url = service.exportCsvUrl({});
      expect(url).toBe('/api/audit-logs/export');
    });

    it('should include query string when params provided', () => {
      const url = service.exportCsvUrl({ action: 'login', user: 'alice' });
      expect(url).toContain('/api/audit-logs/export?');
      expect(url).toContain('action=login');
      expect(url).toContain('user=alice');
    });
  });
});
