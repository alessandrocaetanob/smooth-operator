import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { AuthService, UserInfo } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  describe('me()', () => {
    it('should fetch user info and update currentUser state', () => {
      const mockUserInfo: UserInfo = {
        id: '123',
        email: 'test@example.com',
        name: 'Test User',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: ['Admin'],
      };

      let emittedUser: UserInfo | undefined;
      service.me().subscribe((user) => {
        emittedUser = user;
      });

      const req = httpTesting.expectOne('/api/auth/me');
      expect(req.request.method).toEqual('GET');
      req.flush(mockUserInfo);

      expect(emittedUser).toBeTruthy();
      expect(emittedUser?.id).toBe('123');
      expect(emittedUser?.email).toBe('test@example.com');

      expect(service.currentUser()?.id).toBe('123');
      expect(service.currentUser()?.email).toBe('test@example.com');
    });

    it('should throw an error when the server returns an error', () => {
      let errorThrown = false;
      service.me().subscribe({
        next: () => {
          /* intentional no-op */
        },
        error: () => {
          errorThrown = true;
        },
      });

      const req = httpTesting.expectOne('/api/auth/me');
      req.flush('Error', { status: 500, statusText: 'Internal Server Error' });

      expect(errorThrown).toBe(true);
    });

    it('should normalize PascalCase me response', () => {
      service.me().subscribe();
      const req = httpTesting.expectOne('/api/auth/me');
      req.flush({ Id: '99', Name: 'Bob', Email: 'bob@test.com', Roles: ['Admin'] });
      expect(service.currentUser()?.name).toBe('Bob');
    });
  });

  describe('hasRole()', () => {
    it('returns false when user has no roles', () => {
      service.me().subscribe();
      httpTesting.expectOne('/api/auth/me').flush({ roles: [] });
      expect(service.hasRole('Admin')).toBe(false);
    });

    it('returns true when exact role matches', () => {
      service.me().subscribe();
      httpTesting.expectOne('/api/auth/me').flush({ roles: ['Admin', 'User'] });
      expect(service.hasRole('Admin')).toBe(true);
    });

    it('returns true when role matches ignoring case', () => {
      service.me().subscribe();
      httpTesting.expectOne('/api/auth/me').flush({ roles: ['ADMIN'] });
      expect(service.hasRole('admin')).toBe(true);
    });

    it('returns false when role does not match', () => {
      service.me().subscribe();
      httpTesting.expectOne('/api/auth/me').flush({ roles: ['User'] });
      expect(service.hasRole('Admin')).toBe(false);
    });
  });

  describe('hasAnyRole()', () => {
    it('returns true when at least one role matches', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: ['TeamAdmin'],
      });
      expect(service.hasAnyRole('Owner', 'TeamAdmin')).toBe(true);
    });

    it('returns false when none of the roles match', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: ['TeamAdmin'],
      });
      expect(service.hasAnyRole('Owner', 'Admin')).toBe(false);
    });
  });

  describe('loadSetupStatus()', () => {
    it('should GET /api/auth/setup-status and update requiresSetup', () => {
      let result: { requiresSetup: boolean } | undefined;
      service.loadSetupStatus().subscribe((s) => (result = s));
      const req = httpTesting.expectOne('/api/auth/setup-status');
      req.flush({ requiresSetup: true, providers: { local: true, sso: null } });
      expect(result?.requiresSetup).toBe(true);
      expect(service.requiresSetup()).toBe(true);
    });

    it('should normalize PascalCase RequiresSetup', () => {
      service.loadSetupStatus().subscribe();
      httpTesting
        .expectOne('/api/auth/setup-status')
        .flush({ RequiresSetup: true, Providers: { Local: true } });
      expect(service.requiresSetup()).toBe(true);
    });

    it('should return fallback on HTTP error', () => {
      let result: { requiresSetup: boolean } | undefined;
      service.loadSetupStatus().subscribe((s) => (result = s));
      httpTesting
        .expectOne('/api/auth/setup-status')
        .flush('error', { status: 500, statusText: 'Server Error' });
      expect(result?.requiresSetup).toBe(false);
    });

    it('should parse SSO provider info from response', () => {
      let result: { providers: { sso: { type: string; name: string } | null } } | undefined;
      service.loadSetupStatus().subscribe((s) => (result = s as typeof result));
      httpTesting.expectOne('/api/auth/setup-status').flush({
        requiresSetup: false,
        providers: { local: true, sso: true, ssoType: 'Oidc', ssoName: 'My Provider' },
      });
      expect(result?.providers.sso?.type).toBe('Oidc');
      expect(result?.providers.sso?.name).toBe('My Provider');
    });
  });

  describe('login()', () => {
    it('should POST /api/auth/login and set currentUser from response', () => {
      let result: { user: { id: string } } | undefined;
      service.login({ email: 'a@b.com', password: 'secret' }).subscribe((r) => (result = r));
      httpTesting.expectOne('/api/auth/login').flush({
        token: 'ignored',
        expiresAt: '2099-01-01T00:00:00Z',
        user: { id: '42', name: 'Test' },
      });
      expect(result?.user.id).toBe('42');
      expect(service.currentUser()?.id).toBe('42');
    });

    it('should normalize PascalCase login response', () => {
      service.login({ email: 'a@b.com', password: 'secret' }).subscribe();
      httpTesting.expectOne('/api/auth/login').flush({
        Token: 'ignored',
        ExpiresAt: '2099-01-01T00:00:00Z',
        User: { Id: '42', Name: 'Test' },
      });
      expect(service.currentUser()?.name).toBe('Test');
    });

    it('should mark requiresSetup as false after login', () => {
      service.login({ email: 'a@b.com', password: 'secret' }).subscribe();
      httpTesting
        .expectOne('/api/auth/login')
        .flush({ token: 'ignored', expiresAt: '2099-01-01T00:00:00Z', user: {} });
      expect(service.requiresSetup()).toBe(false);
    });
  });

  describe('setup()', () => {
    it('should POST /api/auth/setup and return auth response with user', () => {
      let result: { user: { id: string } } | undefined;
      service
        .setup({ name: 'Admin', email: 'admin@test.com', password: 'pass' })
        .subscribe((r) => (result = r));
      httpTesting.expectOne('/api/auth/setup').flush({
        token: 'ignored',
        expiresAt: '2099-01-01T00:00:00Z',
        user: { id: '1', name: 'Admin' },
      });
      expect(result?.user.id).toBe('1');
    });
  });

  describe('logout()', () => {
    it('should clear currentUser signal', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: [],
      });
      service.logout();
      httpTesting.expectOne('/api/auth/logout').flush({});
      expect(service.currentUser()).toBeNull();
    });

    it('should POST /api/auth/logout to invalidate server session', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: [],
      });
      service.logout();
      const req = httpTesting.expectOne('/api/auth/logout');
      expect(req.request.method).toBe('POST');
      req.flush({});
    });

    it('should be a no-op when no user is set', () => {
      service.logout();
      httpTesting.expectNone('/api/auth/logout');
    });
  });

  describe('setCurrentUser()', () => {
    it('should update currentUser signal', () => {
      const user: UserInfo = {
        id: '7',
        email: 'u@t.com',
        name: 'User',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: [],
      };
      service.setCurrentUser(user);
      expect(service.currentUser()).toEqual(user);
    });
  });

  describe('computed role signals', () => {
    it('isAuthenticated is false without a user', () => {
      expect(service.isAuthenticated()).toBe(false);
    });

    it('isAuthenticated is true when currentUser is set', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: [],
      });
      expect(service.isAuthenticated()).toBe(true);
    });

    it('isOwnerOrAdmin is true for Owner role', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: ['Owner'],
      });
      expect(service.isOwnerOrAdmin()).toBe(true);
    });

    it('canManageConnections is true for TeamAdmin', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: ['TeamAdmin'],
      });
      expect(service.canManageConnections()).toBe(true);
    });

    it('canAccessSettings is false for TeamAdmin', () => {
      service.setCurrentUser({
        id: '1',
        email: 'a@b.com',
        name: 'A',
        hasPassword: true,
        ssoLinked: false,
        ssoProviderType: null,
        roles: ['TeamAdmin'],
      });
      expect(service.canAccessSettings()).toBe(false);
    });
  });
});
