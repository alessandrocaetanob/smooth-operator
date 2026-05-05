import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { authInterceptor } from './auth.interceptor';

function makeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.sig`;
}

const FUTURE_EXP = Math.floor(Date.now() / 1000) + 3600;

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
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
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should pass through requests when no token is set', () => {
    http.get('/api/connections').subscribe();
    const req = httpMock.expectOne('/api/connections');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('should attach Bearer token to /api/ requests when authenticated', () => {
    const token = makeJwt({ sub: '1', exp: FUTURE_EXP });
    localStorage.setItem('smooth-operator.token', token);
    // Re-create service so it reads token from storage
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigateByUrl: vi.fn() } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);

    http.get('/api/connections').subscribe();
    const req = httpMock.expectOne('/api/connections');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${token}`);
    req.flush([]);
  });

  it('should NOT attach token to non-/api/ requests', () => {
    const token = makeJwt({ sub: '1', exp: FUTURE_EXP });
    localStorage.setItem('smooth-operator.token', token);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigateByUrl: vi.fn() } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);

    http.get('/assets/config.json').subscribe();
    const req = httpMock.expectOne('/assets/config.json');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should NOT redirect on non-401 errors', () => {
    http.get('/api/connections').subscribe({ error: vi.fn() });
    const req = httpMock.expectOne('/api/connections');
    req.flush({ message: 'Not found' }, { status: 404, statusText: 'Not Found' });
    expect((router.navigateByUrl as ReturnType<typeof vi.fn>).mock.calls.length).toBe(0);
  });
});
