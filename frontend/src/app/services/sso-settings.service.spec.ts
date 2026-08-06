import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { SsoSettingsService, SsoProviderView, SsoTestResult } from './sso-settings.service';

describe('SsoSettingsService', () => {
  let svc: SsoSettingsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SsoSettingsService, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    svc = TestBed.inject(SsoSettingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('load() returns empty shape when backend has no provider', () => {
    let result!: SsoProviderView;
    svc.load().subscribe((p) => (result = p));
    const req = http.expectOne('/api/settings/sso');
    req.flush({ Type: '', IsEnabled: false });
    expect(result.type).toBe('');
    expect(result.isEnabled).toBe(false);
    expect(result.oidc).toBeNull();
    expect(result.saml).toBeNull();
    expect(svc.current()).toEqual(result);
  });

  it('load() normalizes PascalCase OIDC payload from .NET', () => {
    let result!: SsoProviderView;
    svc.load().subscribe((p) => (result = p));
    http.expectOne('/api/settings/sso').flush({
      Id: 'p1',
      Name: 'Corporate',
      Type: 'Oidc',
      IsEnabled: true,
      Oidc: {
        Authority: 'https://idp',
        ClientId: 'c1',
        Scopes: 'openid profile email',
        SubjectClaim: 'sub',
        EmailClaim: 'email',
        NameClaim: 'name',
        HasClientSecret: true,
      },
    });
    expect(result.type).toBe('Oidc');
    expect(result.name).toBe('Corporate');
    expect(result.isEnabled).toBe(true);
    expect(result.oidc).toEqual({
      authority: 'https://idp',
      clientId: 'c1',
      scopes: 'openid profile email',
      subjectClaim: 'sub',
      emailClaim: 'email',
      nameClaim: 'name',
      hasClientSecret: true,
    });
    expect(result.saml).toBeNull();
  });

  it('load() normalizes camelCase SAML payload', () => {
    let result!: SsoProviderView;
    svc.load().subscribe((p) => (result = p));
    http.expectOne('/api/settings/sso').flush({
      id: 'p2',
      name: 'IdP',
      type: 'Saml',
      isEnabled: false,
      saml: {
        spEntityId: 'sp',
        idpEntityId: 'idp',
        idpSsoUrl: 'https://idp/sso',
        idpCertificate: 'cert',
        spCertificate: 'spcert',
        nameIdFormat: 'fmt',
        attributeEmail: 'email',
        attributeName: 'name',
        wantAssertionsSigned: true,
        wantResponseSigned: false,
        hasSpPrivateKey: true,
      },
    });
    expect(result.type).toBe('Saml');
    expect(result.saml!.spEntityId).toBe('sp');
    expect(result.saml!.wantResponseSigned).toBe(false);
    expect(result.saml!.hasSpPrivateKey).toBe(true);
    expect(result.oidc).toBeNull();
  });

  it('upsertOidc() PUTs to /api/settings/sso/oidc', () => {
    svc
      .upsertOidc({
        name: 'n',
        authority: 'https://x',
        clientId: 'c',
        clientSecret: 's',
        scopes: 'openid',
        subjectClaim: 'sub',
        emailClaim: 'email',
        nameClaim: 'name',
      })
      .subscribe();
    const req = http.expectOne('/api/settings/sso/oidc');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.name).toBe('n');
    req.flush(null);
  });

  it('toggle() POSTs the enabled flag', () => {
    svc.toggle(true).subscribe();
    const req = http.expectOne('/api/settings/sso/toggle');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ enabled: true });
    req.flush(null);
  });

  it('test() returns a normalized result', () => {
    let result!: SsoTestResult;
    svc.test().subscribe((r) => (result = r));
    const req = http.expectOne('/api/settings/sso/test');
    expect(req.request.method).toBe('POST');
    req.flush({ Success: true, Message: 'ok', Details: { issuer: 'x' } });
    expect(result).toEqual({ success: true, message: 'ok', details: { issuer: 'x' } });
  });
});
