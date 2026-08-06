import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { InvitesService, InvitePreview } from './invites.service';

describe('InvitesService', () => {
  let service: InvitesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [InvitesService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(InvitesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(service).toBeTruthy();
  });

  describe('preview()', () => {
    it('should GET /api/invites/:encodedToken and normalize response', () => {
      const token = 'abc123';
      let result: InvitePreview | undefined;
      service.preview(token).subscribe((p) => (result = p));

      const req = http.expectOne(`/api/invites/${encodeURIComponent(token)}`);
      expect(req.request.method).toBe('GET');
      req.flush({ email: 'alice@example.com', name: 'Alice', type: 'user_invite' });

      expect(result?.email).toBe('alice@example.com');
      expect(result?.name).toBe('Alice');
      expect(result?.type).toBe('user_invite');
    });

    it('should URL-encode special characters in token', () => {
      const token = 'token/with=special+chars';
      service.preview(token).subscribe();
      const req = http.expectOne(`/api/invites/${encodeURIComponent(token)}`);
      req.flush({ email: 'b@b.com', name: 'B', type: 'user_invite' });
      expect(req.request.url).toContain(encodeURIComponent(token));
    });

    it('should normalize PascalCase response', () => {
      let result: InvitePreview | undefined;
      service.preview('tok1').subscribe((p) => (result = p));

      http.expectOne(`/api/invites/${encodeURIComponent('tok1')}`).flush({
        Email: 'bob@example.com',
        Name: 'Bob',
        Type: 'admin_invite',
      });

      expect(result?.email).toBe('bob@example.com');
      expect(result?.name).toBe('Bob');
      expect(result?.type).toBe('admin_invite');
    });

    it('should default type to user_invite when absent', () => {
      let result: InvitePreview | undefined;
      service.preview('tok').subscribe((p) => (result = p));
      http
        .expectOne(`/api/invites/${encodeURIComponent('tok')}`)
        .flush({ email: 'x@x.com', name: 'X' });
      expect(result?.type).toBe('user_invite');
    });
  });

  describe('redeem()', () => {
    it('should POST /api/invites/:encodedToken/redeem with password', () => {
      const token = 'mytoken';
      service.redeem(token, { password: 's3cr3t' }).subscribe();

      const req = http.expectOne(`/api/invites/${encodeURIComponent(token)}/redeem`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ password: 's3cr3t' });
      req.flush(null);
    });

    it('should include name when provided', () => {
      const token = 'tok2';
      service.redeem(token, { password: 'pass', name: 'Alice' }).subscribe();

      const req = http.expectOne(`/api/invites/${encodeURIComponent(token)}/redeem`);
      expect(req.request.body).toEqual({ password: 'pass', name: 'Alice' });
      req.flush(null);
    });

    it('should URL-encode token in redeem URL', () => {
      const token = 'complex/token=value';
      service.redeem(token, { password: 'pw' }).subscribe();

      const req = http.expectOne(`/api/invites/${encodeURIComponent(token)}/redeem`);
      expect(req.request.url).toContain(encodeURIComponent(token));
      req.flush(null);
    });
  });
});
