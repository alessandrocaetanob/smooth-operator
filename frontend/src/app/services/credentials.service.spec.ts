import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import {
  CredentialsService,
  Credential,
  CreateCredentialPayload,
  UpdateCredentialPayload,
} from './credentials.service';

describe('CredentialsService', () => {
  let service: CredentialsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CredentialsService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(CredentialsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create with empty list', () => {
    expect(service).toBeTruthy();
    expect(service.list()).toEqual([]);
  });

  describe('reload()', () => {
    it('should GET /api/credentials and update list signal', () => {
      const raw = [
        {
          Id: 'cr1',
          Name: 'Admin Pass',
          Username: 'admin',
          CredentialType: 'password',
          PublicKey: '',
        },
      ];

      service.reload().subscribe();
      http.expectOne('/api/credentials').flush(raw);

      expect(service.list().length).toBe(1);
      const c = service.list()[0];
      expect(c.id).toBe('cr1');
      expect(c.name).toBe('Admin Pass');
      expect(c.username).toBe('admin');
      expect(c.credentialType).toBe('password');
    });

    it('should normalize camelCase response', () => {
      const raw = [
        {
          id: 'cr2',
          name: 'SSH Key',
          username: 'root',
          credentialType: 'private_key',
          publicKey: 'ssh-rsa AAAA...',
        },
      ];

      service.reload().subscribe();
      http.expectOne('/api/credentials').flush(raw);

      const c = service.list()[0];
      expect(c.id).toBe('cr2');
      expect(c.credentialType).toBe('private_key');
      expect(c.publicKey).toBe('ssh-rsa AAAA...');
    });

    it('should handle empty response', () => {
      service.reload().subscribe();
      http.expectOne('/api/credentials').flush([]);
      expect(service.list()).toEqual([]);
    });
  });

  describe('create()', () => {
    it('should POST /api/credentials', () => {
      const payload: CreateCredentialPayload = {
        name: 'New Cred',
        username: 'user',
        secret: 'pass123',
        credentialType: 'password',
      };
      let result: Credential | undefined;
      service.create(payload).subscribe((c) => (result = c));

      const req = http.expectOne('/api/credentials');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(payload);
      req.flush({ id: 'new1', ...payload, publicKey: '' });
      expect(result?.id).toBe('new1');
    });
  });

  describe('update()', () => {
    it('should PUT /api/credentials/:id', () => {
      const payload: UpdateCredentialPayload = {
        name: 'Updated',
        username: 'user',
        credentialType: 'password',
      };
      service.update('cr1', payload).subscribe();

      const req = http.expectOne('/api/credentials/cr1');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ id: 'cr1', ...payload });
      req.flush(null);
    });
  });

  describe('remove()', () => {
    it('should DELETE /api/credentials/:id', () => {
      service.remove('cr1').subscribe();
      const req = http.expectOne('/api/credentials/cr1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('generateSsh()', () => {
    it('should POST /api/credentials/generate-ssh', () => {
      let result: { privateKey: string; publicKey: string } | undefined;
      service.generateSsh('ed25519').subscribe((r) => (result = r));

      const req = http.expectOne('/api/credentials/generate-ssh');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ keyType: 'ed25519' });
      req.flush({ privateKey: '-----BEGIN...', publicKey: 'ssh-ed25519 AAAA...' });
      expect(result?.publicKey).toContain('ssh-ed25519');
    });
  });
});
