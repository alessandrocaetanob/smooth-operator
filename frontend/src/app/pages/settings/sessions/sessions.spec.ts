import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of, throwError } from 'rxjs';

import { Sessions } from './sessions';
import { SystemSettingsService } from '../../../services/system-settings.service';

describe('Sessions', () => {
  let fixture: ComponentFixture<Sessions>;
  let component: Sessions;
  let svc: { load: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    svc = {
      load: vi.fn(() =>
        of({ auditLogRetentionDays: 30, idleTimeoutMinutes: 0, maxSessionMinutes: 0 }),
      ),
      update: vi.fn((p) => of(p)),
    };

    await TestBed.configureTestingModule({
      imports: [Sessions],
      providers: [{ provide: SystemSettingsService, useValue: svc }],
    }).compileComponents();

    fixture = TestBed.createComponent(Sessions);
    component = fixture.componentInstance;
  });

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit loads existing settings into signals', () => {
    svc.load.mockReturnValueOnce(
      of({ auditLogRetentionDays: 60, idleTimeoutMinutes: 15, maxSessionMinutes: 240 }),
    );
    component.ngOnInit();
    expect(component.idleTimeoutMinutes()).toBe(15);
    expect(component.maxSessionMinutes()).toBe(240);
  });

  it('refresh surfaces load errors', () => {
    svc.load.mockReturnValueOnce(throwError(() => ({ error: { message: 'boom' } })));
    component.refresh();
    expect(component.error()).toBe('boom');
    expect(component.loading()).toBe(false);
  });

  it('selectIdlePreset and selectMaxPreset update signals', () => {
    component.selectIdlePreset(15);
    component.selectMaxPreset(480);
    expect(component.idleTimeoutMinutes()).toBe(15);
    expect(component.maxSessionMinutes()).toBe(480);
  });

  it('save preserves auditLogRetentionDays from load (round-trip)', () => {
    component.ngOnInit();
    component.idleTimeoutMinutes.set(15);
    component.maxSessionMinutes.set(480);
    component.save();
    expect(svc.update).toHaveBeenCalledWith({
      auditLogRetentionDays: 30,
      idleTimeoutMinutes: 15,
      maxSessionMinutes: 480,
    });
    expect(component.message()).toContain('15 min');
    expect(component.message()).toContain('480 min');
  });

  it('save 0/0 reports disabled + unlimited', () => {
    component.idleTimeoutMinutes.set(0);
    component.maxSessionMinutes.set(0);
    component.save();
    expect(component.message()).toContain('disabled');
    expect(component.message()).toContain('No hard');
  });

  it('save rejects negative idle', () => {
    component.idleTimeoutMinutes.set(-1);
    component.save();
    expect(component.error()).toContain('Idle timeout');
    expect(svc.update).not.toHaveBeenCalled();
  });

  it('save rejects max above 10080', () => {
    component.maxSessionMinutes.set(10081);
    component.save();
    expect(component.error()).toContain('Max session');
    expect(svc.update).not.toHaveBeenCalled();
  });

  it('save no-ops when busy', () => {
    component.busy.set(true);
    component.save();
    expect(svc.update).not.toHaveBeenCalled();
  });

  it('save surfaces backend error', () => {
    svc.update.mockReturnValueOnce(throwError(() => ({ error: { message: 'nope' } })));
    component.save();
    expect(component.error()).toBe('nope');
    expect(component.busy()).toBe(false);
  });

  it('save falls back to default error', () => {
    svc.update.mockReturnValueOnce(throwError(() => ({})));
    component.save();
    expect(component.error()).toContain('Failed to save');
  });
});
