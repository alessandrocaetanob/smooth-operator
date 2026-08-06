import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { of, throwError } from 'rxjs';

import { Invite } from './invite';
import { InvitesService, InvitePreview } from '../../services/invites.service';

const INVITE_TRANSLATIONS = {
  pages: {
    invite: {
      missingToken: 'Missing invitation token.',
      invalidOrExpired: 'This invitation is invalid or has expired.',
      passwordTooShort: 'Password must be at least 8 characters.',
      passwordMismatch: 'Passwords do not match.',
      setupFailed: 'Failed to complete account setup.',
    },
  },
};

describe('Invite', () => {
  let component: Invite;
  let fixture: ComponentFixture<Invite>;
  let invites: {
    preview: ReturnType<typeof vi.fn>;
    redeem: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };
  let paramMap: ReturnType<typeof convertToParamMap>;

  beforeEach(async () => {
    paramMap = convertToParamMap({ token: 'inv-tok-1' });
    invites = {
      preview: vi.fn(() => of({ name: 'Alice', email: 'alice@x' } as unknown as InvitePreview)),
      redeem: vi.fn(() => of(undefined)),
    };
    router = { navigate: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [Invite],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: InvitesService, useValue: invites },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap } },
        },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', INVITE_TRANSLATIONS);
    translate.use('en');

    fixture = TestBed.createComponent(Invite);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit reports missing token', async () => {
    paramMap = convertToParamMap({});
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [Invite],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: InvitesService, useValue: invites },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap } } },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', INVITE_TRANSLATIONS);
    translate.use('en');

    fixture = TestBed.createComponent(Invite);
    component = fixture.componentInstance;
    component.ngOnInit();
    expect(component.error()).toBe('Missing invitation token.');
    expect(component.loading()).toBe(false);
  });

  it('ngOnInit loads preview and seeds name', () => {
    component.ngOnInit();
    expect(component.preview()?.name).toBe('Alice');
    expect(component.name()).toBe('Alice');
    expect(component.loading()).toBe(false);
  });

  it('ngOnInit surfaces preview failure', () => {
    invites.preview.mockReturnValueOnce(throwError(() => ({ message: 'expired' })));
    component.ngOnInit();
    expect(component.error()).toBe('expired');
    expect(component.loading()).toBe(false);
  });

  it('ngOnInit falls back to default invite error', () => {
    invites.preview.mockReturnValueOnce(throwError(() => ({})));
    component.ngOnInit();
    expect(component.error()).toBe('This invitation is invalid or has expired.');
  });

  describe('submit()', () => {
    beforeEach(() => component.ngOnInit());

    it('no-ops when busy', () => {
      component.busy.set(true);
      component.submit();
      expect(invites.redeem).not.toHaveBeenCalled();
    });

    it('rejects passwords shorter than 8 chars', () => {
      component.password.set('short');
      component.confirm.set('short');
      component.submit();
      expect(component.error()).toContain('at least 8');
      expect(invites.redeem).not.toHaveBeenCalled();
    });

    it('rejects mismatched passwords', () => {
      component.password.set('password1');
      component.confirm.set('password2');
      component.submit();
      expect(component.error()).toContain('do not match');
    });

    it('redeems on valid input and navigates to /login', () => {
      vi.useFakeTimers();
      component.password.set('strongpassword');
      component.confirm.set('strongpassword');
      component.name.set('Alice');
      component.submit();
      expect(invites.redeem).toHaveBeenCalledWith('inv-tok-1', {
        password: 'strongpassword',
        name: 'Alice',
      });
      expect(component.success()).toBe(true);
      vi.advanceTimersByTime(1600);
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('omits empty name from payload', () => {
      component.password.set('strongpassword');
      component.confirm.set('strongpassword');
      component.name.set('  ');
      component.submit();
      expect(invites.redeem).toHaveBeenCalledWith('inv-tok-1', {
        password: 'strongpassword',
        name: undefined,
      });
    });

    it('surfaces backend error', () => {
      invites.redeem.mockReturnValueOnce(throwError(() => ({ message: 'taken' })));
      component.password.set('strongpassword');
      component.confirm.set('strongpassword');
      component.submit();
      expect(component.error()).toBe('taken');
      expect(component.busy()).toBe(false);
    });

    it('falls back to default submit error', () => {
      invites.redeem.mockReturnValueOnce(throwError(() => ({})));
      component.password.set('strongpassword');
      component.confirm.set('strongpassword');
      component.submit();
      expect(component.error()).toBe('Failed to complete account setup.');
    });
  });
});
