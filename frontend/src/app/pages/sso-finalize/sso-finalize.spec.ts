import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of, throwError } from 'rxjs';

import { SsoFinalize } from './sso-finalize';
import { AuthService } from '../../services/auth.service';

describe('SsoFinalize', () => {
  let auth: { me: ReturnType<typeof vi.fn> };
  let router: { navigateByUrl: ReturnType<typeof vi.fn> };

  function createWithFragment(fragment: string): SsoFinalize {
    TestBed.resetTestingModule();
    auth = { me: vi.fn(() => of({})) };
    router = { navigateByUrl: vi.fn() };
    TestBed.configureTestingModule({
      imports: [SsoFinalize],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { fragment } } },
      ],
    });
    const fixture = TestBed.createComponent(SsoFinalize);
    return fixture.componentInstance;
  }

  beforeEach(() => {
    auth = { me: vi.fn(() => of({})) };
    router = { navigateByUrl: vi.fn() };
  });

  it('should create', () => {
    expect(createWithFragment('')).toBeTruthy();
  });

  it('shows error from fragment', () => {
    const c = createWithFragment('error=invalid_grant');
    c.ngOnInit();
    expect(c.errorMessage()).toBe('invalid_grant');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('calls auth.me and navigates to returnUrl', () => {
    const c = createWithFragment('returnUrl=/vault/special');
    c.ngOnInit();
    expect(auth.me).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/vault/special');
  });

  it('defaults to /vault when no returnUrl', () => {
    const c = createWithFragment('');
    c.ngOnInit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/vault');
  });

  it('rejects unsafe protocol-relative returnUrl', () => {
    const c = createWithFragment('returnUrl=//evil.com/x');
    c.ngOnInit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/vault');
  });

  it('rejects returnUrl with backslash', () => {
    const c = createWithFragment('returnUrl=/some\\path');
    c.ngOnInit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/vault');
  });

  it('rejects returnUrl that does not start with /', () => {
    const c = createWithFragment('returnUrl=relative');
    c.ngOnInit();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/vault');
  });

  it('shows error when auth.me fails', () => {
    const c = createWithFragment('');
    // Wire the failure AFTER createWithFragment swapped in fresh mocks but
    // BEFORE the component initializes — the component reads from `auth` by
    // reference (useValue), so reassigning `me` here is observed at runtime.
    auth.me = vi.fn(() => throwError(() => new Error('x')));
    c.ngOnInit();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(c.errorMessage()).toContain('Authentication');
  });
});
