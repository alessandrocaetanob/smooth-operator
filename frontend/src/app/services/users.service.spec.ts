import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { UsersService, AppUser, AppRole, InviteResult, EffectiveVaults } from './users.service';

const mockUser: AppUser = {
  id: 'u1',
  email: 'alice@example.com',
  name: 'Alice',
  isActive: true,
  ssoLinked: false,
  ssoProviderType: null,
  hasPassword: true,
  createdAt: '2024-01-01T00:00:00Z',
  roles: ['admin'],
  vaultIds: ['v1', 'v2'],
};

describe('UsersService', () => {
  let service: UsersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [UsersService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(UsersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with empty list', () => {
    expect(service).toBeTruthy();
    expect(service.list()).toEqual([]);
  });

  describe('reload()', () => {
    it('should GET /api/users and update list signal with PascalCase response', () => {
      const raw = [
        {
          Id: 'u1',
          Email: 'alice@example.com',
          Name: 'Alice',
          IsActive: true,
          SsoLinked: false,
          SsoProviderType: null,
          HasPassword: true,
          CreatedAt: '2024-01-01T00:00:00Z',
          Roles: ['admin'],
          VaultIds: ['v1'],
        },
      ];

      service.reload().subscribe();
      http.expectOne('/api/users').flush(raw);

      expect(service.list().length).toBe(1);
      const u = service.list()[0];
      expect(u.id).toBe('u1');
      expect(u.email).toBe('alice@example.com');
      expect(u.isActive).toBe(true);
      expect(u.roles).toEqual(['admin']);
      expect(u.vaultIds).toEqual(['v1']);
    });

    it('should normalize camelCase response', () => {
      service.reload().subscribe();
      http.expectOne('/api/users').flush([mockUser]);

      const u = service.list()[0];
      expect(u.ssoLinked).toBe(false);
      expect(u.hasPassword).toBe(true);
    });

    it('should handle empty response', () => {
      service.reload().subscribe();
      http.expectOne('/api/users').flush([]);
      expect(service.list()).toEqual([]);
    });
  });

  describe('setActive()', () => {
    it('should PATCH /api/users/:id/active', () => {
      service.setActive('u1', false).subscribe();
      const req = http.expectOne('/api/users/u1/active');
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ isActive: false });
      req.flush(null);
    });

    it('should PATCH to reactivate user', () => {
      service.setActive('u2', true).subscribe();
      const req = http.expectOne('/api/users/u2/active');
      expect(req.request.body).toEqual({ isActive: true });
      req.flush(null);
    });
  });

  describe('rename()', () => {
    it('should PUT /api/users/:id with name', () => {
      service.rename('u1', 'New Name').subscribe();
      const req = http.expectOne('/api/users/u1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ name: 'New Name' });
      req.flush(null);
    });
  });

  describe('roleCatalog()', () => {
    it('should GET /api/users/roles and normalize', () => {
      let result: AppRole[] | undefined;
      service.roleCatalog().subscribe((r) => (result = r));

      const req = http.expectOne('/api/users/roles');
      expect(req.request.method).toBe('GET');
      req.flush([
        { name: 'admin', description: 'Full access' },
        { Name: 'viewer', Description: 'Read only' },
      ]);

      expect(result?.length).toBe(2);
      expect(result?.[0].name).toBe('admin');
      expect(result?.[1].name).toBe('viewer');
      expect(result?.[1].description).toBe('Read only');
    });
  });

  describe('setRole()', () => {
    it('should PUT /api/users/:id/role', () => {
      service.setRole('u1', 'viewer').subscribe();
      const req = http.expectOne('/api/users/u1/role');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ role: 'viewer' });
      req.flush(null);
    });
  });

  describe('setVaultAssignments()', () => {
    it('should PUT /api/users/:id/vaults', () => {
      service.setVaultAssignments('u1', ['v1', 'v2']).subscribe();
      const req = http.expectOne('/api/users/u1/vaults');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ vaultIds: ['v1', 'v2'] });
      req.flush(null);
    });
  });

  describe('remove()', () => {
    it('should DELETE /api/users/:id', () => {
      service.remove('u1').subscribe();
      const req = http.expectOne('/api/users/u1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('getEffectiveVaults()', () => {
    it('should GET /api/users/:id/effective-vaults and normalize', () => {
      let result: EffectiveVaults | undefined;
      service.getEffectiveVaults('u1').subscribe((r) => (result = r));

      const req = http.expectOne('/api/users/u1/effective-vaults');
      expect(req.request.method).toBe('GET');
      req.flush({
        userId: 'u1',
        direct: [{ id: 'v1', name: 'Direct Vault', parentGroupId: null }],
        viaGroups: [
          {
            groupId: 'g1',
            groupName: 'Admins',
            vaults: [{ id: 'v2', name: 'Group Vault', parentGroupId: null }],
          },
        ],
        merged: [
          { id: 'v1', name: 'Direct Vault', parentGroupId: null },
          { id: 'v2', name: 'Group Vault', parentGroupId: null },
        ],
      });

      expect(result?.userId).toBe('u1');
      expect(result?.direct.length).toBe(1);
      expect(result?.direct[0].name).toBe('Direct Vault');
      expect(result?.viaGroups.length).toBe(1);
      expect(result?.viaGroups[0].groupName).toBe('Admins');
      expect(result?.viaGroups[0].vaults[0].id).toBe('v2');
      expect(result?.merged.length).toBe(2);
    });

    it('should normalize PascalCase effective-vaults response', () => {
      let result: EffectiveVaults | undefined;
      service.getEffectiveVaults('u2').subscribe((r) => (result = r));

      http.expectOne('/api/users/u2/effective-vaults').flush({
        UserId: 'u2',
        Direct: [{ Id: 'v3', Name: 'Admin Vault', ParentGroupId: 'pg1' }],
        ViaGroups: [],
        Merged: [{ Id: 'v3', Name: 'Admin Vault', ParentGroupId: 'pg1' }],
      });

      expect(result?.userId).toBe('u2');
      expect(result?.direct[0].parentGroupId).toBe('pg1');
    });
  });

  describe('invite()', () => {
    it('should POST /api/auth/invite and normalize response', () => {
      let result: InviteResult | undefined;
      service
        .invite({ email: 'new@example.com', name: 'New User', role: 'viewer' })
        .subscribe((r) => (result = r));

      const req = http.expectOne('/api/auth/invite');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        email: 'new@example.com',
        name: 'New User',
        role: 'viewer',
      });
      req.flush({
        message: 'Invite sent',
        inviteUrl: 'https://example.com/invite/abc',
        emailSent: true,
        emailError: null,
      });

      expect(result?.message).toBe('Invite sent');
      expect(result?.inviteUrl).toBe('https://example.com/invite/abc');
      expect(result?.emailSent).toBe(true);
      expect(result?.emailError).toBeNull();
    });

    it('should normalize PascalCase invite response', () => {
      let result: InviteResult | undefined;
      service.invite({ email: 'x@example.com' }).subscribe((r) => (result = r));

      http.expectOne('/api/auth/invite').flush({
        Message: 'Sent',
        InviteUrl: 'https://example.com/invite/xyz',
        EmailSent: false,
        EmailError: 'SMTP not configured',
      });

      expect(result?.message).toBe('Sent');
      expect(result?.emailSent).toBe(false);
      expect(result?.emailError).toBe('SMTP not configured');
    });
  });
});
