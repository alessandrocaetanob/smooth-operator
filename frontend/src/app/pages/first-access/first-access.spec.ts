import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of, throwError } from 'rxjs';

import { FirstAccess } from './first-access';
import { AuthService } from '../../services/auth.service';

describe('FirstAccess', () => {
  let component: FirstAccess;
  let fixture: ComponentFixture<FirstAccess>;
  let auth: { setup: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    auth = { setup: vi.fn(() => of({})) };
    await TestBed.configureTestingModule({
      imports: [FirstAccess],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        firstAccess: {
          createAccountFailed: 'Failed to create the root account.',
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(FirstAccess);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('rejects invalid form', () => {
    component.submit();
    expect(auth.setup).not.toHaveBeenCalled();
  });

  it('rejects mismatched passwords', () => {
    component.form.setValue({
      fullName: 'Alice Wonder',
      email: 'a@b.com',
      password: 'StrongPassword12',
      confirmPassword: 'Different12345!',
    });
    expect(component.form.valid).toBe(false);
  });

  it('accepts matching passwords', () => {
    component.form.setValue({
      fullName: 'Alice Wonder',
      email: 'a@b.com',
      password: 'StrongPassword12',
      confirmPassword: 'StrongPassword12',
    });
    expect(component.form.valid).toBe(true);
  });

  it('submits and navigates on success', () => {
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    component.form.setValue({
      fullName: 'Alice Wonder',
      email: 'a@b.com',
      password: 'StrongPassword12',
      confirmPassword: 'StrongPassword12',
    });
    component.submit();
    expect(auth.setup).toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith('/administration');
  });

  it('shows backend error on failure', () => {
    auth.setup.mockReturnValueOnce(throwError(() => ({ error: { message: 'used' } })));
    component.form.setValue({
      fullName: 'Alice',
      email: 'a@b.com',
      password: 'StrongPassword12',
      confirmPassword: 'StrongPassword12',
    });
    component.submit();
    expect(component.errorMessage()).toBe('used');
    expect(component.submitting()).toBe(false);
  });

  it('falls back to default error', () => {
    auth.setup.mockReturnValueOnce(throwError(() => ({})));
    component.form.setValue({
      fullName: 'Alice',
      email: 'a@b.com',
      password: 'StrongPassword12',
      confirmPassword: 'StrongPassword12',
    });
    component.submit();
    expect(component.errorMessage()).toBe('Failed to create the root account.');
  });

  it('submit no-ops when already submitting', () => {
    component.submitting.set(true);
    component.form.setValue({
      fullName: 'Alice',
      email: 'a@b.com',
      password: 'StrongPassword12',
      confirmPassword: 'StrongPassword12',
    });
    component.submit();
    expect(auth.setup).not.toHaveBeenCalled();
  });
});
