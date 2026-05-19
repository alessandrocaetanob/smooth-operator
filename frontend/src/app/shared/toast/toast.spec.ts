import { TestBed } from '@angular/core/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { ToastContainer } from './toast';
import { ToastService, Toast } from './toast.service';

describe('ToastContainer', () => {
  let component: ToastContainer;
  let svc: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ToastContainer],
      providers: [provideAnimations()],
    });
    svc = TestBed.inject(ToastService);
    const fixture = TestBed.createComponent(ToastContainer);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('toasts reflects service signal', () => {
    expect(component.toasts()).toEqual([]);
    svc.success('hello');
    expect(component.toasts().length).toBe(1);
  });

  it('trackById returns id', () => {
    expect(component.trackById(0, { id: 7 } as Toast)).toBe(7);
  });

  it('drainDuration returns CSS-friendly ms string', () => {
    expect(component.drainDuration({ ttl: 1234 } as Toast)).toBe('1234ms');
  });

  it('dismiss delegates to service', () => {
    const spy = vi.spyOn(svc, 'dismiss');
    component.dismiss(42);
    expect(spy).toHaveBeenCalledWith(42);
  });

  it('ariaRole maps kinds to status/alert', () => {
    expect(component.ariaRole('success')).toBe('status');
    expect(component.ariaRole('info')).toBe('status');
    expect(component.ariaRole('warning')).toBe('alert');
    expect(component.ariaRole('error')).toBe('alert');
  });

  it.each([['success'], ['error'], ['warning'], ['info']] as const)(
    'toneClass / drainClass / toneIcon returns non-empty for %s',
    (kind) => {
      expect(component.toneClass(kind)).toBeTruthy();
      expect(component.drainClass(kind)).toBeTruthy();
      expect(component.toneIcon(kind)).toBeTruthy();
    },
  );

  it('toneClass falls back to info default for unknown kind', () => {
    expect(component.toneClass('mystery' as Toast['kind'])).toBeTruthy();
    expect(component.drainClass('mystery' as Toast['kind'])).toBeTruthy();
    expect(component.toneIcon('mystery' as Toast['kind'])).toBe('info');
  });
});
