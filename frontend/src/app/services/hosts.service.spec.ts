import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { HostsService, AppHost, CreateHostPayload } from './hosts.service';

const mockRaw = { id: 'h1', name: 'My Host', address: '192.168.1.1' };

describe('HostsService', () => {
  let service: HostsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [HostsService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(HostsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with empty list', () => {
    expect(service).toBeTruthy();
    expect(service.list()).toEqual([]);
  });

  describe('reload()', () => {
    it('should GET /api/hosts and update list signal with normalized hosts', () => {
      service.reload().subscribe();

      const req = http.expectOne('/api/hosts');
      expect(req.request.method).toBe('GET');
      req.flush([mockRaw]);

      expect(service.list()).toEqual([{ id: 'h1', name: 'My Host', address: '192.168.1.1' }]);
    });

    it('should normalize PascalCase response', () => {
      service.reload().subscribe();

      http.expectOne('/api/hosts').flush([{ Id: 'h2', Name: 'PascalHost', Address: '10.0.0.1' }]);

      expect(service.list()).toEqual([{ id: 'h2', name: 'PascalHost', address: '10.0.0.1' }]);
    });

    it('should handle empty array', () => {
      service.reload().subscribe();
      http.expectOne('/api/hosts').flush([]);
      expect(service.list()).toEqual([]);
    });

    it('should handle null response gracefully', () => {
      service.reload().subscribe();
      http.expectOne('/api/hosts').flush(null);
      expect(service.list()).toEqual([]);
    });

    it('should update list signal with all hosts', () => {
      service.reload().subscribe();

      http.expectOne('/api/hosts').flush([
        { id: 'h1', name: 'Host 1', address: '1.1.1.1' },
        { id: 'h2', name: 'Host 2', address: '2.2.2.2' },
      ]);

      expect(service.list().length).toBe(2);
      expect(service.list()[1].address).toBe('2.2.2.2');
    });
  });

  describe('create()', () => {
    it('should POST /api/hosts with payload', () => {
      const payload: CreateHostPayload = { name: 'New Host', address: '192.168.1.50' };
      let result: AppHost | undefined;
      service.create(payload).subscribe((h) => (result = h));

      const req = http.expectOne('/api/hosts');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush({ id: 'h-new', name: 'New Host', address: '192.168.1.50' });

      expect(result?.id).toBe('h-new');
      expect(result?.name).toBe('New Host');
    });
  });

  describe('update()', () => {
    it('should PUT /api/hosts/:id with payload', () => {
      const payload: CreateHostPayload = { name: 'Updated', address: '10.0.0.5' };
      let done = false;
      service.update('h1', payload).subscribe(() => (done = true));

      const req = http.expectOne('/api/hosts/h1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(payload);
      req.flush(null);

      expect(done).toBe(true);
    });
  });

  describe('remove()', () => {
    it('should DELETE /api/hosts/:id', () => {
      let done = false;
      service.remove('h1').subscribe(() => (done = true));

      const req = http.expectOne('/api/hosts/h1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);

      expect(done).toBe(true);
    });
  });
});
