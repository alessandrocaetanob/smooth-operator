import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { ConnectionsService, Connection, CreateConnectionPayload } from './connections.service';

const mockConnection: Connection = {
  id: 'c1',
  name: 'My RDP',
  protocol: 'rdp',
  hostId: 'h1',
  credentialId: 'cred1',
  connectionGroupId: null,
  settings: '{}',
  tags: ['prod'],
  host: { id: 'h1', name: 'Server', address: '10.0.0.1' },
};

describe('ConnectionsService', () => {
  let service: ConnectionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ConnectionsService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ConnectionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create and expose initial empty state', () => {
    expect(service).toBeTruthy();
    expect(service.list()).toEqual([]);
    expect(service.loading()).toBe(false);
  });

  describe('reload()', () => {
    it('should GET /api/connections and update list signal', () => {
      const raw = [
        {
          Id: 'c1',
          Name: 'My RDP',
          Protocol: 'rdp',
          HostId: 'h1',
          CredentialId: 'cred1',
          ConnectionGroupId: null,
          Settings: '{}',
          Tags: ['prod'],
          Host: { Id: 'h1', Name: 'Server', Address: '10.0.0.1' },
        },
      ];

      let result: Connection[] | undefined;
      service.reload().subscribe((connections) => (result = connections));

      const req = http.expectOne('/api/connections');
      expect(req.request.method).toBe('GET');
      req.flush(raw);

      expect(service.list().length).toBe(1);
      expect(service.list()[0].id).toBe('c1');
      expect(service.list()[0].name).toBe('My RDP');
      expect(service.list()[0].protocol).toBe('rdp');
      expect(service.list()[0].host?.address).toBe('10.0.0.1');
      expect(service.loading()).toBe(false);
      void result;
    });

    it('should normalize camelCase payload from backend', () => {
      const raw = [
        {
          id: 'c2',
          name: 'SSH',
          protocol: 'ssh',
          hostId: 'h2',
          credentialId: null,
          connectionGroupId: 'g1',
          settings: '{"port":22}',
          tags: [],
          host: null,
        },
      ];

      service.reload().subscribe();
      http.expectOne('/api/connections').flush(raw);

      const c = service.list()[0];
      expect(c.id).toBe('c2');
      expect(c.connectionGroupId).toBe('g1');
      expect(c.settings).toBe('{"port":22}');
      expect(c.host).toBeNull();
    });

    it('should set loading=false on error', () => {
      service.reload().subscribe({ error: () => undefined });
      http.expectOne('/api/connections').error(new ProgressEvent('error'));
      expect(service.loading()).toBe(false);
    });

    it('should handle null/empty response gracefully', () => {
      service.reload().subscribe();
      http.expectOne('/api/connections').flush([]);
      expect(service.list()).toEqual([]);
    });
  });

  describe('listAsMap computed()', () => {
    it('should return a Map indexed by connection id', () => {
      service.reload().subscribe();
      http.expectOne('/api/connections').flush([{ id: 'c1', Name: 'X' }]);
      expect(service.listAsMap().get('c1')?.id).toBe('c1');
      expect(service.listAsMap().size).toBe(1);
    });
  });

  describe('get()', () => {
    it('should GET /api/connections/:id', () => {
      let result: Connection | undefined;
      service.get('c1').subscribe((c) => (result = c));
      const req = http.expectOne('/api/connections/c1');
      expect(req.request.method).toBe('GET');
      req.flush(mockConnection);
      expect(result?.id).toBe('c1');
    });
  });

  describe('create()', () => {
    it('should POST /api/connections', () => {
      const payload: CreateConnectionPayload = {
        name: 'New',
        protocol: 'rdp',
        hostId: 'h1',
        tags: [],
      };
      let created: Connection | undefined;
      service.create(payload).subscribe((c) => (created = c));
      const req = http.expectOne('/api/connections');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush({ ...payload, id: 'new1', settings: '{}' });
      expect(created?.id).toBe('new1');
    });
  });

  describe('update()', () => {
    it('should PUT /api/connections/:id', () => {
      const payload: CreateConnectionPayload = { name: 'Updated', protocol: 'rdp', hostId: 'h1' };
      service.update('c1', payload).subscribe();
      const req = http.expectOne('/api/connections/c1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ id: 'c1', ...payload });
      req.flush(null);
    });
  });

  describe('remove()', () => {
    it('should DELETE /api/connections/:id', () => {
      service.remove('c1').subscribe();
      const req = http.expectOne('/api/connections/c1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('getLastConnected()', () => {
    it('should GET /api/connections/my-last-connected and normalize', () => {
      let result: { connectionId: string; lastConnectedAt: string }[] | undefined;
      service.getLastConnected().subscribe((r) => (result = r));
      const req = http.expectOne('/api/connections/my-last-connected');
      expect(req.request.method).toBe('GET');
      req.flush([{ connectionId: 'c1', lastConnectedAt: '2024-01-01T00:00:00Z' }]);
      expect(result?.[0].connectionId).toBe('c1');
    });

    it('should handle PascalCase response', () => {
      let result: { connectionId: string; lastConnectedAt: string }[] | undefined;
      service.getLastConnected().subscribe((r) => (result = r));
      http
        .expectOne('/api/connections/my-last-connected')
        .flush([{ ConnectionId: 'c2', LastConnectedAt: '2024-06-01T00:00:00Z' }]);
      expect(result?.[0].connectionId).toBe('c2');
    });
  });

  describe('probeConnection()', () => {
    it('should GET /api/connections/:id/probe', () => {
      let result: { reachable: boolean } | undefined;
      service.probeConnection('c1').subscribe((r) => (result = r));
      const req = http.expectOne('/api/connections/c1/probe');
      expect(req.request.method).toBe('GET');
      req.flush({ reachable: true });
      expect(result?.reachable).toBe(true);
    });
  });

  describe('probeBatch()', () => {
    it('should POST /api/connections/probe-batch', () => {
      let result: Record<string, boolean> | undefined;
      service.probeBatch(['c1', 'c2']).subscribe((r) => (result = r));
      const req = http.expectOne('/api/connections/probe-batch');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(['c1', 'c2']);
      req.flush({ c1: true, c2: false });
      expect(result?.['c1']).toBe(true);
    });
  });

  describe('probeConnectionsBulk()', () => {
    it('should POST /api/connections/probe-bulk', () => {
      let result: Record<string, string> | undefined;
      service.probeConnectionsBulk(['c1']).subscribe((r) => (result = r));
      const req = http.expectOne('/api/connections/probe-bulk');
      expect(req.request.method).toBe('POST');
      req.flush({ c1: 'reachable' });
      expect(result?.['c1']).toBe('reachable');
    });
  });

  describe('downloadConnectionFile()', () => {
    it('should GET /api/connections/:id/file as blob response', () => {
      vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock');
      vi.spyOn(URL, 'revokeObjectURL').mockReturnValue(undefined);

      service.downloadConnectionFile('c1', 'rdp').subscribe();
      const req = http.expectOne('/api/connections/c1/file?format=rdp');
      expect(req.request.method).toBe('GET');
      expect(req.request.responseType).toBe('blob');
      req.flush(new Blob(['content'], { type: 'application/octet-stream' }), {
        headers: { 'content-disposition': 'attachment; filename="conn.rdp"' },
      });

      expect(URL.createObjectURL).toHaveBeenCalled();
    });
  });
});
