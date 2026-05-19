import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { Spinner } from './spinner';

describe('Spinner', () => {
  function create(size: 'sm' | 'md' | 'lg' = 'md'): Spinner {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [Spinner] });
    const fixture = TestBed.createComponent(Spinner);
    fixture.componentInstance.size = size;
    return fixture.componentInstance;
  }

  it('should create', () => {
    expect(create()).toBeTruthy();
  });

  it('computes px / strokeWidth / r / cx for sm', () => {
    const c = create('sm');
    expect(c.px).toBe(16);
    expect(c.strokeWidth).toBe(2);
    expect(c.r).toBe(6);
    expect(c.cx).toBe(8);
  });

  it('computes for md', () => {
    const c = create('md');
    expect(c.px).toBe(24);
    expect(c.cx).toBe(12);
  });

  it('computes for lg', () => {
    const c = create('lg');
    expect(c.px).toBe(40);
    expect(c.strokeWidth).toBe(3);
    expect(c.r).toBe(17);
  });
});
