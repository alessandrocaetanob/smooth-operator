import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { VaultsService, Vault, SaveVaultPayload, VaultAssignments } from './vaults.service';

describe('VaultsService', () => {
  let service: VaultsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [VaultsService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(VaultsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with empty list', () => {
    expect(service).toBeTruthy();
    expect(service.list()).toEqual([]);
  });

  describe('reload()', () => {
    it('should GET /api/vaults and update list signal with PascalCase', () => {
      const raw = [
        { Id: 'v1', Name: 'Dev Vault', ParentGroupId: null, UserCount: 3, GroupCount: 1 },
      ];

      service.reload().subscribe();
      http.expectOne('/api/vaults').flush(raw);

      expect(service.list().length).toBe(1);
      const v = service.list()[0];
      expect(v.id).toBe('v1');
      expect(v.name).toBe('Dev Vault');
      expect(v.userCount).toBe(3);
      expect(v.groupCount).toBe(1);
      expect(v.parentGroupId).toBeNull();
    });

    it('should normalize camelCase response', () => {
      service.reload().subscribe();
      http.expectOne('/api/vaults').flush([{ id: 'v2', name: 'Prod', parentGroupId: 'g1' }]);
      expect(service.list()[0].parentGroupId).toBe('g1');
    });

    it('should handle empty list', () => {
      service.reload().subscribe();
      http.expectOne('/api/vaults').flush([]);
      expect(service.list()).toEqual([]);
    });
  });

  describe('create()', () => {
    it('should POST /api/vaults', () => {
      const payload: SaveVaultPayload = { name: 'New Vault', parentGroupId: null };
      let result: Vault | undefined;
      service.create(payload).subscribe((v) => (result = v));

      const req = http.expectOne('/api/vaults');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush({ id: 'new1', name: 'New Vault', parentGroupId: null });
      expect(result?.id).toBe('new1');
    });
  });

  describe('update()', () => {
    it('should PUT /api/vaults/:id', () => {
      const payload: SaveVaultPayload = { name: 'Updated' };
      service.update('v1', payload).subscribe();

      const req = http.expectOne('/api/vaults/v1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(payload);
      req.flush(null);
    });
  });

  describe('remove()', () => {
    it('should DELETE /api/vaults/:id', () => {
      service.remove('v1').subscribe();
      const req = http.expectOne('/api/vaults/v1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('getAssignments()', () => {
    it('should GET /api/vaults/:id/assignments', () => {
      let result: VaultAssignments | undefined;
      service.getAssignments('v1').subscribe((a) => (result = a));

      const req = http.expectOne('/api/vaults/v1/assignments');
      expect(req.request.method).toBe('GET');
      req.flush({ userIds: ['u1'], groupIds: ['g1'] });
      expect(result?.userIds).toEqual(['u1']);
      expect(result?.groupIds).toEqual(['g1']);
    });
  });

  describe('setAssignments()', () => {
    it('should PUT /api/vaults/:id/assignments', () => {
      const payload: VaultAssignments = { userIds: ['u1', 'u2'], groupIds: [] };
      service.setAssignments('v1', payload).subscribe();

      const req = http.expectOne('/api/vaults/v1/assignments');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(payload);
      req.flush(null);
    });
  });
});
