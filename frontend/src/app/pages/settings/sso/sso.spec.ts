import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { SsoSettings } from './sso';
import { SsoProviderView, SsoSettingsService } from '../../../services/sso-settings.service';
import { AuthService } from '../../../services/auth.service';

const EN_TRANSLATIONS = {
  common: {
    actions: {
      cancel: 'Cancel',
      delete: 'Delete',
    },
  },
  pages: {
    settingsSso: {
      status: {
        notConfigured: 'Not configured',
      },
      messages: {
        loadError: 'Failed to load SSO settings.',
        saveSuccess: 'SSO configuration saved.',
        saveError: 'Failed to save SSO settings.',
        toggleOnSuccess: 'SSO enabled.',
        toggleOffSuccess: 'SSO disabled.',
        toggleError: 'Failed to toggle SSO.',
        deleteSuccess: 'SSO provider deleted.',
        deleteError: 'Failed to delete SSO provider.',
        testError: 'SSO test failed.',
        copyMetadataSuccess: 'Metadata URL copied to clipboard.',
        copyCallbackSuccess: 'Redirect URI copied to clipboard.',
        copyAcsSuccess: 'ACS URL copied to clipboard.',
        copyError: 'Could not copy to clipboard.',
      },
    },
  },
};

describe('SsoSettings', () => {
  let component: SsoSettings;
  let fixture: ComponentFixture<SsoSettings>;
  let svc: {
    current: ReturnType<typeof signal<SsoProviderView | null>>;
    load: ReturnType<typeof vi.fn>;
    upsertOidc: ReturnType<typeof vi.fn>;
    upsertSaml: ReturnType<typeof vi.fn>;
    toggle: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
    test: ReturnType<typeof vi.fn>;
  };
  let auth: { loadSetupStatus: ReturnType<typeof vi.fn> };

  const baseView: SsoProviderView = {
    isEnabled: false,
    type: '',
    name: '',
    oidc: null,
    saml: null,
  };

  beforeEach(async () => {
    svc = {
      current: signal<SsoProviderView | null>(null),
      load: vi.fn(() => of({ ...baseView })),
      upsertOidc: vi.fn(() => of(undefined)),
      upsertSaml: vi.fn(() => of(undefined)),
      toggle: vi.fn(() => of(undefined)),
      remove: vi.fn(() => of(undefined)),
      test: vi.fn(() => of({ success: true, message: 'ok' })),
    };
    auth = { loadSetupStatus: vi.fn(() => of(undefined)) };

    await TestBed.configureTestingModule({
      imports: [SsoSettings],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: SsoSettingsService, useValue: svc },
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', EN_TRANSLATIONS);
    translate.use('en');

    fixture = TestBed.createComponent(SsoSettings);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('refresh()', () => {
    it('ngOnInit calls refresh', () => {
      const spy = vi.spyOn(component, 'refresh');
      component.ngOnInit();
      expect(spy).toHaveBeenCalled();
    });

    it('loads provider and applies oidc fields', () => {
      svc.load.mockReturnValueOnce(
        of({
          isEnabled: true,
          type: 'Oidc',
          name: 'Okta',
          oidc: {
            authority: 'https://okta.example.com',
            clientId: 'cid',
            scopes: 'openid email',
            subjectClaim: 'sub',
            emailClaim: 'email',
            nameClaim: 'name',
            hasClientSecret: true,
          },
          saml: null,
        }),
      );
      component.refresh();
      expect(component.type()).toBe('Oidc');
      expect(component.name()).toBe('Okta');
      expect(component.oidcAuthority()).toBe('https://okta.example.com');
      expect(component.oidcClientSecret()).toBe('');
    });

    it('loads provider and applies saml fields', () => {
      svc.load.mockReturnValueOnce(
        of({
          isEnabled: true,
          type: 'Saml',
          name: 'Okta SAML',
          oidc: null,
          saml: {
            spEntityId: 'sp',
            idpEntityId: 'idp',
            idpSsoUrl: 'https://x',
            idpCertificate: 'CERT',
            spCertificate: 'SPCERT',
            nameIdFormat: 'urn:fmt',
            attributeEmail: '',
            attributeName: '',
            wantAssertionsSigned: false,
            wantResponseSigned: false,
            hasSpPrivateKey: false,
          },
        }),
      );
      component.refresh();
      expect(component.type()).toBe('Saml');
      expect(component.samlSpEntityId()).toBe('sp');
      expect(component.samlAttributeEmail()).toBe('email');
      expect(component.samlAttributeName()).toBe('name');
    });

    it('falls back to defaults for missing oidc claim fields', () => {
      svc.load.mockReturnValueOnce(
        of({
          isEnabled: true,
          type: 'Oidc',
          name: 'X',
          oidc: {
            authority: 'a',
            clientId: 'c',
            scopes: '',
            subjectClaim: '',
            emailClaim: '',
            nameClaim: '',
            hasClientSecret: false,
          },
          saml: null,
        }),
      );
      component.refresh();
      expect(component.oidcScopes()).toBe('openid profile email');
      expect(component.oidcSubjectClaim()).toBe('sub');
      expect(component.oidcEmailClaim()).toBe('email');
      expect(component.oidcNameClaim()).toBe('name');
    });

    it('handles load failure', () => {
      svc.load.mockReturnValueOnce(throwError(() => ({ message: 'unauthorized' })));
      component.refresh();
      expect(component.loading()).toBe(false);
      expect(component.error()).toBe('unauthorized');
    });

    it('falls back to default error message on load failure', () => {
      svc.load.mockReturnValueOnce(throwError(() => ({})));
      component.refresh();
      expect(component.error()).toBe('Failed to load SSO settings.');
    });
  });

  describe('computed urls and labels', () => {
    it('metadataUrl, callbackUrl, acsUrl all use origin', () => {
      const origin = globalThis.location.origin;
      expect(component.metadataUrl()).toBe(`${origin}/api/auth/sso/metadata`);
      expect(component.callbackUrl()).toBe(`${origin}/api/auth/sso/callback`);
      expect(component.acsUrl()).toBe(`${origin}/api/auth/sso/acs`);
    });

    it('providerLabel and hasProvider reflect current()', () => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      svc.current.set({ type: null } as any);
      expect(component.hasProvider()).toBe(false);
      expect(component.providerLabel()).toBe('Not configured');
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      svc.current.set({ type: 'Oidc', name: 'Okta' } as any);
      expect(component.hasProvider()).toBe(true);
      expect(component.providerLabel()).toBe('Okta (Oidc)');
    });
  });

  describe('save()', () => {
    it('no-ops when busy', () => {
      component.busy.set(true);
      component.save();
      expect(svc.upsertOidc).not.toHaveBeenCalled();
    });

    it('saves Oidc and refreshes', () => {
      component.type.set('Oidc');
      component.name.set('Okta');
      component.oidcAuthority.set('https://x');
      component.oidcClientId.set('cid');
      component.oidcClientSecret.set('secret');
      component.save();
      expect(svc.upsertOidc).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Okta',
          authority: 'https://x',
          clientId: 'cid',
          clientSecret: 'secret',
        }),
      );
      expect(component.message()).toBe('SSO configuration saved.');
      expect(auth.loadSetupStatus).toHaveBeenCalled();
      expect(component.busy()).toBe(false);
    });

    it('omits Oidc clientSecret when empty', () => {
      component.type.set('Oidc');
      component.oidcClientSecret.set('');
      component.save();
      expect(svc.upsertOidc).toHaveBeenCalledWith(expect.objectContaining({ clientSecret: null }));
    });

    it('saves Saml and refreshes', () => {
      component.type.set('Saml');
      component.name.set('Okta SAML');
      component.samlSpEntityId.set('sp');
      component.samlIdpEntityId.set('idp');
      component.samlIdpSsoUrl.set('https://x');
      component.samlSpPrivateKey.set('PRIVATE');
      component.save();
      expect(svc.upsertSaml).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Okta SAML',
          spEntityId: 'sp',
          spPrivateKey: 'PRIVATE',
        }),
      );
      expect(component.busy()).toBe(false);
    });

    it('omits Saml spPrivateKey when empty', () => {
      component.type.set('Saml');
      component.samlSpPrivateKey.set('');
      component.save();
      expect(svc.upsertSaml).toHaveBeenCalledWith(expect.objectContaining({ spPrivateKey: null }));
    });

    it('falls back to default Saml attributes', () => {
      component.type.set('Saml');
      component.samlAttributeEmail.set('');
      component.samlAttributeName.set('');
      component.save();
      expect(svc.upsertSaml).toHaveBeenCalledWith(
        expect.objectContaining({ attributeEmail: 'email', attributeName: 'name' }),
      );
    });

    it('surfaces error from upsert', () => {
      svc.upsertOidc.mockReturnValueOnce(throwError(() => ({ message: 'bad cert' })));
      component.type.set('Oidc');
      component.save();
      expect(component.error()).toBe('bad cert');
      expect(component.busy()).toBe(false);
    });

    it('falls back to default save error message', () => {
      svc.upsertOidc.mockReturnValueOnce(throwError(() => ({})));
      component.type.set('Oidc');
      component.save();
      expect(component.error()).toBe('Failed to save SSO settings.');
    });
  });

  describe('toggle()', () => {
    it('no-ops when toggleBusy', () => {
      component.toggleBusy.set(true);
      component.toggle(true);
      expect(svc.toggle).not.toHaveBeenCalled();
    });

    it('toggles on and sets message', () => {
      component.toggle(true);
      expect(svc.toggle).toHaveBeenCalledWith(true);
      expect(component.message()).toBe('SSO enabled.');
    });

    it('toggles off and sets message', () => {
      component.toggle(false);
      expect(component.message()).toBe('SSO disabled.');
    });

    it('surfaces toggle error', () => {
      svc.toggle.mockReturnValueOnce(throwError(() => ({ message: 'denied' })));
      component.toggle(true);
      expect(component.error()).toBe('denied');
    });

    it('falls back to default toggle error', () => {
      svc.toggle.mockReturnValueOnce(throwError(() => ({})));
      component.toggle(true);
      expect(component.error()).toBe('Failed to toggle SSO.');
    });

    it('clears toggleBusy if subsequent load fails', () => {
      svc.toggle.mockReturnValueOnce(of(undefined));
      svc.load.mockReturnValueOnce(throwError(() => new Error('x')));
      component.toggle(true);
      expect(component.toggleBusy()).toBe(false);
    });
  });

  describe('remove()', () => {
    it('no-ops when deleteBusy', () => {
      component.deleteBusy.set(true);
      component.remove();
      expect(svc.remove).not.toHaveBeenCalled();
    });

    it('removes provider and reloads', () => {
      component.confirmingDelete.set(true);
      component.remove();
      expect(svc.remove).toHaveBeenCalled();
      expect(component.confirmingDelete()).toBe(false);
      expect(component.message()).toBe('SSO provider deleted.');
    });

    it('surfaces error', () => {
      svc.remove.mockReturnValueOnce(throwError(() => ({ message: 'in-use' })));
      component.remove();
      expect(component.error()).toBe('in-use');
    });

    it('falls back to default error', () => {
      svc.remove.mockReturnValueOnce(throwError(() => ({})));
      component.remove();
      expect(component.error()).toBe('Failed to delete SSO provider.');
    });

    it('clears deleteBusy when load following remove fails', () => {
      svc.remove.mockReturnValueOnce(of(undefined));
      svc.load.mockReturnValueOnce(throwError(() => new Error('x')));
      component.remove();
      expect(component.deleteBusy()).toBe(false);
    });
  });

  describe('runTest()', () => {
    it('no-ops when busy', () => {
      component.testBusy.set(true);
      component.runTest();
      expect(svc.test).not.toHaveBeenCalled();
    });

    it('sets testResult on success', () => {
      component.runTest();
      expect(component.testResult()).toEqual({ success: true, message: 'ok' });
      expect(component.testBusy()).toBe(false);
    });

    it('sets failed testResult on error', () => {
      svc.test.mockReturnValueOnce(throwError(() => ({ message: 'no idp' })));
      component.runTest();
      expect(component.testResult()).toEqual({ success: false, message: 'no idp' });
    });

    it('falls back to default test failure message', () => {
      svc.test.mockReturnValueOnce(throwError(() => ({})));
      component.runTest();
      expect(component.testResult()?.message).toBe('SSO test failed.');
    });
  });

  describe('copy helpers', () => {
    let originalClipboard: PropertyDescriptor | undefined;

    beforeEach(() => {
      originalClipboard = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    });

    afterEach(() => {
      if (originalClipboard) {
        Object.defineProperty(navigator, 'clipboard', originalClipboard);
      } else {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        delete (navigator as any).clipboard;
      }
    });

    function mockClipboard(success: boolean) {
      const writeText = vi.fn(() => (success ? Promise.resolve() : Promise.reject(new Error('x'))));
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText },
      });
      return writeText;
    }

    it('copyMetadataUrl sets message on success', async () => {
      const wt = mockClipboard(true);
      component.copyMetadataUrl();
      await Promise.resolve();
      await Promise.resolve();
      expect(wt).toHaveBeenCalledWith(component.metadataUrl());
      expect(component.message()).toBe('Metadata URL copied to clipboard.');
    });

    it('copyCallbackUrl sets message on success', async () => {
      mockClipboard(true);
      component.copyCallbackUrl();
      await Promise.resolve();
      await Promise.resolve();
      expect(component.message()).toBe('Redirect URI copied to clipboard.');
    });

    it('copyAcsUrl sets message on success', async () => {
      mockClipboard(true);
      component.copyAcsUrl();
      await Promise.resolve();
      await Promise.resolve();
      expect(component.message()).toBe('ACS URL copied to clipboard.');
    });

    it('sets error when clipboard rejects', async () => {
      mockClipboard(false);
      component.copyMetadataUrl();
      await Promise.resolve();
      await Promise.resolve();
      expect(component.error()).toBe('Could not copy to clipboard.');
    });
  });
});
