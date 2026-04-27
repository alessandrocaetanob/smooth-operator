import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { AuthService } from './auth.service';

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

  describe('hasRole', () => {
    it('returns false when user has no roles', () => {
      // Mock the me endpoint to return a user with empty roles
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
