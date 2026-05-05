import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.className = '';
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.className = '';
  });

  function create(): void {
    TestBed.configureTestingModule({ providers: [ThemeService] });
    service = TestBed.inject(ThemeService);
  }

  describe('init()', () => {
    it('should default to dark when nothing is saved and matchMedia returns false', () => {
      window.matchMedia = () =>
        ({ matches: false, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      expect(service.theme()).toBe('dark');
    });

    it('should use dark theme when prefers-color-scheme is dark and no saved value', () => {
      window.matchMedia = () =>
        ({ matches: true, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      expect(service.theme()).toBe('dark');
    });

    it('should restore saved "light" theme from localStorage', () => {
      localStorage.setItem('theme', 'light');
      window.matchMedia = () =>
        ({ matches: true, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      expect(service.theme()).toBe('light');
    });

    it('should restore saved "dark" theme from localStorage', () => {
      localStorage.setItem('theme', 'dark');
      window.matchMedia = () =>
        ({ matches: false, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      expect(service.theme()).toBe('dark');
    });

    it('should add "dark" class to documentElement when applying dark', () => {
      window.matchMedia = () =>
        ({ matches: false, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      expect(document.documentElement.classList.contains('dark')).toBe(true);
    });

    it('should remove "dark" class when applying light', () => {
      document.documentElement.classList.add('dark');
      localStorage.setItem('theme', 'light');
      window.matchMedia = () =>
        ({ matches: false, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      expect(document.documentElement.classList.contains('dark')).toBe(false);
    });

    it('should persist resolved theme to localStorage', () => {
      window.matchMedia = () =>
        ({ matches: true, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      expect(localStorage.getItem('theme')).toBe('dark');
    });
  });

  describe('toggle()', () => {
    it('should flip dark -> light', () => {
      window.matchMedia = () =>
        ({ matches: true, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init(); // sets dark
      service.toggle();
      expect(service.theme()).toBe('light');
    });

    it('should flip light -> dark', () => {
      localStorage.setItem('theme', 'light');
      window.matchMedia = () =>
        ({ matches: false, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init(); // sets light
      service.toggle();
      expect(service.theme()).toBe('dark');
    });

    it('should add dark class when toggling to dark', () => {
      localStorage.setItem('theme', 'light');
      window.matchMedia = () =>
        ({ matches: false, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init();
      service.toggle();
      expect(document.documentElement.classList.contains('dark')).toBe(true);
    });

    it('should remove dark class when toggling to light', () => {
      window.matchMedia = () =>
        ({ matches: true, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init(); // dark
      service.toggle();
      expect(document.documentElement.classList.contains('dark')).toBe(false);
    });

    it('should persist toggled theme to localStorage', () => {
      window.matchMedia = () =>
        ({ matches: true, addEventListener: vi.fn() }) as unknown as MediaQueryList;
      create();
      service.init(); // dark
      service.toggle();
      expect(localStorage.getItem('theme')).toBe('light');
    });
  });
});
