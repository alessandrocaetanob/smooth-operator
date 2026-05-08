import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigateByUrl: vi.fn() } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should set withCredentials on /api/ requests', () => {
    http.get('/api/connections').subscribe();
    const req = httpMock.expectOne('/api/connections');
    expect(req.request.withCredentials).toBe(true);
    req.flush([]);
  });

  it('should NOT set withCredentials on non-/api/ requests', () => {
    http.get('/assets/config.json').subscribe();
    const req = httpMock.expectOne('/assets/config.json');
    expect(req.request.withCredentials).toBe(false);
    req.flush({});
  });

  it('should NOT send Authorization header (cookie-based auth)', () => {
    http.get('/api/connections').subscribe();
    const req = httpMock.expectOne('/api/connections');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('should redirect to /login on 401 from /api/ when authenticated', () => {
    authService.setCurrentUser({
      id: '1', email: 'a@b.com', name: 'A', hasPassword: true,
      ssoLinked: false, ssoProviderType: null, roles: [],
    });
    http.get('/api/connections').subscribe({ error: vi.fn() });
    const req = httpMock.expectOne('/api/connections');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    // auth.logout() fires a POST to /api/auth/logout — flush it so httpMock.verify() passes
    httpMock.expectOne('/api/auth/logout').flush({});
    expect((router.navigateByUrl as ReturnType<typeof vi.fn>).mock.calls[0]?.[0]).toBe('/login');
  });

  it('should NOT redirect on 401 from /api/auth/login', () => {
    authService.setCurrentUser({
      id: '1', email: 'a@b.com', name: 'A', hasPassword: true,
      ssoLinked: false, ssoProviderType: null, roles: [],
    });
    http.post('/api/auth/login', {}).subscribe({ error: vi.fn() });
    const req = httpMock.expectOne('/api/auth/login');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect((router.navigateByUrl as ReturnType<typeof vi.fn>).mock.calls.length).toBe(0);
  });

  it('should NOT redirect on 401 from /api/auth/logout', () => {
    authService.setCurrentUser({
      id: '1', email: 'a@b.com', name: 'A', hasPassword: true,
      ssoLinked: false, ssoProviderType: null, roles: [],
    });
    http.post('/api/auth/logout', {}).subscribe({ error: vi.fn() });
    const req = httpMock.expectOne('/api/auth/logout');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect((router.navigateByUrl as ReturnType<typeof vi.fn>).mock.calls.length).toBe(0);
  });

  it('should NOT redirect on non-401 errors', () => {
    http.get('/api/connections').subscribe({ error: vi.fn() });
    const req = httpMock.expectOne('/api/connections');
    req.flush({ message: 'Not found' }, { status: 404, statusText: 'Not Found' });
    expect((router.navigateByUrl as ReturnType<typeof vi.fn>).mock.calls.length).toBe(0);
  });
});
