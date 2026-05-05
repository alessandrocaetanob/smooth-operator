import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { LayoutService } from './layout.service';

const STORAGE_KEY = 'smooth-operator.sidebar.collapsed';

describe('LayoutService', () => {
  let service: LayoutService;

  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  function create(): void {
    TestBed.configureTestingModule({ providers: [LayoutService] });
    service = TestBed.inject(LayoutService);
  }

  it('should start as collapsed=false when localStorage has no entry', () => {
    create();
    expect(service.collapsed()).toBe(false);
  });

  it('should start as collapsed=true when localStorage has "1"', () => {
    localStorage.setItem(STORAGE_KEY, '1');
    create();
    expect(service.collapsed()).toBe(true);
  });

  it('should start as collapsed=false when localStorage has "0"', () => {
    localStorage.setItem(STORAGE_KEY, '0');
    create();
    expect(service.collapsed()).toBe(false);
  });

  describe('toggle()', () => {
    it('should flip collapsed from false to true', () => {
      create();
      expect(service.collapsed()).toBe(false);
      service.toggle();
      expect(service.collapsed()).toBe(true);
    });

    it('should flip collapsed from true to false', () => {
      localStorage.setItem(STORAGE_KEY, '1');
      create();
      expect(service.collapsed()).toBe(true);
      service.toggle();
      expect(service.collapsed()).toBe(false);
    });

    it('should persist the new value to localStorage', () => {
      create();
      service.toggle(); // false -> true
      TestBed.flushEffects();
      expect(localStorage.getItem(STORAGE_KEY)).toBe('1');
    });
  });

  describe('setCollapsed()', () => {
    it('should set collapsed to true', () => {
      create();
      service.setCollapsed(true);
      expect(service.collapsed()).toBe(true);
    });

    it('should set collapsed to false', () => {
      localStorage.setItem(STORAGE_KEY, '1');
      create();
      service.setCollapsed(false);
      expect(service.collapsed()).toBe(false);
    });

    it('should persist true to localStorage', () => {
      create();
      service.setCollapsed(true);
      TestBed.flushEffects();
      expect(localStorage.getItem(STORAGE_KEY)).toBe('1');
    });

    it('should persist false to localStorage', () => {
      localStorage.setItem(STORAGE_KEY, '1');
      create();
      service.setCollapsed(false);
      TestBed.flushEffects();
      expect(localStorage.getItem(STORAGE_KEY)).toBe('0');
    });
  });
});
