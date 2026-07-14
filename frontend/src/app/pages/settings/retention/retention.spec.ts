import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of, throwError } from 'rxjs';

import { Retention } from './retention';
import { SystemSettingsService } from '../../../services/system-settings.service';

describe('Retention', () => {
  let component: Retention;
  let fixture: ComponentFixture<Retention>;
  let svc: {
    load: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    svc = {
      load: vi.fn(() =>
        of({ auditLogRetentionDays: 30, idleTimeoutMinutes: 15, maxSessionMinutes: 480 }),
      ),
      update: vi.fn((payload) => of(payload)),
    };
    await TestBed.configureTestingModule({
      imports: [Retention],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: SystemSettingsService, useValue: svc },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        settingsRetention: {
          errors: {
            invalidRange: 'Retention must be an integer between 0 (forever) and 3650 days.',
            loadFailed: 'Failed to load system settings.',
            saveFailed: 'Failed to save settings.',
          },
          messages: {
            disabledForever: 'Retention disabled — audit logs will be kept forever.',
            purgeScheduled: 'Audit logs older than {{days}} day(s) will be purged daily.',
          },
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(Retention);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit triggers refresh and applies retentionDays', () => {
    component.ngOnInit();
    expect(component.retentionDays()).toBe(30);
    expect(component.loading()).toBe(false);
  });

  it('refresh handles load failure', () => {
    svc.load.mockReturnValueOnce(throwError(() => ({ message: 'bad' })));
    component.refresh();
    expect(component.error()).toBe('bad');
    expect(component.loading()).toBe(false);
  });

  it('refresh falls back to default error', () => {
    svc.load.mockReturnValueOnce(throwError(() => ({})));
    component.refresh();
    expect(component.error()).toBe('Failed to load system settings.');
  });

  it('selectPreset updates retentionDays', () => {
    component.selectPreset(180);
    expect(component.retentionDays()).toBe(180);
  });

  it('save rejects invalid values', () => {
    component.retentionDays.set(-1);
    component.save();
    expect(component.error()).toContain('integer');
    expect(svc.update).not.toHaveBeenCalled();

    component.retentionDays.set(99999);
    component.save();
    expect(component.error()).toContain('integer');

    component.retentionDays.set(1.5);
    component.save();
    expect(component.error()).toContain('integer');
  });

  it('save sends retention and preserves session-timeout pass-throughs', () => {
    component.ngOnInit();
    component.retentionDays.set(90);
    component.save();
    expect(svc.update).toHaveBeenCalledWith({
      auditLogRetentionDays: 90,
      idleTimeoutMinutes: 15,
      maxSessionMinutes: 480,
    });
    expect(component.message()).toContain('older than 90');
  });

  it('save with 0 retention shows forever message', () => {
    svc.update.mockReturnValueOnce(
      of({ auditLogRetentionDays: 0, idleTimeoutMinutes: 0, maxSessionMinutes: 0 }),
    );
    component.retentionDays.set(0);
    component.save();
    expect(component.message()).toContain('forever');
  });

  it('save no-ops when busy', () => {
    component.busy.set(true);
    component.retentionDays.set(30);
    component.save();
    expect(svc.update).not.toHaveBeenCalled();
  });

  it('save surfaces backend error', () => {
    svc.update.mockReturnValueOnce(throwError(() => ({ error: { Message: 'denied' } })));
    component.retentionDays.set(30);
    component.save();
    expect(component.error()).toBe('denied');
    expect(component.busy()).toBe(false);
  });

  it('save falls back to default error', () => {
    svc.update.mockReturnValueOnce(throwError(() => ({})));
    component.retentionDays.set(30);
    component.save();
    expect(component.error()).toBe('Failed to save settings.');
  });
});
