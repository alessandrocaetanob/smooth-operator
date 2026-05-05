import { TestBed } from '@angular/core/testing';
import { describe, it, expect, vi, afterEach } from 'vitest';

import { MotionService } from './motion.service';

describe('MotionService', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function createService(matches: boolean): MotionService {
    window.matchMedia = () =>
      ({
        matches,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }) as unknown as MediaQueryList;
    TestBed.configureTestingModule({ providers: [MotionService] });
    const svc = TestBed.inject(MotionService);
    svc.init();
    return svc;
  }

  it('should set reducedMotion to true when media query matches', () => {
    const service = createService(true);
    expect(service.reducedMotion()).toBe(true);
  });

  it('should set reducedMotion to false when media query does not match', () => {
    const service = createService(false);
    expect(service.reducedMotion()).toBe(false);
  });

  it('should update reducedMotion signal when media query changes', () => {
    let changeHandler: ((e: MediaQueryListEvent) => void) | undefined;
    window.matchMedia = () =>
      ({
        matches: false,
        addEventListener: (_: string, handler: (e: MediaQueryListEvent) => void) => {
          changeHandler = handler;
        },
      }) as unknown as MediaQueryList;

    TestBed.configureTestingModule({ providers: [MotionService] });
    const service = TestBed.inject(MotionService);
    service.init();

    expect(service.reducedMotion()).toBe(false);
    changeHandler?.({ matches: true } as MediaQueryListEvent);
    expect(service.reducedMotion()).toBe(true);
  });
});
