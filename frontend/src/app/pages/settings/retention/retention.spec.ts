import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
      load: vi.fn(() => of({ auditLogRetentionDays: 30 })),
      update: vi.fn(() => of({ auditLogRetentionDays: 90 })),
    };
    await TestBed.configureTestingModule({
      imports: [Retention],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SystemSettingsService, useValue: svc },
      ],
    }).compileComponents();
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

  it('save sends all three settings fields and shows success message', () => {
    component.retentionDays.set(90);
    component.idleTimeoutMinutes.set(15);
    component.maxSessionMinutes.set(480);
    component.save();
    expect(svc.update).toHaveBeenCalledWith({
      auditLogRetentionDays: 90,
      idleTimeoutMinutes: 15,
      maxSessionMinutes: 480,
    });
    expect(component.message()).toBe('Settings saved.');
  });

  it('save success with 0 retention persists value', () => {
    svc.update.mockReturnValueOnce(
      of({ auditLogRetentionDays: 0, idleTimeoutMinutes: 0, maxSessionMinutes: 0 }),
    );
    component.retentionDays.set(0);
    component.save();
    expect(component.retentionDays()).toBe(0);
    expect(component.message()).toBe('Settings saved.');
  });

  it('save rejects idleTimeout above 10080', () => {
    component.idleTimeoutMinutes.set(10081);
    component.save();
    expect(component.error()).toContain('Idle timeout');
    expect(svc.update).not.toHaveBeenCalled();
  });

  it('save rejects negative maxSession', () => {
    component.maxSessionMinutes.set(-1);
    component.save();
    expect(component.error()).toContain('Max session');
    expect(svc.update).not.toHaveBeenCalled();
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
