import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { Authentication } from './authentication';
import { AuthService, Providers } from '../../services/auth.service';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';

describe('Authentication', () => {
  let component: Authentication;
  let fixture: ComponentFixture<Authentication>;
  let auth: {
    providers: ReturnType<typeof signal<Providers>>;
    canAccessSettings: ReturnType<typeof signal<boolean>>;
    login: ReturnType<typeof vi.fn>;
  };
  let runtimeConfig: { config: { helpUrl: string } };

  beforeEach(async () => {
    auth = {
      providers: signal<Providers>({ local: true, sso: null }),
      canAccessSettings: signal(false),
      login: vi.fn(() => of(undefined)),
    };
    runtimeConfig = { config: { helpUrl: 'https://help.example.com' } };

    await TestBed.configureTestingModule({
      imports: [Authentication],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: RuntimeConfigService, useValue: runtimeConfig },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Authentication);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('helpUrl proxies runtime config', () => {
    expect(component.helpUrl).toBe('https://help.example.com');
  });

  it('sso() returns providers.sso', () => {
    expect(component.sso()).toBeNull();
    auth.providers.set({ local: true, sso: { enabled: true, type: 'Oidc', name: 'Okta' } });
    expect(component.sso()?.name).toBe('Okta');
  });

  describe('mascotState', () => {
    it('"password" when password focused', () => {
      component.onPasswordFocus();
      expect(component.mascotState()).toBe('password');
    });

    it('"sso" when ssoLoading', () => {
      component.ssoLoading.set(true);
      expect(component.mascotState()).toBe('sso');
    });

    it('"loading" when submitting', () => {
      component.submitting.set(true);
      expect(component.mascotState()).toBe('loading');
    });

    it('"error" when errorMessage is set', () => {
      component.errorMessage.set('bad');
      expect(component.mascotState()).toBe('error');
    });

    it('"typing" when typingLength > 0', () => {
      component.typingLength.set(3);
      expect(component.mascotState()).toBe('typing');
    });

    it('"idle" by default', () => {
      expect(component.mascotState()).toBe('idle');
    });
  });

  describe('focus/visibility helpers', () => {
    it('onPasswordBlur clears focus', () => {
      component.onPasswordFocus();
      component.onPasswordBlur();
      expect(component.mascotState()).toBe('idle');
    });

    it('togglePasswordVisibility flips', () => {
      expect(component.passwordVisible()).toBe(false);
      component.togglePasswordVisibility();
      expect(component.passwordVisible()).toBe(true);
    });

    it('onEmailInput updates typingLength', () => {
      const input = document.createElement('input');
      input.value = 'abc';
      component.onEmailInput({ target: input } as unknown as Event);
      expect(component.typingLength()).toBe(3);
    });
  });

  describe('submit()', () => {
    it('no-ops while submitting', () => {
      component.submitting.set(true);
      component.submit();
      expect(auth.login).not.toHaveBeenCalled();
    });

    it('marks all as touched and returns when form invalid', () => {
      const spy = vi.spyOn(component.form, 'markAllAsTouched');
      component.submit();
      expect(spy).toHaveBeenCalled();
      expect(auth.login).not.toHaveBeenCalled();
    });

    it('navigates to /vault on success when not admin', () => {
      const router = TestBed.inject(Router);
      const nav = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
      component.form.setValue({ email: 'a@b.com', password: 'pw' });
      component.submit();
      expect(auth.login).toHaveBeenCalledWith({ email: 'a@b.com', password: 'pw' });
      expect(nav).toHaveBeenCalledWith('/vault');
    });

    it('navigates to /administration when canAccessSettings', () => {
      auth.canAccessSettings.set(true);
      const router = TestBed.inject(Router);
      const nav = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
      component.form.setValue({ email: 'a@b.com', password: 'pw' });
      component.submit();
      expect(nav).toHaveBeenCalledWith('/administration');
    });

    it('shows backend error message', () => {
      auth.login.mockReturnValueOnce(throwError(() => ({ error: { message: 'bad creds' } })));
      component.form.setValue({ email: 'a@b.com', password: 'pw' });
      component.submit();
      expect(component.errorMessage()).toBe('bad creds');
      expect(component.submitting()).toBe(false);
    });

    it('shows rate-limit message on 429', () => {
      auth.login.mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 429, statusText: 'Too many' })),
      );
      component.form.setValue({ email: 'a@b.com', password: 'pw' });
      component.submit();
      expect(component.errorMessage()).toContain('Too many');
    });

    it('falls back to generic error message', () => {
      auth.login.mockReturnValueOnce(throwError(() => ({})));
      component.form.setValue({ email: 'a@b.com', password: 'pw' });
      component.submit();
      expect(component.errorMessage()).toBe('Sign-in failed.');
    });
  });

  describe('loginWithSso()', () => {
    it('no-ops while ssoLoading', () => {
      component.ssoLoading.set(true);
      const setter = vi.fn();
      Object.defineProperty(globalThis, 'location', {
        configurable: true,
        get: () => ({
          set href(v: string) {
            setter(v);
          },
        }),
      });
      component.loginWithSso();
      expect(setter).not.toHaveBeenCalled();
    });

    it('sets ssoLoading and redirects to SSO initiate', () => {
      const setter = vi.fn();
      Object.defineProperty(globalThis, 'location', {
        configurable: true,
        get: () => ({
          set href(v: string) {
            setter(v);
          },
        }),
      });
      component.loginWithSso();
      expect(component.ssoLoading()).toBe(true);
      expect(setter).toHaveBeenCalled();
      expect(setter.mock.calls[0][0]).toContain('/api/auth/sso/initiate');
    });
  });
});
