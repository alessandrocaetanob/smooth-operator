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
    localStorage.clear();
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

      // Verify that the _user signal (exposed via currentUser) was updated
      expect(service.currentUser()?.id).toBe('123');
      expect(service.currentUser()?.email).toBe('test@example.com');
    });

    it('should throw an error when the server returns an error', () => {
      let errorThrown = false;
      service.me().subscribe({
        next: () => {},
        error: () => {
          errorThrown = true;
        },
      });

      const req = httpTesting.expectOne('/api/auth/me');
      req.flush('Error', { status: 500, statusText: 'Internal Server Error' });

      expect(errorThrown).toBe(true);
    });
  });

  describe('hasRole', () => {
    it('returns false when user has no roles', () => {
      service.me().subscribe();
      const req = httpTesting.expectOne('/api/auth/me');
      req.flush({ roles: [] });

      expect(service.hasRole('Admin')).toBe(false);
    });

    it('returns true when exact role matches', () => {
      service.me().subscribe();
      const req = httpTesting.expectOne('/api/auth/me');
      req.flush({ roles: ['Admin', 'User'] });

      expect(service.hasRole('Admin')).toBe(true);
    });

    it('returns true when role matches ignoring case', () => {
      service.me().subscribe();
      const req = httpTesting.expectOne('/api/auth/me');
      req.flush({ roles: ['ADMIN'] });

      expect(service.hasRole('admin')).toBe(true);
    });

    it('returns false when role does not match', () => {
      service.me().subscribe();
      const req = httpTesting.expectOne('/api/auth/me');
      req.flush({ roles: ['User'] });

      expect(service.hasRole('Admin')).toBe(false);
    });
  });
});
