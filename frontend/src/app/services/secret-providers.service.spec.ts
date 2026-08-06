import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import {
  SecretProvidersService,
  SecretProvider,
  CreateSecretProviderPayload,
  UpdateSecretProviderPayload,
} from './secret-providers.service';

const RAW_PROVIDER_PASCAL = {
  Id: 'sp1',
  Name: 'My KV',
  Type: 'AzureKeyVault',
  IsEnabled: true,
  HasClientSecret: true,
  VaultUri: 'https://myvault.vault.azure.net/',
  TenantId: 'tenant-1',
  ClientId: 'client-1',
  CreatedAt: '2024-01-01T00:00:00Z',
  UpdatedAt: '2024-01-02T00:00:00Z',
};

const RAW_PROVIDER_CAMEL = {
  id: 'sp2',
  name: 'Camel KV',
  type: 'AzureKeyVault',
  isEnabled: false,
  hasClientSecret: false,
  vaultUri: 'https://camel.vault.azure.net/',
  tenantId: 'tenant-2',
  clientId: 'client-2',
  createdAt: '2024-02-01T00:00:00Z',
  updatedAt: '2024-02-02T00:00:00Z',
};

describe('SecretProvidersService', () => {
  let service: SecretProvidersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SecretProvidersService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(SecretProvidersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with empty list signal', () => {
    expect(service).toBeTruthy();
    expect(service.list()).toEqual([]);
  });

  describe('reload()', () => {
    it('should GET /api/secretproviders and update list signal (PascalCase)', () => {
      service.reload().subscribe();
      const req = http.expectOne('/api/secretproviders');
      expect(req.request.method).toBe('GET');
      req.flush([RAW_PROVIDER_PASCAL]);

      expect(service.list().length).toBe(1);
      const p = service.list()[0];
      expect(p.id).toBe('sp1');
      expect(p.name).toBe('My KV');
      expect(p.type).toBe('AzureKeyVault');
      expect(p.isEnabled).toBe(true);
      expect(p.hasClientSecret).toBe(true);
      expect(p.vaultUri).toBe('https://myvault.vault.azure.net/');
      expect(p.tenantId).toBe('tenant-1');
      expect(p.clientId).toBe('client-1');
    });

    it('should normalize camelCase payload from backend', () => {
      service.reload().subscribe();
      http.expectOne('/api/secretproviders').flush([RAW_PROVIDER_CAMEL]);

      const p = service.list()[0];
      expect(p.id).toBe('sp2');
      expect(p.isEnabled).toBe(false);
      expect(p.hasClientSecret).toBe(false);
    });

    it('should handle empty array response', () => {
      service.reload().subscribe();
      http.expectOne('/api/secretproviders').flush([]);
      expect(service.list()).toEqual([]);
    });

    it('should handle null response gracefully', () => {
      service.reload().subscribe();
      http.expectOne('/api/secretproviders').flush(null);
      expect(service.list()).toEqual([]);
    });
  });

  describe('create()', () => {
    it('should POST /api/secretproviders with payload', () => {
      const payload: CreateSecretProviderPayload = {
        name: 'New KV',
        vaultUri: 'https://new.vault.azure.net/',
        tenantId: 'tenant-new',
        clientId: 'client-new',
        clientSecret: 'super-secret',
      };
      let result: SecretProvider | undefined;
      service.create(payload).subscribe((p) => (result = p));

      const req = http.expectOne('/api/secretproviders');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush({ ...RAW_PROVIDER_PASCAL, Id: 'sp-new', Name: 'New KV' });

      expect(result).toBeTruthy();
    });
  });

  describe('update()', () => {
    it('should PUT /api/secretproviders/:id with payload', () => {
      const payload: UpdateSecretProviderPayload = { name: 'Renamed KV', isEnabled: false };
      service.update('sp1', payload).subscribe();

      const req = http.expectOne('/api/secretproviders/sp1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(payload);
      req.flush(RAW_PROVIDER_PASCAL);
    });
  });

  describe('remove()', () => {
    it('should DELETE /api/secretproviders/:id', () => {
      service.remove('sp1').subscribe();

      const req = http.expectOne('/api/secretproviders/sp1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('test()', () => {
    it('should POST /api/secretproviders/:id/test and return success flag', () => {
      let result: { success: boolean } | undefined;
      service.test('sp1').subscribe((r) => (result = r));

      const req = http.expectOne('/api/secretproviders/sp1/test');
      expect(req.request.method).toBe('POST');
      req.flush({ success: true });

      expect(result?.success).toBe(true);
    });

    it('should return false when test fails', () => {
      let result: { success: boolean } | undefined;
      service.test('sp1').subscribe((r) => (result = r));

      http.expectOne('/api/secretproviders/sp1/test').flush({ success: false });
      expect(result?.success).toBe(false);
    });
  });

  describe('listSecrets()', () => {
    it('should GET /api/secretproviders/:id/secrets', () => {
      let result: string[] | undefined;
      service.listSecrets('sp1').subscribe((r) => (result = r));

      const req = http.expectOne('/api/secretproviders/sp1/secrets');
      expect(req.request.method).toBe('GET');
      req.flush(['secret-a', 'secret-b']);

      expect(result).toEqual(['secret-a', 'secret-b']);
    });
  });
});
