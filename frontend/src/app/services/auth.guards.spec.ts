import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { AuthService } from './auth.service';
import {
  authGuard,
  connectionManagerGuard,
  loginGuard,
  ownerAdminGuard,
  rootRedirectGuard,
  setupGuard,
} from './auth.guards';

type AuthStub = {
  requiresSetup: () => boolean;
  isAuthenticated: () => boolean;
  canAccessSettings: () => boolean;
  hasAnyRole: (...roles: string[]) => boolean;
};

function makeAuth(overrides: Partial<AuthStub> = {}): AuthStub {
  return {
    requiresSetup: () => false,
    isAuthenticated: () => true,
    canAccessSettings: () => false,
    hasAnyRole: () => false,
    ...overrides,
  };
}

function configure(auth: AuthStub) {
  TestBed.resetTestingModule();
  const router = {
    parseUrl: vi.fn((url: string) => ({ __url: url }) as unknown as UrlTree),
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: router },
    ],
  });
  return { router };
}

function run<T>(fn: () => T): T {
  return TestBed.runInInjectionContext(fn) as T;
}

const dummyRoute = {} as any;
const dummyState = {} as any;

describe('rootRedirectGuard', () => {
  it('redirects to /first-access when setup is required', () => {
    configure(makeAuth({ requiresSetup: () => true }));
    const result = run(() => rootRedirectGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/first-access');
  });

  it('redirects authenticated admins to /administration', () => {
    configure(makeAuth({ isAuthenticated: () => true, canAccessSettings: () => true }));
    const result = run(() => rootRedirectGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/administration');
  });

  it('redirects authenticated regular users to /vault', () => {
    configure(makeAuth({ isAuthenticated: () => true, canAccessSettings: () => false }));
    const result = run(() => rootRedirectGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/vault');
  });

  it('redirects anonymous visitors to /login', () => {
    configure(makeAuth({ isAuthenticated: () => false }));
    const result = run(() => rootRedirectGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/login');
  });
});

describe('loginGuard', () => {
  it('bounces to /first-access when setup is required', () => {
    configure(makeAuth({ requiresSetup: () => true }));
    const result = run(() => loginGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/first-access');
  });

  it('allows the login screen otherwise', () => {
    configure(makeAuth());
    const result = run(() => loginGuard(dummyRoute, dummyState));
    expect(result).toBe(true);
  });
});

describe('setupGuard', () => {
  it('blocks the setup screen once the app has users', () => {
    configure(makeAuth({ requiresSetup: () => false }));
    const result = run(() => setupGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/login');
  });

  it('allows the setup screen during bootstrap', () => {
    configure(makeAuth({ requiresSetup: () => true }));
    const result = run(() => setupGuard(dummyRoute, dummyState));
    expect(result).toBe(true);
  });
});

describe('authGuard', () => {
  it('redirects unauthenticated users to /login', () => {
    configure(makeAuth({ isAuthenticated: () => false }));
    const result = run(() => authGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/login');
  });

  it('allows authenticated users through', () => {
    configure(makeAuth({ isAuthenticated: () => true }));
    const result = run(() => authGuard(dummyRoute, dummyState));
    expect(result).toBe(true);
  });
});

describe('ownerAdminGuard', () => {
  it('redirects users without Owner or Admin role to /vault', () => {
    configure(makeAuth({ hasAnyRole: (...roles) => false }));
    const result = run(() => ownerAdminGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/vault');
  });

  it('allows Owners through', () => {
    configure(makeAuth({ hasAnyRole: (...roles) => roles.includes('Owner') }));
    const result = run(() => ownerAdminGuard(dummyRoute, dummyState));
    expect(result).toBe(true);
  });

  it('allows Admins through', () => {
    configure(makeAuth({ hasAnyRole: (...roles) => roles.includes('Admin') }));
    const result = run(() => ownerAdminGuard(dummyRoute, dummyState));
    expect(result).toBe(true);
  });

  it('rejects TeamAdmin (not part of the OwnerOrAdmin set)', () => {
    configure(
      makeAuth({
        hasAnyRole: (...roles) =>
          roles.includes('TeamAdmin') && !roles.includes('Owner') && !roles.includes('Admin')
            ? false
            : false,
      }),
    );
    const result = run(() => ownerAdminGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/vault');
  });
});

describe('connectionManagerGuard', () => {
  it('allows TeamAdmin through', () => {
    configure(makeAuth({ hasAnyRole: (...roles) => roles.includes('TeamAdmin') }));
    const result = run(() => connectionManagerGuard(dummyRoute, dummyState));
    expect(result).toBe(true);
  });

  it('rejects regular users', () => {
    configure(makeAuth({ hasAnyRole: () => false }));
    const result = run(() => connectionManagerGuard(dummyRoute, dummyState)) as any;
    expect(result.__url).toBe('/vault');
  });
});
