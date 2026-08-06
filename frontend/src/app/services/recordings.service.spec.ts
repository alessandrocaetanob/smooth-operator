import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { RecordingsService } from './recordings.service';

describe('RecordingsService', () => {
  let service: RecordingsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [RecordingsService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(RecordingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() builds query string from filters', () => {
    service
      .list({
        userId: 'u1',
        connectionId: 'c1',
        vaultId: 'v1',
        from: '2026-01-01',
        to: '2026-02-01',
        status: 'Failed',
        page: 2,
        pageSize: 50,
      })
      .subscribe();

    const req = http.expectOne(
      (r) =>
        r.url === '/api/recordings' &&
        r.params.get('userId') === 'u1' &&
        r.params.get('connectionId') === 'c1' &&
        r.params.get('vaultId') === 'v1' &&
        r.params.get('from') === '2026-01-01' &&
        r.params.get('to') === '2026-02-01' &&
        r.params.get('status') === 'Failed' &&
        r.params.get('page') === '2' &&
        r.params.get('pageSize') === '50',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ total: 0, page: 2, pageSize: 50, items: [] });
  });

  it('list() omits unset filters from the query string', () => {
    service.list({}).subscribe();
    const req = http.expectOne('/api/recordings');
    expect(req.request.params.keys()).toHaveLength(0);
    req.flush({ total: 0, page: 1, pageSize: 25, items: [] });
  });

  it('list() normalises PascalCase server responses', () => {
    let result: unknown;
    service.list({}).subscribe((r) => (result = r));

    http.expectOne('/api/recordings').flush({
      Total: 1,
      Page: 1,
      PageSize: 25,
      Items: [
        {
          Id: 'r1',
          SessionId: 'abc',
          ConnectionId: 'c1',
          ConnectionName: 'web-1',
          Protocol: 'rdp',
          VaultId: 'v1',
          VaultName: 'eng',
          UserId: 'u1',
          UserEmail: 'a@b',
          StartedAt: '2026-05-25T10:00:00Z',
          EndedAt: '2026-05-25T10:30:00Z',
          DurationSeconds: 1800,
          StorageType: 'S3',
          FileSizeBytes: 4242,
          Status: 'Available',
          ErrorMessage: null,
          IncludeKeys: false,
        },
      ],
    });

    expect(result).toBeDefined();
    const r = result as { total: number; items: Record<string, unknown>[] };
    expect(r.total).toBe(1);
    expect(r.items[0]['id']).toBe('r1');
    expect(r.items[0]['storageType']).toBe('S3');
    expect(r.items[0]['status']).toBe('Available');
  });

  it('list() falls back to defaults for missing fields', () => {
    let result: unknown;
    service.list({}).subscribe((r) => (result = r));

    http.expectOne('/api/recordings').flush({});

    const r = result as { total: number; page: number; pageSize: number; items: unknown[] };
    expect(r.total).toBe(0);
    expect(r.page).toBe(1);
    expect(r.pageSize).toBe(25);
    expect(r.items).toEqual([]);
  });

  it('fetchStream() requests arraybuffer from /api/recordings/:id/stream', () => {
    service.fetchStream('rec-123').subscribe();
    const req = http.expectOne('/api/recordings/rec-123/stream');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('arraybuffer');
    req.flush(new ArrayBuffer(8));
  });

  it('streamUrl() builds the direct URL', () => {
    expect(service.streamUrl('rec-123')).toBe('/api/recordings/rec-123/stream');
  });

  it('remove() issues DELETE /api/recordings/:id', () => {
    service.remove('rec-9').subscribe();
    const req = http.expectOne('/api/recordings/rec-9');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
