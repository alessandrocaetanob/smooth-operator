import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';

import { Email } from './email';
import { SmtpSettings, SmtpSettingsService } from '../../../services/smtp-settings.service';
import { AuthService } from '../../../services/auth.service';

describe('Email', () => {
  let component: Email;
  let fixture: ComponentFixture<Email>;
  let smtp: {
    current: ReturnType<typeof signal<SmtpSettings | null>>;
    load: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    test: ReturnType<typeof vi.fn>;
  };
  let auth: {
    currentUser: ReturnType<typeof signal<{ id: string; email: string } | null>>;
  };

  const baseSettings: SmtpSettings = {
    configured: true,
    host: 'smtp.example.com',
    port: 587,
    useSsl: true,
    username: 'user',
    fromAddress: 'noreply@example.com',
    fromName: 'Smooth',
    enabled: true,
    hasPassword: true,
  };

  beforeEach(async () => {
    smtp = {
      current: signal<SmtpSettings | null>(null),
      load: vi.fn(() => of(baseSettings)),
      update: vi.fn(() => of(baseSettings)),
      test: vi.fn(() => of(undefined)),
    };
    auth = { currentUser: signal({ id: 'me', email: 'me@example.com' }) };

    await TestBed.configureTestingModule({
      imports: [Email],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: SmtpSettingsService, useValue: smtp },
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        settingsEmail: {
          errors: {
            loadFailed: 'Failed to load SMTP settings.',
            saveFailed: 'Failed to save SMTP settings.',
            recipientRequired: 'Enter a recipient address.',
            testFailed: 'Test failed.',
          },
          messages: {
            saved: 'SMTP settings saved.',
            testSent: 'Test email sent to {{to}}.',
          },
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(Email);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit refreshes and applies form', () => {
    component.ngOnInit();
    expect(component.host()).toBe('smtp.example.com');
    expect(component.testTo()).toBe('me@example.com');
  });

  it('refresh failure surfaces error', () => {
    smtp.load.mockReturnValueOnce(throwError(() => ({ message: 'down' })));
    component.refresh();
    expect(component.error()).toBe('down');
  });

  it('refresh falls back to default error', () => {
    smtp.load.mockReturnValueOnce(throwError(() => ({})));
    component.refresh();
    expect(component.error()).toBe('Failed to load SMTP settings.');
  });

  describe('save()', () => {
    it('no-ops when busy', () => {
      component.busy.set(true);
      component.save();
      expect(smtp.update).not.toHaveBeenCalled();
    });

    it('sends password when set', () => {
      component.host.set('smtp.x');
      component.port.set(25);
      component.useSsl.set(false);
      component.username.set('u');
      component.password.set('pw');
      component.fromAddress.set('f@x');
      component.fromName.set('Hi');
      component.enabled.set(true);
      component.save();
      expect(smtp.update).toHaveBeenCalledWith(
        expect.objectContaining({ password: 'pw', host: 'smtp.x', port: 25 }),
      );
    });

    it('sends empty password when clearPassword is set', () => {
      component.clearPassword.set(true);
      component.save();
      expect(smtp.update).toHaveBeenCalledWith(expect.objectContaining({ password: '' }));
    });

    it('sends null password when neither set nor cleared', () => {
      component.password.set('');
      component.clearPassword.set(false);
      component.save();
      expect(smtp.update).toHaveBeenCalledWith(expect.objectContaining({ password: null }));
    });

    it('omits username when blank', () => {
      component.username.set('   ');
      component.save();
      expect(smtp.update).toHaveBeenCalledWith(expect.objectContaining({ username: null }));
    });

    it('sets success message after update', () => {
      component.save();
      expect(component.message()).toBe('SMTP settings saved.');
    });

    it('surfaces save error', () => {
      smtp.update.mockReturnValueOnce(throwError(() => ({ message: 'bad' })));
      component.save();
      expect(component.error()).toBe('bad');
    });

    it('falls back to default save error', () => {
      smtp.update.mockReturnValueOnce(throwError(() => ({})));
      component.save();
      expect(component.error()).toBe('Failed to save SMTP settings.');
    });
  });

  describe('sendTest()', () => {
    it('no-ops while busy', () => {
      component.testBusy.set(true);
      component.testTo.set('a@x');
      component.sendTest();
      expect(smtp.test).not.toHaveBeenCalled();
    });

    it('rejects empty recipient', () => {
      component.testTo.set('   ');
      component.sendTest();
      expect(component.error()).toContain('recipient');
    });

    it('sends and toasts on success', () => {
      component.testTo.set('a@x');
      component.sendTest();
      expect(smtp.test).toHaveBeenCalledWith('a@x');
      expect(component.message()).toContain('Test email sent');
    });

    it('surfaces test error', () => {
      smtp.test.mockReturnValueOnce(throwError(() => ({ message: 'no mx' })));
      component.testTo.set('a@x');
      component.sendTest();
      expect(component.error()).toBe('no mx');
    });

    it('falls back to default test error', () => {
      smtp.test.mockReturnValueOnce(throwError(() => ({})));
      component.testTo.set('a@x');
      component.sendTest();
      expect(component.error()).toBe('Test failed.');
    });
  });
});
