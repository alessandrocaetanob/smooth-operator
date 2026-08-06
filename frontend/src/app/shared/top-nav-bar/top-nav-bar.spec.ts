import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { TopNavBar } from './top-nav-bar';

describe('TopNavBar', () => {
  let component: TopNavBar;
  let fixture: ComponentFixture<TopNavBar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TopNavBar],
      providers: [
        provideRouter([]),
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideTranslateService(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TopNavBar);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('handleScroll', () => {
    it('updates the scrolled signal when window scrollY crosses threshold', () => {
      const original = window.scrollY;
      try {
        Object.defineProperty(window, 'scrollY', { value: 100, configurable: true });
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (component as any).handleScroll();
        expect(component.scrolled()).toBe(true);

        Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (component as any).handleScroll();
        expect(component.scrolled()).toBe(false);
      } finally {
        Object.defineProperty(window, 'scrollY', { value: original, configurable: true });
      }
    });
  });

  describe('cleanupListeners', () => {
    it('removes scroll and idle event listeners and clears timers', () => {
      const removeSpy = vi.spyOn(document, 'removeEventListener');
      const clearTimeoutSpy = vi.spyOn(globalThis, 'clearTimeout');
      const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');

      // Simulate active timers so the clear branches execute.
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (component as any).idleTimer = setTimeout(() => undefined, 60_000);
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (component as any).healthTimer = setInterval(() => undefined, 60_000);

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (component as any).cleanupListeners();

      expect(clearTimeoutSpy).toHaveBeenCalled();
      expect(clearIntervalSpy).toHaveBeenCalled();
      const removedEvents = removeSpy.mock.calls.map((c) => c[0]);
      expect(removedEvents).toContain('scroll');
      expect(removedEvents).toContain('pointermove');
      expect(removedEvents).toContain('pointerdown');
      expect(removedEvents).toContain('keydown');

      removeSpy.mockRestore();
      clearTimeoutSpy.mockRestore();
      clearIntervalSpy.mockRestore();
    });
  });
});
